using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;

/// <summary>
/// 時間帯（朝 / 夕方 / 夜）とプレイヤーの場所（家の中 / 外）の組み合わせごとに、
/// 見た目の設定（表示するライト類、追加 Volume、Skybox、Fog、環境光）を切り替える。
///
/// プレイヤーの場所が変わったときは、現在の時間帯のまま、その場所用の設定を適用し直す。
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public class TimeOfDayRenderer : Singleton<TimeOfDayRenderer>
{
    public enum TimeOfDay { Morning, Evening, Night }

    /// <summary>1 つの「時間帯 × 場所」に対する見た目の設定。</summary>
    [System.Serializable]
    public class Profile
    {
        [Tooltip("この時間帯で SetActive(true) にするルート（ライトは Light を持つGameObjectをここへ）")]
        public List<GameObject> activeRoots = new();

        [Header("Additional Volumes (任意)")]
        [Tooltip("このプロファイル適用時に有効化する追加Volumeを持つGameObject")]
        public List<GameObject> addVolumes = new();

        [Header("Skybox (任意)")]
        public bool applySkybox = false;
        [ShowIf(nameof(applySkybox))] public Material skybox;

        [Header("Fog (任意)")]
        public bool applyFog = false;
        [ShowIf(nameof(applyFog))] public bool useFog = true;
        [ShowIf(nameof(applyFog))] public Color fogColor = Color.gray;
        [ShowIf(nameof(applyFog))] public FogMode fogMode = FogMode.ExponentialSquared;
        [ShowIf(nameof(applyFog)), Min(0f)] public float fogDensity = 0.02f; // Exp/Exp2用
        [ShowIf(nameof(applyFog))] public float fogStart = 0f;                   // Linear用
        [ShowIf(nameof(applyFog))] public float fogEnd = 100f;                   // Linear用

        [Header("Ambient Light (任意)")]
        public bool applyAmbient = false;
        [ShowIf(nameof(applyAmbient))] public AmbientMode ambientMode = AmbientMode.Skybox;
        [ShowIf(nameof(applyAmbient)), Range(0, 8)] public float ambientIntensity = 1f;

        // Gradient (Trilight) モード用の色
        [ShowIf("@this.applyAmbient && this.ambientMode == UnityEngine.Rendering.AmbientMode.Trilight")]
        [ColorUsage(false, true)] public Color ambientSkyColor = new(0.21f, 0.23f, 0.26f);
        [ShowIf("@this.applyAmbient && this.ambientMode == UnityEngine.Rendering.AmbientMode.Trilight")]
        [ColorUsage(false, true)] public Color ambientEquatorColor = new(0.11f, 0.12f, 0.13f);
        [ShowIf("@this.applyAmbient && this.ambientMode == UnityEngine.Rendering.AmbientMode.Trilight")]
        [ColorUsage(false, true)] public Color ambientGroundColor = new(0.05f, 0.05f, 0.05f);

        // Color (Flat) モード用の色
        [ShowIf("@this.applyAmbient && this.ambientMode == UnityEngine.Rendering.AmbientMode.Flat")]
        [ColorUsage(false, true)] public Color ambientLight = new(0.21f, 0.23f, 0.26f);
    }

    /// <summary>ある時間帯の、場所ごとのプロファイル。</summary>
    [System.Serializable]
    public class LocationProfiles
    {
        [DictionaryDrawerSettings(KeyLabel = "場所", ValueLabel = "プロファイル")]
        public Dictionary<PlayerLocation.Location, Profile> ProfilesByLocation = new();
    }

    // ===== Inspector =====

    [Header("Auto Apply")]
    public bool applyOnEnable = true;
    public TimeOfDay initialTime = TimeOfDay.Morning;

    [Header("URP Global Volume (任意)")]
    public Volume globalVolume;
    public UnityEvent ApplingEvent;

    [Title("Profiles")]
    [InfoBox("時間帯とプレイヤーの場所（家の中/外）に応じて適用するプロファイルを設定します。")]
    [DictionaryDrawerSettings(KeyLabel = "時間帯", ValueLabel = "場所ごとのプロファイル設定")]
    public Dictionary<TimeOfDay, LocationProfiles> profiles = new();

    [ReadOnly, ShowInInspector, LabelText("現在の時間帯")]
    public TimeOfDay Current { get; private set; }

    // ===== 実行時の状態 =====

    // 全プロファイルに登場するオブジェクトの一覧。切り替え時に、いったん全部 OFF にするために使う
    private readonly HashSet<GameObject> allRoots = new();
    private readonly HashSet<GameObject> allAddVolumes = new();

    // ===== Unity ライフサイクル =====

    private void OnEnable()
    {
        BuildCaches();
        if (applyOnEnable) Apply(initialTime);
    }

    private void Start()
    {
        if (!Application.isPlaying) return;

        if (PlayerLocation.instance != null)
        {
            PlayerLocation.instance.SetLocationEvent.AddListener(ApplyCurrentStateWithCurrentTime);
        }
        else
        {
            Debug.LogWarning("[TimeOfDayRenderer] PlayerLocation instance not found. Location changes won't trigger profile updates.");
        }
    }

    private void OnDestroy()
    {
        if (Application.isPlaying && PlayerLocation.instance != null)
        {
            PlayerLocation.instance.SetLocationEvent.RemoveListener(ApplyCurrentStateWithCurrentTime);
        }
    }

    private void OnValidate() => BuildCaches();

    // ===== 公開 API =====

    /// <summary>指定の時間帯の設定を、プレイヤーの現在の場所に合わせて適用する。</summary>
    public void Apply(TimeOfDay t)
    {
        Current = t;

        var profile = GetProfile(t);
        if (profile == null)
        {
            Debug.LogError($"[TimeOfDayRenderer] Apply failed because no matching profile was found for Time: {t} and the current location.");
            return;
        }

        ApplingEvent.Invoke();

        // いったん全部 OFF にしてから、このプロファイルのものだけ ON にする
        SetActiveAll(allRoots, false);
        SetActiveAll(allAddVolumes, false);
        SetActiveAll(profile.activeRoots, true);
        SetActiveAll(profile.addVolumes, true);

        ApplySkybox(profile);
        ApplyFog(profile);
        ApplyAmbientLight(profile);

        // Skybox / 環境光の変更を反映する
        DynamicGI.UpdateEnvironment();
    }

    // ===== プロファイルの適用 =====

    private static void ApplySkybox(Profile profile)
    {
        if (profile.applySkybox && profile.skybox)
        {
            RenderSettings.skybox = profile.skybox;
        }
    }

    private static void ApplyFog(Profile profile)
    {
        if (!profile.applyFog) return;

        RenderSettings.fog = profile.useFog;
        RenderSettings.fogColor = profile.fogColor;
        RenderSettings.fogMode = profile.fogMode;

        if (profile.fogMode == FogMode.Linear)
        {
            RenderSettings.fogStartDistance = profile.fogStart;
            RenderSettings.fogEndDistance = profile.fogEnd;
        }
        else
        {
            RenderSettings.fogDensity = profile.fogDensity;
        }
    }

    private static void ApplyAmbientLight(Profile profile)
    {
        if (!profile.applyAmbient) return;

        RenderSettings.ambientMode = profile.ambientMode;
        RenderSettings.ambientIntensity = profile.ambientIntensity;

        switch (profile.ambientMode)
        {
            case AmbientMode.Trilight: // Inspector 上の表記は Gradient
                RenderSettings.ambientSkyColor = profile.ambientSkyColor;
                RenderSettings.ambientEquatorColor = profile.ambientEquatorColor;
                RenderSettings.ambientGroundColor = profile.ambientGroundColor;
                break;

            case AmbientMode.Flat: // Inspector 上の表記は Color
                RenderSettings.ambientLight = profile.ambientLight;
                break;

            case AmbientMode.Skybox:
                break;
        }
    }

    private static void SetActiveAll(IEnumerable<GameObject> objects, bool active)
    {
        if (objects == null) return;

        foreach (var obj in objects)
        {
            if (obj) obj.SetActive(active);
        }
    }

    // ===== プロファイルの検索 =====

    /// <summary>
    /// 指定の時間帯 × プレイヤーの現在の場所 に対応するプロファイルを返す。
    /// エディタ編集中や PlayerLocation がないときは「外」として扱う。
    /// </summary>
    private Profile GetProfile(TimeOfDay t)
    {
        PlayerLocation.Location currentLocation = PlayerLocation.Location.Outside;
        if (Application.isPlaying && PlayerLocation.instance != null)
        {
            currentLocation = PlayerLocation.instance.currentLocation;
        }

        if (profiles.TryGetValue(t, out LocationProfiles locationProfiles) && locationProfiles?.ProfilesByLocation != null)
        {
            if (locationProfiles.ProfilesByLocation.TryGetValue(currentLocation, out Profile profile))
            {
                return profile;
            }
        }

        Debug.LogWarning($"[TimeOfDayRenderer] Profile for Time: {t}, Location: {currentLocation} is not defined.");
        return null;
    }

    /// <summary>全プロファイルから、切り替え対象のオブジェクトを集め直す。</summary>
    private void BuildCaches()
    {
        allRoots.Clear();
        allAddVolumes.Clear();

        if (profiles == null) return;

        foreach (var locationProfiles in profiles.Values)
        {
            if (locationProfiles?.ProfilesByLocation == null) continue;

            foreach (var profile in locationProfiles.ProfilesByLocation.Values)
            {
                if (profile == null) continue;

                AddAll(allRoots, profile.activeRoots);
                AddAll(allAddVolumes, profile.addVolumes);
            }
        }
    }

    private static void AddAll(HashSet<GameObject> destination, List<GameObject> objects)
    {
        if (objects == null) return;

        foreach (var obj in objects)
        {
            if (obj) destination.Add(obj);
        }
    }

    /// <summary>プレイヤーの場所が変わったとき。現在の時間帯のまま、その場所用の設定を適用し直す。</summary>
    private void ApplyCurrentStateWithCurrentTime()
    {
        Debug.Log($"[TimeOfDayRenderer] Location changed. Re-applying profile for current time: {Current}");
        Apply(Current);
    }

    // ===== Odin Buttons（エディタ／ランタイムOK）=====
    [HorizontalGroup("TOD", Width = 0.33f)]
    [Button("Morning", ButtonSizes.Large), GUIColor(0.8f, 0.95f, 1f)]
    public void SetMorning() => Apply(TimeOfDay.Morning);

    [HorizontalGroup("TOD", Width = 0.34f)]
    [Button("Evening", ButtonSizes.Large), GUIColor(1f, 0.85f, 0.6f)]
    public void SetEvening() => Apply(TimeOfDay.Evening);

    [HorizontalGroup("TOD", Width = 0.33f)]
    [Button("Night", ButtonSizes.Large), GUIColor(0.6f, 0.7f, 1f)]
    public void SetNight() => Apply(TimeOfDay.Night);

    [PropertySpace]
    [EnumToggleButtons, LabelText("プレビュー選択")]
    public TimeOfDay previewSelection;

    [Button("選択を適用")]
    public void ApplyPreview() => Apply(previewSelection);
}

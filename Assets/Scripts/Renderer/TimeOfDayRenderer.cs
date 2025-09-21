using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Sirenix.OdinInspector;
using UnityEngine.Events;

[ExecuteAlways]
[DisallowMultipleComponent]
public class TimeOfDayRenderer : Singleton<TimeOfDayRenderer>
{
    /// <summary>
    /// 時間帯を定義します。
    /// </summary>
    public enum TimeOfDay { Morning, Evening, Night }

    /// <summary>
    /// レンダリング設定をまとめたプロファイルです。
    /// </summary>
    [System.Serializable]
    public class Profile
    {
        [Tooltip("この時間帯で SetActive(true) にするルート（ライトは Light を持つGameObjectをここへ）")]
        public List<GameObject> activeRoots = new();

        [Header("Skybox (任意)")]
        public bool applySkybox = false;
        [ShowIf(nameof(applySkybox))] public Material skybox;

        [Header("Fog (任意)")]
        public bool applyFog = false;
        [ShowIf(nameof(applyFog))] public bool useFog = true;
        [ShowIf(nameof(applyFog))] public Color fogColor = Color.gray;
        [ShowIf(nameof(applyFog))] public FogMode fogMode = FogMode.ExponentialSquared;
        [ShowIf(nameof(applyFog)), Min(0f)] public float fogDensity = 0.02f; // Exp/Exp2用
        [ShowIf(nameof(applyFog))] public float fogStart = 0f;              // Linear用
        [ShowIf(nameof(applyFog))] public float fogEnd = 100f;              // Linear用

        [Header("Ambient Light (任意)")]
        public bool applyAmbient = false;
        [ShowIf(nameof(applyAmbient))] public AmbientMode ambientMode = AmbientMode.Skybox;
        [ShowIf(nameof(applyAmbient)), Range(0, 8)] public float ambientIntensity = 1f;

        // Gradient (Trilight) モード用の色設定
        [ShowIf("@this.applyAmbient && this.ambientMode == UnityEngine.Rendering.AmbientMode.Trilight")]
        [ColorUsage(false, true)] public Color ambientSkyColor = new(0.21f, 0.23f, 0.26f);
        [ShowIf("@this.applyAmbient && this.ambientMode == UnityEngine.Rendering.AmbientMode.Trilight")]
        [ColorUsage(false, true)] public Color ambientEquatorColor = new(0.11f, 0.12f, 0.13f);
        [ShowIf("@this.applyAmbient && this.ambientMode == UnityEngine.Rendering.AmbientMode.Trilight")]
        [ColorUsage(false, true)] public Color ambientGroundColor = new(0.05f, 0.05f, 0.05f);
        
        // Color (Flat) モード用の色設定
        [ShowIf("@this.applyAmbient && this.ambientMode == UnityEngine.Rendering.AmbientMode.Flat")]
        [ColorUsage(false, true)] public Color ambientLight = new(0.21f, 0.23f, 0.26f);

        [Header("URP Volume Profile (任意)")]
        public bool applyVolume = false;
        [ShowIf(nameof(applyVolume))] public VolumeProfile volumeProfile;
    }

    /// <summary>
    /// 場所ごとのプロファイルを格納するためのヘルパークラスです。
    /// </summary>
    [System.Serializable]
    public class LocationProfiles
    {
        [DictionaryDrawerSettings(KeyLabel = "場所", ValueLabel = "プロファイル")]
        public Dictionary<PlayerLocation.Location, Profile> ProfilesByLocation = new();
    }


    [Header("Auto Apply")]
    public bool applyOnEnable = true;
    public TimeOfDay initialTime = TimeOfDay.Morning;

    [Header("URP Global Volume (任意)")]
    public Volume globalVolume; // 任意（切替えたい時だけアサイン）
    public UnityEvent ApplingEvent;

    // ▼▼▼ データ構造をDictionaryに変更 ▼▼▼
    [Title("Profiles")]
    [InfoBox("時間帯とプレイヤーの場所（家の中/外）に応じて適用するプロファイルを設定します。")]
    [DictionaryDrawerSettings(KeyLabel = "時間帯", ValueLabel = "場所ごとのプロファイル設定")]
    public Dictionary<TimeOfDay, LocationProfiles> profiles = new();
    // ▲▲▲ ▲▲▲

    [ReadOnly, ShowInInspector, LabelText("現在の時間帯")]
    public TimeOfDay Current { get; private set; }

    private readonly HashSet<GameObject> allRoots = new();

    // SingletonのAwakeを呼び出すために追記
    protected override void Awake()
    {
        base.Awake();
    }

    void OnEnable()
    {
        BuildCaches();
        if (applyOnEnable) Apply(initialTime);
    }
    
    // ▼▼▼ PlayerLocationのイベントを購読する処理を追加 ▼▼▼
    void Start()
    {
        // 実行中のみ、PlayerLocationの場所変更イベントを監視する
        if (Application.isPlaying)
        {
            if (PlayerLocation.instance != null)
            {
                PlayerLocation.instance.SetLocationEvent.AddListener(ApplyCurrentStateWithCurrentTime);
            }
            else
            {
                Debug.LogWarning("[TimeOfDayRenderer] PlayerLocation instance not found. Location changes won't trigger profile updates.");
            }
        }
    }

    void OnDestroy()
    {
        // オブジェクト破棄時にイベントの監視を解除する
        if (Application.isPlaying && PlayerLocation.instance != null)
        {
            PlayerLocation.instance.SetLocationEvent.RemoveListener(ApplyCurrentStateWithCurrentTime);
        }
    }
    // ▲▲▲ ▲▲▲

    void OnValidate() => BuildCaches();

    // ▼▼▼ 新しいデータ構造に合わせてキャッシュ構築処理を修正 ▼▼▼
    void BuildCaches()
    {
        allRoots.Clear();

        void Collect(Profile p)
        {
            if (p == null || p.activeRoots == null) return;
            foreach (var go in p.activeRoots) if (go) allRoots.Add(go);
        }
        
        if (profiles == null) return;

        // 全ての時間帯、全ての場所のプロファイルを走査してキャッシュする
        foreach(var locationProfiles in profiles.Values)
        {
            if (locationProfiles?.ProfilesByLocation == null) continue;
            foreach(var profile in locationProfiles.ProfilesByLocation.Values)
            {
                Collect(profile);
            }
        }
    }

    /// <summary>
    /// 現在の時間帯と場所に基づいて、適用すべきプロファイルを取得します。
    /// </summary>
    Profile GetProfile(TimeOfDay t)
    {
        // PlayerLocationから現在の場所を取得
        PlayerLocation.Location currentLocation = PlayerLocation.Location.Outside; // デフォルト値
        if (Application.isPlaying && PlayerLocation.instance != null)
        {
            currentLocation = PlayerLocation.instance.currentLocation;
        }

        // Dictionaryから該当するプロファイルを検索
        if (profiles.TryGetValue(t, out LocationProfiles locProfiles) && locProfiles?.ProfilesByLocation != null)
        {
            if (locProfiles.ProfilesByLocation.TryGetValue(currentLocation, out Profile profile))
            {
                return profile;
            }
        }

        Debug.LogWarning($"[TimeOfDayRenderer] Profile for Time: {t}, Location: {currentLocation} is not defined.");
        return null;
    }

    /// <summary>
    /// 指定された時間帯のプロファイルを、現在の場所に応じて適用します。
    /// </summary>
    public void Apply(TimeOfDay t)
    {
        Current = t;
        var p = GetProfile(t);

        // プロファイルが見つからなかった場合は処理を中断
        if (p == null)
        {
            Debug.LogError($"[TimeOfDayRenderer] Apply failed because no matching profile was found for Time: {t} and the current location.");
            return;
        }
        
        ApplingEvent.Invoke();

        // 1) まず全ルートをOFF
        foreach (var go in allRoots) if (go) go.SetActive(false);

        // 2) 対象プロファイルのルートだけON
        if (p.activeRoots != null)
            foreach (var go in p.activeRoots) if (go) go.SetActive(true);

        // 3) Skybox
        if (p.applySkybox && p.skybox)
            RenderSettings.skybox = p.skybox;

        // 4) Fog
        if (p.applyFog)
        {
            RenderSettings.fog = p.useFog;
            RenderSettings.fogColor = p.fogColor;
            RenderSettings.fogMode = p.fogMode;
            if (p.fogMode == FogMode.Linear)
            {
                RenderSettings.fogStartDistance = p.fogStart;
                RenderSettings.fogEndDistance   = p.fogEnd;
            }
            else
            {
                RenderSettings.fogDensity = p.fogDensity;
            }
        }
        
        // 5) Ambient Light
        if (p.applyAmbient)
        {
            RenderSettings.ambientMode = p.ambientMode;
            RenderSettings.ambientIntensity = p.ambientIntensity;
            switch (p.ambientMode)
            {
                case AmbientMode.Trilight: // Gradient
                    RenderSettings.ambientSkyColor = p.ambientSkyColor;
                    RenderSettings.ambientEquatorColor = p.ambientEquatorColor;
                    RenderSettings.ambientGroundColor = p.ambientGroundColor;
                    break;
                case AmbientMode.Flat: // Color
                    RenderSettings.ambientLight = p.ambientLight;
                    break;
                case AmbientMode.Skybox:
                    break;
            }
        }

        // 6) URP Volume
        if (p.applyVolume && globalVolume && p.volumeProfile)
            globalVolume.sharedProfile = p.volumeProfile;

        // 7) 環境反映（Skybox/Ambient）
        DynamicGI.UpdateEnvironment();
    }
    
    /// <summary>
    /// 場所が変更されたときに呼び出され、現在の時間帯設定を再適用します。
    /// </summary>
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
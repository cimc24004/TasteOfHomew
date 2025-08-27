using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Sirenix.OdinInspector;

[ExecuteAlways]
[DisallowMultipleComponent]
public class TimeOfDayRenderer : SerializedMonoBehaviour
{
    public enum TimeOfDay { Morning, Evening, Night }

    [Header("Auto Apply")]
    public bool applyOnEnable = true;
    public TimeOfDay initialTime = TimeOfDay.Morning;

    [Header("URP Global Volume (任意)")]
    public Volume globalVolume; // 任意（切替えたい時だけアサイン）

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
        [ShowIf(nameof(applyFog))] public float fogStart = 0f;               // Linear用
        [ShowIf(nameof(applyFog))] public float fogEnd = 100f;              // Linear用

        [Header("URP Volume Profile (任意)")]
        public bool applyVolume = false;
        [ShowIf(nameof(applyVolume))] public VolumeProfile volumeProfile;
    }

    [FoldoutGroup("Profiles"), Title("Morning")]
    public Profile morning = new();
    [FoldoutGroup("Profiles"), Title("Evening")]
    public Profile evening = new();
    [FoldoutGroup("Profiles"), Title("Night")]
    public Profile night = new();

    [ReadOnly, ShowInInspector, LabelText("現在の時間帯")]
    public TimeOfDay Current { get; private set; }

    // 3プロファイルに登録された全ルートを一括管理
    private readonly HashSet<GameObject> allRoots = new();

    void OnEnable()
    {
        BuildCaches();
        if (applyOnEnable) Apply(initialTime);
    }

    void OnValidate() => BuildCaches();

    void BuildCaches()
    {
        allRoots.Clear();

        void Collect(Profile p)
        {
            if (p == null || p.activeRoots == null) return;
            foreach (var go in p.activeRoots) if (go) allRoots.Add(go);
        }
        Collect(morning);
        Collect(evening);
        Collect(night);
    }

    Profile GetProfile(TimeOfDay t) =>
        t == TimeOfDay.Morning ? morning :
        t == TimeOfDay.Evening ? evening : night;

    public void Apply(TimeOfDay t)
    {
        Current = t;
        var p = GetProfile(t);

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

        // 5) URP Volume
        if (p.applyVolume && globalVolume && p.volumeProfile)
            globalVolume.profile = p.volumeProfile;

        // 6) 環境反映（Skybox/Ambient）
        DynamicGI.UpdateEnvironment();
    }

    // ===== Odin Buttons（エディタ／ランタイムOK） =====
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

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Sirenix.OdinInspector;
using UnityEngine.Events;

[ExecuteAlways]
[DisallowMultipleComponent]
public class TimeOfDayRenderer : Singleton<TimeOfDayRenderer>
{
    public enum TimeOfDay { Morning, Evening, Night }

    [Header("Auto Apply")]
    public bool applyOnEnable = true;
    public TimeOfDay initialTime = TimeOfDay.Morning;

    [Header("URP Global Volume (任意)")]
    public Volume globalVolume; // 任意（切替えたい時だけアサイン）
    public UnityEvent<TimeOfDay> ApplingEvent;
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
        [ShowIf(nameof(applyFog))] public float fogEnd = 100f;               // Linear用

        // ===== 👇ここから追加 =====
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
        // ===== 👆ここまで追加 =====

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
        ApplingEvent.Invoke(t);
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
        
        // ===== 👇ここから追加 =====
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
                    // Skyboxモードは強度を更新するだけでよい
                    break;
            }
        }
        // ===== 👆ここまで追加 =====

        // 6) URP Volume
        if (p.applyVolume && globalVolume && p.volumeProfile)
            globalVolume.sharedProfile  = p.volumeProfile;

        // 7) 環境反映（Skybox/Ambient）
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
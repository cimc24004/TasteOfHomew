using System.IO;
using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.EventSystems;
using UnityEngine.Rendering.Universal;

/// <summary>
/// ゲーム設定の全項目。このクラスがそのまま JSON に変換されて保存される
/// （フィールド名がセーブデータのキーになるので、名前は変えないこと）。
/// </summary>
[System.Serializable]
public class GameSettings
{
    [Header("Graphics")]
    public int resolutionIndex;   // Screen.resolutions のインデックス
    public FullScreenMode fullscreenMode;
    public bool vsync;
    public int shadowQuality;     // 0: High, 1: Medium, 2: Low
    public int textureQuality;    // 0: High, 1: Medium, 2: Low
    public int antiAliasing;      // 0: Off, 1 以上: 2x MSAA

    [Header("Audio")]
    public float masterVolume;    // 0 〜 1

    [Header("Gameplay")]
    public float mouseSensitivity;
    public bool invertCameraY;
    public float fov;
    public int cameraBob;         // 0: ON, 1: OFF

    /// <summary>デフォルト設定で初期化する。</summary>
    public GameSettings()
    {
        resolutionIndex = -1; // 「未設定」。実際の値は ConfigController がモニターに合わせて決める
        fullscreenMode = FullScreenMode.ExclusiveFullScreen;
        vsync = true;
        shadowQuality = 0;
        textureQuality = 0;
        antiAliasing = 1;

        masterVolume = 0.8f;

        mouseSensitivity = 1.0f;
        invertCameraY = false;
        fov = 70f;
        cameraBob = 0;
    }

    /// <summary>既存の設定をコピーする。</summary>
    public GameSettings(GameSettings other)
    {
        resolutionIndex = other.resolutionIndex;
        fullscreenMode = other.fullscreenMode;
        vsync = other.vsync;
        shadowQuality = other.shadowQuality;
        textureQuality = other.textureQuality;
        antiAliasing = other.antiAliasing;

        masterVolume = other.masterVolume;

        mouseSensitivity = other.mouseSensitivity;
        invertCameraY = other.invertCameraY;
        fov = other.fov;
        cameraBob = other.cameraBob;
    }
}

/// <summary>
/// ゲーム設定のロード・保存・適用を行うシングルトン。
///
/// 設定は 2 つ持つ:
///   ・CurrentSettings … いまゲームに適用されている設定（他のスクリプトはこれを読む）
///   ・pendingSettings … 設定画面で変更中の、まだ適用していない設定
/// 設定画面で値を変えると pendingSettings だけが変わり、「適用」で CurrentSettings に反映・保存される。
///
/// 注意: Edit > Project Settings > Script Execution Order で、
/// このスクリプトを「-100」など Default Time より前に設定してください。
/// </summary>
public class ConfigController : SerializedMonoBehaviour
{
    /// <summary>PlayerPrefs に設定の JSON を保存するときのキー。</summary>
    private const string PrefsKey = "GameSettingsJSON";

    /// <summary>AudioMixer 側で Expose しておくパラメーター名。</summary>
    private const string MasterVolumeParameter = "MasterVolume";

    public static ConfigController instance { get; private set; }

    // ===== Inspector =====

    [Header("UI Controller")]
    [SerializeField] public ConfigMenuUIController uiController;

    [Header("Game System References")]
    [SerializeField] private AudioMixer masterMixer;

    [Header("URP")]
    [Tooltip("プロジェクトのURP設定アセット（URP_High, URP_Mediumなど）")]
    [SerializeField] private UniversalRenderPipelineAsset urpAsset;

    [Header("Cinemachine")]
    [Tooltip("プレイヤーのFOVを制御するCinemachineカメラ (Cinemachine 3+)")]
    [SerializeField] private CinemachineCamera playerVirtualCamera;

    /// <summary>設定画面で変更中だが、まだ「適用」されていない設定。</summary>
    [SerializeField] private GameSettings pendingSettings;

    /// <summary>現在ゲームに適用されている設定。</summary>
    public GameSettings CurrentSettings { get; private set; }

    // ===== Unity ライフサイクル =====

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private async void Start()
    {
        LoadSettings();

        // 適用先（PlayerDataHub のカメラなど）の初期化が終わるのを 1 フレーム待ってから適用する
        await UniTask.Yield();
        ApplyAllSettings(CurrentSettings);
    }

    // ===== 公開 API: 設定画面の開閉 =====

    /// <summary>設定画面が開かれたときに呼ばれる。</summary>
    public void OnOpenConfig()
    {
        Debug.Log("ConfigController: 設定画面が開かれました。");

        if (uiController != null)
        {
            uiController.ShowDefaultPanelAndSetFocus();
        }
    }

    /// <summary>設定画面が閉じられたときに呼ばれる。</summary>
    /// <param name="returnSelected">閉じた後にフォーカスを戻す UI（ポーズメニューのボタンなど）。</param>
    public void OnCloseConfig(GameObject returnSelected)
    {
        Debug.Log("ConfigController: 設定画面が閉じられました。");

        if (returnSelected != null)
        {
            EventSystem.current.SetSelectedGameObject(returnSelected);
        }
    }

    // ===== 公開 API: 変更中の設定（pendingSettings）の操作 =====

    /// <summary>現在の設定をコピーして「変更中の設定」を作り直す。UI に表示するためにそれを返す。</summary>
    public GameSettings InitializePendingSettings()
    {
        pendingSettings = new GameSettings(CurrentSettings);
        return pendingSettings;
    }

    /// <summary>[適用] 変更中の設定を現在の設定として確定し、ゲームに適用して保存する。</summary>
    public void ApplyPendingSettings()
    {
        CurrentSettings = new GameSettings(pendingSettings);
        ApplyAllSettings(CurrentSettings);
        SaveSettings();
    }

    /// <summary>[リバート] 変更中の設定を破棄し、現在の設定を UI に表示し直す。</summary>
    public void RevertPendingSettings()
    {
        pendingSettings = new GameSettings(CurrentSettings);
        RefreshUI();
    }

    /// <summary>[リセット] 変更中の設定を初期値に戻す（「適用」するまでゲームには反映されない）。</summary>
    public void ResetToDefaults()
    {
        pendingSettings = CreateDefaultSettings();
        RefreshUI();

        Debug.Log("設定をデフォルトにリセットしました (まだ適用されていません)。");
    }

    // UI で値が変わるたびに呼ばれる。変更中の設定を書き換えるだけで、適用はしない

    // Graphics
    public void UpdatePendingResolution(int index) => pendingSettings.resolutionIndex = index;
    public void UpdatePendingFullscreenMode(int index) => pendingSettings.fullscreenMode = (FullScreenMode)index;
    public void UpdatePendingVsync(bool isOn) => pendingSettings.vsync = isOn;
    public void UpdatePendingShadows(int index) => pendingSettings.shadowQuality = index;
    public void UpdatePendingTextures(int index) => pendingSettings.textureQuality = index;
    public void UpdatePendingAntiAliasing(int index) => pendingSettings.antiAliasing = index;

    // Audio
    public void UpdatePendingMasterVolume(float value) => pendingSettings.masterVolume = value;

    // Gameplay
    public void UpdatePendingMouseSensitivity(float value) => pendingSettings.mouseSensitivity = value;
    public void UpdatePendingInvertY(bool isOn) => pendingSettings.invertCameraY = isOn;
    public void UpdatePendingFOV(float value) => pendingSettings.fov = value;
    public void UpdatePendingCameraBob(int index) => pendingSettings.cameraBob = index;

    private void RefreshUI()
    {
        if (uiController != null)
        {
            uiController.SetAllUIValues(pendingSettings);
        }
    }

    // ===== ロード / セーブ =====

    private void LoadSettings()
    {
        string json = PlayerPrefs.GetString(PrefsKey, "");

        CurrentSettings = string.IsNullOrEmpty(json)
            ? CreateDefaultSettings()
            : JsonUtility.FromJson<GameSettings>(json);

        // ここでは読み込むだけ。ゲームへの適用は Start の 1 フレーム後に行う
    }

    private void SaveSettings()
    {
        string json = JsonUtility.ToJson(CurrentSettings, true);
        PlayerPrefs.SetString(PrefsKey, json);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// 初期設定を作る。解像度だけはモニターがサポートする最大の解像度にする
    /// （Screen.resolutions は低解像度 → 高解像度の順に並んでいる前提で、末尾を使う）。
    /// </summary>
    private GameSettings CreateDefaultSettings()
    {
        var settings = new GameSettings();

        Resolution[] resolutions = Screen.resolutions;
        Resolution maxResolution = resolutions[resolutions.Length - 1];
        settings.resolutionIndex = FindResolutionIndex(maxResolution);

        return settings;
    }

    /// <summary>指定の解像度が Screen.resolutions の何番目かを返す。</summary>
    private int FindResolutionIndex(Resolution target)
    {
        Resolution[] resolutions = Screen.resolutions;

        // まずはリフレッシュレートまで一致するものを探す
        for (int i = 0; i < resolutions.Length; i++)
        {
            if (resolutions[i].width == target.width &&
                resolutions[i].height == target.height &&
                resolutions[i].refreshRateRatio.Equals(target.refreshRateRatio))
            {
                return i;
            }
        }

        // 無ければ、幅と高さだけ一致する最初のもの
        for (int i = 0; i < resolutions.Length; i++)
        {
            if (resolutions[i].width == target.width &&
                resolutions[i].height == target.height)
            {
                return i;
            }
        }

        // それでも無ければ最大のもの
        return resolutions.Length - 1;
    }

    // ===== 設定の適用 =====

    private void ApplyAllSettings(GameSettings settings)
    {
        ApplyGraphics(settings);
        ApplyAudio(settings);
        ApplyGameplay(settings);
    }

    private void ApplyGraphics(GameSettings settings)
    {
        // 解像度・ウィンドウモード
        Resolution[] resolutions = Screen.resolutions;
        if (settings.resolutionIndex >= 0 && settings.resolutionIndex < resolutions.Length)
        {
            Resolution resolution = resolutions[settings.resolutionIndex];
            Screen.SetResolution(resolution.width, resolution.height, settings.fullscreenMode, resolution.refreshRateRatio);
        }

        QualitySettings.vSyncCount = settings.vsync ? 1 : 0;

        // globalTextureMipmapLimit も 0 が最高画質なので、設定値をそのまま使える
        QualitySettings.globalTextureMipmapLimit = settings.textureQuality;

        // ここから下は URP アセットを書き換える設定
        if (urpAsset == null)
        {
            Debug.LogWarning("URP Asset が ConfigController に設定されていません。グラフィック設定（AA, Shadow）は適用されません。");
            return;
        }

        // アンチエイリアス: URP の MSAA は 1 = Off, 2 = 2x
        urpAsset.msaaSampleCount = settings.antiAliasing <= 0 ? 1 : 2;

        ApplyShadowQuality(settings.shadowQuality);
    }

    /// <summary>影の品質（0: High, 1: Medium, 2: Low。範囲外は近い方に丸める）。</summary>
    private void ApplyShadowQuality(int shadowQuality)
    {
        switch (Mathf.Clamp(shadowQuality, 0, 2))
        {
            case 0: // High
                urpAsset.additionalLightsShadowmapResolution = urpAsset.additionalLightsShadowResolutionTierHigh;
                urpAsset.mainLightShadowmapResolution = urpAsset.additionalLightsShadowResolutionTierHigh;
                urpAsset.shadowDistance = 150f;
                urpAsset.shadowCascadeCount = 4;
                break;

            case 1: // Medium
                urpAsset.additionalLightsShadowmapResolution = urpAsset.additionalLightsShadowResolutionTierMedium;
                urpAsset.mainLightShadowmapResolution = urpAsset.additionalLightsShadowResolutionTierMedium;
                urpAsset.shadowDistance = 100f;
                urpAsset.shadowCascadeCount = 2;
                break;

            case 2: // Low
                urpAsset.additionalLightsShadowmapResolution = urpAsset.additionalLightsShadowResolutionTierLow;
                urpAsset.mainLightShadowmapResolution = urpAsset.additionalLightsShadowResolutionTierLow;
                urpAsset.shadowDistance = 50f;
                urpAsset.shadowCascadeCount = 1;
                break;
        }
    }

    private void ApplyAudio(GameSettings settings)
    {
        if (masterMixer == null)
        {
            Debug.LogWarning("AudioMixer が ConfigController に設定されていません。");
            return;
        }

        masterMixer.SetFloat(MasterVolumeParameter, ConvertToDecibel(settings.masterVolume));
    }

    /// <summary>
    /// FOV とカメラの揺れを適用する。
    /// マウス感度と上下反転はここでは適用せず、プレイヤーの入力側が CurrentSettings を直接読む。
    /// </summary>
    private void ApplyGameplay(GameSettings settings)
    {
        // シーンごとにカメラが変わるので、PlayerDataHub から最新のものを取り直す
        if (PlayerDataHub.instance != null && PlayerDataHub.instance.CurrentCMFPSController != null)
        {
            playerVirtualCamera = PlayerDataHub.instance.CurrentCMFPSController.cmCamera;
        }

        if (playerVirtualCamera == null)
        {
            Debug.LogWarning("PlayerVirtualCamera が ConfigController に設定されていないか、PlayerDataHubから取得できませんでした。FOVは適用されません。");
            return;
        }

        // Lens は構造体なので、取り出して書き換えてから代入し直す
        var lens = playerVirtualCamera.Lens;
        lens.FieldOfView = settings.fov;
        playerVirtualCamera.Lens = lens;

        // カメラの揺れは、コンポーネントごと無効にして止める
        var bobExtension = playerVirtualCamera.GetComponent<FPSCameraBobExtension>();
        if (bobExtension != null)
        {
            bobExtension.enabled = settings.cameraBob == 0;
        }
    }

    /// <summary>音量（0 〜 1）をデシベル（-80 〜 0）に変換する。</summary>
    private float ConvertToDecibel(float volume)
    {
        // 0 だと -∞ になるので、ごく小さい値で下限を切る
        return Mathf.Log10(Mathf.Max(volume, 0.0001f)) * 20f;
    }
}

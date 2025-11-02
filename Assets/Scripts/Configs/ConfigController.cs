using UnityEngine;
using UnityEngine.Audio;
using System.IO;
using UnityEngine.Rendering.Universal; // URPアセットを操作するために必要
using Unity.Cinemachine;
using Sirenix.OdinInspector;
using Cysharp.Threading.Tasks; // Cinemachineの仮想カメラを操作するために必要
using UnityEngine.EventSystems; // ★★★ EventSystem を使うために追加 ★★★

/// <summary>
/// すべてのゲーム設定を保持するシリアライズ可能なクラス。
/// (GameSettings クラスは省略 ... 変更なし)
/// </summary>
[System.Serializable]
public class GameSettings
{
    [Header("Graphics")]
    public int resolutionIndex; // Screen.resolutions のインデックス
    public FullScreenMode fullscreenMode;
    public bool vsync;
    public int shadowQuality; // 0: Low, 1: Medium, 2: High (QualitySettingsに対応)
    public int textureQuality; // 0: High, 1: Medium, 2: Low (masterTextureLimit)
    public int antiAliasing; // 0: 0x, 1: 2x, 2: 4x, 3: 8x

    [Header("Audio")]
    public float masterVolume;
    public float musicVolume;
    public float sfxVolume;

    [Header("Gameplay")]
    public float mouseSensitivity;
    public bool invertCameraY;
    public float fov;

    /// <summary>
    /// デフォルト設定で初期化するコンストラクタ
    /// </summary>
    public GameSettings()
    {
        // デフォルト値
        resolutionIndex = -1; // -1は「現在の解像度」を示すために使う（後で設定）
        fullscreenMode = FullScreenMode.FullScreenWindow;
        vsync = true;
        shadowQuality = 1; // Medium
        textureQuality = 0; // High
        antiAliasing = 1; // 2x

        masterVolume = 0.8f;
        musicVolume = 0.8f;
        sfxVolume = 0.8f;

        mouseSensitivity = 1.0f;
        invertCameraY = false;
        fov = 70f;
    }

    /// <summary>
    /// 既存の設定をコピーするコンストラクタ（適用・リバート処理用）
    /// </summary>
    public GameSettings(GameSettings other)
    {
        resolutionIndex = other.resolutionIndex;
        fullscreenMode = other.fullscreenMode;
        vsync = other.vsync;
        shadowQuality = other.shadowQuality;
        textureQuality = other.textureQuality;
        antiAliasing = other.antiAliasing;

        masterVolume = other.masterVolume;
        musicVolume = other.musicVolume;
        sfxVolume = other.sfxVolume;

        mouseSensitivity = other.mouseSensitivity;
        invertCameraY = other.invertCameraY;
        fov = other.fov;
    }
}


/// <summary>
/// ゲーム設定の管理（ロード、保存、適用）を行う。
/// </summary>
public class ConfigController : SerializedMonoBehaviour
{
    public static ConfigController Instance { get; private set; }

    [Header("UI Controller")]
    [SerializeField] private ConfigMenuUIController uiController;

    [Header("Game System References")]
    [SerializeField] private AudioMixer masterMixer;

    [Header("URP")]
    [Tooltip("プロジェクトのURP設定アセット（URP_High, URP_Mediumなど）")]
    [SerializeField] private UniversalRenderPipelineAsset urpAsset;

    [Header("Cinemachine")]
    [Tooltip("プレイヤーのFOVを制御するCinemachineカメラ (Cinemachine 3+)")]
    [SerializeField] private CinemachineCamera playerVirtualCamera;

    // (中略 ... Awake, Start, Load/Save/ApplyAllSettings, ConvertToDecibel, GetCurrentResolutionIndex などは変更なし)
    
    // PlayerPrefsに保存するための単一キー
    private const string PREFS_KEY = "GameSettingsJSON";

    /// <summary>
    /// 現在ゲームに適用されている設定
    /// </summary>
    public GameSettings CurrentSettings { get; private set; }

    /// <summary>
    /// UIで変更中だが、まだ「適用」されていない一時的な設定
    /// </summary>
    [SerializeField] private GameSettings pendingSettings;

    void Awake()
    {
        // シングルトン設定
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            LoadSettings();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    async void Start()
    {
        await UniTask.Yield();
        ApplyAllSettings(CurrentSettings);
    }
    
    
    // ▼▼▼▼▼ ★★★ ここを修正 ★★★ ▼▼▼▼▼
    /// <summary>
    /// 設定画面が（PauseMenuUIControllerによって）開かれたときに呼び出される
    /// </summary>
    /// <param name="firstSelected">設定画面で最初にフォーカスするGameObject</param>
    public void OnOpenConfig(GameObject firstSelected)
    {
        Debug.Log("ConfigController: 設定画面が開かれました。");
        // 必要であれば、ここでアクションマップの切り替えなどを行う
        
        // ★ご指摘の通り、フォーカス設定をここで行う
        if (firstSelected != null)
        {
            EventSystem.current.SetSelectedGameObject(firstSelected);
        }
    }

    /// <summary>
    /// 設定画面が（PauseMenuUIControllerによって）閉じられたときに呼び出される
    /// </summary>
    /// <param name="returnSelected">ポーズメニューに戻った際にフォーカスするGameObject</param>
    public void OnCloseConfig(GameObject returnSelected)
    {
        Debug.Log("ConfigController: 設定画面が閉じられました。");
        // 必要であれば、ここでアクションマップの切り替えなどを行う
        
        // ★閉じる際のフォーカスもここで制御
        if (returnSelected != null)
        {
            EventSystem.current.SetSelectedGameObject(returnSelected);
        }
    }
    // ▲▲▲▲▲ ★★★ ここを修正 ★★★ ▲▲▲▲▲
    
    
    /// <summary>
    /// PlayerPrefsからJSONを読み込み、設定をロードする
    /// </summary>
    private void LoadSettings()
    {
        string json = PlayerPrefs.GetString(PREFS_KEY, "");

        if (string.IsNullOrEmpty(json))
        {
            CurrentSettings = new GameSettings();
            Resolution currentRes = Screen.currentResolution;
            CurrentSettings.resolutionIndex = GetCurrentResolutionIndex(currentRes);
        }
        else
        {
            CurrentSettings = JsonUtility.FromJson<GameSettings>(json);
        }
    }

    /// <summary>
    /// 現在の設定をJSONとしてPlayerPrefsに保存する
    /// </summary>
    private void SaveSettings()
    {
        string json = JsonUtility.ToJson(CurrentSettings, true); // trueで整形
        PlayerPrefs.SetString(PREFS_KEY, json);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// 指定された設定をゲーム全体に適用する
    /// </summary>
    private void ApplyAllSettings(GameSettings settings)
    {
        ApplyGraphics(settings);
        ApplyAudio(settings);
        ApplyGameplay(settings);
    }

    // --- 各カテゴリの適用処理 ---

    private void ApplyGraphics(GameSettings settings)
    {
        // 解像度
        if (settings.resolutionIndex >= 0 && settings.resolutionIndex < Screen.resolutions.Length)
        {
            Resolution res = Screen.resolutions[settings.resolutionIndex];
            Screen.SetResolution(res.width, res.height, settings.fullscreenMode, res.refreshRate);
        }
        QualitySettings.vSyncCount = settings.vsync ? 1 : 0;
        QualitySettings.globalTextureMipmapLimit = settings.textureQuality;
        if (urpAsset == null)
        {
            Debug.LogWarning("URP Asset が ConfigController に設定されていません。");
            return;
        }
        int[] aaValues = { 1, 2, 4, 8 };
        urpAsset.msaaSampleCount = aaValues[Mathf.Clamp(settings.antiAliasing, 0, aaValues.Length - 1)];
        switch (settings.shadowQuality)
        {
            case 0: // Low
                urpAsset.mainLightShadowmapResolution = 1024;
                urpAsset.shadowDistance = 50f;
                urpAsset.shadowCascadeCount = 1; 
                break;
            case 1: // Medium
                urpAsset.mainLightShadowmapResolution = 2028; // (タイポ修正: 2048)
                urpAsset.shadowDistance = 100f;
                urpAsset.shadowCascadeCount = 2;
                break;
            case 2: // High
                urpAsset.mainLightShadowmapResolution = 4096;
                urpAsset.shadowDistance = 150f;
                urpAsset.shadowCascadeCount = 4;
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
        masterMixer.SetFloat("MasterVolumeParam", ConvertToDecibel(settings.masterVolume));
        masterMixer.SetFloat("MusicVolumeParam", ConvertToDecibel(settings.musicVolume));
        masterMixer.SetFloat("SFXVolumeParam", ConvertToDecibel(settings.sfxVolume));
    }
    
    private void ApplyGameplay(GameSettings settings)
    {
        if(PlayerDataHub.instance != null)
        {
            if (PlayerDataHub.instance.CurrentCMFPSController != null)
            {
                playerVirtualCamera = PlayerDataHub.instance.CurrentCMFPSController.cmCamera;
            }
        }
        
        if (playerVirtualCamera != null)
        {
            var lens = playerVirtualCamera.Lens;
            lens.FieldOfView = settings.fov;
            playerVirtualCamera.Lens = lens;
        }
        else
        {
            Debug.LogWarning("PlayerVirtualCamera が ConfigController に設定されていませんでした。");
        }
    }

    // ボリューム(0-1)をデシベル(-80 to 0)に変換
    private float ConvertToDecibel(float volume)
    {
        return Mathf.Log10(Mathf.Max(volume, 0.0001f)) * 20f;
    }

    // 現在の解像度がScreen.resolutionsの何番目かを取得
    private int GetCurrentResolutionIndex(Resolution currentRes)
    {
        for (int i = 0; i < Screen.resolutions.Length; i++)
        {
            if (Screen.resolutions[i].width == currentRes.width &&
                Screen.resolutions[i].height == currentRes.height &&
                Screen.resolutions[i].refreshRate == currentRes.refreshRate)
            {
                return i;
            }
        }
        for (int i = 0; i < Screen.resolutions.Length; i++)
        {
            if (Screen.resolutions[i].width == currentRes.width &&
                Screen.resolutions[i].height == currentRes.height)
            {
                return i;
            }
        }
        return Screen.resolutions.Length - 1; 
    }


    // --- UIControllerから呼び出されるメソッド群 ---

    public GameSettings InitializePendingSettings()
    {
        pendingSettings = new GameSettings(CurrentSettings);
        return pendingSettings;
    }

    public void ApplyPendingSettings()
    {
        CurrentSettings = new GameSettings(pendingSettings);
        ApplyAllSettings(CurrentSettings);
        SaveSettings();
    }

    public void RevertPendingSettings()
    {
        pendingSettings = new GameSettings(CurrentSettings);
        
        if (uiController != null)
        {
            uiController.SetAllUIValues(pendingSettings);
        }
    }

    // --- UIからの値変更（PendingSettingsを更新するだけ） ---

    // Graphics
    public void UpdatePendingResolution(int index) { pendingSettings.resolutionIndex = index; }
    public void UpdatePendingFullscreenMode(int index) { pendingSettings.fullscreenMode = (FullScreenMode)index; }
    public void UpdatePendingVsync(bool isOn) { pendingSettings.vsync = isOn; }
    public void UpdatePendingShadows(int index) { pendingSettings.shadowQuality = index; }
    public void UpdatePendingTextures(int index) { pendingSettings.textureQuality = index; }
    public void UpdatePendingAntiAliasing(int index) { pendingSettings.antiAliasing = index; }

    // Audio
    public void UpdatePendingMasterVolume(float value) { pendingSettings.masterVolume = value; }
    public void UpdatePendingMusicVolume(float value) { pendingSettings.musicVolume = value; }
    public void UpdatePendingSFXVolume(float value) { pendingSettings.sfxVolume = value; }

    // Gameplay
    public void UpdatePendingMouseSensitivity(float value) { pendingSettings.mouseSensitivity = value; }
    public void UpdatePendingInvertY(bool isOn) { pendingSettings.invertCameraY = isOn; }
    public void UpdatePendingFOV(float value) { pendingSettings.fov = value; }
}
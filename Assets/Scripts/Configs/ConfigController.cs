using UnityEngine;
using UnityEngine.Audio; // AudioMixerを使うため
using UnityEngine.Rendering.Universal; // URPの設定（今回はQualitySettingsを使用）

/// <summary>
/// ゲームの設定値を管理し、保存・読み込み・適用を行う。
/// ConfigMenuUIControllerと連携して動作する。
/// </summary>
using UnityEngine;
using UnityEngine.Audio;
using System.IO; // JsonUtilityを使うため（JsonUtilityはCoreに含まれます）
/// <summary>
/// すべてのゲーム設定を保持するシリアライズ可能なクラス。
/// このクラスのインスタンスがJSONに変換されて保存されます。
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
        fov = 60f;
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
/// シングルトンとして機能し、他のスクリプトから設定値（CurrentSettings）を読み取れるようにする。
/// </summary>
public class ConfigController : MonoBehaviour
{
    public static ConfigController Instance { get; private set; }

    [Header("UI Controller")]
    [SerializeField] private ConfigMenuUIController uiController;

    [Header("Game System References")]
    [SerializeField] private AudioMixer masterMixer;
    [SerializeField] private Camera mainCamera; // FOV適用のため

    // PlayerPrefsに保存するための単一キー
    private const string PREFS_KEY = "GameSettingsJSON";

    /// <summary>
    /// 現在ゲームに適用されている設定
    /// </summary>
    public GameSettings CurrentSettings { get; private set; }

    /// <summary>
    /// UIで変更中だが、まだ「適用」されていない一時的な設定
    /// </summary>
    private GameSettings pendingSettings;

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

    /// <summary>
    /// PlayerPrefsからJSONを読み込み、設定をロードする
    /// </summary>
    private void LoadSettings()
    {
        string json = PlayerPrefs.GetString(PREFS_KEY, "");

        if (string.IsNullOrEmpty(json))
        {
            CurrentSettings = new GameSettings();
            
            // 初回起動時、現在の解像度をデフォルトとして設定
            Resolution currentRes = Screen.currentResolution;
            CurrentSettings.resolutionIndex = GetCurrentResolutionIndex(currentRes);
        }
        else
        {
            CurrentSettings = JsonUtility.FromJson<GameSettings>(json);
        }

        // 読み込んだ設定をゲームに適用
        ApplyAllSettings(CurrentSettings);
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

        // VSync
        QualitySettings.vSyncCount = settings.vsync ? 1 : 0;

        // シャドウ (Project Settings > Quality で設定したレベルに対応させる想定)
        // 例: 0=Low(Disable), 1=Medium, 2=High
        // ここではシャドウ解像度を例として変更します
        UnityEngine.ShadowResolution[] shadowRes = { UnityEngine.ShadowResolution.Low, UnityEngine.ShadowResolution.Medium, UnityEngine.ShadowResolution.High };
        QualitySettings.shadowResolution = shadowRes[Mathf.Clamp(settings.shadowQuality, 0, shadowRes.Length - 1)];

        // テクスチャ (0: High, 1: Medium, 2: Low)
        QualitySettings.globalTextureMipmapLimit = settings.textureQuality; // 0=フル, 1=1/2, 2=1/4...

        // アンチエイリアス (0: 0x, 1: 2x, 2: 4x, 3: 8x)
        int[] aaValues = { 0, 2, 4, 8 };
        QualitySettings.antiAliasing = aaValues[Mathf.Clamp(settings.antiAliasing, 0, aaValues.Length - 1)];
    }

    private void ApplyAudio(GameSettings settings)
    {
        if (masterMixer == null) return;
        
        // ※ AudioMixer側で "MasterVolumeParam", "MusicVolumeParam", "SFXVolumeParam" 
        //    という名前の変数をExposeしておく必要があります。
        masterMixer.SetFloat("MasterVolumeParam", ConvertToDecibel(settings.masterVolume));
        masterMixer.SetFloat("MusicVolumeParam", ConvertToDecibel(settings.musicVolume));
        masterMixer.SetFloat("SFXVolumeParam", ConvertToDecibel(settings.sfxVolume));
    }
    
    private void ApplyGameplay(GameSettings settings)
    {
        // FOV
        if (mainCamera != null)
        {
            mainCamera.fieldOfView = settings.fov;
        }

        // マウス感度やカメラ反転は、Playerの入力スクリプト側で
        // ConfigController.Instance.CurrentSettings.mouseSensitivity などを参照して利用します。
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
        return Screen.resolutions.Length - 1; // 見つからなければ最後（最大）のものをデフォルトとする
    }


    // --- UIControllerから呼び出されるメソッド群 ---

    /// <summary>
    /// UIを開いたときに呼ばれ、現在の設定を「変更中」のpendingSettingsにコピーする
    /// </summary>
    /// <returns>UIに表示するためのpendingSettings</returns>
    public GameSettings InitializePendingSettings()
    {
        // CurrentSettingsの完全なコピーを作成
        pendingSettings = new GameSettings(CurrentSettings);
        return pendingSettings;
    }

    /// <summary>
    /// [適用]ボタン。pendingSettingsをCurrentSettingsに上書きし、適用・保存する
    /// </summary>
    public void ApplyPendingSettings()
    {
        CurrentSettings = new GameSettings(pendingSettings);
        ApplyAllSettings(CurrentSettings);
        SaveSettings();
    }

    /// <summary>
    /// [リバート]ボタン。pendingSettingsを破棄し、CurrentSettingsをUIに再反映させる
    /// </summary>
    public void RevertPendingSettings()
    {
        // pendingを現在の設定に戻す
        pendingSettings = new GameSettings(CurrentSettings);
        
        // UIにも反映
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
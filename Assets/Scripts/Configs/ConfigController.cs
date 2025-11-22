using UnityEngine;
using UnityEngine.Audio;
using System.IO;
using UnityEngine.Rendering.Universal; // URPアセットを操作するために必要
using Unity.Cinemachine;
using Sirenix.OdinInspector;
using Cysharp.Threading.Tasks; // Cinemachineの仮想カメラを操作するために必要
using UnityEngine.EventSystems; // ★ EventSystem を使うために追加

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
    public int shadowQuality; // 0: High, 1: Medium, 2: Low (0=High に統一)
    public int textureQuality; // 0: High, 1: Medium, 2: Low (masterTextureLimit)
    public int antiAliasing; // 0: 0x, 1: 2x, 2: 4x, 3: 8x

    [Header("Audio")]
    public float masterVolume;
    // public float musicVolume;
    // public float sfxVolume;

    [Header("Gameplay")]
    public float mouseSensitivity;
    public bool invertCameraY;
    public float fov;
    public int cameraBob;
    /// <summary>
    /// デフォルト設定で初期化するコンストラクタ
    /// </summary>
    public GameSettings()
    {
        // デフォルト値
        resolutionIndex = -1; // -1は「現在の解像度」を示すために使う（後で設定）
        fullscreenMode = FullScreenMode.ExclusiveFullScreen;
        vsync = true;
        shadowQuality = 0; // High
        textureQuality = 0; // High
        antiAliasing = 1; // 2x

        masterVolume = 0.8f;
        // musicVolume = 0.8f;
        // sfxVolume = 0.8f;
        cameraBob = 1;
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
        // musicVolume = other.musicVolume;
        // sfxVolume = other.sfxVolume;
        cameraBob = other.cameraBob;
        mouseSensitivity = other.mouseSensitivity;
        invertCameraY = other.invertCameraY;
        fov = other.fov;
    }
}

/// <summary>
/// ゲーム設定の管理（ロード、保存、適用）を行う。
/// シングルトンとして機能し、他のスクリプトから設定値（CurrentSettings）を読み取れるようにする。
/// ★注意: Edit > Project Settings > Script Execution Order で、
/// このスクリプトを「-100」など、Default Time より前に設定してください。
/// </summary>
public class ConfigController : SerializedMonoBehaviour
{
    public static ConfigController instance { get; private set; }

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
    private FPSCameraBobExtension cameraBobExtension;
    void Awake()
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

    async void Start()
    {
        LoadSettings();
        await UniTask.Yield();
        // 全てのAwake()が完了したこのタイミングで、
        // 読み込み済みの CurrentSettings をゲームに「適用(Push)」する。
        // これで PlayerDataHub.instance などが null ではなくなる。
        ApplyAllSettings(CurrentSettings);
    }
    
    // ▼▼▼▼▼ ★★★ ここを修正 ★★★ ▼▼▼▼▼
    /// <summary>
    /// 設定画面が（PauseMenuUIControllerによって）開かれたときに呼び出される
    /// </summary>
    public void OnOpenConfig() // 引数を削除
    {
        Debug.Log("ConfigController: 設定画面が開かれました。");
        
        // ★ConfigMenuUIControllerに、デフォルトパネルの表示とフォーカス設定を指示する
        if (uiController != null)
        {
            uiController.ShowDefaultPanelAndSetFocus();
        }
    } 

    /// <summary>
    /// 設定画面が（PauseMenuUIControllerによって）閉じられたときに呼び出される
    /// </summary>
    /// <param name="returnSelected">ポーズメニューに戻った際にフォーカスするGameObject</param>
    public void OnCloseConfig(GameObject returnSelected)
    {
        Debug.Log("ConfigController: 設定画面が閉じられました。");
        
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
                
                // ▼▼▼ ★★★ (ここから修正) ★★★ ▼▼▼
                // Resolution currentRes = Screen.currentResolution; // ← 信頼できない
                
                // モニターがサポートする最大の解像度（リストの最後）を取得する
                // (リストは低解像度->高解像度の順でソートされている前提)
                Resolution currentRes = Screen.resolutions[Screen.resolutions.Length - 1]; 
                // ▲▲▲ ★★★ (ここまで修正) ★★★ ▲▲▲

                // 取得した最大解像度のインデックスをデフォルトとして設定
                CurrentSettings.resolutionIndex = GetCurrentResolutionIndex(currentRes);
            }
            else
            {
                CurrentSettings = JsonUtility.FromJson<GameSettings>(json);
            }
            
            // ★変更点: ここで ApplyAllSettings() を「呼ばない」
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
            // Screen.SetResolution(res.width, res.height, settings.fullscreenMode, res.refreshRate);
            Screen.SetResolution(res.width, res.height, settings.fullscreenMode, res.refreshRateRatio);
        }

        // VSync (これはQualitySettingsでOK)
        QualitySettings.vSyncCount = settings.vsync ? 1 : 0;

        // テクスチャ (0=High, 1=Medium, 2=Low)
        // QualitySettings.globalTextureMipmapLimit は 0=High, 1=Medium, 2=Low なので、
        // settings.textureQuality の値をそのまま代入すればOK。
        QualitySettings.globalTextureMipmapLimit = settings.textureQuality;

        // --- URP設定 ---
        if (urpAsset == null)
        {
            Debug.LogWarning("URP Asset が ConfigController に設定されていません。グラフィック設定（AA, Shadow）は適用されません。");
            return;
        }

        // アンチエイリアス (0: Off, 1: On (2x))
        // URPのMSAAは 1=Off, 2=2x
        int[] aaMap = { 1, 2 }; // 0(Off) -> 1, 1(On) -> 2
        int msaaValue = aaMap[Mathf.Clamp(settings.antiAliasing, 0, aaMap.Length - 1)];
        urpAsset.msaaSampleCount = msaaValue;

        // シャドウ品質 (0: High, 1: Medium, 2: Low)
        // settings.shadowQuality (0=High, 1=Medium, 2=Low) を
        // switch文の (0=Low, 1=Medium, 2=High) に変換するマップ
        int[] shadowMap = { 2, 1, 0 }; // 0(High) -> 2, 1(Medium) -> 1, 2(Low) -> 0
        
        // クランプ処理も追加
        int shadowSetting = shadowMap[Mathf.Clamp(settings.shadowQuality, 0, shadowMap.Length - 1)];

        // ※これは一例です。URPアセットのどの項目を変更するかはプロジェクトによります
        switch (shadowSetting) // ← ここで変換後の値を使う
        {
            case 0: // Low
                urpAsset.additionalLightsShadowmapResolution = urpAsset.additionalLightsShadowResolutionTierLow;
                urpAsset.mainLightShadowmapResolution = urpAsset.additionalLightsShadowResolutionTierLow;
                urpAsset.shadowDistance = 50f;
                urpAsset.shadowCascadeCount = 1; // カスケード数も変更する例
                break;
            case 1: // Medium
                urpAsset.additionalLightsShadowmapResolution = urpAsset.additionalLightsShadowResolutionTierMedium;
                urpAsset.mainLightShadowmapResolution = urpAsset.additionalLightsShadowResolutionTierMedium;
                urpAsset.shadowDistance = 100f;
                urpAsset.shadowCascadeCount = 2;
                break;
            case 2: // High
                urpAsset.additionalLightsShadowmapResolution = urpAsset.additionalLightsShadowResolutionTierHigh;
                urpAsset.mainLightShadowmapResolution = urpAsset.additionalLightsShadowResolutionTierHigh;
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
        
        // ※ AudioMixer側で "MasterVolumeParam", "MusicVolumeParam", "SFXVolumeParam" 
        //    という名前の変数をExposeしておく必要があります。
        masterMixer.SetFloat("MasterVolume", ConvertToDecibel(settings.masterVolume));
    }
    
    private void ApplyGameplay(GameSettings settings)
    {

        // Start() で呼ばれるため、PlayerDataHub.instance が存在するはず
        if(PlayerDataHub.instance != null)
        {
            // PlayerDataHubから最新のカメラを取得
            if (PlayerDataHub.instance.CurrentCMFPSController != null)
            {
                playerVirtualCamera = PlayerDataHub.instance.CurrentCMFPSController.cmCamera;
            }
        }
        
        // FOV
        if (playerVirtualCamera != null)
        {
            // Cinemachine 3+: Lens は値型(struct)なので一度取得して変更してから再代入する
            var lens = playerVirtualCamera.Lens;
            lens.FieldOfView = settings.fov;
            playerVirtualCamera.Lens = lens;
            var bobExtension = playerVirtualCamera.GetComponent<FPSCameraBobExtension>();
            if (bobExtension != null)
            {
                // 設定が 0 なら true (ON), 1 なら false (OFF)
                // コンポーネント自体の enabled を切ることで処理を完全に停止させます
                bobExtension.enabled = (settings.cameraBob == 0);
            }
        }
        else
        {
            // Start() の時点でもカメラが見つからない場合
            Debug.LogWarning("PlayerVirtualCamera が ConfigController に設定されていないか、PlayerDataHubから取得できませんでした。FOVは適用されません。");
        }

        // マウス感度やカメラ反転は、Playerの入力スクリプト側で
        // ConfigController.instance.CurrentSettings.mouseSensitivity などを参照して利用します。
        // このスクリプト(ConfigController)側で直接適用する処理はありません。
    }

    // ボリューム(0-1)をデシベル(-80 to 0)に変換
    private float ConvertToDecibel(float volume)
    {
        // 0になると-infinityになるため、極小値でクランプ
        return Mathf.Log10(Mathf.Max(volume, 0.0001f)) * 20f;
    }

    // 現在の解像度がScreen.resolutionsの何番目かを取得
    private int GetCurrentResolutionIndex(Resolution currentRes)
    {
        for (int i = 0; i < Screen.resolutions.Length; i++)
        {
            // リフレッシュレートまで完全に一致するものを探す
            if (Screen.resolutions[i].width == currentRes.width &&
                Screen.resolutions[i].height == currentRes.height &&
                Screen.resolutions[i].refreshRateRatio.Equals(currentRes.refreshRateRatio))
            {
                return i;
            }
        }
        
        // 見つからなければ、幅と高さが一致する最初のエントリを探す（リフレッシュレート違い）
        for (int i = 0; i < Screen.resolutions.Length; i++)
        {
             if (Screen.resolutions[i].width == currentRes.width &&
                 Screen.resolutions[i].height == currentRes.height)
             {
                 return i;
             }
        }
        
        return Screen.resolutions.Length - 1; // それでも見つからなければ最後（最大）のものをデフォルトとする
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

    // ▼▼▼ ★★★ この関数を新しく追加 ★★★ ▼▼▼
    /// <summary>
    /// [リセット] (Rキー)。pendingSettingsを「工場出荷時」のデフォルト設定に戻す
    /// </summary>
    /// <summary>
    /// [リセット] (Rキー)。pendingSettingsを「工場出荷時」のデフォルト設定に戻す
    /// </summary>
    public void ResetToDefaults()
    {
        // 1. デフォルト設定を new GameSettings() で作成
        pendingSettings = new GameSettings();
        
        // ▼▼▼ ★★★ (ここから追加) ★★★ ▼▼▼
        // 2. 解像度だけ「現在のモニター解像度」で上書きする
        //    (LoadSettings と同じ初回起動時の動作)
        Resolution currentRes = Screen.resolutions[Screen.resolutions.Length - 1]; // 最大解像度を取得
        pendingSettings.resolutionIndex = GetCurrentResolutionIndex(currentRes);
        // ▲▲▲ ★★★ (ここまで追加) ★★★ ▲▲▲
        
        // 3. UIにも反映
        if (uiController != null)
        {
            uiController.SetAllUIValues(pendingSettings);
        }
        
        Debug.Log("設定をデフォルトにリセットしました (まだ適用されていません)。");
    }
    // ▲▲▲ ★★★ 追加ここまで ★★★ ▲▲▲

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
    // public void UpdatePendingMusicVolume(float value) { pendingSettings.musicVolume = value; }
    // public void UpdatePendingSFXVolume(float value) { pendingSettings.sfxVolume = value; }

    // Gameplay
    public void UpdatePendingMouseSensitivity(float value) { pendingSettings.mouseSensitivity = value; }
    public void UpdatePendingInvertY(bool isOn) { pendingSettings.invertCameraY = isOn; }
    public void UpdatePendingFOV(float value) { pendingSettings.fov = value; }
    public void UpdatePendingCameraBob(int index) { pendingSettings.cameraBob = index; }
}
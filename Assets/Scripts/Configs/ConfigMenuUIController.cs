using UnityEngine;
using UnityEngine.UI; // Button を使うため
using TMPro; // TextMeshPro
using System.Collections.Generic; // Listを使うため

/// <summary>
/// 設定メニューのパネル状態
/// </summary>
public enum ConfigPanelState
{
    Graphics,
    Audio,
    Gameplay
}

/// <summary>
/// オプション画面のUIコンポーネントを管理し、
/// ユーザー入力をConfigControllerに伝える。
/// 3つの設定パネル（グラフィック、音量、ゲームプレイ）の切り替えも行う。
/// </summary>
public class ConfigMenuUIController : MonoBehaviour
{
    [Header("Controller Reference")]
    [SerializeField] private ConfigController configController; // ※Inspectorで設定していても、OnEnableで上書きします

    [Header("Panels")]
    [SerializeField] private GameObject graphicsPanel;
    [SerializeField] private GameObject audioPanel;
    [SerializeField] private GameObject gameplayPanel;

    [Tooltip("このUIが有効になったときに、最初に表示するパネル")]
    [SerializeField] private ConfigPanelState defaultPanel = ConfigPanelState.Graphics;

    [Header("Tab Buttons")]
    [Tooltip("グラフィックパネルを表示するボタン")]
    [SerializeField] private Button graphicsTabButton;
    [Tooltip("音量パネルを表示するボタン")]
    [SerializeField] private Button audioTabButton;
    [Tooltip("ゲームプレイパネルを表示するボタン")]
    [SerializeField] private Button gameplayTabButton;
    [SerializeField] private Button ApplyButton; // 適用ボタン
    [SerializeField] private Button RevertButton; // リバート（戻す）ボタン（ある場合）

    [Header("Graphics UI")]
    // (中略 ... UIコンポーネントのSerializeField)
    [SerializeField] private TMP_Dropdown resolutionDropdown;
    [SerializeField] private TMP_Dropdown windowModeDropdown;
    [SerializeField] private Toggle vsyncToggle;
    [SerializeField] private TMP_Dropdown shadowDropdown;
    [SerializeField] private TMP_Dropdown textureDropdown;
    [SerializeField] private TMP_Dropdown antiAliasingDropdown;

    [Header("Audio UI")]
    [SerializeField] private Slider masterVolumeSlider;
    [SerializeField] private Slider musicVolumeSlider;
    [SerializeField] private Slider sfxVolumeSlider;

    [Header("Gameplay UI")]
    [SerializeField] private Slider mouseSensitivitySlider;
    [SerializeField] private Toggle invertYToggle;
    [SerializeField] private Slider fovSlider;


    private List<Resolution> resolutions;

    private void Awake()
    {
        // パネル切り替えボタンにリスナーを登録
        // これらはUIController自身を参照するためAwakeでOK
        if (graphicsTabButton != null)
            graphicsTabButton.onClick.AddListener(ShowGraphicsPanel);
        if (audioTabButton != null)
            audioTabButton.onClick.AddListener(ShowAudioPanel);
        if (gameplayTabButton != null)
            gameplayTabButton.onClick.AddListener(ShowGameplayPanel);
            
        // ConfigController (Instance) が必要なボタンは OnEnable で登録します
    }

    /// <summary>
    /// このUIパネルが表示されたときに呼ばれる
    /// </summary>
    void Start()
    {
        // ▼▼▼▼▼ 修正点 1 ▼▼▼▼▼
        // Inspectorで何が設定されていても、必ずシングルトンのInstanceを取得する
        // これで、不正なインスタンスを参照する問題が解決します。
        configController = ConfigController.Instance;

        // Instanceが（何らかの理由で）存在しない場合はエラーを出し、停止する
        if (configController == null)
        {
            Debug.LogError("ConfigController.Instance が見つかりません！シーンにConfigControllerが配置されているか確認してください。");
            return;
        }
        // ▲▲▲▲▲ 修正点 1 ▲▲▲▲▲


        // 解像度ドロップダウンを初期化
        PopulateResolutionDropdown();

        // ConfigControllerから現在の設定（のコピー）を取得
        // この時点で configController.CurrentSettings は null ではなくなります
        GameSettings settings = configController.InitializePendingSettings();

        // UIに値を設定
        SetAllUIValues(settings);

        // defaultPanel に基づいて初期表示を行う
        ShowPanel(defaultPanel);

        // ▼▼▼▼▼ 修正点 2 ▼▼▼▼▼
        // リスナーを OnEnable で登録
        if (ApplyButton != null)
            ApplyButton.onClick.AddListener(configController.ApplyPendingSettings);
        
        // リバートボタンも同様に
        if (RevertButton != null)
            RevertButton.onClick.AddListener(configController.RevertPendingSettings);
    }

    // ▼▼▼▼▼ 修正点 3 (OnDisable を追加) ▼▼▼▼▼
    /// <summary>
    /// このUIパネルが非表示になったときに呼ばれる
    /// </summary>
    private void OnDisable()
    {
        // OnEnable で登録したリスナーは、必ず OnDisable で解除します
        // (さもないと、UIを開き直すたびにリスナーが二重、三重に登録されてしまいます)
        if (configController != null) // configControllerが（シーン終了時などで）先に破棄される可能性に対処
        {
            if (ApplyButton != null)
                ApplyButton.onClick.RemoveListener(configController.ApplyPendingSettings);

            if (RevertButton != null)
                RevertButton.onClick.RemoveListener(configController.RevertPendingSettings);
        }
    }
    // ▲▲▲▲▲ 修正点 3 ▲▲▲▲▲

    // ▼▼▼▼▼ 修正点 4 (OnDestroy を修正) ▼▼▼▼▼
    private void OnDestroy()
    {
        // Awake で登録したリスナーを解除
        if (graphicsTabButton != null)
            graphicsTabButton.onClick.RemoveListener(ShowGraphicsPanel);
        if (audioTabButton != null)
            audioTabButton.onClick.RemoveListener(ShowAudioPanel);
        if (gameplayTabButton != null)
            gameplayTabButton.onClick.RemoveListener(ShowGameplayPanel);
        
        // OnDisable が呼ばれなかった場合に備えて、OnDestroyでも解除処理を試みる
        // (OnDisableのコードと重複するが、安全のため)
        if (configController != null)
        {
            if (ApplyButton != null)
                ApplyButton.onClick.RemoveListener(configController.ApplyPendingSettings);
            if (RevertButton != null)
                RevertButton.onClick.RemoveListener(configController.RevertPendingSettings);
        }
    }
    // ▲▲▲▲▲ 修正点 4 ▲▲▲▲▲


    /// <summary>
    /// 解像度ドロップダウンのリストをOSから取得して設定する
    /// </summary>
    private void PopulateResolutionDropdown()
    {
        // (中略 ... この関数の内容は変更なし)
        resolutions = new List<Resolution>(Screen.resolutions);
        resolutionDropdown.ClearOptions();

        List<string> options = new List<string>();
        foreach (var res in resolutions)
        {
            string option = $"{res.width} x {res.height} @ {res.refreshRate} Hz";
            options.Add(option);
        }
        resolutionDropdown.AddOptions(options);
    }

    /// <summary>
    /// ConfigControllerからロードした設定値をすべてのUIに反映させる
    /// </summary>
    public void SetAllUIValues(GameSettings settings)
    {
        // (中略 ... この関数の内容は変更なし)
        
        // --- リスナーを一時的に解除 ---
        resolutionDropdown.onValueChanged.RemoveAllListeners();
        windowModeDropdown.onValueChanged.RemoveAllListeners();
        vsyncToggle.onValueChanged.RemoveAllListeners();
        shadowDropdown.onValueChanged.RemoveAllListeners();
        textureDropdown.onValueChanged.RemoveAllListeners();
        antiAliasingDropdown.onValueChanged.RemoveAllListeners();
        masterVolumeSlider.onValueChanged.RemoveAllListeners();
        musicVolumeSlider.onValueChanged.RemoveAllListeners();
        sfxVolumeSlider.onValueChanged.RemoveAllListeners();
        mouseSensitivitySlider.onValueChanged.RemoveAllListeners();
        invertYToggle.onValueChanged.RemoveAllListeners();
        fovSlider.onValueChanged.RemoveAllListeners();

        // --- 値をUIにセット ---
        resolutionDropdown.value = settings.resolutionIndex;
        resolutionDropdown.RefreshShownValue(); 
        windowModeDropdown.value = (int)settings.fullscreenMode;
        vsyncToggle.isOn = settings.vsync;
        shadowDropdown.value = settings.shadowQuality;
        textureDropdown.value = settings.textureQuality;
        antiAliasingDropdown.value = settings.antiAliasing;
        masterVolumeSlider.value = settings.masterVolume;
        musicVolumeSlider.value = settings.musicVolume;
        sfxVolumeSlider.value = settings.sfxVolume;
        mouseSensitivitySlider.value = settings.mouseSensitivity;
        invertYToggle.isOn = settings.invertCameraY;
        fovSlider.value = settings.fov;

        // --- リスナーを再登録 ---
        resolutionDropdown.onValueChanged.AddListener(OnResolutionChanged);
        windowModeDropdown.onValueChanged.AddListener(OnWindowModeChanged);
        vsyncToggle.onValueChanged.AddListener(OnVsyncChanged);
        shadowDropdown.onValueChanged.AddListener(OnShadowsChanged);
        textureDropdown.onValueChanged.AddListener(OnTexturesChanged);
        antiAliasingDropdown.onValueChanged.AddListener(OnAntiAliasingChanged);
        masterVolumeSlider.onValueChanged.AddListener(OnMasterVolumeChanged);
        musicVolumeSlider.onValueChanged.AddListener(OnMusicVolumeChanged);
        sfxVolumeSlider.onValueChanged.AddListener(OnSFXVolumeChanged);
        mouseSensitivitySlider.onValueChanged.AddListener(OnMouseSensitivityChanged);
        invertYToggle.onValueChanged.AddListener(OnInvertYChanged);
        fovSlider.onValueChanged.AddListener(OnFOVChanged);
    }


    // --- パネル切り替えロジック ---
    private void ShowPanel(ConfigPanelState panelState)
    {
        // (中略 ... この関数の内容は変更なし)
        graphicsPanel.SetActive(panelState == ConfigPanelState.Graphics);
        audioPanel.SetActive(panelState == ConfigPanelState.Audio);
        gameplayPanel.SetActive(panelState == ConfigPanelState.Gameplay);
    }

    // --- パネル切り替えメソッド (ButtonのAddListenerから呼ばれる) ---
    public void ShowGraphicsPanel()
    {
        ShowPanel(ConfigPanelState.Graphics);
    }
    public void ShowAudioPanel()
    {
        ShowPanel(ConfigPanelState.Audio);
    }
    public void ShowGameplayPanel()
    {
        ShowPanel(ConfigPanelState.Gameplay);
    }

    // --- UIのOnValueChanged()から呼び出すメソッド群 ---
    // (中略 ... このセクションの関数群は変更なし)
    public void OnResolutionChanged(int index) => configController.UpdatePendingResolution(index);
    public void OnWindowModeChanged(int index) => configController.UpdatePendingFullscreenMode(index);
    public void OnVsyncChanged(bool isOn) => configController.UpdatePendingVsync(isOn);
    public void OnShadowsChanged(int index) => configController.UpdatePendingShadows(index);
    public void OnTexturesChanged(int index) => configController.UpdatePendingTextures(index);
    public void OnAntiAliasingChanged(int index) => configController.UpdatePendingAntiAliasing(index);
    public void OnMasterVolumeChanged(float value) => configController.UpdatePendingMasterVolume(value);
    public void OnMusicVolumeChanged(float value) => configController.UpdatePendingMusicVolume(value);
    public void OnSFXVolumeChanged(float value) => configController.UpdatePendingSFXVolume(value);
    public void OnMouseSensitivityChanged(float value) => configController.UpdatePendingMouseSensitivity(value);
    public void OnInvertYChanged(bool isOn) => configController.UpdatePendingInvertY(isOn);
    public void OnFOVChanged(float value)
    {
        configController.UpdatePendingFOV(value);
    }
}
using UnityEngine;
using UnityEngine.UI; // Button を使うため
using TMPro; // TextMeshPro
using System.Collections.Generic; // Listを使うため
using UnityEngine.EventSystems; // EventSystem を使うために追加
using Cysharp.Threading.Tasks; // ★ UniTask を使うために追加

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
/// (このスクリプトがアタッチされたGameObjectは常にアクティブであることを前提とする)
/// </summary>
public class ConfigMenuUIController : MonoBehaviour
{
    [Header("Controller Reference")]
    [SerializeField] private ConfigController configController; // ※Inspectorで設定していても、Startで上書きします

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
            
        // ConfigController (Instance) が必要なボタンは Start で登録します
    }

    /// <summary>
    /// このコンポーネントが有効になった最初のフレームで実行
    /// </summary>
    void Start()
    {
        // ▼▼▼ 修正: 1フレーム待機 ▼▼▼
        // ConfigController.Instance が Start() で1フレーム待機するため、
        // こちらも1フレーム待機してから Instance を参照する
        // ▲▲▲ 修正: 1フレーム待機 ▲▲▲

        // Inspectorで何が設定されていても、必ずシングルトンのInstanceを取得する
        configController = ConfigController.Instance;

        if (configController == null)
        {
            Debug.LogError("ConfigController.Instance が見つかりません！シーンにConfigControllerが配置されているか確認してください。");
            return;
        }

        // 解像度ドロップダウンを初期化
        PopulateResolutionDropdown();

        // ConfigControllerから現在の設定（のコピー）を取得
        GameSettings settings = configController.InitializePendingSettings();

        // UIに値を設定
        SetAllUIValues(settings);

        // defaultPanel に基づいて初期表示を行う
        ShowPanel(defaultPanel);

        // リスナーを Start で登録
        if (ApplyButton != null)
            ApplyButton.onClick.AddListener(configController.ApplyPendingSettings);
        
        if (RevertButton != null)
            RevertButton.onClick.AddListener(configController.RevertPendingSettings);
    }

    // OnDisable は不要 (Start/OnDestroy でリスナー管理するため)

    private void OnDestroy()
    {
        // Awake で登録したリスナーを解除
        if (graphicsTabButton != null)
            graphicsTabButton.onClick.RemoveListener(ShowGraphicsPanel);
        if (audioTabButton != null)
            audioTabButton.onClick.RemoveListener(ShowAudioPanel);
        if (gameplayTabButton != null)
            gameplayTabButton.onClick.RemoveListener(ShowGameplayPanel);
        
        // Start で登録したリスナーを解除
        if (configController != null) // configControllerが先に破棄される可能性に対処
        {
            if (ApplyButton != null)
                ApplyButton.onClick.RemoveListener(configController.ApplyPendingSettings);

            if (RevertButton != null)
                RevertButton.onClick.RemoveListener(configController.RevertPendingSettings);
        }
    }


    /// <summary>
    /// 解像度ドロップダウンのリストをOSから取得して設定する
    /// </summary>
    private void PopulateResolutionDropdown()
    {
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
        graphicsPanel.SetActive(panelState == ConfigPanelState.Graphics);
        audioPanel.SetActive(panelState == ConfigPanelState.Audio);
        gameplayPanel.SetActive(panelState == ConfigPanelState.Gameplay);

        // ★コントローラーフォーカスは、
        // PauseMenuUIController の OnSettingsButtonClicked() と
        // HideConfigPanel() で制御します。
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
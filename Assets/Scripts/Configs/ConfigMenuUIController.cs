using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using UnityEngine.EventSystems;
using Cysharp.Threading.Tasks;
using UnityEngine.InputSystem; 

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
    // ▼▼▼ ★★★ PauseMenuUIController への参照を追加 ★★★ ▼▼▼
    [Header("Parent UI Controller")]
    [Tooltip("このConfigパネルを開いた親のUI (PauseMenuUIController)")]
    [SerializeField] private PauseMenuUIController pauseMenuUIController;
    // ▲▲▲ ★★★ 追加 ★★★ ▲▲▲

    [Header("Controller Reference")]
    [SerializeField] private ConfigController configController; 

    [Header("Panels")]
    [SerializeField] private GameObject graphicsPanel;
    [SerializeField] private GameObject audioPanel;
    [SerializeField] private GameObject gameplayPanel;

    [Header("First Focus Elements")]
    [SerializeField] private SelectableRow firstGraphicsElement; 
    [SerializeField] private SelectableRow firstAudioElement;    
    [SerializeField] private SelectableRow firstGameplayElement; 

    [Tooltip("このUIが有効になったときに、最初に表示するパネル")]
    [SerializeField] private ConfigPanelState defaultPanel = ConfigPanelState.Graphics;

    [Header("Tab Buttons")]
    [SerializeField] private Button graphicsTabButton;
    [SerializeField] private Button audioTabButton;
    [SerializeField] private Button gameplayTabButton;
    
    // [Header("Graphics UI")] ... (省略なし)
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
    private ColorBlock normalTabColors;
    private ColorBlock activeTabColors;
    private PlayerInput playerInput; 
    private ConfigPanelState currentPanelState; 

    private void Awake()
    {
        if (graphicsTabButton != null)
            graphicsTabButton.onClick.AddListener(ShowGraphicsPanel);
        if (audioTabButton != null)
            audioTabButton.onClick.AddListener(ShowAudioPanel);
        if (gameplayTabButton != null)
            gameplayTabButton.onClick.AddListener(ShowGameplayPanel);
    }

    private void OnEnable()
    {
        // playerInput が (Start()で) 取得済みの場合にのみ実行
        SubscribeInputEvents();
    }

    private void OnDisable()
    {
        // OnDisableではイベントを「解除」する
        UnsubscribeInputEvents();
    }


    async void Start() 
    {
        await UniTask.Yield(); 

        configController = ConfigController.Instance;

        if (configController == null)
        {
            Debug.LogError("ConfigController.Instance が見つかりません！");
            return;
        }

        // ▼▼▼ ★★★ PauseMenuUIController が設定されているか確認 ★★★ ▼▼▼
        if (pauseMenuUIController == null)
        {
            Debug.LogError("pauseMenuUIController が " + gameObject.name + " に設定されていません！「適用して戻る」が機能しません。");
        }
        // ▲▲▲ ★★★ 追加 ★★★ ▲▲▲

        if (PlayerDataHub.instance == null || PlayerDataHub.instance.CurrentplayerInput == null)
        {
            Debug.LogError("PlayerInput が PlayerDataHub から取得できません。", this);
        }
        else
        {
            // playerInput をフィールドにキャッシュする
            this.playerInput = PlayerDataHub.instance.CurrentplayerInput;
            
            // ★重要★: Start() で取得した直後に、最初のイベント購読を行う
            SubscribeInputEvents();
        }
        
        if (graphicsTabButton != null)
        {
            normalTabColors = graphicsTabButton.colors;
            activeTabColors = normalTabColors;
            activeTabColors.normalColor = normalTabColors.selectedColor; 
        }

        PopulateResolutionDropdown();
        GameSettings settings = configController.InitializePendingSettings();
        SetAllUIValues(settings);
    }

    private void OnDestroy()
    {
        if (graphicsTabButton != null)
            graphicsTabButton.onClick.RemoveListener(ShowGraphicsPanel);
        if (audioTabButton != null)
            audioTabButton.onClick.AddListener(ShowAudioPanel);
        if (gameplayTabButton != null)
            gameplayTabButton.onClick.AddListener(ShowGameplayPanel);
    }
    
    /// <summary>
    /// InputSystem のイベントを購読する
    /// </summary>
    private void SubscribeInputEvents()
    {
        // playerInput が (Start()で) 取得できていない場合は何もしない
        if (playerInput == null || playerInput.actions == null)
        {
            return;
        }

        // 念のため、多重登録を防ぐために一度解除してから登録する
        UnsubscribeInputEvents(); 

        playerInput.actions["RightClick"].performed += HandleApply;
        playerInput.actions["R"].performed += HandleRevert;
        playerInput.actions["E"].performed += HandleNextTab;
        playerInput.actions["Q"].performed += HandlePrevTab;
        
        Debug.Log("[ConfigMenuUI] InputSystem イベントを購読しました。");
    }

    /// <summary>
    /// InputSystem のイベント購読を解除する
    /// </summary>
    private void UnsubscribeInputEvents()
    {
        if (playerInput == null || playerInput.actions == null)
        {
            return;
        }

        playerInput.actions["RightClick"].performed -= HandleApply;
        playerInput.actions["R"].performed -= HandleRevert;
        playerInput.actions["E"].performed -= HandleNextTab;
        playerInput.actions["Q"].performed -= HandlePrevTab;
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


    /// <summary>
    /// ConfigControllerから呼び出され、デフォルトのパネルを表示してフォーカスを当てる
    /// </summary>
    public void ShowDefaultPanelAndSetFocus()
    {
        ShowPanel(defaultPanel);
    }


    // --- パネル切り替えロジック ---
    private async void ShowPanel(ConfigPanelState panelState)
    {
        this.currentPanelState = panelState; 
        graphicsPanel.SetActive(panelState == ConfigPanelState.Graphics);
        audioPanel.SetActive(panelState == ConfigPanelState.Audio);
        gameplayPanel.SetActive(panelState == ConfigPanelState.Gameplay);

        if (graphicsTabButton != null)
        {
            graphicsTabButton.colors = (panelState == ConfigPanelState.Graphics) ? activeTabColors : normalTabColors;
        }
        if (audioTabButton != null)
        {
            audioTabButton.colors = (panelState == ConfigPanelState.Audio) ? activeTabColors : normalTabColors;
        }
        if (gameplayTabButton != null)
        {
            gameplayTabButton.colors = (panelState == ConfigPanelState.Gameplay) ? activeTabColors : normalTabColors;
        }
        
        if(EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }

        await UniTask.Yield(); 

        switch (panelState)
        {
            case ConfigPanelState.Graphics:
                if (firstGraphicsElement != null)
                    firstGraphicsElement.SelectChildElement();
                break;
            case ConfigPanelState.Audio:
                if (firstAudioElement != null)
                    firstAudioElement.SelectChildElement();
                break;
            case ConfigPanelState.Gameplay:
                if (firstGameplayElement != null)
                    firstGameplayElement.SelectChildElement();
                break;
        }
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

    // --- InputSystemのハンドラ関数 ---

    /// <summary>
    /// "RightClick" アクションが実行された (Apply and Return)
    /// </summary>
    private void HandleApply(InputAction.CallbackContext context)
    {
        if (configController != null)
        {
            Debug.Log("Input Action 'RightClick' -> ApplyPendingSettings and Return");
            // 1. 設定を適用
            configController.ApplyPendingSettings();
            
            // ▼▼▼ ★★★ 2. ポーズメニューに戻る ★★★ ▼▼▼
            if (pauseMenuUIController != null)
            {
                // true を渡すことで、「設定を適用した」ことを伝える
                pauseMenuUIController.HideConfigPanel(true);
            }
            // ▲▲▲ ★★★ 修正箇所 ★★★ ▲▲▲
        }
    }

    /// <summary>
    /// "R" アクションが実行された (Reset to Default)
    /// </summary>
    private void HandleRevert(InputAction.CallbackContext context)
    {
        if (configController != null)
        {
            Debug.Log("Input Action 'R' -> ResetToDefaults");
            // ▼▼▼ ★★★ 呼び出す関数を変更 ★★★ ▼▼▼
            // configController.RevertPendingSettings();
            configController.ResetToDefaults();
            // ▲▲▲ ★★★ 修正箇所 ★★★ ▲▲▲
        }
    }

    /// <summary>
    /// "E" アクションが実行された (Next Tab)
    /// </summary>
    private void HandleNextTab(InputAction.CallbackContext context)
    {
        Debug.Log("Input Action 'E' -> Next Tab");
        
        if (currentPanelState == ConfigPanelState.Graphics)
        {
            ShowPanel(ConfigPanelState.Audio);
        }
        else if (currentPanelState == ConfigPanelState.Audio)
        {
            ShowPanel(ConfigPanelState.Gameplay);
        }
        else if (currentPanelState == ConfigPanelState.Gameplay)
        {
            ShowPanel(ConfigPanelState.Graphics);
        }
    }

    /// <summary>
    /// "Q" アクションが実行された (Previous Tab)
    /// </summary>
    private void HandlePrevTab(InputAction.CallbackContext context)
    {
        Debug.Log("Input Action 'Q' -> Previous Tab");

        if (currentPanelState == ConfigPanelState.Graphics)
        {
            ShowPanel(ConfigPanelState.Gameplay);
        }
        else if (currentPanelState == ConfigPanelState.Audio)
        {
            ShowPanel(ConfigPanelState.Graphics);
        }
        else if (currentPanelState == ConfigPanelState.Gameplay)
        {
            ShowPanel(ConfigPanelState.Audio);
        }
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
using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 設定メニューのパネル（タブ）。並び順がそのままタブの切り替え順になる。
/// </summary>
public enum ConfigPanelState
{
    Graphics,
    Audio,
    Gameplay
}

/// <summary>
/// オプション画面の UI を管理する。
///
/// ・3 つのパネル（グラフィック / 音量 / ゲームプレイ）のタブ切り替え
/// ・UI の操作内容を ConfigController の「変更中の設定」へ伝える
/// ・適用 / リセットのボタンとショートカット入力の処理
///
/// 設定値そのものの保存・適用は ConfigController の役割。
/// </summary>
public class ConfigMenuUIController : Singleton<ConfigMenuUIController>
{
    // ショートカットに使う InputAction 名
    private const string ApplyActionName = "RightClick";
    private const string ResetActionName = "R";
    private const string NextTabActionName = "E";
    private const string PrevTabActionName = "Q";

    /// <summary>解像度ドロップダウンに出すアスペクト比（16:9）と、その判定の許容誤差。</summary>
    private const float TargetAspectRatio = 16f / 9f;
    private const float AspectRatioTolerance = 0.01f;

    // ===== Inspector =====

    [Header("Controller Reference")]
    [SerializeField] private ConfigController configController;
    [SerializeField] public GameObject SettingCanvas;

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

    [Header("Action Buttons")]
    [Tooltip("設定を適用して閉じるボタン")]
    [SerializeField] private Button ApplyButton;
    [Tooltip("設定をデフォルトに戻すボタン")]
    [SerializeField] private Button ResetButton;

    [Header("Graphics UI")]
    [SerializeField] private TMP_Dropdown resolutionDropdown;
    [SerializeField] private TMP_Dropdown windowModeDropdown;
    [SerializeField] private Toggle vsyncToggle;
    [SerializeField] private TMP_Dropdown shadowDropdown;
    [SerializeField] private TMP_Dropdown textureDropdown;
    [SerializeField] private TMP_Dropdown antiAliasingDropdown;

    [Header("Audio UI")]
    [SerializeField] private Slider masterVolumeSlider;

    [Header("Gameplay UI")]
    [SerializeField] private Slider mouseSensitivitySlider;
    [SerializeField] private Toggle invertYToggle;
    [SerializeField] private Slider fovSlider;
    [Tooltip("0: ON, 1: OFF の順番でOptionを設定してください")]
    [SerializeField] private TMP_Dropdown cameraBobDropdown;

    public UnityEvent HandleApplyEvent;

    // ===== 実行時の状態 =====

    /// <summary>この設定画面を開いた親 UI（ポーズメニュー）。タイトル画面などでは null。</summary>
    private PauseMenuUIController pauseMenuUIController;
    private PlayerInput playerInput;

    /// <summary>
    /// 解像度ドロップダウンの N 番目が、Screen.resolutions の何番目に当たるか。
    /// （ドロップダウンには 16:9 だけを並べるため、インデックスがずれる）
    /// </summary>
    private readonly List<int> resolutionOptionIndexMap = new List<int>();

    private ColorBlock normalTabColors;
    private ColorBlock activeTabColors;
    private ConfigPanelState currentPanelState;

    private bool IsSettingCanvasVisible => SettingCanvas != null && SettingCanvas.activeInHierarchy;

    // ===== Unity ライフサイクル =====

    private async void Start()
    {
        if (graphicsTabButton != null) graphicsTabButton.onClick.AddListener(ShowGraphicsPanel);
        if (audioTabButton != null) audioTabButton.onClick.AddListener(ShowAudioPanel);
        if (gameplayTabButton != null) gameplayTabButton.onClick.AddListener(ShowGameplayPanel);
        if (ApplyButton != null) ApplyButton.onClick.AddListener(OnApplyButtonClicked);
        if (ResetButton != null) ResetButton.onClick.AddListener(OnResetButtonClicked);

        // 他のシングルトン（ConfigController / PlayerDataHub など）の初期化を 1 フレーム待つ
        await UniTask.Yield();

        configController = ConfigController.instance;
        if (PauseMenuController.instance != null)
        {
            pauseMenuUIController = PauseMenuController.instance.uiController;
        }

        if (configController == null)
        {
            Debug.LogError("ConfigController.instance が見つかりません！");
            return;
        }

        if (PlayerDataHub.instance == null || PlayerDataHub.instance.CurrentplayerInput == null)
        {
            Debug.LogError("PlayerInput が PlayerDataHub から取得できません。", this);
        }
        else
        {
            playerInput = PlayerDataHub.instance.CurrentplayerInput;
            SubscribeInputEvents();
        }

        // 選択中のタブは、通常色を「選択色」に差し替えて強調する
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

    private void OnDisable()
    {
        UnsubscribeInputEvents();
    }

    private void OnDestroy()
    {
        if (graphicsTabButton != null) graphicsTabButton.onClick.RemoveListener(ShowGraphicsPanel);
        if (audioTabButton != null) audioTabButton.onClick.RemoveListener(ShowAudioPanel);
        if (gameplayTabButton != null) gameplayTabButton.onClick.RemoveListener(ShowGameplayPanel);
        if (ApplyButton != null) ApplyButton.onClick.RemoveListener(OnApplyButtonClicked);
        if (ResetButton != null) ResetButton.onClick.RemoveListener(OnResetButtonClicked);
    }

    // ===== 公開 API: 画面の開閉・パネル切り替え =====

    public void OpenSettingCanvas()
    {
        configController.OnOpenConfig();
        SettingCanvas.gameObject.SetActive(true);
    }

    public void CloseSettingCanvas()
    {
        SettingCanvas.gameObject.SetActive(false);
    }

    /// <summary>デフォルトのパネルを表示して、その先頭要素にフォーカスを当てる（ConfigController から呼ばれる）。</summary>
    public void ShowDefaultPanelAndSetFocus()
    {
        ShowPanel(defaultPanel);
    }

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

    // ===== 公開 API: 設定値を UI に反映 =====

    /// <summary>
    /// 設定値をすべての UI 部品に反映する。
    /// 反映中に OnValueChanged が走って「変更中の設定」を書き換えないよう、リスナーを一度外してから付け直す。
    /// </summary>
    public void SetAllUIValues(GameSettings settings)
    {
        RemoveValueChangedListeners();

        // 保存されているのは Screen.resolutions のインデックスなので、ドロップダウン上の位置に変換する
        int dropdownIndex = resolutionOptionIndexMap.IndexOf(settings.resolutionIndex);
        if (dropdownIndex < 0)
        {
            // モニターが変わった場合など
            dropdownIndex = 0;
            Debug.LogWarning("保存された解像度インデックスが現在の16:9リストに見つかりません。デフォルト(0)に設定します。");
        }

        if (cameraBobDropdown != null)
        {
            cameraBobDropdown.value = settings.cameraBob;
        }

        // Graphics
        resolutionDropdown.value = dropdownIndex;
        resolutionDropdown.RefreshShownValue();
        windowModeDropdown.value = (int)settings.fullscreenMode;
        vsyncToggle.isOn = settings.vsync;
        shadowDropdown.value = settings.shadowQuality;
        textureDropdown.value = settings.textureQuality;
        antiAliasingDropdown.value = settings.antiAliasing;

        // Audio
        masterVolumeSlider.value = settings.masterVolume;

        // Gameplay
        mouseSensitivitySlider.value = settings.mouseSensitivity;
        invertYToggle.isOn = settings.invertCameraY;
        fovSlider.value = settings.fov;

        AddValueChangedListeners();
    }

    private void RemoveValueChangedListeners()
    {
        resolutionDropdown.onValueChanged.RemoveAllListeners();
        windowModeDropdown.onValueChanged.RemoveAllListeners();
        vsyncToggle.onValueChanged.RemoveAllListeners();
        shadowDropdown.onValueChanged.RemoveAllListeners();
        textureDropdown.onValueChanged.RemoveAllListeners();
        antiAliasingDropdown.onValueChanged.RemoveAllListeners();
        masterVolumeSlider.onValueChanged.RemoveAllListeners();
        mouseSensitivitySlider.onValueChanged.RemoveAllListeners();
        invertYToggle.onValueChanged.RemoveAllListeners();
        fovSlider.onValueChanged.RemoveAllListeners();
        if (cameraBobDropdown != null) cameraBobDropdown.onValueChanged.RemoveAllListeners();
    }

    private void AddValueChangedListeners()
    {
        resolutionDropdown.onValueChanged.AddListener(OnResolutionChanged);
        windowModeDropdown.onValueChanged.AddListener(OnWindowModeChanged);
        vsyncToggle.onValueChanged.AddListener(OnVsyncChanged);
        shadowDropdown.onValueChanged.AddListener(OnShadowsChanged);
        textureDropdown.onValueChanged.AddListener(OnTexturesChanged);
        antiAliasingDropdown.onValueChanged.AddListener(OnAntiAliasingChanged);
        masterVolumeSlider.onValueChanged.AddListener(OnMasterVolumeChanged);
        mouseSensitivitySlider.onValueChanged.AddListener(OnMouseSensitivityChanged);
        invertYToggle.onValueChanged.AddListener(OnInvertYChanged);
        fovSlider.onValueChanged.AddListener(OnFOVChanged);
        if (cameraBobDropdown != null) cameraBobDropdown.onValueChanged.AddListener(OnCameraBobChanged);
    }

    // ===== 公開 API: UI の OnValueChanged ハンドラ =====

    public void OnResolutionChanged(int dropdownIndex)
    {
        if (dropdownIndex < 0 || dropdownIndex >= resolutionOptionIndexMap.Count)
        {
            Debug.LogError($"不正なドロップダウンインデックス {dropdownIndex} が渡されました。");
            return;
        }

        // ドロップダウン上の位置を Screen.resolutions のインデックスに戻して渡す
        int originalIndex = resolutionOptionIndexMap[dropdownIndex];
        configController.UpdatePendingResolution(originalIndex);
    }

    public void OnWindowModeChanged(int index) => configController.UpdatePendingFullscreenMode(index);
    public void OnVsyncChanged(bool isOn) => configController.UpdatePendingVsync(isOn);
    public void OnShadowsChanged(int index) => configController.UpdatePendingShadows(index);
    public void OnTexturesChanged(int index) => configController.UpdatePendingTextures(index);
    public void OnAntiAliasingChanged(int index) => configController.UpdatePendingAntiAliasing(index);
    public void OnMasterVolumeChanged(float value) => configController.UpdatePendingMasterVolume(value);
    public void OnMouseSensitivityChanged(float value) => configController.UpdatePendingMouseSensitivity(value);
    public void OnInvertYChanged(bool isOn) => configController.UpdatePendingInvertY(isOn);
    public void OnFOVChanged(float value) => configController.UpdatePendingFOV(value);
    public void OnCameraBobChanged(int index) => configController.UpdatePendingCameraBob(index);

    // ===== 公開 API: 適用 / リセット ボタン =====

    public void OnApplyButtonClicked()
    {
        ApplyAndClose("Button 'Apply' -> ApplyPendingSettings and Return");
    }

    public async void OnResetButtonClicked()
    {
        await ResetToDefaultsAsync("Button 'Reset' -> ResetToDefaults");
    }

    // ===== ショートカット入力 =====

    private void SubscribeInputEvents()
    {
        if (playerInput == null || playerInput.actions == null) return;

        // 多重登録を防ぐため、一度解除してから登録する
        UnsubscribeInputEvents();

        // ショートカットの属する ActionMap が無効だと入力が届かないので、有効にしておく
        var nextTabAction = playerInput.actions[NextTabActionName];
        if (nextTabAction != null && nextTabAction.actionMap != null && !nextTabAction.actionMap.enabled)
        {
            Debug.Log($"[ConfigMenuUI] ActionMap '{nextTabAction.actionMap.name}' が無効だったため有効化しました。");
            nextTabAction.actionMap.Enable();
        }

        playerInput.actions[ApplyActionName].performed += HandleApply;
        playerInput.actions[ResetActionName].performed += HandleRevert;
        playerInput.actions[NextTabActionName].performed += HandleNextTab;
        playerInput.actions[PrevTabActionName].performed += HandlePrevTab;

        Debug.Log("[ConfigMenuUI] InputSystem イベントを購読しました。");
    }

    private void UnsubscribeInputEvents()
    {
        if (playerInput == null || playerInput.actions == null) return;

        playerInput.actions[ApplyActionName].performed -= HandleApply;
        playerInput.actions[ResetActionName].performed -= HandleRevert;
        playerInput.actions[NextTabActionName].performed -= HandleNextTab;
        playerInput.actions[PrevTabActionName].performed -= HandlePrevTab;
    }

    // ショートカットは設定画面が表示されているときだけ反応させる

    private void HandleApply(InputAction.CallbackContext context)
    {
        if (!IsSettingCanvasVisible) return;

        ApplyAndClose("Input Action 'RightClick' -> ApplyPendingSettings and Return");
    }

    private async void HandleRevert(InputAction.CallbackContext context)
    {
        if (!IsSettingCanvasVisible) return;

        await ResetToDefaultsAsync("Input Action 'R' -> ResetToDefaults");
    }

    private void HandleNextTab(InputAction.CallbackContext context)
    {
        if (!IsSettingCanvasVisible) return;

        Debug.Log("Input Action 'E' -> Next Tab");
        ShowPanel(GetAdjacentPanel(currentPanelState, +1));
    }

    private void HandlePrevTab(InputAction.CallbackContext context)
    {
        if (!IsSettingCanvasVisible) return;

        Debug.Log("Input Action 'Q' -> Previous Tab");
        ShowPanel(GetAdjacentPanel(currentPanelState, -1));
    }

    // ===== 適用 / リセットの共通処理 =====

    /// <summary>変更中の設定を適用・保存し、設定画面を閉じる。</summary>
    private void ApplyAndClose(string logMessage)
    {
        if (configController == null) return;

        Debug.Log(logMessage);
        configController.ApplyPendingSettings();

        // ポーズメニューから開かれている場合は、そちら経由で閉じる
        if (pauseMenuUIController != null)
        {
            pauseMenuUIController.HideConfigPanel(true);
        }

        HandleApplyEvent.Invoke();
    }

    /// <summary>変更中の設定をデフォルトに戻し（まだ適用はしない）、フォーカスをパネルの先頭に戻す。</summary>
    private async UniTask ResetToDefaultsAsync(string logMessage)
    {
        if (configController == null) return;

        Debug.Log(logMessage);
        configController.ResetToDefaults(); // この中で SetAllUIValues が呼ばれ、UI も更新される

        await SetFocusToCurrentPanelFirstElement();
    }

    // ===== パネル切り替えの内部処理 =====

    private async void ShowPanel(ConfigPanelState panelState)
    {
        currentPanelState = panelState;

        graphicsPanel.SetActive(panelState == ConfigPanelState.Graphics);
        audioPanel.SetActive(panelState == ConfigPanelState.Audio);
        gameplayPanel.SetActive(panelState == ConfigPanelState.Gameplay);

        SetTabHighlighted(graphicsTabButton, panelState == ConfigPanelState.Graphics);
        SetTabHighlighted(audioTabButton, panelState == ConfigPanelState.Audio);
        SetTabHighlighted(gameplayTabButton, panelState == ConfigPanelState.Gameplay);

        await SetFocusToCurrentPanelFirstElement();
    }

    private void SetTabHighlighted(Button tabButton, bool isActive)
    {
        if (tabButton == null) return;

        tabButton.colors = isActive ? activeTabColors : normalTabColors;
    }

    /// <summary>current から offset 個となりのパネル。端まで行ったら反対側へ回り込む。</summary>
    private static ConfigPanelState GetAdjacentPanel(ConfigPanelState current, int offset)
    {
        int panelCount = Enum.GetValues(typeof(ConfigPanelState)).Length;
        int index = ((int)current + offset + panelCount) % panelCount;
        return (ConfigPanelState)index;
    }

    /// <summary>現在のパネルの先頭要素にフォーカスを当てる。</summary>
    private async UniTask SetFocusToCurrentPanelFirstElement()
    {
        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }

        // UI の更新と選択解除が反映されるのを 1 フレーム待つ
        await UniTask.Yield();

        // 待っている間に自分が破棄されていたら何もしない
        if (this == null) return;

        SelectableRow firstElement = currentPanelState switch
        {
            ConfigPanelState.Graphics => firstGraphicsElement,
            ConfigPanelState.Audio => firstAudioElement,
            ConfigPanelState.Gameplay => firstGameplayElement,
            _ => null
        };

        if (firstElement != null)
        {
            firstElement.SelectChildElement();
        }
    }

    // ===== 解像度ドロップダウン =====

    /// <summary>
    /// 解像度の選択肢を作る。16:9 の解像度だけを並べ、1 つも無ければすべての解像度を並べる。
    /// </summary>
    private void PopulateResolutionDropdown()
    {
        Resolution[] resolutions = Screen.resolutions;
        var options = new List<string>();

        resolutionDropdown.ClearOptions();
        resolutionOptionIndexMap.Clear();

        for (int i = 0; i < resolutions.Length; i++)
        {
            var resolution = resolutions[i];
            if (resolution.width == 0 || resolution.height == 0) continue; // 不正なデータ

            float aspectRatio = (float)resolution.width / (float)resolution.height;
            if (Mathf.Abs(aspectRatio - TargetAspectRatio) < AspectRatioTolerance)
            {
                options.Add(FormatResolution(resolution));
                resolutionOptionIndexMap.Add(i);
            }
        }

        if (options.Count == 0)
        {
            Debug.LogWarning("16:9 の解像度が見つかりませんでした。すべての解像度を表示します。");
            for (int i = 0; i < resolutions.Length; i++)
            {
                options.Add(FormatResolution(resolutions[i]));
                resolutionOptionIndexMap.Add(i);
            }
        }

        resolutionDropdown.AddOptions(options);
    }

    private static string FormatResolution(Resolution resolution)
    {
        return $"{resolution.width} x {resolution.height} @ {resolution.refreshRateRatio.value:F0} Hz";
    }
}

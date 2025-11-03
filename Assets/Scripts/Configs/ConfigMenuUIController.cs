using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using UnityEngine.EventSystems;
using Cysharp.Threading.Tasks; // ★ UniTask.Yield のために必要
using UnityEngine.InputSystem;
using UnityEngine.Events;
using System; // ★ Enum.GetNames を使うために追加

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
public class ConfigMenuUIController : Singleton<ConfigMenuUIController>
{
    // ▼▼▼ ★★★ PauseMenuUIController への参照（これは任意でOK） ★★★ ▼▼▼
    [Header("Parent UI Controller")]
    [Tooltip("このConfigパネルを開いた親のUI (PauseMenuUIController) ※任意")]
    [SerializeField] private PauseMenuUIController pauseMenuUIController;
    // ▲▲▲ ★★★ 変更 ★★★ ▲▲▲

    [Header("Controller Reference")]
    [SerializeField] private ConfigController configController;
    [SerializeField] private GameObject SettingCanvas;
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

    // ▼▼▼ ★★★ (ここから追加) ★★★ ▼▼▼
    [Header("Action Buttons")]
    [Tooltip("設定を適用して閉じるボタン")]
    [SerializeField] private Button ApplyButton;
    [Tooltip("設定をデフォルトに戻すボタン")]
    [SerializeField] private Button ResetButton;
    // ▲▲▲ ★★★ (ここまで追加) ★★★ ▲▲▲
    
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

    [Header("Gameplay UI")]
    [SerializeField] private Slider mouseSensitivitySlider;
    [SerializeField] private Toggle invertYToggle;
    [SerializeField] private Slider fovSlider;

    public UnityEvent HandleApplyEvent;
    
    // ▼▼▼ ★★★ (ここから修正) ★★★ ▼▼▼
    // 全解像度リスト（ConfigControllerとインデックスを合わせるため）
    private List<Resolution> resolutions;
    // フィルタリングされた解像度の「元のインデックス」をマッピングするリスト
    private List<int> resolutionOptionIndexMap = new List<int>();
    // ▲▲▲ ★★★ (ここまで修正) ★★★ ▲▲▲

    private ColorBlock normalTabColors;
    private ColorBlock activeTabColors;
    private PlayerInput playerInput; 
    private ConfigPanelState currentPanelState; 


    private void OnDisable()
    {
        // OnDisableではイベントを「解除」する
        UnsubscribeInputEvents();
    }


    async void Start()
    {
        // --- タブボタンのリスナー登録 ---
        if (graphicsTabButton != null)
            graphicsTabButton.onClick.AddListener(ShowGraphicsPanel);
        if (audioTabButton != null)
            audioTabButton.onClick.AddListener(ShowAudioPanel);
        if (gameplayTabButton != null)
            gameplayTabButton.onClick.AddListener(ShowGameplayPanel);

        // --- アクションボタンのリスナー登録 ---
        if (ApplyButton != null)
            ApplyButton.onClick.AddListener(OnApplyButtonClicked);
        if (ResetButton != null)
            ResetButton.onClick.AddListener(OnResetButtonClicked);

        // playerInput が (Start()で) 取得済みの場合にのみ実行
        SubscribeInputEvents();
        await UniTask.Yield(); 

        configController = ConfigController.Instance;

        if (configController == null)
        {
            Debug.LogError("ConfigController.Instance が見つかりません！");
            return;
        }

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

        // ▼▼▼ ★★★ (ここから修正) ★★★ ▼▼▼
        PopulateResolutionDropdown(); // 解像度リストをフィルタリング
        PopulateWindowModeDropdown(); // ウィンドウモードリストを生成
        // ▲▲▲ ★★★ (ここまで修正) ★★★ ▲▲▲

        GameSettings settings = configController.InitializePendingSettings();
        SetAllUIValues(settings);
    }

    private void OnDestroy()
    {
        if (graphicsTabButton != null)
            graphicsTabButton.onClick.RemoveListener(ShowGraphicsPanel);
        if (audioTabButton != null)
            audioTabButton.onClick.RemoveListener(ShowAudioPanel);
        if (gameplayTabButton != null)
            gameplayTabButton.onClick.RemoveListener(ShowGameplayPanel);
        
        if (ApplyButton != null)
            ApplyButton.onClick.RemoveListener(OnApplyButtonClicked);
        if (ResetButton != null)
            ResetButton.onClick.RemoveListener(OnResetButtonClicked);
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
    
    // ▼▼▼ ★★★ (ここから修正) 16:9フィルター機能を追加 ★★★ ▼▼▼
    /// <summary>
    /// 解像度ドロップダウンのリストをOSから取得し、16:9のみにフィルタリングする
    /// </summary>
    private void PopulateResolutionDropdown()
    {
        // 1. 元のリストをすべて取得
        resolutions = new List<Resolution>(Screen.resolutions);
        resolutionDropdown.ClearOptions();

        List<string> options = new List<string>();
        resolutionOptionIndexMap.Clear(); // マッピングをクリア
        
        // 16:9 のアスペクト比 (1.777...)
        const float targetRatio = 16f / 9f;
        const float epsilon = 0.01f; // 浮動小数点数の比較のための許容誤差

        // 2. 元のリストをループし、16:9だけを絞り込む
        for (int i = 0; i < resolutions.Count; i++) // インデックス(i)を取得するためにforを使用
        {
            var res = resolutions[i];
            
            // 幅や高さが0の場合は除外 (不正なデータ)
            if (res.width == 0 || res.height == 0) continue; 
            
            float currentRatio = (float)res.width / (float)res.height;
            
            // 3. アスペクト比が 16:9 に近いかチェック
            if (Mathf.Abs(currentRatio - targetRatio) < epsilon)
            {
                // ConfigControllerのロジックに合わせてリフレッシュレートも表示
                string option = $"{res.width} x {res.height}";
                options.Add(option);
                
                // ★重要★: ドロップダウンのN番目が、元のリストの「i」番目であることを記録
                resolutionOptionIndexMap.Add(i); 
            }
        }
        
        // 4. (安全策) 16:9の解像度が1つも見つからなかった場合
        if (options.Count == 0)
        {
             Debug.LogWarning("16:9 の解像度が見つかりませんでした。すべての解像度を表示します。");
             for (int i = 0; i < resolutions.Count; i++)
             {
                var res = resolutions[i];
                string option = $"{res.width} x {res.height} @ {res.refreshRateRatio.value:F0} Hz";
                options.Add(option);
                resolutionOptionIndexMap.Add(i); // この場合、indexは 1:1 になる
             }
        }

        resolutionDropdown.AddOptions(options);
    }
    // ▲▲▲ ★★★ (ここまで修正) ★★★ ▲▲▲

    // ▼▼▼ ★★★ (ここから追加) ★★★ ▼▼▼
    /// <summary>
    /// ウィンドウモードのドロップダウンリストを初期化する
    /// </summary>
    private void PopulateWindowModeDropdown()
    {
        if (windowModeDropdown == null) return;
        
        windowModeDropdown.ClearOptions();
        
        // Enumは (0, 1, 2, 3) の順番が保証されています。
        // ユーザーフレンドリーな名前に手動でマッピングします。
        List<string> options = new List<string>
        {
            "フルスクリーン", // (FullScreenMode)0 = ExclusiveFullScreen
            "ボーダーレス",   // (FullScreenMode)1 = FullScreenWindow
            "最大化ウィンドウ",       // (FullScreenMode)2 = MaximizedWindow
            "ウィンドウモード"        // (FullScreenMode)3 = Windowed
        };
        
        windowModeDropdown.AddOptions(options);
    }
    // ▲▲▲ ★★★ (ここまで追加) ★★★ ▲▲▲

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
        mouseSensitivitySlider.onValueChanged.RemoveAllListeners();
        invertYToggle.onValueChanged.RemoveAllListeners();
        fovSlider.onValueChanged.RemoveAllListeners();

        // ▼▼▼ ★★★ (ここから修正) インデックスの逆引き ★★★ ▼▼▼
        // 1. 保存されている「元のインデックス」(settings.resolutionIndex) を取得
        int originalIndexToFind = settings.resolutionIndex;
        // 2. マップリスト (resolutionOptionIndexMap) から、そのインデックスが
        //    ドロップダウンの何番目にあるかを逆引きする
        int dropdownIndex = resolutionOptionIndexMap.IndexOf(originalIndexToFind);

        // 3. もし見つからなければ (例: モニタが変更された)、リストの先頭(0)にする
        if (dropdownIndex < 0)
        {
            dropdownIndex = 0; 
            Debug.LogWarning("保存された解像度インデックスが現在の16:9リストに見つかりません。デフォルト(0)に設定します。");
        }
        
        resolutionDropdown.value = dropdownIndex;
        // ▲▲▲ ★★★ (ここまで修正) ★★★ ▲▲▲
        
        resolutionDropdown.RefreshShownValue(); 
        windowModeDropdown.value = (int)settings.fullscreenMode;
        vsyncToggle.isOn = settings.vsync;
        shadowDropdown.value = settings.shadowQuality;
        textureDropdown.value = settings.textureQuality;
        antiAliasingDropdown.value = settings.antiAliasing;
        masterVolumeSlider.value = settings.masterVolume;
        // musicVolumeSlider.value = settings.musicVolume;
        // sfxVolumeSlider.value = settings.sfxVolume;
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
        // musicVolumeSlider.onValueChanged.AddListener(OnMusicVolumeChanged);
        // sfxVolumeSlider.onValueChanged.AddListener(OnSFXVolumeChanged);
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
        
        await SetFocusToCurrentPanelFirstElement();
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

    // --- ボタンクリック用ハンドラ ---

    /// <summary>
    /// ApplyButton がクリックされた (HandleApply と同じ動作)
    /// </summary>
    public void OnApplyButtonClicked()
    {
        // ボタンクリックの場合、Canvas非表示チェックは不要 (押せないため)
        if (configController != null)
        {
            Debug.Log("Button 'Apply' -> ApplyPendingSettings and Return");
            // 1. 設定を適用
            configController.ApplyPendingSettings();
            
            // 2. 画面を閉じる
            if (pauseMenuUIController != null)
            {
                // 親 (PauseMenu) が設定されていれば、そこ経S経由で閉じる
                pauseMenuUIController.HideConfigPanel(true);
            }
            // 3. 閉じるイベントを発行
            HandleApplyEvent.Invoke();
        }
    }

    /// <summary>
    /// ResetButton がクリックされた (HandleRevert と同じ動作)
    /// </summary>
    public async void OnResetButtonClicked()
    {
        // ボタンクリックの場合、Canvas非表示チェックは不要 (押せないため)
        if (configController != null)
        {
            Debug.Log("Button 'Reset' -> ResetToDefaults");
            // 1. 設定をデフォルトに戻す (これにより SetAllUIValues が呼ばれる)
            configController.ResetToDefaults();

            // 2. UIが更新された後、フォーカスを現在のパネルの先頭に戻す
            await SetFocusToCurrentPanelFirstElement();
        }
    }


    // --- InputSystemのハンドラ関数 ---

    /// <summary>
    /// "RightClick" アクションが実行された (Apply and Return)
    /// </summary>
    private void HandleApply(InputAction.CallbackContext context)
    {
        // SettingCanvasが非表示なら、何もせず処理を終了する
        if (SettingCanvas == null || !SettingCanvas.activeInHierarchy) return;

        if (configController != null)
        {
            Debug.Log("Input Action 'RightClick' -> ApplyPendingSettings and Return");
            // 1. 設定を適用
            configController.ApplyPendingSettings();
            
            if (pauseMenuUIController != null)
            {
                // 親 (PauseMenu) が設定されていれば、そこ経由で閉じる
                pauseMenuUIController.HideConfigPanel(true);
            }
            HandleApplyEvent.Invoke();
        }
    }

    /// <summary>
    /// "R" アクションが実行された (Reset to Default)
    /// </summary>
    private async void HandleRevert(InputAction.CallbackContext context)
    {
        // SettingCanvasが非表示なら、何もせず処理を終了する
        if (SettingCanvas == null || !SettingCanvas.activeInHierarchy) return;

        if (configController != null)
        {
            Debug.Log("Input Action 'R' -> ResetToDefaults");
            // 1. 設定をデフォルトに戻す
            configController.ResetToDefaults();
            
            // 2. UIが更新された後、フォーカスを現在のパネルの先頭に戻す
            await SetFocusToCurrentPanelFirstElement();
        }
    }

    /// <summary>
    /// "E" アクションが実行された (Next Tab)
    /// </summary>
    private void HandleNextTab(InputAction.CallbackContext context)
    {
        // SettingCanvasが非表示なら、何もせず処理を終了する
        if (SettingCanvas == null || !SettingCanvas.activeInHierarchy) return;

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
        // SettingCanvasが非表示なら、何もせず処理を終了する
        if (SettingCanvas == null || !SettingCanvas.activeInHierarchy) return;

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
    
    /// <summary>
    /// 現在開いているパネルの最初の要素にフォーカスを当てる (1フレーム待機付き)
    /// </summary>
    private async UniTask SetFocusToCurrentPanelFirstElement()
    {
        // 選択を一旦クリア
        if(EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }

        // 1フレーム待機 (UI更新やEventSystemのクリアを待つ)
        await UniTask.Yield(); 

        // 待機中にオブジェクトが破棄された可能性を考慮
        if (this == null) return;

        // 現在のパネル状態に応じてフォーカスを設定
        switch (currentPanelState)
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
    

    public void OpenSettingCanvas()
    {
        configController.OnOpenConfig();
        SettingCanvas.gameObject.SetActive(true);
    }
    public void CloseSettingCanvas()
    {
        SettingCanvas.gameObject.SetActive(false);
    }
    
    // --- UIのOnValueChanged()から呼び出すメソッド群 ---

    // ▼▼▼ ★★★ (ここから修正) インデックスの変換 ★★★ ▼▼▼
    public void OnResolutionChanged(int dropdownIndex) 
    {
        if (dropdownIndex < 0 || dropdownIndex >= resolutionOptionIndexMap.Count)
        {
            Debug.LogError($"不正なドロップダウンインデックス {dropdownIndex} が渡されました。");
            return; 
        }
        
        // ★ ドロップダウンのインデックスを、Screen.resolutions のインデックスに変換
        int originalIndex = resolutionOptionIndexMap[dropdownIndex];
        configController.UpdatePendingResolution(originalIndex);
    }
    // ▲▲▲ ★★★ (ここまで修正) ★★★ ▲▲▲

    public void OnWindowModeChanged(int index) => configController.UpdatePendingFullscreenMode(index);
    public void OnVsyncChanged(bool isOn) => configController.UpdatePendingVsync(isOn);
    public void OnShadowsChanged(int index) => configController.UpdatePendingShadows(index);
    public void OnTexturesChanged(int index) => configController.UpdatePendingTextures(index);
    public void OnAntiAliasingChanged(int index) => configController.UpdatePendingAntiAliasing(index);
    public void OnMasterVolumeChanged(float value) => configController.UpdatePendingMasterVolume(value);
    // public void OnMusicVolumeChanged(float value) => configController.UpdatePendingMusicVolume(value);
    // public void OnSFXVolumeChanged(float value) => configController.UpdatePendingSFXVolume(value);
    public void OnMouseSensitivityChanged(float value) => configController.UpdatePendingMouseSensitivity(value);
    public void OnInvertYChanged(bool isOn) => configController.UpdatePendingInvertY(isOn);
    public void OnFOVChanged(float value)
    {
        configController.UpdatePendingFOV(value);
    }
}
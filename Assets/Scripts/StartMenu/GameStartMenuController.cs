using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// タイトル画面のメニュー。
/// スタートメニュー（はじめから / つづける / 設定 / クレジット / 終了）と、
/// セーブ地点を選ぶコンティニューメニューの切り替えを担当する。
/// </summary>
public class GameStartMenuController : Singleton<GameStartMenuController>
{
    // UIFlowManager に登録されているパネル名
    private const string StartNavPanelName = "StartNavMenu";
    private const string ContinuePanelName = "ContinueMenu";

    // 遷移先のシーン名
    private const string FirstSceneName = "LeaveSchool";
    private const string CreditSceneName = "Credit";

    // ===== Inspector =====

    [SerializeField] private Button QuitGameButton;
    [SerializeField] private Button GameStartFromBeginButton;
    [SerializeField] private Button GameContinueButton;
    [SerializeField] public Button ContinueToTopBackButton;
    [SerializeField] private Button SettingButton;
    [SerializeField] private Button CreditButton;

    [Header("Canvas Groups")]
    [SerializeField] private CanvasGroup StartNavMenuCanvasGroup;
    [SerializeField] private CanvasGroup ContinueMenuCanvasGroup;

    // ===== 他のコントローラーへの参照 =====

    private SceneManagerController sceneManagerController;
    private UIFlowManager uIFlowManager;
    private ConfigController configController;
    private ConfigMenuUIController configMenuUIController;
    private ContinueController continueController;
    private GameStateController gameStateController;

    // ===== Unity ライフサイクル =====

    private void Start()
    {
        uIFlowManager = UIFlowManager.instance;
        sceneManagerController = SceneManagerController.Instance;
        configController = ConfigController.instance;
        configMenuUIController = ConfigMenuUIController.instance;
        continueController = ContinueController.instance;
        gameStateController = GameStateController.instance;

        // 最初はスタートメニューだけを表示する
        StartNavMenuCanvasGroup.alpha = 1;
        SetInteractive(StartNavMenuCanvasGroup, true);
        ContinueMenuCanvasGroup.alpha = 0;
        SetInteractive(ContinueMenuCanvasGroup, false);

        QuitGameButton.onClick.AddListener(QuitGame);
        GameStartFromBeginButton.onClick.AddListener(StartFromBeginning);
        GameContinueButton.onClick.AddListener(OpenContinueMenu);
        ContinueToTopBackButton.onClick.AddListener(BackToStartMenu);
        CreditButton.onClick.AddListener(OpenCredit);
        SettingButton.onClick.AddListener(OpenSettings);
        configMenuUIController.HandleApplyEvent.AddListener(OnSettingsClosed);

        GameStartFromBeginButton.Select();

        gameStateController.ActionByModeSelect(GameStateController.GameState.MenuOnGamePlay, true);
    }

    // ===== ボタンの処理 =====

    private void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void StartFromBeginning()
    {
        sceneManagerController.LoadScene(FirstSceneName);
        GameStartFromBeginButton.enabled = false; // 二重クリック防止
    }

    private void OpenContinueMenu()
    {
        // スタートメニューはクリックだけ通さないようにする（interactable は true のまま）
        StartNavMenuCanvasGroup.interactable = true;
        StartNavMenuCanvasGroup.blocksRaycasts = false;

        uIFlowManager.SwitchToPanel(ContinuePanelName);

        // 先頭のセーブスロットが選べるならそこに、選べなければ「戻る」ボタンにフォーカスを置く
        Button firstSlotButton = continueController.continueSlots.Keys.First();
        if (firstSlotButton.interactable)
        {
            firstSlotButton.Select();
        }
        else
        {
            ContinueToTopBackButton.Select();
        }

        continueController.SetupContinueButtons();
    }

    private void BackToStartMenu()
    {
        SetInteractive(ContinueMenuCanvasGroup, false);

        uIFlowManager.SwitchToPanel(StartNavPanelName);

        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(GameStartFromBeginButton.gameObject);
        }
    }

    private void OpenCredit()
    {
        sceneManagerController.LoadScene(CreditSceneName);
    }

    // ===== 設定画面との連携 =====

    private void OpenSettings()
    {
        configMenuUIController.OpenSettingCanvas();

        // 設定画面を開いている間は、後ろのメニューを操作できないようにする
        SetInteractive(StartNavMenuCanvasGroup, false);
        SetInteractive(ContinueMenuCanvasGroup, false);
    }

    /// <summary>設定画面で「適用」されたとき。設定画面を閉じて、メニューを操作できる状態に戻す。</summary>
    private void OnSettingsClosed()
    {
        configMenuUIController.CloseSettingCanvas();

        // フォーカスを設定ボタンに戻す
        configController.OnCloseConfig(SettingButton.gameObject);

        // 表示されている方（alpha が 1 の方）のメニューだけ操作可能に戻す
        if (StartNavMenuCanvasGroup.alpha > 0.5f)
        {
            SetInteractive(StartNavMenuCanvasGroup, true);
        }

        if (ContinueMenuCanvasGroup.alpha > 0.5f)
        {
            SetInteractive(ContinueMenuCanvasGroup, true);
        }
    }

    private static void SetInteractive(CanvasGroup canvasGroup, bool interactive)
    {
        canvasGroup.interactable = interactive;
        canvasGroup.blocksRaycasts = interactive;
    }
}

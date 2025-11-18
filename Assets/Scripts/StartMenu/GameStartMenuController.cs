using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using DG.Tweening; // DOTweenを使用しているため
using UnityEngine.EventSystems;
using System.Collections.Generic;
using System.Linq; // EventSystemを使用しているため

public class GameStartMenuController : Singleton<GameStartMenuController>
{
    [SerializeField] private Button QuitGameButton;
    [SerializeField] private Button GameStartFromBeginButton;
    [SerializeField] private Button GameContinueButton;
    [SerializeField] public Button ContinueToTopBackButton;
    [SerializeField] private Button SettingButton;
    [SerializeField] private Button CreditButton;

    
    [Header("Canvas Groups")]
    [SerializeField] private CanvasGroup StartNavMenuCanvasGroup;
    [SerializeField] private CanvasGroup ContinueMenuCanvasGroup;

    // --- 各種コントローラーへの参照 ---
    private SceneManagerController sceneManagerController;
    private UIFlowManager uIFlowManager;
    private ConfigController configController;
    private ConfigMenuUIController configMenuUIController;
    private ContinueController continueController;
    void Start()
    {
        // --- 参照の取得 ---
        uIFlowManager = UIFlowManager.instance;
        sceneManagerController = SceneManagerController.Instance;
        configController = ConfigController.instance;
        configMenuUIController = ConfigMenuUIController.instance;
        continueController = ContinueController.instance;
        // --- メニューの初期状態を設定 ---
        // スタートメニューは表示
        StartNavMenuCanvasGroup.alpha = 1;
        StartNavMenuCanvasGroup.interactable = true;
        StartNavMenuCanvasGroup.blocksRaycasts = true;
        // コンティニューメニューは非表示
        ContinueMenuCanvasGroup.alpha = 0;
        ContinueMenuCanvasGroup.interactable = false;
        ContinueMenuCanvasGroup.blocksRaycasts = false;
        // ------------------------------

        // --- ボタンリスナーの登録 ---

        // ゲーム終了ボタン
        QuitGameButton.onClick.AddListener(() =>
        {
            #if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;//ゲームプレイ終了
            #else
                Application.Quit();//ゲームプレイ終了
            #endif
        });

        // 「はじめから」ボタン
        GameStartFromBeginButton.onClick.AddListener(() =>
        {
            sceneManagerController.LoadScene("LeaveSchool");
        });

        // 「つづける」ボタン（コンティニューメニューを表示）
        GameContinueButton.onClick.AddListener(() =>
        {
            // スタートメニューを操作不可に
            StartNavMenuCanvasGroup.interactable = true;
            StartNavMenuCanvasGroup.blocksRaycasts = false;
            // コンティニューメニューへ切り替え
            uIFlowManager.SwitchToPanel("ContinueMenu");
           Dictionary<Button, ContinueController.ContinueData>.KeyCollection continueSlotsKeys = continueController.continueSlots.Keys;
           continueSlotsKeys.First();
            if (continueSlotsKeys.First().interactable)
            {
                continueSlotsKeys.First().Select();
            }
            else
            {
                ContinueToTopBackButton.Select();
            }
           continueController.SetupContinueButtons();
        });

        // 「トップへ戻る」ボタン（スタートメニューを表示）
        ContinueToTopBackButton.onClick.AddListener(() =>
        {
            
            // コンティニューメニューを操作不可に
            ContinueMenuCanvasGroup.interactable = false;
            ContinueMenuCanvasGroup.blocksRaycasts = false;
            // スタートメニューへ切り替え
            uIFlowManager.SwitchToPanel("StartNavMenu");
            // フォーカスを「はじめから」ボタンに戻す
            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(GameStartFromBeginButton.gameObject);
            }
        });
        CreditButton.onClick.AddListener(() =>
        {
            sceneManagerController.LoadScene("Credit");
        });
        // 最初のフォーカスを設定
        GameStartFromBeginButton.Select();


        // ▼▼▼ ★★★ 設定画面との連携 (修正箇所) ★★★ ▼▼▼

        // --- 設定ボタンのリスナー ---
        SettingButton.onClick.AddListener(() =>
        {
            // 1. 設定画面を開く
            configMenuUIController.OpenSettingCanvas();
            
            // 2. メインメニュー（両方）の操作を無効化する
            // これにより、設定画面が開いている間はボタンが選択できなくなる
            StartNavMenuCanvasGroup.interactable = false;
            StartNavMenuCanvasGroup.blocksRaycasts = false;
            ContinueMenuCanvasGroup.interactable = false;
            ContinueMenuCanvasGroup.blocksRaycasts = false;
        });

        // --- 設定を閉じるイベントのリスナー ---
        configMenuUIController.HandleApplyEvent.AddListener(() =>
        {
            // 1. 設定画面を閉じる
            configMenuUIController.CloseSettingCanvas();
            
            // 2. ConfigControllerに閉じたことを通知し、フォーカスを設定ボタンに戻す
            configController.OnCloseConfig(SettingButton.gameObject);
            
            // 3. メインメニューの操作を有効化する
            // alpha値を見て、表示されている方だけを操作可能に戻す
            if (StartNavMenuCanvasGroup.alpha > 0.5f) // 0 or 1 の想定
            {
                StartNavMenuCanvasGroup.interactable = true;
                StartNavMenuCanvasGroup.blocksRaycasts = true;
            }
            
            if (ContinueMenuCanvasGroup.alpha > 0.5f)
            {
                ContinueMenuCanvasGroup.interactable = true;
                ContinueMenuCanvasGroup.blocksRaycasts = true;
            }
        });
        // ▲▲▲ ★★★ 修正箇所 (ここまで) ★★★ ▲▲▲
    }

    // Updateは現在使用していない
    // void Update()
    // {
    // }
}
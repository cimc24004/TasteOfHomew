using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using DG.Tweening;
public class GameStartMenuController : MonoBehaviour
{
    [SerializeField] private Button QuitGameButton;
    [SerializeField] private Button GameStartFromBeginButton;
    [SerializeField] private Button GameContinueButton;
    [SerializeField] private Button ContinueToTopBackButton;
    private SceneManagerController sceneManagerController;
    [SerializeField] private CanvasGroup StartNavMenuCanvasGroup;
    [SerializeField] private CanvasGroup ContinueMenuCanvasGroup; 
    private UIFlowManager uIFlowManager;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        uIFlowManager = UIFlowManager.instance;
        sceneManagerController = SceneManagerController.Instance;

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

        QuitGameButton.onClick.AddListener(() =>
        {
            #if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;//ゲームプレイ終了
            #else
                Application.Quit();//ゲームプレイ終了
            #endif
        });

        GameStartFromBeginButton.onClick.AddListener(() =>
        {
            sceneManagerController.LoadScene("LeaveSchool");
        });

        // 「つづける」ボタン（コンティニューメニューを表示）
        GameContinueButton.onClick.AddListener(() =>
        {
            // 1. スタートメニューを操作不可にし、フェードアウト
            StartNavMenuCanvasGroup.interactable = false;
            StartNavMenuCanvasGroup.blocksRaycasts = false;
            // StartNavMenuCanvasGroup.DOFade(0, 0.3f).OnComplete(() =>
            // {
            //     // 2. フェードアウト完了後、コンティニューメニューを操作可能にし、フェードイン
            //     ContinueMenuCanvasGroup.interactable = true;
            //     ContinueMenuCanvasGroup.blocksRaycasts = true;
            //     ContinueMenuCanvasGroup.DOFade(1, 0.3f);
            // });
            uIFlowManager.SwitchToPanel("ContinueMenu");
        });

        // 【追加】「トップへ戻る」ボタン（スタートメニューを表示）
        ContinueToTopBackButton.onClick.AddListener(() =>
        {
            // 1. コンティニューメニューを操作不可にし、フェードアウト
            ContinueMenuCanvasGroup.interactable = false;
            ContinueMenuCanvasGroup.blocksRaycasts = false;
            uIFlowManager.SwitchToPanel("StartNavMenu");

        });
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}

using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
// using NUnit.Framework; // 不要なusing
using UnityEngine.EventSystems;
/// <summary>
/// ゲームのポーズ/再開状態を管理し、シーン遷移を行う。
/// 入力イベント（ESCキー）を購読して実行される。
/// </summary>
public class PauseMenuController : Singleton<PauseMenuController>
{
    [Header("UI Controller Reference")]
    [SerializeField] public PauseMenuUIController uiController;

    [Header("Settings")]
    [Tooltip("タイトルシーンの名前")]
    [SerializeField] private string titleSceneName = "Start"; // Inspectorでタイトルシーン名を設定
    [SerializeField]
    private Volume mosaicVolume;
    public static bool IsPaused { get; private set; } = false;

    private PlayerDataHub playerDataHub;
    [SerializeField]
    private InputActionReference pauseAction; // ポーズ用のアクションをキャッシュする変数
    private GameStateController gameStateController;
    public bool PauseMenuOpenPermit = true;
    void Start()
    {
        playerDataHub = PlayerDataHub.instance;
        gameStateController = GameStateController.instance;
        Debug.Log("PauseMenuController Start");
        // タイポ修正: CurrentplayerInput -> CurrentPlayerStatus.playerInput (以前のコードを参照)
        if (playerDataHub?.CurrentplayerInput != null)
        {
            Debug.Log("PauseMenuController Start playerDataHub?.CurrentplayerInput != null");
            // タイポ修正: playerInput -> playerInput
            // pauseAction = playerDataHub.CurrentplayerInput.actions.FindAction("ESC");
            Debug.Log("PauseMenuController Start pauseAction: " + pauseAction);
            if (pauseAction != null)
            {
                Debug.Log("pauseAction.performed += OnTogglePause");
                // pauseAction.performed += OnTogglePause;
            }
            else
            {
                Debug.LogWarning("PauseMenuController: 'ESC'のアクションが見つかりません。");
            }
        }
        else
        {
            Debug.LogError("PauseMenuController: PlayerDataHub または PlayerInput が見つかりません。");
        }
    }

    void OnDestroy()
    {
        if (pauseAction != null)
        {
            // pauseAction.performed -= OnTogglePause;
        }
    }
    void Update()
    {
        Debug.Log("pauseAction: " + pauseAction);
        if (pauseAction.action.triggered)
        {
            Debug.Log("PauseAction.Triggered");
            OnTogglePause();
        }
    }
    private void OnTogglePause()
    {
        Debug.Log("OnTogglePause ない" );
        if (!PauseMenuOpenPermit) return;
        Debug.Log("OnTogglePause PauseMenuOpenPermitはtrueでした" );
        // ポーズ解除のロジックを復元
        if (IsPaused)
        {
            Debug.Log("OnTogglePause ない　IsPaused" );
            // ポーズ解除はいつでも許可
            ResumeGame();
        }
        else
        {
            Debug.Log("OnTogglePause ない　!IsPaused" );
            // ロード中かのチェックを最優先で行う
            if (SceneManagerController.Instance != null && SceneManagerController.Instance.IsLoading)
            {
                Debug.Log("[PauseMenuController] シーンロード中のため、ポーズできません。");
                return; // ロード中はポーズ処理を中断
            }

            // ポーズする際に時間チェック
            if (SceneManagerController.Instance != null)
            {
                // 最後に明転完了した時刻から1秒経っていない場合
                if (Time.realtimeSinceStartup < SceneManagerController.LastFadeOutCompletedTime + 1.0f)
                {
                    Debug.Log($"[PauseMenuController] シーン遷移から1秒経過していないため、ポーズできません。");
                    return; // ポーズ処理を中断
                }
            }
            Debug.Log("PauseGameを実行しようとします" );
            PauseGame();
        }
    }

    public void ResumeGame()
    {
        mosaicVolume.enabled = false;
        Time.timeScale = 1f;
        IsPaused = false;
        uiController.HidePauseMenu();
        gameStateController.ActionByModeSelect(GameStateController.GameState.Gameplay, false);
        // Cursor.lockState = CursorLockMode.Locked;
        // Cursor.visible = false;
    }

    private void PauseGame()
    {
        Debug.Log("PauseGame関数内です" );
        mosaicVolume.enabled = true;
        Time.timeScale = 0f;
        IsPaused = true;
        uiController.ShowPauseMenu();
        gameStateController.ActionByModeSelect(GameStateController.GameState.MenuOnGamePlay, true);
        // Cursor.lockState = CursorLockMode.None;
        // Cursor.visible = true;
    }

    public void BackToTitle()
    {
        Time.timeScale = 1f;
        IsPaused = false;

        // シーン遷移を SceneManagerController に任せる
        if (SceneManagerController.Instance != null)
        {
            SceneManagerController.Instance.LoadScene(titleSceneName);
        }
        else
        {
            // SceneManagerController がない場合のフォールバック
            SceneManager.LoadScene(titleSceneName);
        }
    }
}

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
public class PauseMenuController : MonoBehaviour
{
    [Header("UI Controller Reference")]
    [SerializeField] private PauseMenuUIController uiController;

    [Header("Settings")]
    [Tooltip("タイトルシーンの名前")]
    [SerializeField] private string titleSceneName = "Title"; // Inspectorでタイトルシーン名を設定
    [SerializeField]
    private Volume mosaicVolume;
    public static bool IsPaused { get; private set; } = false;

    private PlayerDataHub playerDataHub;
    private InputAction pauseAction; // ポーズ用のアクションをキャッシュする変数
    private GameStateController gameStateController;
    private ConfigController configController;
    void Start()
    {
        playerDataHub = PlayerDataHub.instance;
        gameStateController = GameStateController.instance;
        configController = ConfigController.Instance;
        // タイポ修正: CurrentplayerInput -> CurrentPlayerStatus.playerInput (以前のコードを参照)
        if (playerDataHub?.CurrentplayerInput != null)
        {
            // タイポ修正: playerInput -> playerInput
            pauseAction = playerDataHub.CurrentplayerInput.actions.FindAction("ESC");

            if (pauseAction != null)
            {
                pauseAction.performed += OnTogglePause;
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
            pauseAction.performed -= OnTogglePause;
        }
    }

    private void OnTogglePause(InputAction.CallbackContext context)
    {
        // ポーズ解除のロジックを復元
        if (IsPaused)
        {
            // ポーズ解除はいつでも許可
            ResumeGame();
        }
        else
        {
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
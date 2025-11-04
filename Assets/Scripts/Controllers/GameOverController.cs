// GameOverController.cs
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems; // Odin使わないなら消してOK

[AddComponentMenu("UI/GameOverController")]
public class GameOverController : Singleton<GameOverController>
{
    [TitleGroup("Refs")]
    [SerializeField, Tooltip("GameOver用のCanvasルート。ここをSetActive(true)にします")]
    private GameObject gameOverRoot;

    [SerializeField, Tooltip("フェードに使う CanvasGroup。gameOverRoot から自動取得可")]
    private CanvasGroup canvasGroup;

    [SerializeField, Tooltip("やり直しボタン（任意）。自動で onClick に Restart を登録")]
    private Button restartButton;

    [TitleGroup("Fade"), MinValue(0f)]
    [SerializeField] private float fadeDuration = 0.6f;

    [TitleGroup("Fade")]
    [SerializeField] private Ease fadeEase = Ease.OutQuad;

    [TitleGroup("Fade"), Tooltip("Time.timeScale=0 でもフェードする（推奨ON）")]
    [SerializeField] private bool useUnscaledTime = true;

    [TitleGroup("Startup"), Tooltip("開始時に非表示にしておく")]
    [SerializeField] private bool hideOnStart = true;

    private Tween _fadeTween;
    private PlayerStatus playerStatus;
    private PlayerMovement playerMovement;
    private GameStateController gameStateController;
    private SceneManagerController sceneManagerController;
    private bool restartOnce;
    private void Start()
    {
        sceneManagerController = SceneManagerController.Instance;
        gameStateController = GameStateController.instance;
        playerStatus = PlayerStatus.instance;
        playerMovement = playerStatus.gameObject.GetComponent<PlayerMovement>();
        SceneManager.sceneLoaded += OnSceneLoaded;
        // 参照の自動補完
        if (canvasGroup == null)
            canvasGroup = (gameOverRoot ? gameOverRoot : gameObject).GetComponentInChildren<CanvasGroup>(true);

        if (gameOverRoot == null && canvasGroup != null)
            gameOverRoot = canvasGroup.gameObject;
        // 起動時は隠す
        if (hideOnStart && gameOverRoot != null)
        {
            gameOverRoot.SetActive(false);
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.blocksRaycasts = false;
                canvasGroup.interactable = false;
            }
        }

        // ボタン登録
        if (restartButton != null)
        {
            restartButton.onClick.RemoveListener(Restart);
            restartButton.onClick.AddListener(Restart);
        }
    }

    private void OnDisable()
    {
        _fadeTween?.Kill();
        if (restartButton != null)
            restartButton.onClick.RemoveListener(Restart);
    }

    [Button("Show GameOver (Test)")]
    public void GameOver()
    {
        if (gameOverRoot == null || canvasGroup == null)
        {
            Debug.LogWarning("[GameOverController] gameOverRoot / canvasGroup の参照がありません。");
            return;
        }
        playerMovement.enabled = false;
        gameStateController.ActionByModeSelect(GameStateController.GameState.MenuOnGamePlay, true);
        _fadeTween?.Kill();
        gameOverRoot.SetActive(true);
        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;

        _fadeTween = canvasGroup
            .DOFade(1f, fadeDuration)
            .SetEase(fadeEase)
            .SetUpdate(useUnscaledTime)
            .OnComplete(() =>
            {
                canvasGroup.blocksRaycasts = true;
                canvasGroup.interactable = true;
                EventSystem.current.SetSelectedGameObject(restartButton.gameObject);
            });
    }

    // とりあえず空でOK（ボタンに紐づけられます）
    public void Restart()
    {
        if (restartOnce) return;
        // TODO: シーン再読み込み or リトライ処理を書く
        // 例）SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        EventSystem.current.SetSelectedGameObject(null);

        sceneManagerController.ReloadCurrentScene();
        restartOnce = true;
    }
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        restartOnce = false;
    }
    // 即時で非表示に戻したい時用（任意）
    [Button("Hide Immediate (Test)")]
    public void HideImmediate()
    {
        _fadeTween?.Kill();
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }
        if (gameOverRoot != null)
            gameOverRoot.SetActive(false);
    }
}

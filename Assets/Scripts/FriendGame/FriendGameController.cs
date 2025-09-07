using TMPro;
using UnityEngine.UI;
using UnityEditor.Search;
using UnityEngine;
using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine.Events;
using System.Collections.Generic;

public class FriendGameController : Singleton<FriendGameController>
{
    [Title("Refs")]
    [SerializeField] private FriendGameAdvert friendGameAdvert;
    [Required, SerializeField] private List<TextMeshProUGUI> HaveCoinsTextUIs; // ★複数表示に対応
    [Required, SerializeField] private Button RetryButton;
    [Required, SerializeField] private GameObject GameOverUI;
    [Required, SerializeField] private FriendGamePlayerController friendGamePlayerController;
    [SerializeField] private AudioSource coinAudioSource;

    [Title("Coins 管理")]
    [Required, LabelText("コイン親（子を全部復活させる）")]
    [SerializeField] private GameObject coins;

    [Title("UI: カウントダウン")]
    [SerializeField] private TextMeshProUGUI countdownText;
    [MinValue(0.1f)] public float countdownStep = 0.6f;
    public Vector3 countdownPopScale = new Vector3(1.25f, 1.25f, 1f);

    [Title("State")]
    [ReadOnly, SerializeField] private int HaveCoins;
    [ShowInInspector, ReadOnly] private bool isGameOver = false;
    [ShowInInspector, ReadOnly, LabelText("Retry Count")] private int retryCount = 0; // ★追加

    [Title("Events")]
    public UnityEvent OnGameStart;
    public UnityEvent OnGameReset;
    public UnityEvent OnGameOver;

    [Title("Fail 判定")]
    [LabelText("落下しきい値 (Y)")] public float fallKillY = -10f;
    [LabelText("Scene上にYラインを表示")] public bool showFallLineGizmo = true;

    [Title("GameOver 演出")]
    [Required, LabelText("GameOver用SE出力先")] public AudioSource gameOverAudioSource;
    [LabelText("落下時に鳴らすSE")] public AudioClip fallGameOverClip;
    [MinValue(0f), LabelText("GameOverUIを出すまでの待機秒数")] public float gameOverDelay = 1.0f;

    [Title("CountDown SFX")]
    [Required, LabelText("カウントダウンSE出力先")] public AudioSource countdownAudioSource;
    [Required, LabelText("3/2/1 で鳴らすSE")] public AudioClip countdownTickClip;
    [Required, LabelText("GO! で鳴らすSE")] public AudioClip countdownGoClip;

    [Title("GameClear UI / Next")]
    [Required, LabelText("GameClear UI")] public GameObject gameClearUI; // ★追加
    [Required, LabelText("Next Stage Button")] public Button NextStageButton; // ★追加

    [ShowInInspector, ReadOnly, LabelText("初期プレイヤー位置")]
    private Vector3 initialPlayerPos;
    private CanvasGroup AdvertCanvasGroup;
    private Tween _gameOverDelayTween;

    void Start()
    {
        AdvertCanvasGroup = friendGameAdvert.FriendGameAdvertCanvasGroup;
        RetryButton.onClick.AddListener(() =>
        {
            // ★RetryCountを増やす（押した時点でインクリメント）
            retryCount++;

            // ★現時点のretryCountが2なら FriendGameEnd() を実行
            if (retryCount >= 2)
            {
                FriendGameEnd();
            }

            GameReset();
        });

        NextStageButton.onClick.AddListener(FriendGameEnd); // ★NextStageButtonでFriendGameEnd()

        GameOverUI.SetActive(false);
        gameClearUI.SetActive(false);
        UpdateCoinUI();
        isGameOver = false;

        initialPlayerPos = friendGamePlayerController.rb.position;

        friendGamePlayerController.moveMode = false;
        friendGamePlayerController.Stop();
    }

    void Update()
    {
        if (!isGameOver)
        {
            float py = friendGamePlayerController.rb.position.y;
            if (py < fallKillY)
            {
                TriggerFallGameOver();
            }
        }
    }

    [Button(ButtonSizes.Large)]
    public void GameStart()
    {
        if (countdownText)
        {
            countdownText.gameObject.SetActive(true);
            countdownText.alpha = 1f;

            var seq = DOTween.Sequence();
            seq.Append(CountStep("3"));
            seq.Append(CountStep("2"));
            seq.Append(CountStep("1"));
            seq.Append(CountStep("GO!", 0.5f));
            seq.OnComplete(() =>
            {
                countdownText.DOFade(0, 0.2f)
                    .OnComplete(() => countdownText.gameObject.SetActive(false));

                friendGamePlayerController.moveMode = true;
                OnGameStart?.Invoke();
                friendGamePlayerController.Movestart();
            });
        }
        else
        {
            friendGamePlayerController.moveMode = true;
            OnGameStart?.Invoke();
            friendGamePlayerController.Movestart();
        }

        isGameOver = false;
    }

    [Button]
    public void GameReset()
    {
        _gameOverDelayTween?.Kill();
        _gameOverDelayTween = null;

        GameOverUI.SetActive(false);
        gameClearUI.SetActive(false);

        HaveCoins = 0;
        UpdateCoinUI();

        int childCount = coins.transform.childCount;
        for (int i = 0; i < childCount; i++)
        {
            var child = coins.transform.GetChild(i);
            if (!child.gameObject.activeSelf) child.gameObject.SetActive(true);
        }

        friendGamePlayerController.rb.position = initialPlayerPos;
        friendGamePlayerController.rb.linearVelocity = Vector2.zero;
        friendGamePlayerController.Stop();
        friendGamePlayerController.moveMode = false;

        OnGameReset?.Invoke();
        isGameOver = false;
    }

    public void CoinGet(int GetCount)
    {
        HaveCoins += GetCount;
        UpdateCoinUI();
        coinAudioSource.Play();
    }

    [Button(ButtonSizes.Medium)]
    public void GameOver()
    {
        if (isGameOver) return;
        isGameOver = true;

        friendGamePlayerController.moveMode = false;
        friendGamePlayerController.Stop();

        GameOverUI.SetActive(true);
        OnGameOver?.Invoke();
    }

    private void TriggerFallGameOver()
    {
        if (isGameOver) return;
        isGameOver = true;

        friendGamePlayerController.moveMode = false;
        friendGamePlayerController.Stop();

        gameOverAudioSource.PlayOneShot(fallGameOverClip);

        _gameOverDelayTween = DOVirtual.DelayedCall(gameOverDelay, () =>
        {
            GameOverUI.SetActive(true);
            OnGameOver?.Invoke();
        });
    }

    // ★追加：ゲームクリア
    [Button("GameClear")]
    public void GameClear()
    {
        gameClearUI.SetActive(true);
        friendGamePlayerController.Stop();
    }

    [Button("FriendGameEnd")]
    public void FriendGameEnd()
    {
        // ここにステージ遷移や集計、シーンロード等を書く想定
        GameOverUI.SetActive(false);
        gameClearUI.SetActive(false);
        // gameClearUI.SetActive(false);
        friendGamePlayerController.Stop();
        Debug.Log("シューーーリョオウウ");
        friendGameAdvert.gameObject.SetActive(true);
        AdvertCanvasGroup.alpha = 0;
        AdvertCanvasGroup.DOFade(1, 0.5f).SetEase(Ease.Linear).OnComplete(() =>
        {
            friendGameAdvert.FriendGameAdvertAudioSource.Play();
        });
    }

    private void UpdateCoinUI()
    {
        // ★複数の所持コイン表示を一括更新
        for (int i = 0; i < HaveCoinsTextUIs.Count; i++)
        {
            var t = HaveCoinsTextUIs[i];
            t.SetText($"{HaveCoins}");
            t.rectTransform.DOPunchScale(Vector3.one * 0.15f, 0.18f, 6, 0.8f);
        }
    }

    private Sequence CountStep(string text, float? stepOverride = null)
    {
        float d = stepOverride ?? countdownStep;
        var t = countdownText;

        var s = DOTween.Sequence();
        s.AppendCallback(() =>
        {
            t.text = text;
            t.transform.localScale = Vector3.one;
            t.alpha = 1f;

            var clip = (text == "GO!") ? countdownGoClip : countdownTickClip;
            countdownAudioSource.PlayOneShot(clip);
        });
        s.Append(t.rectTransform.DOScale(countdownPopScale, d * 0.45f).SetEase(Ease.OutBack));
        s.AppendInterval(d * 0.2f);
        s.Append(t.rectTransform.DOScale(Vector3.one, d * 0.35f));
        return s;
    }

    private void OnDrawGizmos()
    {
        if (!showFallLineGizmo) return;
        Gizmos.color = Color.red;
        const float span = 100f;
        Vector3 a = new Vector3(-span, fallKillY, 0f);
        Vector3 b = new Vector3( span, fallKillY, 0f);
        Gizmos.DrawLine(a, b);
    }
}

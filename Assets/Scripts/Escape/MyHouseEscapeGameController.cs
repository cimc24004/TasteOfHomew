// MyHouseEscapeGameController.cs
using System.Collections;
using Cysharp.Threading.Tasks;          // ← UniTask だけ使う
using UnityEngine;
using UnityEngine.Events;

[AddComponentMenu("Game/Controllers/MyHouseEscapeGameController")]
public class MyHouseEscapeGameController : MonoBehaviour
{
    public enum State { Idle, PhoneCalled, MotherEntered, Stealth, Success, Failed }
    public State CurrentState { get; private set; } = State.Idle;

    [Header("電話インタラクト後の挙動")]
    [Tooltip("電話にインタラクトした瞬間に呼ばれる（通報SE/画面表示などを割り当て）")]
    public UnityEvent onPhoneInteracted;

    [Tooltip("母親が“登場した”タイミングで呼ぶ（ドア開閉SE/カメラ/AI起動など）")]
    public UnityEvent onMotherEnter;

    [Header("ステルス（警察到着まで）")]
    [Tooltip("警察到着までの秒数")]
    [SerializeField] private float policeArrivalSeconds = 150f;

    [Tooltip("ステルス開始時に呼ぶ（UI表示/BGM切替など）")]
    public UnityEvent onStealthStart;

    [Tooltip("ステルス中、毎フレーム進捗(0..1)を投げる。UIバー更新用")]
    public UnityEvent<float> onStealthTick;

    [Tooltip("警察到着＝成功時に呼ぶ（救出演出）")]
    public UnityEvent onStealthSuccess;

    [Tooltip("見つかった＝失敗時に呼ぶ（BAD演出）")]
    public UnityEvent onStealthFailed;

    // ランタイム
    [Header("発見時の演出")]
    [SerializeField] private GameObject motherCPUHead;                 // カメラ注視ターゲット
    [SerializeField] private CameraFocusController cameraFocusController;
    [SerializeField] private AudioSource FoundOutAudioSource;

    [Header("UI/その他")]
    [SerializeField] private GameOverController gameOverController;    // Inspector で設定可（未設定なら自動取得）

    private Coroutine stealthCo;
    private float remain;
    private bool _failing; // 多重起動防止

    void Awake()
    {
        // 参照の自動補完
        if (gameOverController == null)
        {
            // あなたの実装が Singleton の場合
            // （無い場合でも FindObjectOfType で拾う）
            try { gameOverController = GameOverController.instance; } catch { /* ignore */ }
            if (gameOverController == null)
                gameOverController = FindObjectOfType<GameOverController>(true);
        }

        if (cameraFocusController == null)
            cameraFocusController = FindObjectOfType<CameraFocusController>(true);
    }

    void Start()
    {
        // 必要ならここで追加の参照取得をしてOK
    }

    public void OnPhoneInteracted()
    {
        CurrentState = State.PhoneCalled;
        onPhoneInteracted?.Invoke();
    }

    public void OnMotherEntered()
    {
        onMotherEnter?.Invoke();
        CurrentState = State.MotherEntered;
    }

    public void OnStealthGameStart()
    {
        onStealthStart.Invoke();
        // 必要なら BeginStealth(); を呼ぶ
    }

    /// <summary>AIやトリガーから「プレイヤーが見つかった」と通知する。</summary>
    public void NotifyPlayerDetected()
    {
        // ここはイベントハンドラなので「火を付けて放置」でOK
        if (!_failing) FailStealthAsync().Forget();
    }

    /// <summary>外部（カットシーン等）から「警察が到着した」と通知</summary>
    public void NotifyPoliceArrived()
    {
        SucceedStealth();
    }

    public float GetStealthProgress01()
    {
        if (policeArrivalSeconds <= 0f) return 1f;
        return Mathf.Clamp01(1f - (remain / policeArrivalSeconds));
    }

    // ===== 内部処理 =====

    private void BeginStealth()
    {
        if (stealthCo != null) StopCoroutine(stealthCo);
        remain = Mathf.Max(0f, policeArrivalSeconds);
        CurrentState = State.Stealth;
        onStealthStart?.Invoke();
        stealthCo = StartCoroutine(StealthCountdown());
    }

    private IEnumerator StealthCountdown()
    {
        if (remain <= 0f)
        {
            SucceedStealth();
            yield break;
        }

        while (remain > 0f && CurrentState == State.Stealth)
        {
            remain -= Time.deltaTime;
            onStealthTick?.Invoke(GetStealthProgress01());
            yield return null;
        }

        if (CurrentState == State.Stealth) SucceedStealth();
    }

    private void SucceedStealth()
    {
        if (stealthCo != null) { StopCoroutine(stealthCo); stealthCo = null; }
        CurrentState = State.Success;
        onStealthSuccess?.Invoke();
    }

    /// <summary>
    /// 失敗処理：音→カメラ注視→少し待つ→GameOver→イベント
    /// fire-and-forget 用に UniTask で実装
    /// </summary>
    private async UniTask FailStealthAsync()
    {
        _failing = true;

        if (stealthCo != null) { StopCoroutine(stealthCo); stealthCo = null; }
        CurrentState = State.Failed;

        if (FoundOutAudioSource != null) FoundOutAudioSource.Play();

        if (cameraFocusController != null && motherCPUHead != null)
            cameraFocusController.FocusOnTransform(motherCPUHead.transform);

        // タイムスケール0でも確実に待つ
        await UniTask.WaitForSeconds(1f, ignoreTimeScale: true);

        if (gameOverController != null)
            gameOverController.GameOver();
        else
            Debug.LogWarning("[MyHouseEscape] GameOverController の参照がありません。");

        onStealthFailed?.Invoke();

        _failing = false;
    }
}

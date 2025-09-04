// MyHouseEscapeGameController.cs
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

[AddComponentMenu("Game/Controllers/MyHouseEscapeGameController")]
public class MyHouseEscapeGameController : MonoBehaviour
{
    public enum State { Idle, PhoneCalled, Stealth, Success, Failed }
    public State CurrentState { get; private set; } = State.Idle;

    [Header("電話インタラクト後の挙動")]
    [Tooltip("電話にインタラクトした瞬間に呼ばれる（通報SE/画面表示などを割り当て）")]
    public UnityEvent onPhoneInteracted;

    [Header("母親の登場")]
    [Tooltip("母親をプレハブから生成したい場合に設定（空なら生成しない）")]
    [SerializeField] private GameObject motherPrefab;
    [Tooltip("母親のスポーン位置（未設定ならこのオブジェクトの位置）")]
    [SerializeField] private Transform motherSpawnPoint;
    [Tooltip("電話後に自動で母親をスポーンさせる")]
    [SerializeField] private bool spawnMotherOnPhone = true;

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
    private GameObject motherInstance;
    private Coroutine stealthCo;
    private float remain;

    /// <summary>
    /// 電話機のインタラクトから呼ぶ。InspectorでButtonや自作Interactから紐付け推奨。
    /// </summary>
    public void OnPhoneInteracted()
    {
        if (CurrentState != State.Idle) return;

        CurrentState = State.PhoneCalled;
        onPhoneInteracted?.Invoke();

        // 母親登場処理
        if (spawnMotherOnPhone)
        {
            SpawnOrShowMother();
        }
        onMotherEnter?.Invoke();

        // ステルス開始（警察が来るまで耐える）
        BeginStealth();
    }

    /// <summary>
    /// AIやトリガーから「プレイヤーが見つかった」と通知する。
    /// </summary>
    public void NotifyPlayerDetected()
    {
        if (CurrentState != State.Stealth) return;
        FailStealth();
    }

    /// <summary>
    /// 外部（カットシーン等）から「警察が到着した」と通知する場合に使用。
    /// 自動カウントダウンを使わず、演出のタイミングで成功にしたい時に便利。
    /// </summary>
    public void NotifyPoliceArrived()
    {
        if (CurrentState != State.Stealth) return;
        SucceedStealth();
    }

    /// <summary>
    /// 進捗（0..1）付きで現在の残り時間をUIなどから参照したい時。
    /// </summary>
    public float GetStealthProgress01()
    {
        if (policeArrivalSeconds <= 0f) return 1f;
        return Mathf.Clamp01(1f - (remain / policeArrivalSeconds));
    }

    // ===== 内部処理 =====

    private void SpawnOrShowMother()
    {
        if (motherInstance != null)
        {
            motherInstance.SetActive(true);
            return;
        }

        if (motherPrefab == null) return;

        var pos = motherSpawnPoint != null ? motherSpawnPoint.position : transform.position;
        var rot = motherSpawnPoint != null ? motherSpawnPoint.rotation : Quaternion.identity;
        motherInstance = Instantiate(motherPrefab, pos, rot);
    }

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
        // 0秒設定なら即成功
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

        if (CurrentState == State.Stealth)
        {
            SucceedStealth();
        }
    }

    private void SucceedStealth()
    {
        if (stealthCo != null) { StopCoroutine(stealthCo); stealthCo = null; }
        CurrentState = State.Success;
        onStealthSuccess?.Invoke();
        // 成功時の後片付け（必要なら母親AI停止など）をUnityEvent側で実装推奨
    }

    private void FailStealth()
    {
        if (stealthCo != null) { StopCoroutine(stealthCo); stealthCo = null; }
        CurrentState = State.Failed;
        onStealthFailed?.Invoke();
        // 失敗時の後片付けもUnityEventで
    }
}

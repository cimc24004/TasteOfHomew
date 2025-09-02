using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using Sirenix.OdinInspector;

[RequireComponent(typeof(Rigidbody))]
public class AddForceController : MonoBehaviour
{
    [Header("Force Settings")]
    public Vector3 direction = Vector3.forward;
    public float intensity = 5f;
    [Tooltip("Inspectorで指定した direction をローカル座標で解釈するか")]
    public bool useLocalDirection = true;
    [Tooltip("デフォルトの ForceMode（Inspectorで変えて試して）")]
    public ForceMode forceMode = ForceMode.Impulse;

    [Header("Trigger / Collision (optional)")]
    [Tooltip("飛んでいる間に collider を isTrigger=true にするか")]
    public bool setTriggerWhileFlying = false;
    [Tooltip("isTrigger を元に戻すまでの時間（<=0 なら戻さない）")]
    public float triggerResetDelay = 0.5f;

    [Header("Options")]
    [Tooltip("速度を直接変更して確実に吹っ飛ばす（質量無視）")]
    public bool useVelocityChangeInstead = false;
    [Tooltip("衝突検出を安全側に（高速で移動する場合に有効）")]
    public CollisionDetectionMode ensureContinuousCollision = CollisionDetectionMode.Discrete;

    [Header("Events")]
    public UnityEvent onForceAdded;

    // internals
    private Rigidbody rb;
    private Collider col;
    private bool pendingAddForce = false;
    private Vector3 pendingDir;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();

        if (rb == null)
            Debug.LogError("[AddForceController] Rigidbody が見つかりません。");

        if (col == null && setTriggerWhileFlying)
            Debug.LogWarning("[AddForceController] Collider が見つかりません。setTriggerWhileFlying は無視されます。");

        // 初期ログ
        Debug.Log($"[AddForceController] Awake. useLocal={useLocalDirection}, forceMode={forceMode}, useVelocityChangeInstead={useVelocityChangeInstead}");
    }

    // --- 呼び出し方法（SignalReceiver に登録しやすい） ---
    // SignalReceiver からはこのメソッドを登録してくれ（parameterless）
    public void TriggerAddForce()
    {
        // Timeline の評価タイミングを考慮して「予定→FixedUpdateで実行」方式にする
        Vector3 dir = useLocalDirection ? transform.TransformDirection(direction.normalized) : direction.normalized;
        pendingDir = dir;
        pendingAddForce = true;
        Debug.Log($"[AddForceController] TriggerAddForce called (scheduled). dir={pendingDir}, intensity={intensity}, mode={forceMode}");
    }

    // Signal からカスタム方向で呼びたい場合（SignalReceiver の UnityEvent に Vector3 を流せるなら）
    public void TriggerAddForceWithVector(Vector3 customDir)
    {
        Vector3 dir = useLocalDirection ? transform.TransformDirection(customDir.normalized) : customDir.normalized;
        pendingDir = dir;
        pendingAddForce = true;
        Debug.Log($"[AddForceController] TriggerAddForceWithVector called (scheduled). dir={pendingDir}, intensity={intensity}, mode={forceMode}");
    }

    // Odinボタンや手動テスト用（Inspectorボタンから直接即時実行）
    [Button("AddForce Immediate")]
    public void AddForceImmediate()
    {
        Vector3 dir = useLocalDirection ? transform.TransformDirection(direction.normalized) : direction.normalized;
        Debug.Log("[AddForceController] AddForceImmediate called.");
        ApplyForceNow(dir);
    }

    // Odinボタンでスケジュール（Timelineと同じ流れで実行したい場合）
    [Button("AddForce Schedule (FixedUpdate)")]
    public void AddForceSchedule()
    {
        Vector3 dir = useLocalDirection ? transform.TransformDirection(direction.normalized) : direction.normalized;
        pendingDir = dir;
        pendingAddForce = true;
        Debug.Log("[AddForceController] AddForceSchedule called (will run in FixedUpdate).");
    }

    void FixedUpdate()
    {
        if (!pendingAddForce) return;
        pendingAddForce = false;
        ApplyForceNow(pendingDir);
    }

    private void ApplyForceNow(Vector3 dir)
    {
        if (rb == null)
        {
            Debug.LogError("[AddForceController] Rigidbody が null のため力を加えられません。");
            return;
        }

        if (rb.isKinematic)
            Debug.LogWarning("[AddForceController] Rigidbody.isKinematic == true。力は無視されます。isKinematic を false にしてください。");

        // Wake up to be safe
        rb.WakeUp();

        // optionally ensure collision mode
        if (ensureContinuousCollision != CollisionDetectionMode.Discrete)
        {
            rb.collisionDetectionMode = ensureContinuousCollision;
            Debug.Log($"[AddForceController] collisionDetectionMode set to {rb.collisionDetectionMode}");
        }

        // optional: temporarily switch to trigger
        if (setTriggerWhileFlying && col != null)
        {
            try
            {
                col.isTrigger = true;
                if (triggerResetDelay > 0f)
                    StartCoroutine(ResetTriggerAfter(triggerResetDelay));
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[AddForceController] Collider.isTrigger の切替で例外: " + ex);
            }
        }

        // Apply force or set velocity directly
        if (useVelocityChangeInstead)
        {
            // velocity change ignores mass -> immediate and reliable
            Vector3 newVel = dir * intensity;
            rb.linearVelocity = newVel;
            Debug.Log($"[AddForceController] Set velocity directly. velocity={rb.linearVelocity}, mass={rb.mass}");
        }
        else
        {
            rb.AddForce(dir * intensity, forceMode);
            Debug.Log($"[AddForceController] AddForce called. dir={dir}, intensity={intensity}, mode={forceMode}, mass={rb.mass}, currentVel(before)={rb.linearVelocity}");
        }

        onForceAdded?.Invoke();
    }

    private IEnumerator ResetTriggerAfter(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        if (col != null)
        {
            col.isTrigger = false;
            Debug.Log("[AddForceController] Collider.isTrigger を元に戻した");
        }
    }
}

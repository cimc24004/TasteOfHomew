using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;
using Sirenix.OdinInspector;

/// <summary>
/// NavMeshAgentを使用して指定された目的地(Transform)へ移動し、
/// 到着時にUnityEventを発行するコンポーネント。
/// 一時停止・再開機能を含む。
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class AgentMoveToTarget : SerializedMonoBehaviour
{
    [Title("Information", "スクリプトの概要")]
    [InfoBox("指定された目的地 (Destination Transform) へ移動し、到着したらイベントを発行します。")]
    [Space(10)]

    [Title("Components", "必須コンポーネント")]
    [Required(InfoMessageType.Error)]
    [SerializeField]
    [ReadOnly]
    private NavMeshAgent agent;

    [Title("Settings", "移動設定")]
    [Tooltip("エージェントが移動する目的地（シーン上のGameObjectを指定）")]
    [SerializeField]
    [Required("目的地を指定してください")]
    private Transform destinationTransform;

    [Tooltip("目的地に「到着した」とみなす距離。AgentのStopping Distanceと連動します。")]
    [SerializeField]
    [Range(0.1f, 5f)]
    private float stoppingDistance = 0.5f;

    [Tooltip("ゲーム開始時に自動で移動を開始するかどうか")]
    [SerializeField]
    private bool moveOnStart = true;

    [Tooltip("目的地が移動した場合、自動で追跡するかどうか")]
    [SerializeField]
    private bool trackDestination = false;

    [Title("Events", "到着時イベント")]
    [Tooltip("目的地に到着したときに呼び出されるUnityEvent")]
    [SerializeField]
    private UnityEvent OnDestinationReached;
    [SerializeField]
    private UnityEvent2 OnDestinationReachedByUnityEvent2;

    [Title("Status (Read Only)", "現在の状態")]
    [ShowInInspector]
    [ReadOnly]
    [PropertyOrder(50)] // 表示順を調整
    private bool hasReached = false;

    [ShowInInspector]
    [ReadOnly]
    [PropertyOrder(51)]
    private bool isPaused = false;

    /// <summary>
    /// コンポーネント初期化時
    /// </summary>
    private void Awake()
    {
        if (agent == null)
        {
            TryGetComponent(out agent);
        }

        agent.stoppingDistance = this.stoppingDistance;
    }

    /// <summary>
    /// ゲーム開始時
    /// </summary>
    private void Start()
    {
        if (moveOnStart)
        {
            StartMovement();
        }
    }

    /// <summary>
    /// 毎フレームの更新処理
    /// </summary>
    private void Update()
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh)
        {
            return;
        }

        // 一時停止中、または既に到着済みの場合は処理しない
        if (isPaused || hasReached)
        {
            return;
        }

        // 追跡モードがオンの場合、毎フレーム目的地を更新
        if (trackDestination)
        {
            // 目的地が設定されていれば移動
            if (destinationTransform != null)
            {
                agent.SetDestination(destinationTransform.position);
            }
        }

        // 経路計算中でなく、残りの距離が停止距離以下になったかを確認
        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
        {
            // 速度がほぼゼロになったかを確認
            if (!agent.hasPath || agent.velocity.sqrMagnitude == 0f)
            {
                ReachDestination();
            }
        }
    }

    /// <summary>
    /// 目的地への移動を開始します。
    /// </summary>
    [Button("Move to Destination", ButtonSizes.Large)]
    [PropertyOrder(100)]
    [GUIColor(0.4f, 0.8f, 1f)]
    public void StartMovement()
    {
        if (agent == null)
        {
            Debug.LogError("NavMeshAgentが見つかりません。", this);
            return;
        }

        if (destinationTransform == null)
        {
            Debug.LogError("目的地 (Destination Transform) が設定されていません。", this);
            return;
        }

        hasReached = false; // 再度移動できるようにリセット
        isPaused = false;
        agent.isStopped = false; // NavMeshAgentの停止を解除
        agent.SetDestination(destinationTransform.position);
    }

    /// <summary>
    /// エージェントの移動を一時停止します。
    /// </summary>
    [Button("Pause Movement", ButtonSizes.Medium)]
    [PropertyOrder(101)]
    [GUIColor(1.0f, 0.7f, 0.3f)] // オレンジ色
    [EnableIf("@agent != null && agent.hasPath && !hasReached && !isPaused")]
    public void PauseMovement()
    {
        if (agent == null || !agent.enabled) return;

        agent.isStopped = true;
        isPaused = true;
    }

    /// <summary>
    /// エージェントの移動を再開します。
    /// </summary>
    [Button("Resume Movement", ButtonSizes.Medium)]
    [PropertyOrder(102)]
    [GUIColor(0.6f, 1.0f, 0.6f)] // 緑色
    [EnableIf("isPaused")]
    public void ResumeMovement()
    {
        if (agent == null || !agent.enabled) return;

        agent.isStopped = false;
        isPaused = false;
    }

    /// <summary>
    /// 目的地に到着した際の処理
    /// </summary>
    private void ReachDestination()
    {
        hasReached = true;
        isPaused = false; // 到着したので一時停止状態は解除
        agent.isStopped = true; // 目的地に到着したので、移動を停止
        Debug.Log("目的地に到着しました。", this);

        OnDestinationReached?.Invoke();
        OnDestinationReachedByUnityEvent2?.Invoke();
    }

    /// <summary>
    /// 目的地のGizmoをシーンビューに表示します。
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        // 目的地が設定されている場合のみGizmoを表示
        if (destinationTransform != null)
        {
            Vector3 targetPosition = destinationTransform.position;

            // 目的地をシアンの球で表示
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(targetPosition, stoppingDistance);
            Gizmos.DrawSphere(targetPosition, 0.3f);

            // 現在地から目的地への線を表示
            Gizmos.color = (agent != null && agent.hasPath) ? Color.yellow : Color.gray;
            Gizmos.DrawLine(transform.position, targetPosition);
        }
    }
}

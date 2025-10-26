using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;
using Sirenix.OdinInspector;

/// <summary>
/// NavMeshAgentを使用して指定された目的地(Transform)へ移動し、
/// 到着時にUnityEventを発行するコンポーネント。
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
    [Required("目的地を指定してください")] // 目的地が未設定の場合にエラーを表示
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
    private bool hasReached = false;

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

        // 追跡モードがオンの場合、毎フレーム目的地を更新
        if (trackDestination && !hasReached)
        {
            // 目的地が設定されていれば移動
            if (destinationTransform != null)
            {
                agent.SetDestination(destinationTransform.position);
            }
        }

        // 既に到着済みの場合は処理しない
        if (hasReached)
        {
            return;
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
        agent.SetDestination(destinationTransform.position);
    }

    /// <summary>
    /// 目的地に到着した際の処理
    /// </summary>
    private void ReachDestination()
    {
        hasReached = true;
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
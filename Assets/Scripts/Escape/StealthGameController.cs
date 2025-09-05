// StealthGameController.cs
// Manager版 + Odin対応 + 一定待機時間 + 往復(Ping-Pong) + 到着判定強化 + Gizmos強化
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Sirenix.OdinInspector;

#if UNITY_EDITOR
using UnityEditor;
#endif

[AddComponentMenu("Game/Controllers/StealthGameController (Manager)")]
public class StealthGameController : SerializedMonoBehaviour
{
    // ======= References =======
    [TitleGroup("References"), PropertyOrder(-100)]
    [Tooltip("進行管理の核。検知時に NotifyPlayerDetected() を呼ぶ")]
    [Required] public MyHouseEscapeGameController myHouseEscapeGameController;

    [TitleGroup("References")]
    [Tooltip("プレイヤー Transform。未設定なら Tag=Player を探索")]
    public Transform player;

    // ★追加: PlayerStatusのシングルトンから頭を参照
    private PlayerStatus playerStatus;
    private Transform playerHead;

    // ======= Behaviour (全体) =======
    [TitleGroup("Behaviour"), PropertySpace]
    [InfoBox("発見したら全ガードを停止するか？", InfoMessageType.None)]
    public bool stopAllOnDetect = true;

    [TitleGroup("Behaviour")]
    [InfoBox("MyHouseEscapeGameController の状態が Stealth 以外になったら自動停止する", InfoMessageType.None)]
    public bool autoStopWhenStateChanged = true;

    [TitleGroup("Behaviour")]
    [LabelText("Wait Time Scale (全体倍率)")]
    [Tooltip("全ガードの待機時間に掛ける倍率。0.5=半分, 2=2倍")]
    [MinValue(0f)]
    public float waitTimeScale = 1f;

    // ======= Guard 設定 =======
    [System.Serializable]
    public class GuardEntry
    {
        [HorizontalGroup("H", Width = 220)]
        [BoxGroup("H/ID", false)]
        [LabelText("Name")] public string name = "Mother";

        [BoxGroup("H/Agent", false)]
        [Required, LabelText("NavMeshAgent")] public NavMeshAgent agent;

        [BoxGroup("H/Agent", false)]
        [LabelText("Eye(任意)")] public Transform eye;

        [FoldoutGroup("Patrol", true)]
        [ListDrawerSettings(Expanded = true, DraggableItems = true)]
        [LabelText("Patrol Points")] public List<Transform> patrolPoints = new();

        [FoldoutGroup("Patrol")]
        [LabelText("Random Patrol")] public bool randomPatrol = false;

        [FoldoutGroup("Patrol")]
        [LabelText("Ping-Pong (非ランダム時)")] public bool pingPong = true;

        [FoldoutGroup("Patrol")]
        [LabelText("Start From Closest")] public bool startFromClosestPoint = true;

        [FoldoutGroup("Patrol")]
        [LabelText("Arrive Threshold")] public float arriveThreshold = 0.2f;

        [FoldoutGroup("Patrol")]
        [LabelText("Use Agent.autoBraking")] public bool useAutoBraking = true;

        [FoldoutGroup("Patrol")]
        [LabelText("CloseEnough Dist (fallback)")] public float closeEnoughDistance = 0.5f;

        [FoldoutGroup("Patrol")]
        [LabelText("Arrival Timeout (s)")] public float arrivalTimeoutSeconds = 6f;

        [FoldoutGroup("Patrol")]
        [LabelText("Planar Distance (XZ) で到着判定も見る")] public bool usePlanarDistance = true;

        [FoldoutGroup("Patrol")]
        [LabelText("Wait (s) 一定時間"), MinValue(0f)]
        [Tooltip("各ポイントで待機する“固定”秒数（min/maxは使わない）")]
        public float waitAtPointSeconds = 1.0f;

        [FoldoutGroup("Vision", true)]
        [Range(10f, 180f), LabelText("View Angle")] public float viewAngle = 70f;

        [FoldoutGroup("Vision")]
        [LabelText("View Distance")] public float viewDistance = 12f;

        [FoldoutGroup("Vision")]
        [LabelText("Eye Height (Y)")] public float eyeHeight = 1.6f;

        [FoldoutGroup("Vision")]
        [LabelText("Player Layer")] public LayerMask playerLayer = 1 << 6;

        [FoldoutGroup("Vision")]
        [LabelText("Obstruction Layers")] public LayerMask obstructionLayers = ~0;

        // Runtime
        [HideInInspector] public bool active;
        [HideInInspector] public int currentIndex;
        [HideInInspector] public Coroutine patrolCo;
        [HideInInspector] public int direction = 1; // +1/-1（往復用）
    }

    [TitleGroup("Guards"), PropertySpace]
    [ListDrawerSettings(Expanded = true, DraggableItems = true, ShowIndexLabels = true)]
    public List<GuardEntry> guards = new();

    // ======= Odin Buttons =======
    [TitleGroup("Controls"), ButtonGroup("Controls/Row1"), GUIColor(0.2f, 0.75f, 0.2f)]
    [Button("Start Stealth", ButtonSizes.Large)]
    public void StealthGameStart()
    {
        // プレイヤー自動補完
        if (player == null)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p) player = p.transform;
        }

        foreach (var g in guards) StartGuard(g);
    }

    [ButtonGroup("Controls/Row1"), GUIColor(0.9f, 0.35f, 0.35f)]
    [Button("Stop All", ButtonSizes.Large)]
    public void StopAllGuards()
    {
        foreach (var g in guards) StopGuard(g);
    }

    [ButtonGroup("Controls/Row2"), GUIColor(0.35f, 0.6f, 1f)]
    [Button("Snap CurrentIndex From Closest")]
    public void SnapAllToClosestPoint()
    {
        foreach (var g in guards)
        {
            if (g.agent == null) continue;
            g.currentIndex = FindClosestPointIndex(g);
            g.direction = 1;
        }
    }

    [ButtonGroup("Controls/Row2"), GUIColor(0.9f, 0.7f, 0.2f)]
    [Button("Collect PatrolPoints From Selected")]
    public void CollectPointsFromSelected()
    {
#if UNITY_EDITOR
        if (Selection.activeTransform == null) { Debug.LogWarning("選択中のTransformがありません"); return; }
        var pts = new List<Transform>();
        foreach (Transform child in Selection.activeTransform) pts.Add(child);
        if (guards.Count == 0) guards.Add(new GuardEntry());
        guards[0].patrolPoints = pts;
        Debug.Log($"Guard[0] に {pts.Count} 個のポイントを採用");
#endif
    }

    // ======= Lifecycle =======
    private void Awake()
    {
        // PlayerStatusのシングルトン取得（順序の都合で null なら Start でもう一度取得）
        playerStatus = PlayerStatus.instance;
        if (playerStatus != null)
        {
            player = playerStatus.transform;
            playerHead = playerStatus.playerHead;
        }
        else
        {
            // フォールバック
            if (player == null)
            {
                var p = GameObject.FindGameObjectWithTag("Player");
                if (p) player = p.transform;
            }
        }
    }

    void Start()
    {
        // Start時点で再確認（Awake順序の保険）
        if (playerStatus == null) playerStatus = PlayerStatus.instance;
        if (playerStatus != null)
        {
            player = playerStatus.transform;
            if (playerHead == null) playerHead = playerStatus.playerHead;
        }
    }

    private void OnDisable() => StopAllGuards();

    private void Update()
    {
        if (autoStopWhenStateChanged && myHouseEscapeGameController != null)
        {
            var s = myHouseEscapeGameController.CurrentState;
            if (s != MyHouseEscapeGameController.State.Stealth &&
                s != MyHouseEscapeGameController.State.MotherEntered &&
                s != MyHouseEscapeGameController.State.PhoneCalled)
            {
                StopAllGuards();
                return;
            }
        }

        // ★ playerHead 優先で存在確認
        if (playerHead == null)
        {
            // 遅延初期化（シーン上で後から設定された場合の保険）
            if (playerStatus == null) playerStatus = PlayerStatus.instance;
            if (playerStatus != null) playerHead = playerStatus.playerHead;
            if (playerHead == null) return;
        }

        // 可視判定
        foreach (var g in guards)
        {
            if (!g.active || g.agent == null) continue;
            if (IsPlayerVisible(g))
            {
                myHouseEscapeGameController?.NotifyPlayerDetected();
                if (stopAllOnDetect) StopAllGuards();
                else g.active = false; // このガードだけ停止
                break;
            }
        }
    }

    // ======= Guard 制御 =======
    private void StartGuard(GuardEntry g)
    {
        if (g == null || g.agent == null) return;
        g.agent.enabled = true;
        g.active = true;
        g.direction = 1;
        g.currentIndex = 0;
        
        // Agent 初期化
        g.agent.autoBraking = g.useAutoBraking;
        if (g.arriveThreshold > 0f)
            g.agent.stoppingDistance = Mathf.Max(g.agent.stoppingDistance, g.arriveThreshold);

        if (g.startFromClosestPoint && g.patrolPoints != null && g.patrolPoints.Count > 1)
            g.currentIndex = FindClosestPointIndex(g);

        if (g.patrolCo != null) StopCoroutine(g.patrolCo);
        g.patrolCo = StartCoroutine(PatrolLoop(g));
    }

    private void StopGuard(GuardEntry g)
    {
        g.active = false;
        if (g.patrolCo != null) { StopCoroutine(g.patrolCo); g.patrolCo = null; }
        if (g.agent != null && g.agent.isOnNavMesh) g.agent.ResetPath();
    }

    private IEnumerator PatrolLoop(GuardEntry g)
    {
        while (g.active)
        {
            Transform target = null;
            if (g.patrolPoints != null && g.patrolPoints.Count > 0)
                target = g.patrolPoints[Mathf.Clamp(g.currentIndex, 0, g.patrolPoints.Count - 1)];

            if (target != null && g.agent.isOnNavMesh)
            {
                Vector3 targetPos = target.position;
                g.agent.SetDestination(targetPos);

                // ===== 到着待ち（強化版）=====
                float sinceSet = 0f;
                while (g.active)
                {
                    // path 計算が済んでいる時に各種到着判定を評価
                    if (!g.agent.pathPending)
                    {
                        float stopThresh = Mathf.Max(g.agent.stoppingDistance, g.arriveThreshold);

                        // 1) official: remainingDistance と速度
                        if (g.agent.remainingDistance <= stopThresh &&
                            (!g.agent.hasPath || g.agent.velocity.sqrMagnitude < 0.01f))
                            break;

                        // 2) fallback: 物理的な距離（XZ平面）
                        if (g.usePlanarDistance)
                        {
                            float planar = PlanarDistance(g.agent.transform.position, targetPos);
                            if (planar <= Mathf.Max(g.closeEnoughDistance, 0.001f))
                                break;
                        }

                        // 3) パス無効/部分的なら諦めて次へ
                        if (g.agent.pathStatus == NavMeshPathStatus.PathInvalid ||
                            g.agent.pathStatus == NavMeshPathStatus.PathPartial)
                        {
                            Debug.LogWarning($"[Stealth] {g.name}: pathStatus={g.agent.pathStatus} → 次へスキップ");
                            break;
                        }
                    }

                    // 4) タイムアウト保険
                    sinceSet += Time.deltaTime;
                    if (g.arrivalTimeoutSeconds > 0f && sinceSet >= g.arrivalTimeoutSeconds)
                    {
                        Debug.LogWarning($"[Stealth] {g.name}: ArrivalTimeout({g.arrivalTimeoutSeconds}s) 到達 → 次へ進む");
                        break;
                    }

                    yield return null;
                }

                // ===== 一定待機 =====
                float waitSec = Mathf.Max(0f, g.waitAtPointSeconds * Mathf.Max(0f, waitTimeScale));
                if (waitSec > 0f)
                {
                    float t = 0f;
                    while (g.active && t < waitSec) { t += Time.deltaTime; yield return null; }
                }

                // 次のポイントへ（Ping-Pong/Random対応）
                g.currentIndex = GetNextIndex(g);
            }
            else
            {
                // ポイントがない/Agent未配置のときは少し待ってリトライ
                yield return new WaitForSeconds(0.25f);
            }
        }
    }

    private int GetNextIndex(GuardEntry g)
    {
        if (g.patrolPoints == null || g.patrolPoints.Count <= 1) return 0;

        // ランダム巡回
        if (g.randomPatrol)
        {
            int next;
            do { next = Random.Range(0, g.patrolPoints.Count); } while (next == g.currentIndex);
            g.direction = 1; // ランダム時は方向意味なし
            return next;
        }

        // Ping-Pong（往復）
        if (g.pingPong)
        {
            int next = g.currentIndex + g.direction;

            // 端に到達したら折り返し
            if (next >= g.patrolPoints.Count)
            {
                g.direction = -1;
                next = g.currentIndex + g.direction;
            }
            else if (next < 0)
            {
                g.direction = 1;
                next = g.currentIndex + g.direction;
            }
            return Mathf.Clamp(next, 0, g.patrolPoints.Count - 1);
        }

        // ループ（0→1→…→N→0）
        int idx = g.currentIndex + 1;
        if (idx >= g.patrolPoints.Count) idx = 0;
        g.direction = 1;
        return idx;
    }

    private int FindClosestPointIndex(GuardEntry g)
    {
        if (g.patrolPoints == null || g.patrolPoints.Count == 0) return 0;
        var pos = g.agent.transform.position;
        int best = 0; float bestSqr = float.MaxValue;
        for (int i = 0; i < g.patrolPoints.Count; i++)
        {
            var t = g.patrolPoints[i]; if (t == null) continue;
            float d2 = (t.position - pos).sqrMagnitude;
            if (d2 < bestSqr) { bestSqr = d2; best = i; }
        }
        return best;
    }

    // ======= 視認判定（★ playerHead を狙う） =======
    private bool IsPlayerVisible(GuardEntry g)
    {
        if (playerHead == null) return false;

        // 目の位置＆向き
        Vector3 eyePos;
        Vector3 forward;
        if (g.eye != null) { eyePos = g.eye.position; forward = g.eye.forward; }
        else
        {
            var baseT = g.agent != null ? g.agent.transform : transform;
            eyePos = baseT.position + Vector3.up * g.eyeHeight;
            forward = baseT.forward;
        }

        // ★ Head に向けて判定（playerHeight は使わない）
        Vector3 target = playerHead.position;
        Vector3 toPlayer = target - eyePos;
        float dist = toPlayer.magnitude;
        if (dist > g.viewDistance) return false;

        float ang = Vector3.Angle(forward, toPlayer.normalized);
        if (ang > g.viewAngle * 0.5f) return false;

        int mask = g.playerLayer | g.obstructionLayers;
        if (Physics.Raycast(eyePos, toPlayer.normalized, out var hit, dist, mask, QueryTriggerInteraction.Ignore))
        {
            // Debug.Log("hit.collider: " + hit.collider.gameObject);
            return (g.playerLayer.value & (1 << hit.collider.gameObject.layer)) != 0;
        }
        return false;
    }

    private float PlanarDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f; b.y = 0f;
        return Vector3.Distance(a, b);
    }

    // ======= Gizmos（各ガードの目から描画） =======
    [FoldoutGroup("Gizmos", expanded: true), PropertySpace]
    public bool gizmoAlways = true;

    [FoldoutGroup("Gizmos")] public bool gizmoDrawFOV = true;
    [FoldoutGroup("Gizmos")] public bool gizmoDrawPatrol = true;
    [FoldoutGroup("Gizmos")] public bool gizmoDrawNavPath = true;
    [FoldoutGroup("Gizmos")] public bool gizmoShowAllRandomLinks = false;

    [FoldoutGroup("Gizmos")] public Color gizmoFOVFillColor = new Color(1f, 1f, 0f, 0.15f);
    [FoldoutGroup("Gizmos")] public Color gizmoFOVEdgeColor = new Color(1f, 1f, 0f, 0.65f);
    [FoldoutGroup("Gizmos")] public Color gizmoSightRayVisible = Color.red;
    [FoldoutGroup("Gizmos")] public Color gizmoSightRayBlocked = Color.gray;
    [FoldoutGroup("Gizmos")] public Color gizmoPatrolPointColor = Color.cyan;
    [FoldoutGroup("Gizmos")] public Color gizmoPatrolLinkColor = new Color(0f, 1f, 1f, 0.8f);
    [FoldoutGroup("Gizmos")] public Color gizmoRandomLinkColor = new Color(0f, 1f, 1f, 0.15f);
    [FoldoutGroup("Gizmos")] public Color gizmoNextLinkColor = new Color(0f, 1f, 0.5f, 1f);
    [FoldoutGroup("Gizmos")] public Color gizmoNavPathColor = new Color(0.9f, 0.5f, 1f, 0.9f);
    [FoldoutGroup("Gizmos")] public float gizmoPointRadius = 0.23f;
    [FoldoutGroup("Gizmos")] public float gizmoArrowSize = 0.35f;
    [FoldoutGroup("Gizmos")] public bool gizmoLabelPoints = true;

#if UNITY_EDITOR
    private void OnDrawGizmos() { if (gizmoAlways) DrawGizmosInternal(false); }
    private void OnDrawGizmosSelected() { DrawGizmosInternal(true); }

    private void DrawGizmosInternal(bool selected)
    {
        if (guards == null) return;

        foreach (var g in guards)
        {
            if (g == null) continue;

            // 目の位置＆向き
            Vector3 eyePos;
            Vector3 forward;
            if (g.eye != null) { eyePos = g.eye.position; forward = g.eye.forward; }
            else
            {
                var baseT = g.agent ? g.agent.transform : transform;
                eyePos = baseT.position + Vector3.up * g.eyeHeight;
                forward = baseT.forward;
            }

            // 視野
            if (gizmoDrawFOV)
            {
                Handles.color = gizmoFOVFillColor;
                Handles.DrawSolidArc(
                    eyePos, Vector3.up,
                    Quaternion.Euler(0, -g.viewAngle * 0.5f, 0) * forward,
                    g.viewAngle, g.viewDistance);

                Handles.color = gizmoFOVEdgeColor;
                Handles.DrawWireArc(eyePos, Vector3.up,
                    Quaternion.Euler(0, -g.viewAngle * 0.5f, 0) * forward,
                    g.viewAngle, g.viewDistance);

                // 距離リング
                Handles.DrawWireDisc(eyePos, Vector3.up, g.viewDistance * 0.33f);
                Handles.DrawWireDisc(eyePos, Vector3.up, g.viewDistance * 0.66f);
                Handles.DrawWireDisc(eyePos, Vector3.up, g.viewDistance);

                // ★視線（Head を狙う）
                if (playerHead != null)
                {
                    var target = playerHead.position;
                    bool visible = IsPlayerVisible(g);
                    Handles.color = visible ? gizmoSightRayVisible : gizmoSightRayBlocked;
                    Handles.DrawAAPolyLine(3f, eyePos, target);
                }
            }

            // パトロール点・リンク
            if (gizmoDrawPatrol && g.patrolPoints != null && g.patrolPoints.Count > 0)
            {
                Gizmos.color = gizmoPatrolPointColor;
                for (int i = 0; i < g.patrolPoints.Count; i++)
                {
                    var p = g.patrolPoints[i]; if (!p) continue;
                    Gizmos.DrawWireSphere(p.position, gizmoPointRadius);

                    if (gizmoLabelPoints)
                    {
                        var labelPos = p.position + Vector3.up * 0.2f;
                        var style = new GUIStyle(EditorStyles.boldLabel);
                        style.normal.textColor = Color.cyan;
                        Handles.Label(labelPos, $"{g.name} #{i}", style);
                    }
                }

                // 順番巡回のリンク（視覚化用ループ）
                if (!g.randomPatrol && g.patrolPoints.Count > 1)
                {
                    Handles.color = gizmoPatrolLinkColor;
                    for (int i = 0; i < g.patrolPoints.Count; i++)
                    {
                        var a = g.patrolPoints[i];
                        var b = g.patrolPoints[(i + 1) % g.patrolPoints.Count];
                        if (!a || !b) continue;
                        Handles.DrawAAPolyLine(3f, a.position, b.position);
                        DrawArrow(a.position, b.position, gizmoArrowSize, gizmoPatrolLinkColor);
                    }
                }

                // ランダム巡回の全リンク（薄線）
                if (g.randomPatrol && gizmoShowAllRandomLinks && g.patrolPoints.Count > 1)
                {
                    Handles.color = gizmoRandomLinkColor;
                    for (int i = 0; i < g.patrolPoints.Count; i++)
                    {
                        var a = g.patrolPoints[i]; if (!a) continue;
                        for (int j = i + 1; j < g.patrolPoints.Count; j++)
                        {
                            var b = g.patrolPoints[j]; if (!b) continue;
                            Handles.DrawLine(a.position, b.position);
                        }
                    }
                }

                // 次に向かう想定リンクを強調
                if (g.patrolPoints.Count > 1)
                {
                    int next = PreviewNextIndex(g);
                    var curT = g.patrolPoints[Mathf.Clamp(g.currentIndex, 0, g.patrolPoints.Count - 1)];
                    var nextT = g.patrolPoints[Mathf.Clamp(next, 0, g.patrolPoints.Count - 1)];
                    if (curT && nextT)
                    {
                        Handles.color = gizmoNextLinkColor;
                        Handles.DrawAAPolyLine(5f, curT.position, nextT.position);
                        DrawArrow(curT.position, nextT.position, gizmoArrowSize * 1.2f, gizmoNextLinkColor);
                    }
                }
            }

            // NavMesh 経路
            if (gizmoDrawNavPath && g.agent && g.agent.isOnNavMesh && g.agent.hasPath)
            {
                var corners = g.agent.path.corners;
                if (corners != null && corners.Length > 1)
                {
                    Handles.color = gizmoNavPathColor;
                    Handles.DrawAAPolyLine(4f, corners);
                    for (int i = 0; i < corners.Length - 1; i++)
                        DrawArrow(corners[i], corners[i + 1], gizmoArrowSize * 0.8f, gizmoNavPathColor);
                }
            }
        }
    }

    private void DrawArrow(Vector3 from, Vector3 to, float size, Color color)
    {
        var dir = to - from; if (dir.sqrMagnitude < 1e-4f) return;
        var pos = Vector3.Lerp(from, to, 0.85f);
        var rot = Quaternion.LookRotation(dir.normalized, Vector3.up);
        Handles.color = color;
        Handles.ConeHandleCap(0, pos, rot, size, EventType.Repaint);
    }
#endif

    // Gizmos内プレビュー用（Editor外でも安全）
    private int PreviewNextIndex(GuardEntry g)
    {
        if (g.patrolPoints == null || g.patrolPoints.Count <= 1) return 0;

        if (g.randomPatrol)
        {
            int idx = g.currentIndex + 1;
            if (idx >= g.patrolPoints.Count) idx = 0;
            return idx;
        }

        if (g.pingPong)
        {
            int dir = (g.direction == 0) ? 1 : g.direction;
            int next = g.currentIndex + dir;
            if (next >= g.patrolPoints.Count) next = g.currentIndex - 1;
            else if (next < 0) next = g.currentIndex + 1;
            return Mathf.Clamp(next, 0, g.patrolPoints.Count - 1);
        }

        int loopNext = g.currentIndex + 1;
        if (loopNext >= g.patrolPoints.Count) loopNext = 0;
        return loopNext;
    }
}

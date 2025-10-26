// StealthGameController.cs (修正後 全文)

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Sirenix.OdinInspector;
using TMPro;

#if UNITY_EDITOR
using UnityEditor;
#endif

[AddComponentMenu("Game/Controllers/StealthGameController (Manager)")]
public class StealthGameController : SerializedMonoBehaviour
{
    // ======= References =======
    [TitleGroup("References"), PropertyOrder(-100)]
    [Required] public MyHouseEscapeGameController myHouseEscapeGameController;

    [TitleGroup("References")]
    [Tooltip("プレイヤー Transform。未設定なら Tag=Player を探索")]
    public Transform player;
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

    // ======= Timer (Police到着まで) =======
    [TitleGroup("Timer"), PropertySpace]
    [LabelText("Enable Timer")]
    [Tooltip("残り時間のカウントダウンを有効化")]
    public bool enableTimer = true;

    [TitleGroup("Timer")]
    public TextMeshProUGUI timerText;
    [Tooltip("タイマーの枠（開始でON／終了でOFFにする）")]
    public GameObject timerBox;

    [TitleGroup("Timer")]
    [LabelText("Hide When Idle")]
    [Tooltip("非ステルス時は Text を空にする")]
    public bool hideTimerWhenIdle = true;

    private Coroutine timerCo;

    [Tooltip("現在のタイマー値")]
    [SerializeField] private float timerRemain;
    
    // ======= Random Dialogue =======
    [TitleGroup("Random Dialogue"), PropertySpace]
    [LabelText("Enable Random Dialogue")]
    [Tooltip("ステルスゲーム中にランダムなセリフを表示し続けるか")]
    public bool enableRandomDialogue = true;

    [TitleGroup("Random Dialogue"), ShowIf("enableRandomDialogue")]
    [Required, Tooltip("セリフが含まれるDialogue Sequence Group")]
    public DialogueSequenceGroupAsset randomDialogueGroup;

    [TitleGroup("Random Dialogue"), ShowIf("enableRandomDialogue")]
    [Tooltip("使用するSequenceのID")]
    public string randomDialogueSequenceId = "StealthTalk";

    [TitleGroup("Random Dialogue"), ShowIf("enableRandomDialogue")]
    [MinMaxSlider(0.1f, 30f, true), Tooltip("次のセリフを表示するまでのランダムな待機時間（秒）")]
    public Vector2 randomDialogueInterval = new Vector2(5f, 10f);

    [TitleGroup("Random Dialogue"), ShowIf("enableRandomDialogue")]
    [Range(1f, 10f), Tooltip("一回のセリフの表示時間（秒）")]
    public float dialogueDisplayDuration = 3.5f;

    private Coroutine _randomDialogueCo;
    private DialogueSequenceAsset _targetSequence;
    [SerializeField]
    private AudioSource StealthBGM;
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
        
        [BoxGroup("H/Agent", false)]
        [LabelText("CPU Status(任意)")] public CPUStatus cpuStatus;

        [FoldoutGroup("Patrol", true)]
        [ListDrawerSettings(Expanded = true, DraggableItems = true)]
        [LabelText("Patrol Points")] public List<Transform> patrolPoints = new();

        [FoldoutGroup("Patrol")][LabelText("Random Patrol")] public bool randomPatrol = false;
        [FoldoutGroup("Patrol")][LabelText("Ping-Pong (非ランダム時)")] public bool pingPong = true;
        [FoldoutGroup("Patrol")][LabelText("Start From Closest")] public bool startFromClosestPoint = true;
        [FoldoutGroup("Patrol")][LabelText("Arrive Threshold")] public float arriveThreshold = 0.2f;
        [FoldoutGroup("Patrol")][LabelText("Use Agent.autoBraking")] public bool useAutoBraking = true;
        [FoldoutGroup("Patrol")][LabelText("CloseEnough Dist (fallback)")] public float closeEnoughDistance = 0.5f;
        [FoldoutGroup("Patrol")][LabelText("Arrival Timeout (s)")] public float arrivalTimeoutSeconds = 6f;
        [FoldoutGroup("Patrol")][LabelText("Planar Distance (XZ) で到着判定も見る")] public bool usePlanarDistance = true;
        [FoldoutGroup("Patrol")][LabelText("Wait (s) 一定時間"), MinValue(0f)] public float waitAtPointSeconds = 1.0f;

        [FoldoutGroup("Vision", true)]
        [Range(10f, 180f), LabelText("View Angle")] public float viewAngle = 70f;
        [FoldoutGroup("Vision")][LabelText("View Distance")] public float viewDistance = 12f;
        [FoldoutGroup("Vision")][LabelText("Eye Height (Y)")] public float eyeHeight = 1.6f;
        [FoldoutGroup("Vision")][LabelText("Player Height (Y)")] public float playerHeight = 1.6f;
        [FoldoutGroup("Vision")][LabelText("Player Layer")] public LayerMask playerLayer = 1 << 6;
        [FoldoutGroup("Vision")][LabelText("Obstruction Layers")] public LayerMask obstructionLayers = ~0;

        // Runtime
        [HideInInspector] public bool active;
        [HideInInspector] public int currentIndex;
        [HideInInspector] public Coroutine patrolCo;
        [HideInInspector] public int direction = 1;
    }

    [TitleGroup("Guards"), PropertySpace]
    [ListDrawerSettings(Expanded = true, DraggableItems = true, ShowIndexLabels = true)]
    public List<GuardEntry> guards = new();

    // ======= Odin Buttons =======
    [TitleGroup("Controls"), ButtonGroup("Controls/Row1"), GUIColor(0.2f, 0.75f, 0.2f)]
    [Button("Start Stealth", ButtonSizes.Large)]
    public void StealthGameStart()
    {
        if (player == null)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p) player = p.transform;
        }
        myHouseEscapeGameController.OnStealthGameStart();
        if (enableTimer) StartTimer();
        StealthBGM.Play();
        StartRandomDialogue();
        
        foreach (var g in guards) StartGuard(g);
    }

    [ButtonGroup("Controls/Row1"), GUIColor(0.9f, 0.35f, 0.35f)]
    [Button("Stop All", ButtonSizes.Large)]
    public void StopAllGuards()
    {
        foreach (var g in guards) StopGuard(g);
        StopTimer();
        StealthBGM.Stop();
        StopRandomDialogue();
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
        playerStatus = PlayerStatus.instance;
        if (playerStatus != null)
        {
            player = playerStatus.transform;
            playerHead = playerStatus.playerHead;
        }
        else
        {
            if (player == null)
            {
                var p = GameObject.FindGameObjectWithTag("Player");
                if (p) player = p.transform;
            }
        }
    }

    private void OnDisable()
    {
        StopAllGuards();
    }

    private void Update()
    {
        if (autoStopWhenStateChanged && myHouseEscapeGameController != null)
        {
            var s = myHouseEscapeGameController.CurrentState;
            if (s == MyHouseEscapeGameController.State.Success ||
                s == MyHouseEscapeGameController.State.Failed)
            {
                StopAllGuards();
                if (hideTimerWhenIdle) UpdateTimerText(null);
                return;
            }
        }

        if (player == null) return;

        foreach (var g in guards)
        {
            if (!g.active || g.agent == null) continue;
            if (IsPlayerVisible(g))
            {
                if (g.cpuStatus != null && g.cpuStatus.multiAimConstraint != null)
                {
                    g.cpuStatus.multiAimConstraint.weight = 1;
                }
                myHouseEscapeGameController?.NotifyPlayerDetected();
                if (stopAllOnDetect) { StopAllGuards(); }
                else { StopGuard(g); }
                break;
            }
        }
    }

    // ======= Timer =======
    private void StartTimer()
    {
        if (timerCo != null) StopCoroutine(timerCo);

        timerRemain = Mathf.Max(0f, myHouseEscapeGameController.policeArrivalSeconds);
        if (timerBox != null) timerBox.SetActive(true);

        if (myHouseEscapeGameController != null)
        {
            float p = (myHouseEscapeGameController.policeArrivalSeconds <= 0f) ? 1f : Mathf.Clamp01(1f - (timerRemain / myHouseEscapeGameController.policeArrivalSeconds));
            myHouseEscapeGameController.onStealthTick?.Invoke(p);
        }

        UpdateTimerText(timerRemain);
        timerCo = StartCoroutine(TimerLoop());
    }

    public void StopTimer()
    {
        if (timerBox != null) timerBox.SetActive(false);
        if (timerCo != null) { StopCoroutine(timerCo); timerCo = null; }
    }

    private IEnumerator TimerLoop()
    {
        if (timerRemain <= 0f)
        {
            TriggerPoliceArrive();
            yield break;
        }

        while (timerRemain > 0f)
        {
            timerRemain -= Time.deltaTime;
            if (timerRemain < 0f) timerRemain = 0f;

            UpdateTimerText(timerRemain);

            if (myHouseEscapeGameController != null && myHouseEscapeGameController.policeArrivalSeconds > 0f)
            {
                float p = Mathf.Clamp01(1f - (timerRemain / myHouseEscapeGameController.policeArrivalSeconds));
                myHouseEscapeGameController.onStealthTick?.Invoke(p);
            }

            yield return null;
        }

        TriggerPoliceArrive();
    }

    private void TriggerPoliceArrive()
    {
        UpdateTimerText(0f);
        if (timerBox != null) timerBox.SetActive(false);
        myHouseEscapeGameController?.NotifyPoliceArrived();
    }

    private void UpdateTimerText(float? remainNullable)
    {
        if (timerText == null) return;

        if (!remainNullable.HasValue)
        {
            timerText.SetText("");
            return;
        }

        int t = Mathf.Max(0, Mathf.FloorToInt(remainNullable.Value));
        int m = t / 60;
        int s = t % 60;
        timerText.SetText("{0}:{1:00}", m, s);
    }

    // ======= Random Dialogue Control =======
    private void StartRandomDialogue()
    {
        Debug.Log("oi 1");
        if (!enableRandomDialogue)
        {
            return;
        }
        Debug.Log("oi 2");

        // 指定されたIDのSequenceを探す
        _targetSequence = randomDialogueGroup.sequences.Find(seq => seq.id == randomDialogueSequenceId);

        if (_targetSequence == null || _targetSequence.steps.Count == 0)
        {
            Debug.LogWarning($"[StealthGameController] Sequence '{randomDialogueSequenceId}' not found or is empty.");
            return;
        }

        if (_randomDialogueCo != null)
        {
            Debug.Log("_randomDialogueCo-----------1--------------------");
            StopCoroutine(_randomDialogueCo);
        }
        Debug.Log("oi 3");

        _randomDialogueCo = StartCoroutine(RandomDialogueLoop());
    }

    private void StopRandomDialogue()
    {
        if (_randomDialogueCo != null)
        {
            StopCoroutine(_randomDialogueCo);
            _randomDialogueCo = null;
        }
        
        // 念のため、全ガードのUIを非表示にする
        if (DialogueController.instance != null)
        {
            foreach (var guard in guards)
            {
                if (guard.cpuStatus != null)
                {
                    DialogueController.instance.ShowBriefMessage(guard.cpuStatus.dialogueUIRefs, "", "", 0);
                }
            }
        }
    }

    private IEnumerator RandomDialogueLoop()
    {
        // 最初のセリフ表示まで少し待つ
        yield return new WaitForSeconds(randomDialogueInterval.x);

        while (true)
        {
            // --- 話者となるガードを決定 ---
            // 1. 現在アクティブで、かつ有効なUIを持つガードのリストを作成
            var validGuards = guards.FindAll(g => g.active && g.cpuStatus != null);

            // 2. 有効なガードが一人もいなければ、次のループまで待機
            if (validGuards.Count == 0)
            {
                yield return new WaitForSeconds(1f); // 1秒待って再試行
                continue;
            }

            // 3. リストからランダムに一人を選ぶ
            var randomGuard = validGuards[Random.Range(0, validGuards.Count)];
            
            // --- 表示するセリフを決定 ---
            int randomIndex = Random.Range(0, _targetSequence.steps.Count);
            DialogueStep randomStep = _targetSequence.steps[randomIndex];

            if (randomStep != null && DialogueController.instance != null)
            {
                // 選んだガードのUI（dialogueUIRefs）とセリフ情報を渡して表示を依頼
                DialogueController.instance.ShowBriefMessage(
                    randomGuard.cpuStatus.dialogueUIRefs, 
                    randomStep.speaker, 
                    randomStep.text, 
                    dialogueDisplayDuration
                );
            }
            
            // 次の表示までランダムに待機
            float waitTime = Random.Range(randomDialogueInterval.x, randomDialogueInterval.y);
            yield return new WaitForSeconds(waitTime);
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
                
                float sinceSet = 0f;
                while (g.active)
                {
                    if (!g.agent.pathPending)
                    {
                        float stopThresh = Mathf.Max(g.agent.stoppingDistance, g.arriveThreshold);

                        if (g.agent.remainingDistance <= stopThresh &&
                            (!g.agent.hasPath || g.agent.velocity.sqrMagnitude < 0.01f))
                            break;

                        if (g.usePlanarDistance)
                        {
                            float planar = PlanarDistance(g.agent.transform.position, targetPos);
                            if (planar <= Mathf.Max(g.closeEnoughDistance, 0.001f))
                                break;
                        }

                        if (g.agent.pathStatus == NavMeshPathStatus.PathInvalid ||
                            g.agent.pathStatus == NavMeshPathStatus.PathPartial)
                        {
                            Debug.LogWarning($"[Stealth] {g.name}: pathStatus={g.agent.pathStatus} → 次へスキップ");
                            break;
                        }
                    }

                    sinceSet += Time.deltaTime;
                    if (g.arrivalTimeoutSeconds > 0f && sinceSet >= g.arrivalTimeoutSeconds)
                    {
                        Debug.LogWarning($"[Stealth] {g.name}: ArrivalTimeout({g.arrivalTimeoutSeconds}s) 到達 → 次へ進む");
                        break;
                    }

                    yield return null;
                }
                
                while (g.active && g.agent.velocity.sqrMagnitude > 0.01f)
                {
                    yield return null;
                }

                float waitSec = Mathf.Max(0f, g.waitAtPointSeconds * Mathf.Max(0f, waitTimeScale));
                if (waitSec > 0f)
                {
                    float t = 0f;
                    while (g.active && t < waitSec) { t += Time.deltaTime; yield return null; }
                }
                
                g.currentIndex = GetNextIndex(g);
            }
            else
            {

                yield return new WaitForSeconds(0.25f);
            }
        }
    }
    private int GetNextIndex(GuardEntry g)
    {
        if (g.patrolPoints == null || g.patrolPoints.Count <= 1) return 0;

        if (g.randomPatrol)
        {
            int next;
            do { next = Random.Range(0, g.patrolPoints.Count); } while (next == g.currentIndex);
            g.direction = 1;
            return next;
        }

        if (g.pingPong)
        {
            int next = g.currentIndex + g.direction;

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

    private bool IsPlayerVisible(GuardEntry g)
    {
        if (player == null) return false;

        Vector3 eyePos;
        Vector3 forward;
        if (g.eye != null) { eyePos = g.eye.position; forward = g.eye.forward; }
        else
        {
            var baseT = g.agent != null ? g.agent.transform : transform;
            eyePos = baseT.position + Vector3.up * g.eyeHeight;
            forward = baseT.forward;
        }

        Vector3 target = player.position + Vector3.up * g.playerHeight;
        Vector3 toPlayer = target - eyePos;
        float dist = toPlayer.magnitude;
        if (dist > g.viewDistance) return false;

        float ang = Vector3.Angle(forward, toPlayer.normalized);
        if (ang > g.viewAngle * 0.5f) return false;

        int mask = g.playerLayer | g.obstructionLayers;
        if (Physics.Raycast(eyePos, toPlayer.normalized, out var hit, dist, mask, QueryTriggerInteraction.Ignore))
        {
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
    [FoldoutGroup("Gizmos", expanded: true), PropertySpace] public bool gizmoAlways = true;
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

            Vector3 eyePos;
            Vector3 forward;
            if (g.eye != null) { eyePos = g.eye.position; forward = g.eye.forward; }
            else
            {
                var baseT = g.agent ? g.agent.transform : transform;
                eyePos = baseT.position + Vector3.up * g.eyeHeight;
                forward = baseT.forward;
            }

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

                Handles.DrawWireDisc(eyePos, Vector3.up, g.viewDistance * 0.33f);
                Handles.DrawWireDisc(eyePos, Vector3.up, g.viewDistance * 0.66f);
                Handles.DrawWireDisc(eyePos, Vector3.up, g.viewDistance);

                if (player != null)
                {
                    var target = player.position + Vector3.up * g.playerHeight;
                    bool visible = IsPlayerVisible(g);
                    Handles.color = visible ? gizmoSightRayVisible : gizmoSightRayBlocked;
                    Handles.DrawAAPolyLine(3f, eyePos, target);
                }
            }

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
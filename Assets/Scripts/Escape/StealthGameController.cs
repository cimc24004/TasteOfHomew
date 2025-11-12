// (using は元のままでOK)
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Sirenix.OdinInspector;
using TMPro;
using System; 

#if UNITY_EDITOR
using UnityEditor;
#endif

[AddComponentMenu("Game/Controllers/StealthGameController (Manager)")]
public class StealthGameController : SerializedMonoBehaviour
{
    // ... (Fields from OnWorldSoundMade to StealthBGM are unchanged) ...
    public static event Action<Vector3, float, GameObject> OnWorldSoundMade;
    public static void MakeSoundAt(Vector3 position, float radius, GameObject soundSource = null)
    {
        OnWorldSoundMade?.Invoke(position, radius, soundSource);
    }
    
    [TitleGroup("References"), PropertyOrder(-100)]
    [Required] public MyHouseEscapeGameController myHouseEscapeGameController;
    [TitleGroup("References")]
    public Transform player;
    private PlayerStatus playerStatus;
    private Transform playerHead;
    [TitleGroup("References")]
    [ReadOnly] public FootStepController playerFootStepController;
    [TitleGroup("Behaviour"), PropertySpace]
    public bool stopAllOnDetect = true;
    [TitleGroup("Behaviour")]
    public bool autoStopWhenStateChanged = true;
    [TitleGroup("Behaviour")]
    [LabelText("Wait Time Scale (全体倍率)")]
    [MinValue(0f)]
    public float waitTimeScale = 1f;
    
    [TitleGroup("Hearing (Investigate)"), PropertySpace]
    public bool enableFastWalk = true;
    [TitleGroup("Hearing (Investigate)"), ShowIf("enableFastWalk")]
    [MinValue(1f)] public float fastWalkAgentSpeedMultiplier = 1.5f;
    [TitleGroup("Hearing (Investigate)"), ShowIf("enableFastWalk")]
    [MinValue(1f)] public float fastWalkAnimatorSpeedMultiplier = 1.5f;
    [TitleGroup("Hearing (Investigate)"), ShowIf("enableFastWalk")]
    [MinValue(1f)] public float fastWalkFootstepMultiplier = 2f;
    [TitleGroup("Timer"), PropertySpace]
    public bool enableTimer = true;
    [TitleGroup("Timer")]
    public TextMeshProUGUI timerText;
    [TitleGroup("Timer")]
    public GameObject timerBox;
    [TitleGroup("Timer")]
    public bool hideTimerWhenIdle = true;
    private Coroutine timerCo;
    [SerializeField] private float timerRemain;
    
    [TitleGroup("Random Dialogue"), PropertySpace]
    public bool enableRandomDialogue = true;
    [TitleGroup("Random Dialogue"), ShowIf("enableRandomDialogue")]
    [Required] public DialogueSequenceGroupAsset randomDialogueGroup;
    [TitleGroup("Random Dialogue"), ShowIf("enableRandomDialogue")]
    public string randomDialogueSequenceId = "StealthTalk";
    [TitleGroup("Random Dialogue"), ShowIf("enableRandomDialogue")]
    [MinMaxSlider(0.1f, 30f, true)] public Vector2 randomDialogueInterval = new Vector2(5f, 10f);
    [TitleGroup("Random Dialogue"), ShowIf("enableRandomDialogue")]
    [Range(1f, 10f)] public float dialogueDisplayDuration = 3.5f;
    private Coroutine _randomDialogueCo;
    private DialogueSequenceAsset _targetSequence;
    [SerializeField]
    private AudioSource StealthBGM;
    
    // ... (GuardEntry class is unchanged) ...
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
        [BoxGroup("H/Agent", false)]
        [LabelText("Animator(任意)")]
        public Animator animator;
        [BoxGroup("H/Agent", false)]
        [LabelText("FootStepController(任意)")]
        public FootStepController footStepController;
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
        [FoldoutGroup("Hearing", true)]
        [LabelText("Can Hear")] public bool canHear = true;
        [FoldoutGroup("Hearing"), ShowIf("canHear")]
        [LabelText("Hearing Distance")] public float hearingDistance = 8f;
        [FoldoutGroup("Hearing"), ShowIf("canHear")]
        [LabelText("Investigate Wait (s)")] public float investigateWaitTime = 3f;
        [FoldoutGroup("Investigate Dialogue", true)]
        public bool playDialogueOnInvestigate = true;
        [FoldoutGroup("Investigate Dialogue"), ShowIf("playDialogueOnInvestigate")]
        [Required] public DialogueSequenceGroupAsset investigateDialogueGroup;
        [FoldoutGroup("Investigate Dialogue"), ShowIf("playDialogueOnInvestigate")]
        public string investigateDialogueSequenceId = "InvestigateTalk";
        [FoldoutGroup("Investigate Dialogue"), ShowIf("playDialogueOnInvestigate")]
        [Range(1f, 10f)] public float investigateDialogueDuration = 3.5f;
        [HideInInspector] public bool active;
        [HideInInspector] public int currentIndex;
        [HideInInspector] public Coroutine patrolCo;
        [HideInInspector] public int direction = 1;
        [HideInInspector] public bool isInvestigating = false;
        [HideInInspector] public DialogueSequenceAsset cachedInvestigateSequence;
        [HideInInspector] public float defaultAgentSpeed;
        [HideInInspector] public float defaultAnimatorSpeed;
        [HideInInspector] public float defaultFootstepInterval;
        [HideInInspector] public bool isHastening;
    }

    [TitleGroup("Guards"), PropertySpace]
    [ListDrawerSettings(Expanded = true, DraggableItems = true, ShowIndexLabels = true)]
    public List<GuardEntry> guards = new();

    // ... (Odin Buttons are unchanged) ...
    [TitleGroup("Controls"), ButtonGroup("Controls/Row1"), GUIColor(0.2f, 0.75f, 0.2f)]
    [Button("Start Stealth", ButtonSizes.Large)]
    public void StealthGameStart()
    {
        if (playerStatus != null)
        {
            player = playerStatus.transform;
            playerHead = playerStatus.playerHead;
            playerFootStepController = playerStatus.playerFootStepController;
        }
        else
        {
            if (player == null)
            {
                var p = GameObject.FindGameObjectWithTag("Player");
                if (p) player = p.transform;
            }
            if (player != null && playerFootStepController == null)
            {
                playerFootStepController = player.GetComponent<FootStepController>();
            }
        }
        if (playerFootStepController == null && player != null)
        {
             playerFootStepController = player.GetComponent<FootStepController>();
        }

        myHouseEscapeGameController.OnStealthGameStart();
        if (enableTimer) StartTimer();
        StealthBGM.Play();
        StartRandomDialogue();
        
        foreach (var g in guards)
        {
            g.footStepController = g.cpuStatus?.footStepController;
            g.animator = g.cpuStatus.CPUAnimator;
            StartGuard(g);
        }
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

    // ... (Lifecycle: OnEnable, Start, OnDisable are unchanged) ...
    private void OnEnable()
    {
        OnWorldSoundMade += HandleWorldSound;
    }

    private void Start()
    {
        playerStatus = PlayerStatus.instance;
        if (playerStatus != null)
        {
            player = playerStatus.transform;
            playerHead = playerStatus.playerHead;
            playerFootStepController = playerStatus.playerFootStepController;
        }
        else
        {
            if (player == null)
            {
                var p = GameObject.FindGameObjectWithTag("Player");
                if (p) player = p.transform;
            }
            if (player != null)
            {
                playerFootStepController = player.GetComponent<FootStepController>();
            }
        }

        if (player != null && playerFootStepController == null)
        {
            Debug.LogWarning($"[StealthGameController] Player '{player.name}' に FootStepController が見つかりません。聴覚が機能しません。", player);
        }
    }
    
    private void OnDisable()
    {
        OnWorldSoundMade -= HandleWorldSound;
        StopAllGuards();
    }
    
    // ... (HandleWorldSound and Update are unchanged) ...
    private void HandleWorldSound(Vector3 soundPosition, float soundRadius, GameObject soundSource)
    {
        if (player == null || myHouseEscapeGameController.CurrentState != MyHouseEscapeGameController.State.Stealth)
        {
            return;
        }

        foreach (var g in guards)
        {
            if (!g.active || g.agent == null || !g.agent.isOnNavMesh) continue;
            if (!g.canHear) continue; 
            if (soundSource != null && soundSource == g.agent.gameObject)
            {
                continue; 
            }
            if (g.isInvestigating) continue;

            float distSqr = (g.agent.transform.position - soundPosition).sqrMagnitude;
            if (distSqr <= soundRadius * soundRadius &&
                distSqr <= g.hearingDistance * g.hearingDistance)
            {
                Debug.Log($"[Stealth] {g.name} heard a world sound at {soundPosition}!");
                if (g.patrolCo != null) StopCoroutine(g.patrolCo);
                g.patrolCo = StartCoroutine(InvestigateSoundCo(g, soundPosition));
            }
        }
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

            if (g.isInvestigating) continue; 

            if (IsPlayerHeard(g))
            {
                Debug.Log($"[Stealth] {g.name} heard player!");
                if (g.patrolCo != null) StopCoroutine(g.patrolCo); 
                g.patrolCo = StartCoroutine(InvestigateSoundCo(g, player.position)); 
            }
        }
    }

    // ... (Timer methods are unchanged) ...
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
        if (!enableRandomDialogue) return;
        _targetSequence = randomDialogueGroup.sequences.Find(seq => seq.id == randomDialogueSequenceId);
        if (_targetSequence == null || _targetSequence.steps.Count == 0)
        {
            Debug.LogWarning($"[StealthGameController] Sequence '{randomDialogueSequenceId}' not found or is empty.");
            return;
        }
        if (_randomDialogueCo != null) StopCoroutine(_randomDialogueCo);
        _randomDialogueCo = StartCoroutine(RandomDialogueLoop());
    }
    private void StopRandomDialogue()
    {
        if (_randomDialogueCo != null)
        {
            StopCoroutine(_randomDialogueCo);
            _randomDialogueCo = null;
        }
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
        yield return new WaitForSeconds(UnityEngine.Random.Range(randomDialogueInterval.x, randomDialogueInterval.y) * 0.5f);
        while (true)
        {
            var validGuards = guards.FindAll(g => g.active && !g.isInvestigating && g.cpuStatus != null);
            if (validGuards.Count == 0)
            {
                yield return new WaitForSeconds(1f);
                continue;
            }
            var randomGuard = validGuards[UnityEngine.Random.Range(0, validGuards.Count)];
            int randomIndex = UnityEngine.Random.Range(0, _targetSequence.steps.Count);
            DialogueStep randomStep = _targetSequence.steps[randomIndex];
            if (randomStep != null && DialogueController.instance != null)
            {
                // ▼▼▼ 【(1) 修正】 ShowBriefMessage → ShowBriefLocalizedStep ▼▼▼
                DialogueController.instance.ShowBriefLocalizedStep(
                    randomGuard.cpuStatus.dialogueUIRefs, 
                    randomStep, // 生の DialogueStep を渡す
                    dialogueDisplayDuration
                );
                // ▲▲▲ 【(1) 修正ここまで】 ▲▲▲
            }
            float waitTime = UnityEngine.Random.Range(randomDialogueInterval.x, randomDialogueInterval.y);
            yield return new WaitForSeconds(waitTime);
        }
    }
    
    // ======= Guard 制御 =======

    private void SetHasten(GuardEntry g, bool hasten)
    {
        if (!enableFastWalk) return;
        if (g == null || g.agent == null) return;
        if (g.isHastening == hasten) return;
        g.isHastening = hasten;

        if (hasten)
        {
            g.agent.speed = g.defaultAgentSpeed * fastWalkAgentSpeedMultiplier;
            if (g.animator != null)
            {
                g.animator.speed = g.defaultAnimatorSpeed * fastWalkAnimatorSpeedMultiplier;
            }
            if (g.footStepController != null)
            {
                g.footStepController.walkStepInterval = g.defaultFootstepInterval / fastWalkFootstepMultiplier;
            }
        }
        else
        {
            g.agent.speed = g.defaultAgentSpeed;
            if (g.animator != null)
            {
                g.animator.speed = g.defaultAnimatorSpeed;
            }
            if (g.footStepController != null)
            {
                g.footStepController.walkStepInterval = g.defaultFootstepInterval;
            }
        }
    }


    private void StartGuard(GuardEntry g)
    {
        if (g == null || g.agent == null) return;
        g.agent.enabled = true;
        g.active = true;
        g.direction = 1;
        g.currentIndex = 0;
        g.isInvestigating = false;
        g.agent.autoBraking = g.useAutoBraking;
        if (g.arriveThreshold > 0f)
            g.agent.stoppingDistance = Mathf.Max(g.agent.stoppingDistance, g.arriveThreshold);

        if (g.startFromClosestPoint && g.patrolPoints != null && g.patrolPoints.Count > 1)
            g.currentIndex = FindClosestPointIndex(g);

        g.defaultAgentSpeed = g.agent.speed;
        g.isHastening = false; 

        if (g.animator != null)
        {
            g.defaultAnimatorSpeed = g.animator.speed; 
        }
        else if (enableFastWalk)
        {
            Debug.LogWarning($"[Stealth] Guard '{g.name}' に Animator が設定されていません。早歩きアニメーションが機能しません。", g.agent);
        }

        if (g.footStepController != null)
        {
            g.defaultFootstepInterval = g.footStepController.walkStepInterval;
        }
        else if (enableFastWalk)
        {
            Debug.LogWarning($"[Stealth] Guard '{g.name}' に FootStepController が設定されていません。早歩き足音が機能しません。", g.agent);
        }

        if (g.playDialogueOnInvestigate && g.investigateDialogueGroup != null)
        {
            g.cachedInvestigateSequence = g.investigateDialogueGroup.sequences.Find(seq => seq.id == g.investigateDialogueSequenceId);
            if (g.cachedInvestigateSequence == null || g.cachedInvestigateSequence.steps.Count == 0)
            {
                Debug.LogWarning($"[Stealth] {g.name}: Investigate sequence '{g.investigateDialogueSequenceId}' not found or is empty.");
                g.cachedInvestigateSequence = null; 
            }
        }
        else
        {
            g.cachedInvestigateSequence = null;
        }

        if (g.patrolCo != null) StopCoroutine(g.patrolCo);
        g.patrolCo = StartCoroutine(PatrolLoop(g));
    }

    private void StopGuard(GuardEntry g)
    {
        SetHasten(g, false);
        g.isInvestigating = false;
        g.active = false;
        
        if (DialogueController.instance != null && g.cpuStatus != null)
        {
            DialogueController.instance.ShowBriefMessage(g.cpuStatus.dialogueUIRefs, "", "", 0);
        }

        if (g.patrolCo != null) { StopCoroutine(g.patrolCo); g.patrolCo = null; }
        if (g.agent != null && g.agent.isOnNavMesh) g.agent.ResetPath();
    }

    private IEnumerator PatrolLoop(GuardEntry g)
    {
        SetHasten(g, false);

        while (g.active)
        {
            g.isInvestigating = false;
            
            Transform target = null;
            if (g.patrolPoints != null && g.patrolPoints.Count > 0)
                target = g.patrolPoints[Mathf.Clamp(g.currentIndex, 0, g.patrolPoints.Count - 1)];

            if (target != null && g.agent.isOnNavMesh)
            {
                Vector3 targetPos = target.position;
                g.agent.SetDestination(targetPos);
                
                float sinceSet = 0f;
                while (g.active && !g.isInvestigating)
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
                    if (g.isInvestigating) yield break;
                    yield return null;
                }
                
                if (g.isInvestigating) yield break; 
                
                while (g.active && g.agent.velocity.sqrMagnitude > 0.01f)
                {
                    yield return null;
                }

                float waitSec = Mathf.Max(0f, g.waitAtPointSeconds * Mathf.Max(0f, waitTimeScale));
                if (waitSec > 0f)
                {
                    float t = 0f;
                    while (g.active && !g.isInvestigating && t < waitSec) { t += Time.deltaTime; yield return null; }
                }
                
                if (g.isInvestigating) yield break; 

                g.currentIndex = GetNextIndex(g);
            }
            else
            {
                yield return new WaitForSeconds(0.25f);
            }
        }
    }

    private IEnumerator InvestigateSoundCo(GuardEntry g, Vector3 soundPosition)
    {
        g.isInvestigating = true;
        
        SetHasten(g, true);

        if (g.playDialogueOnInvestigate && g.cachedInvestigateSequence != null && 
            g.cachedInvestigateSequence.steps.Count > 0 && DialogueController.instance != null && g.cpuStatus != null)
        {
            var steps = g.cachedInvestigateSequence.steps;
            DialogueStep randomStep = steps[UnityEngine.Random.Range(0, steps.Count)];
            
            // ▼▼▼ 【(2) 修正】 ShowBriefMessage → ShowBriefLocalizedStep ▼▼▼
            DialogueController.instance.ShowBriefLocalizedStep(
                g.cpuStatus.dialogueUIRefs,
                randomStep, // 生の DialogueStep を渡す
                g.investigateDialogueDuration
            );
            // ▲▲▲ 【(2) 修正ここまで】 ▲▲▲
        }


        if (g.agent.isOnNavMesh)
        {
            g.agent.SetDestination(soundPosition);

            // --- 到着待ち ---
            float sinceSet = 0f;
            while (g.active && g.isInvestigating)
            {
                if (!g.agent.pathPending)
                {
                    float stopThresh = Mathf.Max(g.agent.stoppingDistance, g.arriveThreshold);
                    if (g.agent.remainingDistance <= stopThresh &&
                        (!g.agent.hasPath || g.agent.velocity.sqrMagnitude < 0.01f))
                        break; 
                    if (g.usePlanarDistance)
                    {
                        float planar = PlanarDistance(g.agent.transform.position, soundPosition);
                        if (planar <= Mathf.Max(g.closeEnoughDistance, 0.001f))
                            break; 
                    }
                    if (g.agent.pathStatus == NavMeshPathStatus.PathInvalid ||
                        g.agent.pathStatus == NavMeshPathStatus.PathPartial)
                    {
                        Debug.LogWarning($"[Stealth] {g.name} (Investigate): pathStatus={g.agent.pathStatus} → 調査中断");
                        break; 
                    }
                }
                sinceSet += Time.deltaTime;
                if (g.arrivalTimeoutSeconds > 0f && sinceSet >= g.arrivalTimeoutSeconds)
                {
                    Debug.LogWarning($"[Stealth] {g.name} (Investigate): ArrivalTimeout → 調査中断");
                    break; 
                }
                if (!g.active) yield break; 
                yield return null;
            }
            // --- 到着待ちここまで ---
            
            while (g.active && g.isInvestigating && g.agent.velocity.sqrMagnitude > 0.01f)
            {
                yield return null;
            }

            float waitSec = Mathf.Max(0f, g.investigateWaitTime * Mathf.Max(0f, waitTimeScale));
            if (waitSec > 0f)
            {
                if (g.animator != null && g.isHastening)
                {
                    g.animator.speed = g.defaultAnimatorSpeed; 
                }

                float t = 0f;
                while (g.active && g.isInvestigating && t < waitSec) 
                { 
                    t += Time.deltaTime; 
                    yield return null; 
                }
            }
        }
        else
        {
            yield return new WaitForSeconds(0.25f);
        }

        if (g.playDialogueOnInvestigate && DialogueController.instance != null && g.cpuStatus != null)
        {
            DialogueController.instance.ShowBriefMessage(g.cpuStatus.dialogueUIRefs, "", "", 0);
        }

        g.isInvestigating = false;
        
        if (g.active)
        {
            if (g.patrolCo != null) StopCoroutine(g.patrolCo);
            g.patrolCo = StartCoroutine(PatrolLoop(g)); 
        }
        else
        {
            SetHasten(g, false);
        }
    }

    // ======= ユーティリティ (変更なし) =======
    private int GetNextIndex(GuardEntry g)
    {
        if (g.patrolPoints == null || g.patrolPoints.Count <= 1) return 0;

        if (g.randomPatrol)
        {
            int next;
            do { next = UnityEngine.Random.Range(0, g.patrolPoints.Count); } while (next == g.currentIndex);
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

    private bool IsPlayerHeard(GuardEntry g)
    {
        if (!g.canHear) return false;
        if (playerFootStepController == null) return false;
        
        var playerState = playerFootStepController.CurrentState;
        bool isNoisy = (playerState == MovementState.Walking || playerState == MovementState.Running);
        if (!isNoisy) return false; 

        Vector3 guardPos = g.agent.transform.position;
        Vector3 playerPos = player.position;
        
        float distSqr = (guardPos - playerPos).sqrMagnitude;
        if (distSqr > g.hearingDistance * g.hearingDistance) return false; 
        
        return true; 
    }

    private float PlanarDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f; b.y = 0f;
        return Vector3.Distance(a, b);
    }

    // ======= Gizmos（変更なし） =======
    [FoldoutGroup("Gizmos", expanded: true), PropertySpace] public bool gizmoAlways = true;
    [FoldoutGroup("Gizmos")] public bool gizmoDrawFOV = true;
    [FoldoutGroup("Gizmos")] public bool gizmoDrawHearing = true;
    [FoldoutGroup("Gizmos")] public bool gizmoDrawPatrol = true;
    [FoldoutGroup("Gizmos")] public bool gizmoDrawNavPath = true;
    [FoldoutGroup("Gizmos")] public bool gizmoShowAllRandomLinks = false;
    [FoldoutGroup("Gizmos")] public Color gizmoFOVFillColor = new Color(1f, 1f, 0f, 0.15f);
    [FoldoutGroup("Gizmos")] public Color gizmoFOVEdgeColor = new Color(1f, 1f, 0f, 0.65f);
    [FoldoutGroup("Gizmos")] public Color gizmoHearingColor = new Color(0f, 0.5f, 1f, 0.15f);
    [FoldoutGroup("Gizmos")] public Color gizmoHearingEdgeColor = new Color(0f, 0.5f, 1f, 0.5f);
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
                Handles.DrawSolidArc(eyePos, Vector3.up, Quaternion.Euler(0, -g.viewAngle * 0.5f, 0) * forward, g.viewAngle, g.viewDistance);
                Handles.color = gizmoFOVEdgeColor;
                Handles.DrawWireArc(eyePos, Vector3.up, Quaternion.Euler(0, -g.viewAngle * 0.5f, 0) * forward, g.viewAngle, g.viewDistance);
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
            if (gizmoDrawHearing && g.canHear && g.agent != null)
            {
                var pos = g.agent.transform.position;
                Handles.color = gizmoHearingColor;
                Handles.DrawSolidDisc(pos, Vector3.up, g.hearingDistance);
                Handles.color = gizmoHearingEdgeColor;
                Handles.DrawWireDisc(pos, Vector3.up, g.hearingDistance);
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
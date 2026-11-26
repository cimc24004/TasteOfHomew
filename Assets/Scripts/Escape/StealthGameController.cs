using System;
using System.Collections;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 自宅脱出パートのステルスゲームを管理する。
///
/// ・見張り（Guard）に巡回ポイントを順番に歩かせる
/// ・視界に入ったら「発見」、足音や物音が聞こえたら「音の場所まで調査」させる
/// ・警察到着までのタイマーと、見張りがランダムに喋るセリフを制御する
///
/// ゲーム全体の成功/失敗の判定は MyHouseEscapeGameController が持ち、ここからは通知だけを行う。
/// </summary>
[AddComponentMenu("Game/Controllers/StealthGameController (Manager)")]
public class StealthGameController : SerializedMonoBehaviour
{
    /// <summary>これ以下の速度（の 2 乗）なら「立ち止まった」とみなす。</summary>
    private const float StoppedSpeedSqr = 0.01f;

    /// <summary>NavMesh に乗っていないときに再確認するまでの待ち時間。</summary>
    private const float OffNavMeshRetrySeconds = 0.25f;

    // ===== ワールドの物音（ドアの開閉音など） =====

    public static event Action<Vector3, float, GameObject> OnWorldSoundMade;

    /// <summary>position で半径 radius に届く物音が鳴ったことを、すべての見張りに知らせる。</summary>
    public static void MakeSoundAt(Vector3 position, float radius, GameObject soundSource = null)
    {
        OnWorldSoundMade?.Invoke(position, radius, soundSource);
    }

    // ===== Inspector =====

    [TitleGroup("References"), PropertyOrder(-100)]
    [Required] public MyHouseEscapeGameController myHouseEscapeGameController;
    [TitleGroup("References")]
    public Transform player;
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

    [SerializeField]
    private AudioSource StealthBGM;

    [TitleGroup("Guards"), PropertySpace]
    [ListDrawerSettings(Expanded = true, DraggableItems = true, ShowIndexLabels = true)]
    public List<GuardEntry> guards = new();

    // ===== Inspector ボタン =====

    [TitleGroup("Controls"), ButtonGroup("Controls/Row1"), GUIColor(0.2f, 0.75f, 0.2f)]
    [Button("Start Stealth", ButtonSizes.Large)]
    public void StealthGameStart()
    {
        ResolvePlayerReferences();

        myHouseEscapeGameController.OnStealthGameStart();
        if (enableTimer) StartTimer();
        StealthBGM.Play();
        StartRandomDialogue();

        foreach (var guard in guards)
        {
            guard.footStepController = guard.cpuStatus?.footStepController;
            guard.animator = guard.cpuStatus.CPUAnimator;
            StartGuard(guard);
        }
    }

    [ButtonGroup("Controls/Row1"), GUIColor(0.9f, 0.35f, 0.35f)]
    [Button("Stop All", ButtonSizes.Large)]
    public void StopAllGuards()
    {
        foreach (var guard in guards)
        {
            StopGuard(guard);
        }

        StopTimer();
        StealthBGM.Stop();
        StopRandomDialogue();
    }

    [ButtonGroup("Controls/Row2"), GUIColor(0.35f, 0.6f, 1f)]
    [Button("Snap CurrentIndex From Closest")]
    public void SnapAllToClosestPoint()
    {
        foreach (var guard in guards)
        {
            if (guard.agent == null) continue;

            guard.currentIndex = FindClosestPointIndex(guard);
            guard.direction = 1;
        }
    }

    /// <summary>（エディタ用）選択中オブジェクトの子を、先頭の見張りの巡回ポイントとして取り込む。</summary>
    [ButtonGroup("Controls/Row2"), GUIColor(0.9f, 0.7f, 0.2f)]
    [Button("Collect PatrolPoints From Selected")]
    public void CollectPointsFromSelected()
    {
#if UNITY_EDITOR
        if (Selection.activeTransform == null)
        {
            Debug.LogWarning("選択中のTransformがありません");
            return;
        }

        var points = new List<Transform>();
        foreach (Transform child in Selection.activeTransform)
        {
            points.Add(child);
        }

        if (guards.Count == 0) guards.Add(new GuardEntry());
        guards[0].patrolPoints = points;
        Debug.Log($"Guard[0] に {points.Count} 個のポイントを採用");
#endif
    }

    // ===== Inspector: Gizmos の表示設定 =====

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

    // ===== 実行時の状態 =====

    private PlayerStatus playerStatus;
    private Coroutine timerCo;
    private Coroutine randomDialogueCo;
    private DialogueSequenceAsset randomDialogueSequence;

    // ===== 公開 API =====

    public void StopTimer()
    {
        if (timerBox != null) timerBox.SetActive(false);

        if (timerCo != null)
        {
            StopCoroutine(timerCo);
            timerCo = null;
        }
    }

    // ===== Unity ライフサイクル =====

    private void OnEnable()
    {
        OnWorldSoundMade += HandleWorldSound;
    }

    private void Start()
    {
        playerStatus = PlayerStatus.instance;
        ResolvePlayerReferences();

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

    private void Update()
    {
        if (StopIfGameFinished()) return;
        if (player == null) return;

        foreach (var guard in guards)
        {
            if (!guard.active || guard.agent == null) continue;

            if (IsPlayerVisible(guard))
            {
                OnPlayerDetected(guard);
                break;
            }

            if (guard.isInvestigating) continue;

            if (IsPlayerHeard(guard))
            {
                Debug.Log($"[Stealth] {guard.name} heard player!");
                RunGuardRoutine(guard, InvestigateSoundCo(guard, player.position));
            }
        }
    }

    /// <summary>プレイヤー関連の参照を取り直す。PlayerStatus があればそれを優先し、なければタグ検索で補う。</summary>
    private void ResolvePlayerReferences()
    {
        if (playerStatus != null)
        {
            player = playerStatus.transform;
            playerFootStepController = playerStatus.playerFootStepController;
        }
        else if (player == null)
        {
            var playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject) player = playerObject.transform;
        }

        if (playerFootStepController == null && player != null)
        {
            playerFootStepController = player.GetComponent<FootStepController>();
        }
    }

    /// <summary>脱出ゲームが成功/失敗で終わっていたら全体を止める。止めた場合は true。</summary>
    private bool StopIfGameFinished()
    {
        if (!autoStopWhenStateChanged || myHouseEscapeGameController == null) return false;

        var state = myHouseEscapeGameController.CurrentState;
        bool isFinished = state == MyHouseEscapeGameController.State.Success ||
                          state == MyHouseEscapeGameController.State.Failed;
        if (!isFinished) return false;

        StopAllGuards();
        if (hideTimerWhenIdle) UpdateTimerText(null);
        return true;
    }

    // ===== 発見・聴覚 =====

    private void OnPlayerDetected(GuardEntry guard)
    {
        // 見つけた見張りはプレイヤーの方を向く
        if (guard.cpuStatus != null && guard.cpuStatus.multiAimConstraint != null)
        {
            guard.cpuStatus.multiAimConstraint.weight = 1;
        }

        myHouseEscapeGameController?.NotifyPlayerDetected();

        if (stopAllOnDetect)
        {
            StopAllGuards();
        }
        else
        {
            StopGuard(guard);
        }
    }

    /// <summary>物音（MakeSoundAt）が鳴ったとき、聞こえる範囲にいる見張りを調査に向かわせる。</summary>
    private void HandleWorldSound(Vector3 soundPosition, float soundRadius, GameObject soundSource)
    {
        if (player == null || myHouseEscapeGameController.CurrentState != MyHouseEscapeGameController.State.Stealth) return;

        foreach (var guard in guards)
        {
            if (!guard.active || guard.agent == null || !guard.agent.isOnNavMesh) continue;
            if (!guard.canHear) continue;
            if (soundSource != null && soundSource == guard.agent.gameObject) continue; // 自分が出した音は無視
            if (guard.isInvestigating) continue;

            float distanceSqr = (guard.agent.transform.position - soundPosition).sqrMagnitude;
            bool isInSoundRange = distanceSqr <= soundRadius * soundRadius;
            bool isInHearingRange = distanceSqr <= guard.hearingDistance * guard.hearingDistance;
            if (isInSoundRange && isInHearingRange)
            {
                Debug.Log($"[Stealth] {guard.name} heard a world sound at {soundPosition}!");
                RunGuardRoutine(guard, InvestigateSoundCo(guard, soundPosition));
            }
        }
    }

    /// <summary>プレイヤーが歩き/走りで音を立てていて、かつ聞こえる距離にいるか。</summary>
    private bool IsPlayerHeard(GuardEntry guard)
    {
        if (!guard.canHear) return false;
        if (playerFootStepController == null) return false;

        var playerState = playerFootStepController.CurrentState;
        bool isNoisy = playerState == MovementState.Walking || playerState == MovementState.Running;
        if (!isNoisy) return false;

        float distanceSqr = (guard.agent.transform.position - player.position).sqrMagnitude;
        return distanceSqr <= guard.hearingDistance * guard.hearingDistance;
    }

    /// <summary>プレイヤーが視野角・視界距離の中にいて、間に遮蔽物がないか。</summary>
    private bool IsPlayerVisible(GuardEntry guard)
    {
        if (player == null) return false;

        GetEyePose(guard, out Vector3 eyePosition, out Vector3 eyeForward);

        Vector3 target = player.position + Vector3.up * guard.playerHeight;
        Vector3 toPlayer = target - eyePosition;
        float distance = toPlayer.magnitude;
        if (distance > guard.viewDistance) return false;

        float angle = Vector3.Angle(eyeForward, toPlayer.normalized);
        if (angle > guard.viewAngle * 0.5f) return false;

        // 最初に当たったものがプレイヤーなら見えている（壁などが先に当たれば見えていない）
        int mask = guard.playerLayer | guard.obstructionLayers;
        if (Physics.Raycast(eyePosition, toPlayer.normalized, out var hit, distance, mask, QueryTriggerInteraction.Ignore))
        {
            return (guard.playerLayer.value & (1 << hit.collider.gameObject.layer)) != 0;
        }
        return false;
    }

    /// <summary>見張りの目の位置と向き。eye が未設定なら、本体の位置 + eyeHeight で代用する。</summary>
    private void GetEyePose(GuardEntry guard, out Vector3 position, out Vector3 forward)
    {
        if (guard.eye != null)
        {
            position = guard.eye.position;
            forward = guard.eye.forward;
            return;
        }

        var body = guard.agent != null ? guard.agent.transform : transform;
        position = body.position + Vector3.up * guard.eyeHeight;
        forward = body.forward;
    }

    // ===== タイマー（警察到着まで） =====

    /// <summary>警察到着までの進み具合（0 = 開始直後, 1 = 到着）。</summary>
    private float TimerProgress
    {
        get
        {
            float total = myHouseEscapeGameController.policeArrivalSeconds;
            return total <= 0f ? 1f : Mathf.Clamp01(1f - (timerRemain / total));
        }
    }

    private void StartTimer()
    {
        if (timerCo != null) StopCoroutine(timerCo);

        timerRemain = Mathf.Max(0f, myHouseEscapeGameController.policeArrivalSeconds);
        if (timerBox != null) timerBox.SetActive(true);

        if (myHouseEscapeGameController != null)
        {
            myHouseEscapeGameController.onStealthTick?.Invoke(TimerProgress);
        }

        UpdateTimerText(timerRemain);
        timerCo = StartCoroutine(TimerLoop());
    }

    private IEnumerator TimerLoop()
    {
        while (timerRemain > 0f)
        {
            timerRemain -= Time.deltaTime;
            if (timerRemain < 0f) timerRemain = 0f;

            UpdateTimerText(timerRemain);
            if (myHouseEscapeGameController != null && myHouseEscapeGameController.policeArrivalSeconds > 0f)
            {
                myHouseEscapeGameController.onStealthTick?.Invoke(TimerProgress);
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

    /// <summary>残り時間を "分:秒" で表示する。null を渡すと表示を消す。</summary>
    private void UpdateTimerText(float? remainSeconds)
    {
        if (timerText == null) return;

        if (!remainSeconds.HasValue)
        {
            timerText.SetText("");
            return;
        }

        int totalSeconds = Mathf.Max(0, Mathf.FloorToInt(remainSeconds.Value));
        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;
        timerText.SetText("{0}:{1:00}", minutes, seconds);
    }

    // ===== ランダムなセリフ =====

    private void StartRandomDialogue()
    {
        if (!enableRandomDialogue) return;

        randomDialogueSequence = randomDialogueGroup.sequences.Find(seq => seq.id == randomDialogueSequenceId);
        if (randomDialogueSequence == null || randomDialogueSequence.steps.Count == 0)
        {
            Debug.LogWarning($"[StealthGameController] Sequence '{randomDialogueSequenceId}' not found or is empty.");
            return;
        }

        if (randomDialogueCo != null) StopCoroutine(randomDialogueCo);
        randomDialogueCo = StartCoroutine(RandomDialogueLoop());
    }

    private void StopRandomDialogue()
    {
        if (randomDialogueCo != null)
        {
            StopCoroutine(randomDialogueCo);
            randomDialogueCo = null;
        }

        foreach (var guard in guards)
        {
            HideGuardMessage(guard);
        }
    }

    /// <summary>一定間隔で、手の空いている見張りの誰かにランダムなセリフを喋らせる。</summary>
    private IEnumerator RandomDialogueLoop()
    {
        // 開始直後は通常の半分の間隔で最初のセリフを出す
        yield return new WaitForSeconds(Random.Range(randomDialogueInterval.x, randomDialogueInterval.y) * 0.5f);

        while (true)
        {
            var idleGuards = guards.FindAll(g => g.active && !g.isInvestigating && g.cpuStatus != null);
            if (idleGuards.Count == 0)
            {
                yield return new WaitForSeconds(1f);
                continue;
            }

            var speaker = idleGuards[Random.Range(0, idleGuards.Count)];
            int stepIndex = Random.Range(0, randomDialogueSequence.steps.Count);
            DialogueStep step = randomDialogueSequence.steps[stepIndex];
            if (step != null && DialogueController.instance != null)
            {
                DialogueController.instance.ShowBriefLocalizedStep(speaker.cpuStatus.dialogueUIRefs, step, dialogueDisplayDuration);
            }

            float waitTime = Random.Range(randomDialogueInterval.x, randomDialogueInterval.y);
            yield return new WaitForSeconds(waitTime);
        }
    }

    /// <summary>見張りの吹き出しを消す。</summary>
    private static void HideGuardMessage(GuardEntry guard)
    {
        if (DialogueController.instance == null || guard.cpuStatus == null) return;

        DialogueController.instance.ShowBriefMessage(guard.cpuStatus.dialogueUIRefs, "", "", 0);
    }

    // ===== 見張りの開始・停止 =====

    private void StartGuard(GuardEntry guard)
    {
        if (guard == null || guard.agent == null) return;

        guard.agent.enabled = true;
        guard.active = true;
        guard.direction = 1;
        guard.currentIndex = 0;
        guard.isInvestigating = false;
        guard.agent.autoBraking = guard.useAutoBraking;

        if (guard.arriveThreshold > 0f)
        {
            guard.agent.stoppingDistance = Mathf.Max(guard.agent.stoppingDistance, guard.arriveThreshold);
        }

        if (guard.startFromClosestPoint && guard.patrolPoints != null && guard.patrolPoints.Count > 1)
        {
            guard.currentIndex = FindClosestPointIndex(guard);
        }

        RememberDefaultSpeeds(guard);
        CacheInvestigateSequence(guard);

        RunGuardRoutine(guard, PatrolLoop(guard));
    }

    private void StopGuard(GuardEntry guard)
    {
        SetHasten(guard, false);
        guard.isInvestigating = false;
        guard.active = false;

        HideGuardMessage(guard);

        if (guard.patrolCo != null)
        {
            StopCoroutine(guard.patrolCo);
            guard.patrolCo = null;
        }

        if (guard.agent != null && guard.agent.isOnNavMesh) guard.agent.ResetPath();
    }

    /// <summary>見張りの行動コルーチン（巡回 or 調査）を差し替える。動いているものがあれば止めてから始める。</summary>
    private void RunGuardRoutine(GuardEntry guard, IEnumerator routine)
    {
        if (guard.patrolCo != null) StopCoroutine(guard.patrolCo);
        guard.patrolCo = StartCoroutine(routine);
    }

    /// <summary>早歩きから元に戻せるよう、通常時の移動速度・アニメ速度・足音間隔を覚えておく。</summary>
    private void RememberDefaultSpeeds(GuardEntry guard)
    {
        guard.defaultAgentSpeed = guard.agent.speed;
        guard.isHastening = false;

        if (guard.animator != null)
        {
            guard.defaultAnimatorSpeed = guard.animator.speed;
        }
        else if (enableFastWalk)
        {
            Debug.LogWarning($"[Stealth] Guard '{guard.name}' に Animator が設定されていません。早歩きアニメーションが機能しません。", guard.agent);
        }

        if (guard.footStepController != null)
        {
            guard.defaultFootstepInterval = guard.footStepController.walkStepInterval;
        }
        else if (enableFastWalk)
        {
            Debug.LogWarning($"[Stealth] Guard '{guard.name}' に FootStepController が設定されていません。早歩き足音が機能しません。", guard.agent);
        }
    }

    /// <summary>調査開始時に喋るセリフのシーケンスを、ID から引いて覚えておく（なければ null）。</summary>
    private void CacheInvestigateSequence(GuardEntry guard)
    {
        guard.cachedInvestigateSequence = null;
        if (!guard.playDialogueOnInvestigate || guard.investigateDialogueGroup == null) return;

        var sequence = guard.investigateDialogueGroup.sequences.Find(seq => seq.id == guard.investigateDialogueSequenceId);
        if (sequence == null || sequence.steps.Count == 0)
        {
            Debug.LogWarning($"[Stealth] {guard.name}: Investigate sequence '{guard.investigateDialogueSequenceId}' not found or is empty.");
            return;
        }

        guard.cachedInvestigateSequence = sequence;
    }

    /// <summary>早歩き（調査へ急ぐ状態）の ON/OFF。移動速度・アニメ速度・足音間隔をまとめて切り替える。</summary>
    private void SetHasten(GuardEntry guard, bool hasten)
    {
        if (!enableFastWalk) return;
        if (guard == null || guard.agent == null) return;
        if (guard.isHastening == hasten) return;

        guard.isHastening = hasten;

        float agentSpeedScale = hasten ? fastWalkAgentSpeedMultiplier : 1f;
        float animatorSpeedScale = hasten ? fastWalkAnimatorSpeedMultiplier : 1f;
        float footstepRateScale = hasten ? fastWalkFootstepMultiplier : 1f;

        guard.agent.speed = guard.defaultAgentSpeed * agentSpeedScale;
        if (guard.animator != null)
        {
            guard.animator.speed = guard.defaultAnimatorSpeed * animatorSpeedScale;
        }
        if (guard.footStepController != null)
        {
            guard.footStepController.walkStepInterval = guard.defaultFootstepInterval / footstepRateScale;
        }
    }

    // ===== 見張りの行動: 巡回 =====

    /// <summary>巡回ポイントを順番に回り続ける。調査が始まったら（isInvestigating）自分は終了する。</summary>
    private IEnumerator PatrolLoop(GuardEntry guard)
    {
        SetHasten(guard, false);

        while (guard.active)
        {
            guard.isInvestigating = false;

            Transform target = GetCurrentPatrolPoint(guard);
            if (target == null || !guard.agent.isOnNavMesh)
            {
                yield return new WaitForSeconds(OffNavMeshRetrySeconds);
                continue;
            }

            // 1) ポイントへ向かう
            Vector3 destination = target.position;
            guard.agent.SetDestination(destination);

            float elapsed = 0f;
            while (guard.active && !guard.isInvestigating)
            {
                if (ShouldStopMoving(guard, destination, ref elapsed, investigating: false)) break;
                yield return null;
            }
            if (guard.isInvestigating) yield break;

            // 2) 完全に止まるのを待つ
            while (guard.active && guard.agent.velocity.sqrMagnitude > StoppedSpeedSqr)
            {
                yield return null;
            }

            // 3) ポイントで少し待機する
            float waitSeconds = Mathf.Max(0f, guard.waitAtPointSeconds * Mathf.Max(0f, waitTimeScale));
            float waited = 0f;
            while (guard.active && !guard.isInvestigating && waited < waitSeconds)
            {
                waited += Time.deltaTime;
                yield return null;
            }
            if (guard.isInvestigating) yield break;

            // 4) 次のポイントへ
            guard.currentIndex = GetNextIndex(guard);
        }
    }

    private static Transform GetCurrentPatrolPoint(GuardEntry guard)
    {
        if (guard.patrolPoints == null || guard.patrolPoints.Count == 0) return null;

        int index = Mathf.Clamp(guard.currentIndex, 0, guard.patrolPoints.Count - 1);
        return guard.patrolPoints[index];
    }

    /// <summary>次に向かう巡回ポイントのインデックス。ランダム / 往復（Ping-Pong）/ 周回 の 3 通り。</summary>
    private int GetNextIndex(GuardEntry guard)
    {
        int count = guard.patrolPoints == null ? 0 : guard.patrolPoints.Count;
        if (count <= 1) return 0;

        if (guard.randomPatrol)
        {
            // 今いるポイント以外からランダムに選ぶ
            int next;
            do
            {
                next = Random.Range(0, count);
            }
            while (next == guard.currentIndex);

            guard.direction = 1;
            return next;
        }

        if (guard.pingPong)
        {
            // 端まで来たら向きを反転して折り返す
            int next = guard.currentIndex + guard.direction;
            if (next >= count)
            {
                guard.direction = -1;
                next = guard.currentIndex + guard.direction;
            }
            else if (next < 0)
            {
                guard.direction = 1;
                next = guard.currentIndex + guard.direction;
            }
            return Mathf.Clamp(next, 0, count - 1);
        }

        // 周回: 最後まで行ったら先頭へ戻る
        guard.direction = 1;
        int loopNext = guard.currentIndex + 1;
        return loopNext >= count ? 0 : loopNext;
    }

    private int FindClosestPointIndex(GuardEntry guard)
    {
        if (guard.patrolPoints == null || guard.patrolPoints.Count == 0) return 0;

        Vector3 guardPosition = guard.agent.transform.position;
        int closestIndex = 0;
        float closestDistanceSqr = float.MaxValue;

        for (int i = 0; i < guard.patrolPoints.Count; i++)
        {
            var point = guard.patrolPoints[i];
            if (point == null) continue;

            float distanceSqr = (point.position - guardPosition).sqrMagnitude;
            if (distanceSqr < closestDistanceSqr)
            {
                closestDistanceSqr = distanceSqr;
                closestIndex = i;
            }
        }
        return closestIndex;
    }

    // ===== 見張りの行動: 音の調査 =====

    /// <summary>音がした場所まで早歩きで向かい、しばらく様子を見てから巡回に戻る。</summary>
    private IEnumerator InvestigateSoundCo(GuardEntry guard, Vector3 soundPosition)
    {
        guard.isInvestigating = true;
        SetHasten(guard, true);
        SayInvestigateLine(guard);

        if (guard.agent.isOnNavMesh)
        {
            // 1) 音の場所へ向かう
            guard.agent.SetDestination(soundPosition);

            float elapsed = 0f;
            while (guard.active && guard.isInvestigating)
            {
                if (ShouldStopMoving(guard, soundPosition, ref elapsed, investigating: true)) break;
                yield return null;
            }

            // 2) 完全に止まるのを待つ
            while (guard.active && guard.isInvestigating && guard.agent.velocity.sqrMagnitude > StoppedSpeedSqr)
            {
                yield return null;
            }

            // 3) その場で様子を見る（立ち止まっている間はアニメ速度を通常に戻す）
            float waitSeconds = Mathf.Max(0f, guard.investigateWaitTime * Mathf.Max(0f, waitTimeScale));
            if (waitSeconds > 0f)
            {
                if (guard.animator != null && guard.isHastening)
                {
                    guard.animator.speed = guard.defaultAnimatorSpeed;
                }

                float waited = 0f;
                while (guard.active && guard.isInvestigating && waited < waitSeconds)
                {
                    waited += Time.deltaTime;
                    yield return null;
                }
            }
        }
        else
        {
            yield return new WaitForSeconds(OffNavMeshRetrySeconds);
        }

        if (guard.playDialogueOnInvestigate)
        {
            HideGuardMessage(guard);
        }

        guard.isInvestigating = false;

        // 4) まだ有効なら巡回に戻る
        if (guard.active)
        {
            RunGuardRoutine(guard, PatrolLoop(guard));
        }
        else
        {
            SetHasten(guard, false);
        }
    }

    /// <summary>調査開始時のセリフ（「誰かいるの？」など）をランダムに 1 つ喋らせる。</summary>
    private static void SayInvestigateLine(GuardEntry guard)
    {
        if (!guard.playDialogueOnInvestigate) return;
        if (guard.cachedInvestigateSequence == null || guard.cachedInvestigateSequence.steps.Count == 0) return;
        if (DialogueController.instance == null || guard.cpuStatus == null) return;

        var steps = guard.cachedInvestigateSequence.steps;
        DialogueStep step = steps[Random.Range(0, steps.Count)];
        DialogueController.instance.ShowBriefLocalizedStep(guard.cpuStatus.dialogueUIRefs, step, guard.investigateDialogueDuration);
    }

    // ===== 移動の共通処理 =====

    /// <summary>
    /// 目的地への移動を打ち切るべきなら true（到着した / 経路が無効 / タイムアウト）。
    /// 移動中は毎フレーム 1 回呼ぶこと（elapsed に経過時間を加算する）。
    /// </summary>
    private bool ShouldStopMoving(GuardEntry guard, Vector3 destination, ref float elapsed, bool investigating)
    {
        var agent = guard.agent;

        if (!agent.pathPending)
        {
            if (HasArrived(guard, destination)) return true;

            bool isPathBroken = agent.pathStatus == NavMeshPathStatus.PathInvalid ||
                                agent.pathStatus == NavMeshPathStatus.PathPartial;
            if (isPathBroken)
            {
                Debug.LogWarning(investigating
                    ? $"[Stealth] {guard.name} (Investigate): pathStatus={agent.pathStatus} → 調査中断"
                    : $"[Stealth] {guard.name}: pathStatus={agent.pathStatus} → 次へスキップ");
                return true;
            }
        }

        elapsed += Time.deltaTime;
        if (guard.arrivalTimeoutSeconds > 0f && elapsed >= guard.arrivalTimeoutSeconds)
        {
            Debug.LogWarning(investigating
                ? $"[Stealth] {guard.name} (Investigate): ArrivalTimeout → 調査中断"
                : $"[Stealth] {guard.name}: ArrivalTimeout({guard.arrivalTimeoutSeconds}s) 到達 → 次へ進む");
            return true;
        }

        return false;
    }

    private bool HasArrived(GuardEntry guard, Vector3 destination)
    {
        var agent = guard.agent;

        // NavMeshAgent 基準の到着判定
        float stopThreshold = Mathf.Max(agent.stoppingDistance, guard.arriveThreshold);
        bool hasStopped = !agent.hasPath || agent.velocity.sqrMagnitude < StoppedSpeedSqr;
        if (agent.remainingDistance <= stopThreshold && hasStopped) return true;

        // 予備の判定: 高さを無視した水平距離が十分近ければ到着とみなす
        if (guard.usePlanarDistance)
        {
            float planarDistance = PlanarDistance(agent.transform.position, destination);
            if (planarDistance <= Mathf.Max(guard.closeEnoughDistance, 0.001f)) return true;
        }

        return false;
    }

    private float PlanarDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    // ===== Gizmos（エディタ表示専用） =====

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (gizmoAlways) DrawAllGuardGizmos();
    }

    private void OnDrawGizmosSelected()
    {
        DrawAllGuardGizmos();
    }

    private void DrawAllGuardGizmos()
    {
        if (guards == null) return;

        foreach (var guard in guards)
        {
            if (guard == null) continue;

            if (gizmoDrawFOV) DrawVisionGizmo(guard);
            if (gizmoDrawHearing) DrawHearingGizmo(guard);
            if (gizmoDrawPatrol) DrawPatrolGizmo(guard);
            if (gizmoDrawNavPath) DrawNavPathGizmo(guard);
        }
    }

    /// <summary>視野の扇形と、プレイヤーへの視線（見えていれば赤）。</summary>
    private void DrawVisionGizmo(GuardEntry guard)
    {
        GetEyePose(guard, out Vector3 eyePosition, out Vector3 eyeForward);
        Vector3 arcStart = Quaternion.Euler(0, -guard.viewAngle * 0.5f, 0) * eyeForward;

        Handles.color = gizmoFOVFillColor;
        Handles.DrawSolidArc(eyePosition, Vector3.up, arcStart, guard.viewAngle, guard.viewDistance);

        Handles.color = gizmoFOVEdgeColor;
        Handles.DrawWireArc(eyePosition, Vector3.up, arcStart, guard.viewAngle, guard.viewDistance);
        Handles.DrawWireDisc(eyePosition, Vector3.up, guard.viewDistance * 0.33f);
        Handles.DrawWireDisc(eyePosition, Vector3.up, guard.viewDistance * 0.66f);
        Handles.DrawWireDisc(eyePosition, Vector3.up, guard.viewDistance);

        if (player != null)
        {
            Vector3 target = player.position + Vector3.up * guard.playerHeight;
            Handles.color = IsPlayerVisible(guard) ? gizmoSightRayVisible : gizmoSightRayBlocked;
            Handles.DrawAAPolyLine(3f, eyePosition, target);
        }
    }

    private void DrawHearingGizmo(GuardEntry guard)
    {
        if (!guard.canHear || guard.agent == null) return;

        Vector3 center = guard.agent.transform.position;
        Handles.color = gizmoHearingColor;
        Handles.DrawSolidDisc(center, Vector3.up, guard.hearingDistance);
        Handles.color = gizmoHearingEdgeColor;
        Handles.DrawWireDisc(center, Vector3.up, guard.hearingDistance);
    }

    /// <summary>巡回ポイント、ポイント間の経路、次に向かう区間（強調表示）。</summary>
    private void DrawPatrolGizmo(GuardEntry guard)
    {
        var points = guard.patrolPoints;
        if (points == null || points.Count == 0) return;

        // ポイント本体とラベル
        Gizmos.color = gizmoPatrolPointColor;
        for (int i = 0; i < points.Count; i++)
        {
            var point = points[i];
            if (!point) continue;

            Gizmos.DrawWireSphere(point.position, gizmoPointRadius);
            if (gizmoLabelPoints)
            {
                var style = new GUIStyle(EditorStyles.boldLabel);
                style.normal.textColor = Color.cyan;
                Handles.Label(point.position + Vector3.up * 0.2f, $"{guard.name} #{i}", style);
            }
        }

        if (points.Count <= 1) return;

        // 順番に回る場合: 隣同士を矢印でつなぐ
        if (!guard.randomPatrol)
        {
            Handles.color = gizmoPatrolLinkColor;
            for (int i = 0; i < points.Count; i++)
            {
                var from = points[i];
                var to = points[(i + 1) % points.Count];
                if (!from || !to) continue;

                Handles.DrawAAPolyLine(3f, from.position, to.position);
                DrawArrow(from.position, to.position, gizmoArrowSize, gizmoPatrolLinkColor);
            }
        }

        // ランダム巡回の場合: 全ポイント同士を薄い線でつなぐ（オプション）
        if (guard.randomPatrol && gizmoShowAllRandomLinks)
        {
            Handles.color = gizmoRandomLinkColor;
            for (int i = 0; i < points.Count; i++)
            {
                var from = points[i];
                if (!from) continue;

                for (int j = i + 1; j < points.Count; j++)
                {
                    var to = points[j];
                    if (!to) continue;

                    Handles.DrawLine(from.position, to.position);
                }
            }
        }

        // 現在地 → 次のポイント を強調
        var current = points[Mathf.Clamp(guard.currentIndex, 0, points.Count - 1)];
        var next = points[Mathf.Clamp(PreviewNextIndex(guard), 0, points.Count - 1)];
        if (current && next)
        {
            Handles.color = gizmoNextLinkColor;
            Handles.DrawAAPolyLine(5f, current.position, next.position);
            DrawArrow(current.position, next.position, gizmoArrowSize * 1.2f, gizmoNextLinkColor);
        }
    }

    /// <summary>NavMeshAgent が実際にたどっている経路。</summary>
    private void DrawNavPathGizmo(GuardEntry guard)
    {
        if (!guard.agent || !guard.agent.isOnNavMesh || !guard.agent.hasPath) return;

        var corners = guard.agent.path.corners;
        if (corners == null || corners.Length <= 1) return;

        Handles.color = gizmoNavPathColor;
        Handles.DrawAAPolyLine(4f, corners);
        for (int i = 0; i < corners.Length - 1; i++)
        {
            DrawArrow(corners[i], corners[i + 1], gizmoArrowSize * 0.8f, gizmoNavPathColor);
        }
    }

    private void DrawArrow(Vector3 from, Vector3 to, float size, Color color)
    {
        Vector3 direction = to - from;
        if (direction.sqrMagnitude < 1e-4f) return;

        Vector3 position = Vector3.Lerp(from, to, 0.85f);
        Quaternion rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        Handles.color = color;
        Handles.ConeHandleCap(0, position, rotation, size, EventType.Repaint);
    }

    /// <summary>
    /// Gizmo 表示用に「次のポイント」を予測する。GetNextIndex と違い、状態（direction）を書き換えず、
    /// ランダム巡回でも乱数を使わない（毎フレーム表示が変わらないようにするため）。
    /// </summary>
    private int PreviewNextIndex(GuardEntry guard)
    {
        int count = guard.patrolPoints == null ? 0 : guard.patrolPoints.Count;
        if (count <= 1) return 0;

        if (!guard.randomPatrol && guard.pingPong)
        {
            int direction = guard.direction == 0 ? 1 : guard.direction;
            int next = guard.currentIndex + direction;
            if (next >= count) next = guard.currentIndex - 1;
            else if (next < 0) next = guard.currentIndex + 1;
            return Mathf.Clamp(next, 0, count - 1);
        }

        int loopNext = guard.currentIndex + 1;
        return loopNext >= count ? 0 : loopNext;
    }
#endif

    // ===== データ定義: 見張り 1 人分の設定と状態 =====

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

        // ---- 以下は実行時の状態（Inspector には出さない） ----

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
}

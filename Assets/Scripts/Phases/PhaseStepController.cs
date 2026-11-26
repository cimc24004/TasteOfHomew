using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// ゲーム進行を「フェーズ（親）」と「ステップ（子）」の 2 段階で管理するシングルトン。
///
/// ・フェーズは GameFlowController の切り替えに追従し、ステップは NextStep / SwitchStep などで進める
/// ・ステップに入ると、そのステップ用のオブジェクトを表示し、OnEnter イベントを実行する
/// ・訪問済みのステップをセーブデータに記録する（<see cref="HasVisited"/> で参照）
/// ・「現在フェーズが指定範囲内ならイベント実行」という範囲ゲートも持つ
/// </summary>
[DisallowMultipleComponent]
public class PhaseStepController : Singleton<PhaseStepController>
{
    private const string VisitedHistorySaveKey = "PhaseStepController_VisitedHistory";

    // ===== Inspector =====

    [Title("Phases（親：フェーズ / 子：ステップ）")]
    [ListDrawerSettings(Expanded = true, DraggableItems = true)]
    public List<PhaseBlock> phases = new();

    [Title("起動設定")]
    [LabelText("OnEnableで開始")] public bool autoStartOnEnable = false;

    [Title("表示制御オプション")]
    [LabelText("開始時に全Stepオブジェクトを非表示にする")]
    public bool hideAllObjectsOnStart = true;

    [LabelText("フェーズ跨ぎで他フェーズの表示物を全消しする")]
    public bool hardExclusiveShowAcrossPhases = true;

    [Title("Phase Range Gates（範囲内ならイベント実行）")]
    [Tooltip("起動時と（オプションで）フェーズ遷移時に、現在フェーズが各ゲートの範囲内ならそのUnityEventを実行")]
    public bool enablePhaseRangeGates = true;

    [ShowIf(nameof(enablePhaseRangeGates))]
    [Tooltip("フェーズの並び順。未設定なら GameFlowController.flowOrder、さらに無ければ enum 順を使用")]
    public List<GameFlowController.Phase> phaseOrderOverride = new();

    [ShowIf(nameof(enablePhaseRangeGates))]
    [Tooltip("範囲ゲートのリスト。各要素で [開始→終了] を指定して、その区間に現在フェーズが入っていれば eventOnMatch を実行")]
    [ListDrawerSettings(Expanded = true, DraggableItems = true)]
    public List<PhaseRangeGate> phaseRangeGates = new();

    [ShowIf(nameof(enablePhaseRangeGates))]
    [Tooltip("OnEnable 初期化完了時に範囲ゲートを評価してイベント実行")]
    public bool evaluateRangeOnEnable = true;

    [ShowIf(nameof(enablePhaseRangeGates))]
    [Tooltip("フェーズが変わるたびに範囲ゲートを再評価してイベント実行")]
    public bool evaluateRangeOnPhaseChanged = false;

    [Title("Events")]
    public UnityEventPhase   onPhaseEntered;
    public UnityEventPhase   onPhaseExited;
    public UnityEventPhase2  onPhaseChanged;
    public UnityEventString  onStepEntered;
    public UnityEventString  onStepExited;
    public UnityEventString2 onStepChanged;

    // ===== 現在位置 =====

    [ShowInInspector] public GameFlowController.Phase CurrentPhase;
    [ShowInInspector] public string CurrentStepKey;

    public int CurrentPhaseIndex => FindPhaseIndex(CurrentPhase);
    public int CurrentStepIndex  => FindStepIndex(CurrentPhaseIndex, CurrentStepKey);

    // ===== 実行時の状態 =====

    private DataShareController dataShareController;
    private SaveDataBaseController saveController;

    private bool isSubscribedToGameFlow;
    private bool hasAutoStarted;

    /// <summary>フェーズごとの訪問済みステップキー。</summary>
    private readonly Dictionary<GameFlowController.Phase, HashSet<string>> visitedSteps = new();

    /// <summary>「一度だけ実行」設定で、すでに実行済みの範囲ゲートのインデックス。</summary>
    private readonly HashSet<int> invokedGateIndices = new();

    // ===== Unity ライフサイクル =====

    private void Start()
    {
        dataShareController = DataShareController.instance;
        saveController = SaveDataBaseController.instance;

        // 現在のステップを常に DataShareController へ反映しておく（シーンをまたいだ復帰に使う）
        onStepEntered.AddListener((step) =>
        {
            dataShareController.CurrentPhaseStep = step;
        });

        if (hideAllObjectsOnStart) HideAllObjects();
        InitializeAsync().Forget();
    }

    private void OnDisable()
    {
        if (TryGetStep(CurrentPhaseIndex, CurrentStepIndex, out var step))
        {
            SetObjectsActive(step.objectsToShow, false);
        }

        if (GameFlowController.instance)
        {
            GameFlowController.instance.PhaseChanged -= OnGameFlowPhaseChanged;
        }

        isSubscribedToGameFlow = false;
        hasAutoStarted = false;
    }

    private void OnDestroy()
    {
        onStepEntered.RemoveAllListeners();
    }

    // ===== 初期化 =====

    private async UniTask InitializeAsync()
    {
        var destroyToken = this.GetCancellationTokenOnDestroy();

        // 他のシングルトンの Start が終わるのを 1 フレーム待つ
        await UniTask.NextFrame(destroyToken);
        TrySubscribeGameFlow();

        if (autoStartOnEnable && !hasAutoStarted)
        {
            LoadVisitedHistory();

            var initialPhase = dataShareController.CurrentPhase;
            var initialStepKey = dataShareController.CurrentPhaseStep;
            Debug.Log($"[PhaseStepController] DataShareController から初期位置を取得: Phase={initialPhase}, Step={initialStepKey}");

            await EnterInitialStateAsync(initialPhase, initialStepKey, destroyToken);

            hasAutoStarted = true;
        }

        if (enablePhaseRangeGates && evaluateRangeOnEnable)
        {
            EvaluatePhaseRangeGates(phaseOverride: null, isFromPhaseChange: false);
        }
    }

    /// <summary>
    /// 起動時（セーブからの復帰を含む）に、指定のフェーズ/ステップへ直接入る。
    /// 通常の切り替えと違い Exit 系の処理は行わない。
    /// </summary>
    private async UniTask<bool> EnterInitialStateAsync(GameFlowController.Phase phase, string stepKey, CancellationToken cancellationToken)
    {
        int phaseIndex = FindPhaseIndex(phase);
        if (phaseIndex < 0)
        {
            Debug.LogError($"[PhaseStepController] Initial phase '{phase}' not found in configuration!");
            return false;
        }

        int stepIndex = FindStepIndex(phaseIndex, stepKey);
        if (stepIndex < 0)
        {
            Debug.LogWarning($"[PhaseStepController] Initial stepKey '{stepKey}' not found. Falling back to the first step of phase '{phase}'.");
            if (!TryResolveFirstStepIndex(phaseIndex, null, out stepIndex))
            {
                Debug.LogError($"[PhaseStepController] Phase '{phase}' has no steps to initialize to!");
                return false;
            }
        }

        CurrentPhase = phase;
        CurrentStepKey = phases[phaseIndex].steps[stepIndex].key;

        if (hideAllObjectsOnStart)
        {
            HideAllObjects();
            await UniTask.Yield(cancellationToken);
        }
        cancellationToken.ThrowIfCancellationRequested();

        if (TryGetStep(phaseIndex, stepIndex, out var step))
        {
            ShowStepAndInvokeEnterEvents(step);
        }

        onPhaseEntered?.Invoke(CurrentPhase);
        onStepEntered?.Invoke(CurrentStepKey);

        MarkVisitedAndSave();

        return true;
    }

    private void TrySubscribeGameFlow()
    {
        if (isSubscribedToGameFlow) return;
        if (!GameFlowController.instance) return;

        GameFlowController.instance.PhaseChanged += OnGameFlowPhaseChanged;
        isSubscribedToGameFlow = true;
    }

    private void OnGameFlowPhaseChanged(GameFlowController.Phase oldPhase, GameFlowController.Phase newPhase)
    {
        SwitchPhaseAsync(newPhase, null, invokeOldExit: true).Forget();

        if (enablePhaseRangeGates && evaluateRangeOnPhaseChanged)
        {
            EvaluatePhaseRangeGates(newPhase, isFromPhaseChange: true);
        }
    }

    // ===== 公開 API: ステップの切り替え =====

    [Button("UnityEvent → NextStep")]
    public void NextStepEvent() => NextStep();

    [Button("UnityEvent → PrevStep")]
    public void PrevStepEvent() => PrevStep();

    [Button("次のStep →")]
    public bool NextStep() => MoveStep(+1);

    [Button("← 前のStep")]
    public bool PrevStep() => MoveStep(-1);

    /// <summary>現在フェーズ内で、キー指定のステップへ切り替える。</summary>
    public bool SwitchStep(string stepKey)
    {
        if (string.IsNullOrWhiteSpace(stepKey)) return false;
        if (string.Equals(stepKey, CurrentStepKey, StringComparison.Ordinal)) return false;

        int phaseIndex = CurrentPhaseIndex;
        if (phaseIndex < 0) return false;

        int stepIndex = FindStepIndex(phaseIndex, stepKey);
        if (stepIndex < 0) return false;

        return SwitchStepByIndex(phaseIndex, stepIndex);
    }

    // ===== 公開 API: フェーズの切り替え =====

    /// <summary>
    /// フェーズを切り替える。入るステップは firstStepKey → フェーズの startStepKey → 先頭、の優先順で決まる。
    /// すでに同じフェーズにいる場合は、フェーズ内のステップ切り替えとして扱う。
    /// </summary>
    /// <param name="invokeOldExit">false にすると、切り替え前のステップ/フェーズの Exit 処理を行わない。</param>
    public async UniTask<bool> SwitchPhaseAsync(GameFlowController.Phase phase, string firstStepKey = null, bool invokeOldExit = true)
    {
        int newPhaseIndex = FindPhaseIndex(phase);
        if (newPhaseIndex < 0)
        {
            Debug.LogWarning($"[PhaseStepController] Phase '{phase}' not found in list.");
            return false;
        }

        // 同じフェーズ内: ステップの Exit / Enter は SwitchStepByIndex がまとめて行う
        if (phase == CurrentPhase)
        {
            if (!TryResolveFirstStepIndex(newPhaseIndex, firstStepKey, out int stepIndex)) return false;
            return SwitchStepByIndex(newPhaseIndex, stepIndex);
        }

        var oldPhase = CurrentPhase;
        ExitCurrentPhase(invokeOldExit);

        if (!TryResolveFirstStepIndex(newPhaseIndex, firstStepKey, out int firstStepIndex)) return false;

        await UniTask.Yield();

        // 新フェーズの最初のステップに入れた場合だけ、フェーズ系イベントを通知する
        bool switched = SwitchStepByIndex(newPhaseIndex, firstStepIndex);
        if (switched)
        {
            CurrentPhase = phases[newPhaseIndex].phase;
            onPhaseEntered?.Invoke(CurrentPhase);
            onPhaseChanged?.Invoke(oldPhase, CurrentPhase);
        }

        return switched;
    }

    public void SwitchPhase(GameFlowController.Phase phase, string firstStepKey = null, bool invokeOldExit = true)
        => SwitchPhaseAsync(phase, firstStepKey, invokeOldExit).Forget();

    /// <summary>フェーズとステップを同時に指定して切り替える。どちらかが存在しなければ何もしない。</summary>
    public UniTask<bool> SwitchAsync(GameFlowController.Phase phase, string stepKey)
    {
        bool isAlreadyThere = phase == CurrentPhase && string.Equals(stepKey, CurrentStepKey, StringComparison.Ordinal);
        if (isAlreadyThere) return UniTask.FromResult(false);

        int phaseIndex = FindPhaseIndex(phase);
        if (phaseIndex < 0) return UniTask.FromResult(false);

        int stepIndex = FindStepIndex(phaseIndex, stepKey);
        if (stepIndex < 0) return UniTask.FromResult(false);

        return SwitchPhaseAsync(phase, stepKey);
    }

    public void Switch(GameFlowController.Phase phase, string stepKey)
        => SwitchAsync(phase, stepKey).Forget();

    // ===== 公開 API: 訪問履歴 =====

    public bool HasVisited(GameFlowController.Phase phase, string stepKey)
    {
        return visitedSteps.TryGetValue(phase, out var stepKeys) && stepKeys.Contains(stepKey);
    }

    // ===== 公開 API: 範囲ゲート =====

    /// <summary>
    /// 各範囲ゲートについて、対象フェーズが [fromPhase, toPhase] の範囲内ならイベントを実行する。
    /// </summary>
    /// <param name="phaseOverride">判定に使うフェーズ。null なら GameFlowController（未初期化なら自分）の現在フェーズ。</param>
    /// <param name="isFromPhaseChange">フェーズ変更をきっかけにした評価か。true のときは evaluateOnPhaseChange が有効なゲートだけを見る。</param>
    public void EvaluatePhaseRangeGates(GameFlowController.Phase? phaseOverride = null, bool isFromPhaseChange = false)
    {
        if (!enablePhaseRangeGates || phaseRangeGates == null || phaseRangeGates.Count == 0) return;

        var phase = ResolveGateTargetPhase(phaseOverride);
        var orderByPhase = BuildPhaseOrderIndex();

        if (!orderByPhase.TryGetValue(phase, out int currentOrder))
        {
            Debug.LogWarning($"[PhaseStepController] Phase order not defined for '{phase}'");
            return;
        }

        for (int i = 0; i < phaseRangeGates.Count; i++)
        {
            var gate = phaseRangeGates[i];
            if (isFromPhaseChange && !gate.evaluateOnPhaseChange) continue;
            if (!orderByPhase.TryGetValue(gate.fromPhase, out int fromOrder)) continue;
            if (!orderByPhase.TryGetValue(gate.toPhase, out int toOrder)) continue;

            // from / to が逆順で設定されていても同じ範囲として扱う
            if (fromOrder > toOrder)
            {
                (fromOrder, toOrder) = (toOrder, fromOrder);
            }

            bool lowerOk = gate.inclusiveFrom ? currentOrder >= fromOrder : currentOrder > fromOrder;
            bool upperOk = gate.inclusiveTo ? currentOrder <= toOrder : currentOrder < toOrder;
            if (!lowerOk || !upperOk) continue;

            if (gate.invokeOnce && invokedGateIndices.Contains(i)) continue;

            try
            {
                gate.eventOnMatch?.Invoke();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PhaseStepController] PhaseRangeGate '{gate.label}' 実行中に例外: {ex}");
            }

            if (gate.invokeOnce)
            {
                invokedGateIndices.Add(i);
            }
        }
    }

    private GameFlowController.Phase ResolveGateTargetPhase(GameFlowController.Phase? phaseOverride)
    {
        if (phaseOverride.HasValue) return phaseOverride.Value;

        var gameFlow = GameFlowController.instance;
        if (gameFlow && gameFlow.CurrentIndex >= 0) return gameFlow.CurrentPhase;

        return CurrentPhase;
    }

    /// <summary>
    /// フェーズ → 並び順 の対応表を作る。
    /// 優先順: phaseOrderOverride → GameFlowController.flowOrder → enum の定義順。
    /// </summary>
    private Dictionary<GameFlowController.Phase, int> BuildPhaseOrderIndex()
    {
        IReadOnlyList<GameFlowController.Phase> order;
        var gameFlow = GameFlowController.instance;

        if (phaseOrderOverride != null && phaseOrderOverride.Count > 0)
        {
            order = phaseOrderOverride;
        }
        else if (gameFlow != null && gameFlow.flowOrder != null && gameFlow.flowOrder.Count > 0)
        {
            order = gameFlow.flowOrder;
        }
        else
        {
            order = (GameFlowController.Phase[])Enum.GetValues(typeof(GameFlowController.Phase));
        }

        var orderByPhase = new Dictionary<GameFlowController.Phase, int>();
        for (int i = 0; i < order.Count; i++)
        {
            orderByPhase[order[i]] = i;
        }
        return orderByPhase;
    }

    // ===== ステップ切り替えの内部処理 =====

    private bool MoveStep(int offset)
    {
        int phaseIndex = CurrentPhaseIndex;
        if (!TryGetPhase(phaseIndex, out var phase)) return false;

        int targetIndex = CurrentStepIndex + offset;
        if (targetIndex < 0 || targetIndex >= phase.steps.Count) return false;

        return SwitchStepByIndex(phaseIndex, targetIndex);
    }

    /// <summary>
    /// 指定のステップへ入る。現在位置（CurrentPhase / CurrentStepKey）の更新、表示切り替え、
    /// イベント通知、訪問履歴の保存までをまとめて行う。
    /// </summary>
    private bool SwitchStepByIndex(int phaseIndex, int stepIndex)
    {
        if (!TryGetPhase(phaseIndex, out var phase)) return false;
        if (stepIndex < 0 || stepIndex >= phase.steps.Count) return false;

        var newStep = phase.steps[stepIndex];
        var oldStepKey = CurrentStepKey;
        int oldPhaseIndex = CurrentPhaseIndex;
        bool isSamePhase = phaseIndex == oldPhaseIndex;

        // 同じフェーズの同じステップなら何もしない
        if (isSamePhase && string.Equals(newStep.key, oldStepKey, StringComparison.Ordinal)) return false;

        // 同じフェーズ内の移動なら、ここで古いステップを抜ける
        // （フェーズをまたぐ場合の Exit は SwitchPhaseAsync 側で済んでいる）
        if (isSamePhase && TryGetStep(oldPhaseIndex, FindStepIndex(oldPhaseIndex, oldStepKey), out var oldStep))
        {
            ExitStep(oldStepKey, oldStep);
        }

        // 訪問履歴が新しいフェーズ/ステップの組で保存されるよう、先に現在位置を確定させる
        CurrentStepKey = newStep.key;
        CurrentPhase = phase.phase;

        ShowStepAndInvokeEnterEvents(newStep);

        onStepEntered?.Invoke(CurrentStepKey);
        if (!string.IsNullOrEmpty(oldStepKey))
        {
            onStepChanged?.Invoke(oldStepKey, CurrentStepKey);
        }

        MarkVisitedAndSave();

        return true;
    }

    /// <summary>現在のステップとフェーズを抜ける（フェーズをまたぐ切り替えの前半）。</summary>
    private void ExitCurrentPhase(bool invokeExitEvents)
    {
        var oldPhase = CurrentPhase;

        if (invokeExitEvents && TryGetStep(CurrentPhaseIndex, CurrentStepIndex, out var oldStep))
        {
            ExitStep(CurrentStepKey, oldStep);
        }

        if (invokeExitEvents && phases.Count > 0)
        {
            onPhaseExited?.Invoke(oldPhase);
        }

        if (hardExclusiveShowAcrossPhases) HideAllObjects();
    }

    private void ExitStep(string stepKey, StepData step)
    {
        onStepExited?.Invoke(stepKey);
        SetObjectsActive(step.objectsToShow, false);
    }

    private void ShowStepAndInvokeEnterEvents(StepData step)
    {
        SetObjectsActive(step.objectsToShow, true);
        InvokeEvents(step.onEnterEvents);
        InvokeEvents(step.onEnterEvents2);
    }

    /// <summary>
    /// フェーズに入るときの最初のステップを決める。
    /// 優先順: preferredKey → フェーズの startStepKey → 先頭。ステップが 1 つもなければ false。
    /// </summary>
    private bool TryResolveFirstStepIndex(int phaseIndex, string preferredKey, out int stepIndex)
    {
        stepIndex = -1;
        if (!TryGetPhase(phaseIndex, out var phase) || phase.steps == null || phase.steps.Count == 0) return false;

        if (!string.IsNullOrWhiteSpace(preferredKey))
        {
            stepIndex = FindStepIndex(phaseIndex, preferredKey);
        }

        if (stepIndex < 0 && !string.IsNullOrWhiteSpace(phase.startStepKey))
        {
            stepIndex = FindStepIndex(phaseIndex, phase.startStepKey);
        }

        if (stepIndex < 0) stepIndex = 0;

        stepIndex = Mathf.Clamp(stepIndex, 0, phase.steps.Count - 1);
        return true;
    }

    // ===== 訪問履歴のセーブ / ロード =====

    private void LoadVisitedHistory()
    {
        PhaseStepSaveData loadedData = saveController.LoadData<PhaseStepSaveData>(VisitedHistorySaveKey);

        visitedSteps.Clear();
        if (loadedData == null || loadedData.visitedHistory == null)
        {
            Debug.LogWarning($"[PhaseStepController] 訪問履歴セーブデータが見つかりません。");
            return;
        }

        // セーブデータではフェーズを文字列で持っているので enum に戻す
        foreach (var entry in loadedData.visitedHistory)
        {
            if (!Enum.TryParse<GameFlowController.Phase>(entry.phaseName, out var phase))
            {
                Debug.LogWarning($"[PhaseStepController] ロード失敗: 不明なPhase名 '{entry.phaseName}'");
                continue;
            }

            if (entry.visitedStepKeys != null)
            {
                visitedSteps[phase] = new HashSet<string>(entry.visitedStepKeys);
            }
        }

        Debug.Log($"[PhaseStepController] 訪問履歴をロードしました (Phase: {visitedSteps.Count}件)");
    }

    /// <summary>現在のフェーズ/ステップを訪問済みにする。初めての訪問だった場合だけセーブする。</summary>
    private void MarkVisitedAndSave()
    {
        if (saveController == null)
        {
            Debug.LogError("[PhaseStepController] SaveControllerがnullのためセーブできません。");
            return;
        }

        if (!visitedSteps.TryGetValue(CurrentPhase, out var stepKeys))
        {
            stepKeys = new HashSet<string>();
            visitedSteps[CurrentPhase] = stepKeys;
        }

        bool isNewVisit = stepKeys.Add(CurrentStepKey);
        if (!isNewVisit) return;

        var dataToSave = new PhaseStepSaveData
        {
            visitedHistory = visitedSteps.Select(pair => new PhaseHistoryEntry
            {
                phaseName = pair.Key.ToString(),
                visitedStepKeys = pair.Value.ToList()
            }).ToList()
        };

        saveController.SaveData<PhaseStepSaveData>(VisitedHistorySaveKey, dataToSave);
        Debug.Log($"[PhaseStepController] 新規訪問を保存: {CurrentPhase}::{CurrentStepKey} (履歴Phase数: {dataToSave.visitedHistory.Count}件)");
    }

    // ===== 小さなヘルパー =====

    private void HideAllObjects()
    {
        foreach (var phase in phases)
        {
            foreach (var step in phase.steps)
            {
                SetObjectsActive(step.objectsToShow, false);
            }
        }
    }

    /// <summary>リスト内のオブジェクトの表示を切り替える。自分自身（このコントローラー）は対象外。</summary>
    private void SetObjectsActive(List<GameObject> objects, bool active)
    {
        if (objects == null) return;

        foreach (var obj in objects)
        {
            if (!obj) continue;
            if (obj == gameObject) continue;
            if (obj.activeSelf != active) obj.SetActive(active);
        }
    }

    private static void InvokeEvents(List<UnityEvent> events)
    {
        if (events == null) return;

        foreach (var unityEvent in events)
        {
            unityEvent?.Invoke();
        }
    }

    private static void InvokeEvents(List<UnityEvent2> events)
    {
        if (events == null) return;

        foreach (var unityEvent in events)
        {
            unityEvent?.Invoke();
        }
    }

    private int FindPhaseIndex(GameFlowController.Phase phase)
    {
        for (int i = 0; i < phases.Count; i++)
        {
            if (phases[i].phase.Equals(phase)) return i;
        }
        return -1;
    }

    private int FindStepIndex(int phaseIndex, string stepKey)
    {
        if (phaseIndex < 0 || phaseIndex >= phases.Count) return -1;
        if (string.IsNullOrWhiteSpace(stepKey)) return -1;

        var steps = phases[phaseIndex].steps;
        for (int i = 0; i < steps.Count; i++)
        {
            if (string.Equals(steps[i].key, stepKey, StringComparison.Ordinal)) return i;
        }
        return -1;
    }

    private bool TryGetPhase(int phaseIndex, out PhaseBlock phase)
    {
        if (phaseIndex >= 0 && phaseIndex < phases.Count)
        {
            phase = phases[phaseIndex];
            return true;
        }

        phase = null;
        return false;
    }

    private bool TryGetStep(int phaseIndex, int stepIndex, out StepData step)
    {
        step = null;
        if (!TryGetPhase(phaseIndex, out var phase)) return false;
        if (stepIndex < 0 || stepIndex >= phase.steps.Count) return false;

        step = phase.steps[stepIndex];
        return true;
    }

    // ===== データ定義: フェーズ / ステップ =====

    [Serializable]
    public class PhaseBlock
    {
        [LabelText("Phase"), EnumToggleButtons] public GameFlowController.Phase phase;
        [LabelText("開始 Step Key（空なら先頭）")] public string startStepKey;

        [LabelText("Steps（順序は並び順）")]
        [ListDrawerSettings(Expanded = true, DraggableItems = true)]
        public List<StepData> steps = new();
    }

    [Serializable]
    public class StepData
    {
        [LabelText("Step Key")] public string key;

        [LabelText("表示するオブジェクト"), ListDrawerSettings(Expanded = true)]
        public List<GameObject> objectsToShow = new();

        [LabelText("OnEnter イベント（順に実行）"), ListDrawerSettings(Expanded = true)]
        public List<UnityEvent> onEnterEvents = new();
        public List<UnityEvent2> onEnterEvents2 = new();
    }

    // ===== データ定義: 範囲ゲート =====

    [Serializable]
    public class PhaseRangeGate
    {
        [LabelText("ラベル（任意）")] public string label;

        [LabelText("開始フェーズ")] public GameFlowController.Phase fromPhase = GameFlowController.Phase.HomeSearch;
        [LabelText("終了フェーズ")] public GameFlowController.Phase toPhase = GameFlowController.Phase.Escape;

        [LabelText("開始を含む")] public bool inclusiveFrom = true;
        [LabelText("終了を含む")] public bool inclusiveTo = true;

        [LabelText("一致時イベント")] public UnityEvent eventOnMatch;

        [LabelText("一度だけ実行")] public bool invokeOnce = true;

        [Tooltip("trueなら、フェーズが変更されるたびにこのゲートの条件を再評価します。")]
        [LabelText("フェーズ変更時にも評価")]
        public bool evaluateOnPhaseChange = false;
    }

    // ===== データ定義: セーブデータ =====

    /// <summary>
    /// 1 フェーズ分の訪問履歴。
    /// 例: {"phaseName": "Morning", "visitedStepKeys": ["1", "2 Gohan", "3"]}
    /// </summary>
    [Serializable]
    public class PhaseHistoryEntry
    {
        /// <summary>フェーズ名（enum の並び替えに強いよう、数値ではなく文字列で保存する）。</summary>
        public string phaseName;
        public List<string> visitedStepKeys;
    }

    /// <summary>JSON として保存するセーブデータ本体。</summary>
    [Serializable]
    public class PhaseStepSaveData
    {
        public List<PhaseHistoryEntry> visitedHistory = new List<PhaseHistoryEntry>();
    }

    // ===== データ定義: イベント型 =====

    [Serializable] public class UnityEventPhase   : UnityEvent2<GameFlowController.Phase> {}
    [Serializable] public class UnityEventPhase2  : UnityEvent2<GameFlowController.Phase, GameFlowController.Phase> {}
    [Serializable] public class UnityEventString  : UnityEvent2<string> {}
    [Serializable] public class UnityEventString2 : UnityEvent2<string, string> {}
}

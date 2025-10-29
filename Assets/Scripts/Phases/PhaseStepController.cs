using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Sirenix.OdinInspector;
using Cysharp.Threading.Tasks;
using System.Threading;
using System.Linq; 

[DisallowMultipleComponent]
public class PhaseStepController : Singleton<PhaseStepController>
{
    private const string SAVE_KEY = "PhaseStepController_VisitedHistory";

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

    // ─────────────────────────────────────────────────────────────────────────────
    // 範囲ゲート (変更なし)
    // ─────────────────────────────────────────────────────────────────────────────
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

    private readonly HashSet<int> _invokedGateIndices = new();

    // ─────────────────────────────────────────────────────────────────────────────

    [Title("Events")]
    public UnityEventPhase   onPhaseEntered;
    public UnityEventPhase   onPhaseExited;
    public UnityEventPhase2  onPhaseChanged;
    public UnityEventString  onStepEntered;
    public UnityEventString  onStepExited;
    public UnityEventString2 onStepChanged;

    [ShowInInspector] public GameFlowController.Phase CurrentPhase;
    [ShowInInspector] public string CurrentStepKey;

    public int CurrentPhaseIndex => FindPhaseIndex(CurrentPhase);
    public int CurrentStepIndex  => FindStepIndex(CurrentPhaseIndex, CurrentStepKey);

    // 内部状態
    private bool _subscribed;
    private bool _autoStarted;
    private DataShareController dataShareController;
    private SaveDataBaseController saveController; 
    
    // ★ 実行時データ構造 (変更なし)
    private Dictionary<GameFlowController.Phase, HashSet<string>> _visitedHistoryRuntime = 
        new Dictionary<GameFlowController.Phase, HashSet<string>>();

    // ===== Unity =====

    private void OnDisable()
    {
        if (TryGetStep(CurrentPhaseIndex, CurrentStepIndex, out var step))
            SetActiveList(step.objectsToShow, false, this.gameObject);

        if (GameFlowController.instance)
            GameFlowController.instance.PhaseChanged -= OnGameFlowPhaseChanged;

        _subscribed = false;
        _autoStarted = false;
    }

    void Start()
    {
        dataShareController = DataShareController.instance;
        saveController = SaveDataBaseController.instance; 

        onStepEntered.AddListener((step) =>
        {
            dataShareController.CurrentPhaseStep = step;
        });
        
        if (hideAllObjectsOnStart) HideAllObjects();
        InitializeOnEnableAsync().Forget();
    }

    void OnDestroy()
    {
        onStepEntered.RemoveAllListeners();
    }
    
    // ★ 変更：InitializeOnEnableAsync (string -> enum への変換を追加)
    private async UniTask InitializeOnEnableAsync()
    {
        var ct = this.GetCancellationTokenOnDestroy(); 

        await UniTask.NextFrame(ct);
        TrySubscribeGameFlow();

        if (autoStartOnEnable && !_autoStarted)
        {
            PhaseStepSaveData loadedData = saveController.LoadData<PhaseStepSaveData>(SAVE_KEY);

            _visitedHistoryRuntime.Clear();
            if (loadedData != null && loadedData.visitedHistory != null)
            {
                // ロードしたリスト (string) を実行時データ (enum) に変換
                foreach (var entry in loadedData.visitedHistory)
                {
                    // ★ string -> enum への変換
                    if (Enum.TryParse<GameFlowController.Phase>(entry.phaseName, out var phaseEnum))
                    {
                        if (entry.visitedStepKeys != null)
                        {
                            _visitedHistoryRuntime[phaseEnum] = new HashSet<string>(entry.visitedStepKeys);
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"[PhaseStepController] ロード失敗: 不明なPhase名 '{entry.phaseName}'");
                    }
                }
                Debug.Log($"[PhaseStepController] 訪問履歴をロードしました (Phase: {_visitedHistoryRuntime.Count}件)");
            }
            else
            {
                Debug.LogWarning($"[PhaseStepController] 訪問履歴セーブデータが見つかりません。");
            }
            
            var initialPhase = dataShareController.CurrentPhase;
            var initialStepKey = dataShareController.CurrentPhaseStep;
            Debug.Log($"[PhaseStepController] DataShareController から初期位置を取得: Phase={initialPhase}, Step={initialStepKey}");
            
            await ForceSwitchStateOnInit(initialPhase, initialStepKey,ct);

            _autoStarted = true;
        }

        if (enablePhaseRangeGates && evaluateRangeOnEnable)
        {
            EvaluatePhaseRangeGates(phaseOverride: null, isFromPhaseChange: false);
        }
    }
    
    private async UniTask<bool> ForceSwitchStateOnInit(GameFlowController.Phase phase, string stepKey, CancellationToken cancellationToken)
    {
        int pi = FindPhaseIndex(phase);
        if (pi < 0)
        {
            Debug.LogError($"[PhaseStepController] Initial phase '{phase}' not found in configuration!");
            return false;
        }

        int si = FindStepIndex(pi, stepKey);
        if (si < 0)
        {
            Debug.LogWarning($"[PhaseStepController] Initial stepKey '{stepKey}' not found. Falling back to the first step of phase '{phase}'.");
            if (!TryResolveFirstStepIndex(pi, null, out si))
            {
                Debug.LogError($"[PhaseStepController] Phase '{phase}' has no steps to initialize to!");
                return false;
            }
        }
        
        CurrentPhase = phase;
        CurrentStepKey = phases[pi].steps[si].key;

        if (hideAllObjectsOnStart)
        {
            HideAllObjects();
            await UniTask.Yield(cancellationToken); 
        }
         cancellationToken.ThrowIfCancellationRequested();

        if (TryGetStep(pi, si, out var stepData))
        {
            SetActiveList(stepData.objectsToShow, true, this.gameObject);
            InvokeEvents(stepData.onEnterEvents);
            InvokeEvents(stepData.onEnterEvents2);
        }

        onPhaseEntered?.Invoke(CurrentPhase);
        onStepEntered?.Invoke(CurrentStepKey);

        MarkVisitedAndSave();

        return true;
    }


    private void TrySubscribeGameFlow()
    {
        if (_subscribed) return;
        if (!GameFlowController.instance) return;

        GameFlowController.instance.PhaseChanged += OnGameFlowPhaseChanged;
        _subscribed = true;
    }

    private void OnGameFlowPhaseChanged(GameFlowController.Phase oldP, GameFlowController.Phase newP)
    {
        SwitchPhaseAsync(newP, null, invokeOldExit: true).Forget();

        if (enablePhaseRangeGates && evaluateRangeOnPhaseChanged)
            EvaluatePhaseRangeGates(newP, isFromPhaseChange: true); 
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // 範囲ゲート：評価ロジック (変更なし)
    // ─────────────────────────────────────────────────────────────────────────────
    public void EvaluatePhaseRangeGates(GameFlowController.Phase? phaseOverride = null, bool isFromPhaseChange = false)
    {
        if (!enablePhaseRangeGates || phaseRangeGates == null || phaseRangeGates.Count == 0)
            return;

        var phase = phaseOverride ?? (GameFlowController.instance && GameFlowController.instance.CurrentIndex >= 0
                     ? GameFlowController.instance.CurrentPhase
                     : CurrentPhase);

        var orderMap = BuildPhaseOrderIndex();

        if (!orderMap.TryGetValue(phase, out int curIdx))
        {
            Debug.LogWarning($"[PhaseStepController] Phase order not defined for '{phase}'");
            return;
        }

        for (int i = 0; i < phaseRangeGates.Count; i++)
        {
            var gate = phaseRangeGates[i];
            if (isFromPhaseChange && !gate.evaluateOnPhaseChange)
            {
                continue;
            }
            if (!orderMap.TryGetValue(gate.fromPhase, out int fromIdx)) continue;
            if (!orderMap.TryGetValue(gate.toPhase, out int toIdx)) continue;

            if (fromIdx > toIdx)
            {
                var t = fromIdx; fromIdx = toIdx; toIdx = t;
            }

            bool lowerOk = gate.inclusiveFrom ? curIdx >= fromIdx : curIdx > fromIdx;
            bool upperOk = gate.inclusiveTo   ? curIdx <= toIdx   : curIdx < toIdx;

            if (lowerOk && upperOk)
            {
                if (gate.invokeOnce && _invokedGateIndices.Contains(i))
                    continue;

                try
                {
                    gate.eventOnMatch?.Invoke();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[PhaseStepController] PhaseRangeGate '{gate.label}' 実行中に例外: {ex}");
                }

                if (gate.invokeOnce)
                    _invokedGateIndices.Add(i);
            }
        }
    }

    private Dictionary<GameFlowController.Phase, int> BuildPhaseOrderIndex()
    {
        var map = new Dictionary<GameFlowController.Phase, int>();

        if (phaseOrderOverride != null && phaseOrderOverride.Count > 0)
        {
            for (int i = 0; i < phaseOrderOverride.Count; i++)
                map[phaseOrderOverride[i]] = i;
            return map;
        }

        var gfc = GameFlowController.instance;
        if (gfc != null && gfc.flowOrder != null && gfc.flowOrder.Count > 0)
        {
            for (int i = 0; i < gfc.flowOrder.Count; i++)
                map[gfc.flowOrder[i]] = i;
            return map;
        }

        var values = (GameFlowController.Phase[])Enum.GetValues(typeof(GameFlowController.Phase));
        for (int i = 0; i < values.Length; i++)
            map[values[i]] = i;

        return map;
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Public API (変更なし)
    // ─────────────────────────────────────────────────────────────────────────────

    public bool HasVisited(GameFlowController.Phase phase, string stepKey)
    {
        if (_visitedHistoryRuntime.TryGetValue(phase, out var stepSet))
        {
            return stepSet.Contains(stepKey);
        }
        return false;
    }

    [Button("UnityEvent → NextStep")]
    public void NextStepEvent() => NextStep();

    [Button("UnityEvent → PrevStep")]
    public void PrevStepEvent() => PrevStep();

    [Button("次のStep →")]
    public bool NextStep()
    {
        int pi = CurrentPhaseIndex;
        if (!TryGetPhase(pi, out var phase)) return false;

        int nextSi = CurrentStepIndex + 1;
        if (nextSi < 0 || nextSi >= phase.steps.Count) return false;
        return SwitchStepByIndex(pi, nextSi);
    }

    [Button("← 前のStep")]
    public bool PrevStep()
    {
        int pi = CurrentPhaseIndex;
        if (!TryGetPhase(pi, out var phase)) return false;

        int prevSi = CurrentStepIndex - 1;
        if (prevSi < 0 || prevSi >= phase.steps.Count) return false;
        return SwitchStepByIndex(pi, prevSi);
    }

    public bool SwitchStep(string stepKey)
    {
        if (string.IsNullOrWhiteSpace(stepKey)) return false;
        if (string.Equals(stepKey, CurrentStepKey, StringComparison.Ordinal)) return false;

        int pi = CurrentPhaseIndex;
        if (pi < 0) return false;
        int si = FindStepIndex(pi, stepKey);
        if (si < 0) return false;
        return SwitchStepByIndex(pi, si);
    }

    public async UniTask<bool> SwitchPhaseAsync(GameFlowController.Phase phase, string firstStepKey = null, bool invokeOldExit = true)
    {
        int newPi = FindPhaseIndex(phase);
        if (newPi < 0) { Debug.LogWarning($"[PhaseStepController] Phase '{phase}' not found in list."); return false; }

        if (Equals(phase, CurrentPhase))
        {
            if (!TryResolveFirstStepIndex(newPi, firstStepKey, out var si2))
            {
                if (string.IsNullOrWhiteSpace(CurrentStepKey))
                {
                    if (!TryResolveFirstStepIndex(newPi, null, out si2)) return false;
                }
                else
                {
                    return false;
                }
            }
            return SwitchStepByIndex(newPi, si2);
        }

        var oldPhase = CurrentPhase;
        var oldStep  = CurrentStepKey;

        if (invokeOldExit && TryGetStep(CurrentPhaseIndex, CurrentStepIndex, out var oldStepData))
        {
            onStepExited?.Invoke(oldStep);
            SetActiveList(oldStepData.objectsToShow, false, this.gameObject);
        }
        if (invokeOldExit && phases.Count > 0)
            onPhaseExited?.Invoke(oldPhase);

        CurrentPhase = phases[newPi].phase;

        if (hardExclusiveShowAcrossPhases) HideAllObjects();

        if (!TryResolveFirstStepIndex(newPi, firstStepKey, out var si))
            return false;

        await UniTask.Yield();

        onPhaseEntered?.Invoke(CurrentPhase);
        onPhaseChanged?.Invoke(oldPhase, CurrentPhase);

        return SwitchStepByIndex(newPi, si);
    }
    public void SwitchPhase(GameFlowController.Phase phase, string firstStepKey = null, bool invokeOldExit = true)
        => SwitchPhaseAsync(phase, firstStepKey, invokeOldExit).Forget();

    public UniTask<bool> SwitchAsync(GameFlowController.Phase phase, string stepKey)
    {
        if (Equals(phase, CurrentPhase) && string.Equals(stepKey, CurrentStepKey, StringComparison.Ordinal))
            return UniTask.FromResult(false);

        int pi = FindPhaseIndex(phase);
        if (pi < 0) return UniTask.FromResult(false);
        int si = FindStepIndex(pi, stepKey);
        if (si < 0) return UniTask.FromResult(false);

        return SwitchPhaseAsync(phase, stepKey);
    }

    public void Switch(GameFlowController.Phase phase, string stepKey)
        => SwitchAsync(phase, stepKey).Forget();

    // ===== Internals =====

    // ★ 変更：MarkVisitedAndSave (enum -> string への変換を追加)
    private void MarkVisitedAndSave()
    {
        if (saveController == null)
        {
            Debug.LogError("[PhaseStepController] SaveControllerがnullのためセーブできません。");
            return;
        }
        
        if (!_visitedHistoryRuntime.TryGetValue(CurrentPhase, out var stepSet))
        {
            stepSet = new HashSet<string>();
            _visitedHistoryRuntime[CurrentPhase] = stepSet;
        }
        
        bool isNewVisit = stepSet.Add(CurrentStepKey);

        if (!isNewVisit)
        {
            return;
        }

        // ★ 実行時データ(enum) から セーブ用リスト(string) に変換
        var historyListForSave = _visitedHistoryRuntime.Select(pair => new PhaseHistoryEntry
        {
            phaseName = pair.Key.ToString(), // ★ enum -> string に変換
            visitedStepKeys = pair.Value.ToList() 
        }).ToList();

        PhaseStepSaveData dataToSave = new PhaseStepSaveData
        {
            visitedHistory = historyListForSave
        };
        
        saveController.SaveData<PhaseStepSaveData>(SAVE_KEY, dataToSave);
        Debug.Log($"[PhaseStepController] 新規訪問を保存: {CurrentPhase}::{CurrentStepKey} (履歴Phase数: {dataToSave.visitedHistory.Count}件)");
    }

    private bool TryResolveFirstStepIndex(int phaseIndex, string preferredKey, out int stepIndex)
    {
        stepIndex = -1;
        if (!TryGetPhase(phaseIndex, out var ph) || ph.steps == null || ph.steps.Count == 0)
            return false;

        if (!string.IsNullOrWhiteSpace(preferredKey))
            stepIndex = FindStepIndex(phaseIndex, preferredKey);

        if (stepIndex < 0 && !string.IsNullOrWhiteSpace(ph.startStepKey))
            stepIndex = FindStepIndex(phaseIndex, ph.startStepKey);

        if (stepIndex < 0) stepIndex = 0;

        stepIndex = Mathf.Clamp(stepIndex, 0, ph.steps.Count - 1);
        return true;
    }

    private bool SwitchStepByIndex(int phaseIndex, int stepIndex)
    {
        if (!TryGetPhase(phaseIndex, out var phase)) return false;
        if (stepIndex < 0 || stepIndex >= phase.steps.Count) return false;

        var newStep = phase.steps[stepIndex];
        if (string.Equals(newStep.key, CurrentStepKey, StringComparison.Ordinal) &&
            phaseIndex == CurrentPhaseIndex)
            return false;

        var oldStepKey = CurrentStepKey;

        if (TryGetStep(CurrentPhaseIndex, CurrentStepIndex, out var oldStep))
        {
            onStepExited?.Invoke(oldStepKey);
            SetActiveList(oldStep.objectsToShow, false, this.gameObject);
        }

        CurrentStepKey = newStep.key;

        SetActiveList(newStep.objectsToShow, true, this.gameObject);
        InvokeEvents(newStep.onEnterEvents);
        InvokeEvents(newStep.onEnterEvents2);
        
        onStepEntered?.Invoke(CurrentStepKey);
        if (!string.IsNullOrEmpty(oldStepKey))
            onStepChanged?.Invoke(oldStepKey, CurrentStepKey);

        MarkVisitedAndSave();

        return true;
    }

    private void HideAllObjects()
    {
        foreach (var ph in phases)
            foreach (var st in ph.steps)
                SetActiveList(st.objectsToShow, false, this.gameObject);
    }

    private static void SetActiveList(List<GameObject> list, bool active, GameObject self = null)
    {
        if (list == null) return;
        foreach (var go in list)
        {
            if (!go) continue;
            if (self && go == self) continue; 
            if (go.activeSelf != active) go.SetActive(active);
        }
    }

    private static void InvokeEvents(List<UnityEvent> list)
    {
        if (list == null) return;
        foreach (var ev in list)
            ev?.Invoke();
    }
    private static void InvokeEvents(List<UnityEvent2> list)
    {
        if (list == null) return;
        foreach (var ev in list)
            ev?.Invoke();
    }
    private int FindPhaseIndex(GameFlowController.Phase phase)
    {
        for (int i = 0; i < phases.Count; i++)
            if (phases[i].phase.Equals(phase)) return i;
        return -1;
    }

    private int FindStepIndex(int phaseIndex, string stepKey)
    {
        if (phaseIndex < 0 || phaseIndex >= phases.Count) return -1;
        if (string.IsNullOrWhiteSpace(stepKey)) return -1;
        var list = phases[phaseIndex].steps;
        for (int i = 0; i < list.Count; i++)
            if (string.Equals(list[i].key, stepKey, StringComparison.Ordinal)) return i;
        return -1;
    }

    private bool TryGetPhase(int phaseIndex, out PhaseBlock phase)
    {
        if (phaseIndex >= 0 && phaseIndex < phases.Count) { phase = phases[phaseIndex]; return true; }
        phase = null; return false;
    }

    private bool TryGetStep(int phaseIndex, int stepIndex, out StepData step)
    {
        step = null;
        if (!TryGetPhase(phaseIndex, out var ph)) return false;
        if (stepIndex < 0 || stepIndex >= ph.steps.Count) return false;
        step = ph.steps[stepIndex]; return true;
    }

    // ===== データ定義 =====

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

    // ─────────────────────────────────────────────────────────────────────────────
    // 範囲ゲート用データ (変更なし)
    // ─────────────────────────────────────────────────────────────────────────────
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

    // ★★★ 全面的に変更 ★★★
    
    /// <summary>
    /// ご要望のデータ構造：訪問したステップキーのリストをフェーズごとに保持
    /// (例: {"phaseName" : "Morning", "visitedStepKeys" : ["1", "2 Gohan", "3"]})
    /// </summary>
    [Serializable]
    public class PhaseHistoryEntry
    {
        // ★ enum ではなく string で保存
        public string phaseName; 
        public List<string> visitedStepKeys; // このPhaseで訪問済みのStepKeyリスト
    }

    /// <summary>
    /// JSONとして保存するセーブデータのコンテナ
    /// </summary>
    [Serializable]
    public class PhaseStepSaveData
    {
        // 訪問履歴のリスト (これだけ保存すればOK)
        public List<PhaseHistoryEntry> visitedHistory = new List<PhaseHistoryEntry>();
    }

    // ★★★ 変更ここまで ★★★

    // ===== UnityEvent ジェネリック（外部定義の UnityEvent2 を利用） =====
    [Serializable] public class UnityEventPhase   : UnityEvent2<GameFlowController.Phase> {}
    [Serializable] public class UnityEventPhase2  : UnityEvent2<GameFlowController.Phase, GameFlowController.Phase> {}
    [Serializable] public class UnityEventString  : UnityEvent2<string> {}
    [Serializable] public class UnityEventString2 : UnityEvent2<string, string> {}
}
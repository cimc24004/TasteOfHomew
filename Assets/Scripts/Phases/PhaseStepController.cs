using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Sirenix.OdinInspector;
using Cysharp.Threading.Tasks;

[DisallowMultipleComponent]
public class PhaseStepController : SerializedMonoBehaviour
{
    [Title("Phases（親：フェーズ / 子：ステップ）")]
    [ListDrawerSettings(Expanded = true, DraggableItems = true)]
    public List<PhaseBlock> phases = new();

    [Title("起動設定")]
    [LabelText("OnEnableで開始")] public bool autoStartOnEnable = false;
    [LabelText("開始フェーズ")] public GameFlowController.Phase startPhase = GameFlowController.Phase.Morning;
    [LabelText("開始ステップ Key（空なら先頭）")] public string startStepKey;

    [Title("表示制御オプション")]
    [LabelText("開始時に全Stepオブジェクトを非表示にする")]
    public bool hideAllObjectsOnStart = true;

    [LabelText("フェーズ跨ぎで他フェーズの表示物を全消しする")]
    public bool hardExclusiveShowAcrossPhases = true;

    // ─────────────────────────────────────────────────────────────────────────────
    // 追加：フェーズ範囲ゲート
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

    // 実行済みゲートを記録（InvokeOnce 用）
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

    // ===== Unity =====
    private void OnEnable()
    {
        if (hideAllObjectsOnStart) HideAllObjects();
        InitializeOnEnableAsync().Forget();
    }

    private void OnDisable()
    {
        if (TryGetStep(CurrentPhaseIndex, CurrentStepIndex, out var step))
            SetActiveList(step.objectsToShow, false, this.gameObject);

        if (GameFlowController.instance)
            GameFlowController.instance.PhaseChanged -= OnGameFlowPhaseChanged;

        _subscribed = false;
        _autoStarted = false;
        // ゲートの InvokeOnce 状態は保持（必要ならここで _invokedGateIndices.Clear() してリセット運用に）
    }

    private async UniTask InitializeOnEnableAsync()
    {
        // 1フレ待って他シングルトンの初期化を待つ
        await UniTask.NextFrame();

        TrySubscribeGameFlow();

        // autoStart
        if (autoStartOnEnable && phases.Count > 0 && !_autoStarted)
        {
            var gfc = GameFlowController.instance;
            if (gfc && gfc.CurrentIndex >= 0)
            {
                await SwitchPhaseAsync(gfc.CurrentPhase, null, invokeOldExit: false);
            }
            else
            {
                var has = FindPhaseIndex(startPhase) >= 0;
                var phaseToUse = has ? startPhase : phases[0].phase;
                await SwitchPhaseAsync(phaseToUse, startStepKey, invokeOldExit: false);
            }
            _autoStarted = true;
        }

        // 起動時の範囲評価
        if (enablePhaseRangeGates && evaluateRangeOnEnable)
            EvaluatePhaseRangeGates();
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
        // Stepの初期キーはPhaseBlock.startStepKeyを使う（null指定で内部ロジック任せ）
        SwitchPhaseAsync(newP, null, invokeOldExit: true).Forget();

        if (enablePhaseRangeGates && evaluateRangeOnPhaseChanged)
            EvaluatePhaseRangeGates(newP);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // 範囲ゲート：評価ロジック
    // ─────────────────────────────────────────────────────────────────────────────
    /// <summary>現在フェーズ（引数なし時は GameFlow / CurrentPhase）で範囲ゲートを評価し、該当イベントを実行</summary>
    public void EvaluatePhaseRangeGates(GameFlowController.Phase? phaseOverride = null)
    {
        if (!enablePhaseRangeGates || phaseRangeGates == null || phaseRangeGates.Count == 0)
            return;

        var phase = phaseOverride ?? (GameFlowController.instance && GameFlowController.instance.CurrentIndex >= 0
                    ? GameFlowController.instance.CurrentPhase
                    : CurrentPhase);

        // 並び順のマップを作成
        var orderMap = BuildPhaseOrderIndex();

        // 不明な並びは弾く
        if (!orderMap.TryGetValue(phase, out int curIdx))
        {
            Debug.LogWarning($"[PhaseStepController] Phase order not defined for '{phase}'");
            return;
        }

        for (int i = 0; i < phaseRangeGates.Count; i++)
        {
            var gate = phaseRangeGates[i];

            // 開始/終了のインデックスが取れない場合はスキップ
            if (!orderMap.TryGetValue(gate.fromPhase, out int fromIdx)) continue;
            if (!orderMap.TryGetValue(gate.toPhase, out int toIdx)) continue;

            // from > to の場合は自動で入れ替え（運用ミス防止）
            if (fromIdx > toIdx)
            {
                var t = fromIdx; fromIdx = toIdx; toIdx = t;
            }

            // 境界の包含/非包含
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

    /// <summary>フェーズの並び順インデックスを構築（Override → GameFlow → enum）</summary>
    private Dictionary<GameFlowController.Phase, int> BuildPhaseOrderIndex()
    {
        var map = new Dictionary<GameFlowController.Phase, int>();

        // 1) 明示オーバーライド
        if (phaseOrderOverride != null && phaseOrderOverride.Count > 0)
        {
            for (int i = 0; i < phaseOrderOverride.Count; i++)
                map[phaseOrderOverride[i]] = i;
            return map;
        }

        // 2) GameFlowController の flowOrder
        var gfc = GameFlowController.instance;
        if (gfc != null && gfc.flowOrder != null && gfc.flowOrder.Count > 0)
        {
            for (int i = 0; i < gfc.flowOrder.Count; i++)
                map[gfc.flowOrder[i]] = i;
            return map;
        }

        // 3) enum の宣言順
        var values = (GameFlowController.Phase[])Enum.GetValues(typeof(GameFlowController.Phase));
        for (int i = 0; i < values.Length; i++)
            map[values[i]] = i;

        return map;
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Public API（従来）
    // ─────────────────────────────────────────────────────────────────────────────

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
        if (newPi < 0) { Debug.LogWarning($"Phase '{phase}' not found in list."); return false; }

        if (Equals(phase, CurrentPhase))
        {
            if (string.IsNullOrWhiteSpace(CurrentStepKey))
            {
                if (!TryResolveFirstStepIndex(newPi, firstStepKey, out var siInit))
                    return false;
                return SwitchStepByIndex(newPi, siInit);
            }

            if (string.IsNullOrWhiteSpace(firstStepKey) ||
                string.Equals(firstStepKey, CurrentStepKey, StringComparison.Ordinal))
                return false;

            int siSame = FindStepIndex(newPi, firstStepKey);
            if (siSame < 0) return false;
            return SwitchStepByIndex(newPi, siSame);
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

        onStepEntered?.Invoke(CurrentStepKey);
        if (!string.IsNullOrEmpty(oldStepKey))
            onStepChanged?.Invoke(oldStepKey, CurrentStepKey);

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
            if (self && go == self) continue; // 自分自身は触らない
            if (go.activeSelf != active) go.SetActive(active);
        }
    }

    private static void InvokeEvents(List<UnityEvent> list)
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
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // 範囲ゲート用データ
    // ─────────────────────────────────────────────────────────────────────────────
    [Serializable]
    public class PhaseRangeGate
    {
        [LabelText("ラベル（任意）")] public string label;

        [LabelText("開始フェーズ")] public GameFlowController.Phase fromPhase = GameFlowController.Phase.HomeSearch;
        [LabelText("終了フェーズ")] public GameFlowController.Phase toPhase   = GameFlowController.Phase.Escape;

        [LabelText("開始を含む")] public bool inclusiveFrom = true;
        [LabelText("終了を含む")] public bool inclusiveTo   = true;

        [LabelText("一致時イベント")] public UnityEvent eventOnMatch;

        [LabelText("一度だけ実行")] public bool invokeOnce = true;
    }

    // ===== UnityEvent ジェネリック（外部定義の UnityEvent2 を利用） =====
    [Serializable] public class UnityEventPhase   : UnityEvent2<GameFlowController.Phase> {}
    [Serializable] public class UnityEventPhase2  : UnityEvent2<GameFlowController.Phase, GameFlowController.Phase> {}
    [Serializable] public class UnityEventString  : UnityEvent2<string> {}
    [Serializable] public class UnityEventString2 : UnityEvent2<string, string> {}
}

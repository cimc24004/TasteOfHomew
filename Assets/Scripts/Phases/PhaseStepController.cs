using System;
using System.Collections.Generic;
using System.Threading;
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

    [Title("Events")]
    public UnityEventPhase   onPhaseEntered;
    public UnityEventPhase   onPhaseExited;
    public UnityEventPhase2  onPhaseChanged;
    public UnityEventString  onStepEntered;
    public UnityEventString  onStepExited;
    public UnityEventString2 onStepChanged;

    [ShowInInspector, ReadOnly] public GameFlowController.Phase CurrentPhase { get; private set; }
    [ShowInInspector, ReadOnly] public string CurrentStepKey { get; private set; } = null;

    public int CurrentPhaseIndex => FindPhaseIndex(CurrentPhase);
    public int CurrentStepIndex  => FindStepIndex(CurrentPhaseIndex, CurrentStepKey);

    // GameFlow購読管理
    private bool _subscribedToGameFlow = false;
    private CancellationTokenSource _cts;

    // ===== Unity =====
    void OnEnable()
    {
        // 先に全部消す（起動時に表示が消されるレース対策）
        if (hideAllObjectsOnStart) HideAllObjects();

        _cts = new CancellationTokenSource();
        SubscribeWhenGameFlowReady(_cts.Token).Forget();
    }

    void Start()
    {
        // ここではもう HideAllObjects() を呼ばない（初期表示を潰さないため）
        // GameFlow が居ないケース用のローカル自動開始（フォールバック）
        if (!_subscribedToGameFlow && autoStartOnEnable && phases.Count > 0)
        {
            var has = FindPhaseIndex(startPhase) >= 0;
            var phaseToUse = has ? startPhase : phases[0].phase;
            SwitchPhaseAsync(phaseToUse, startStepKey, invokeOldExit: false).Forget();
        }
    }

    void OnDisable()
    {
        // 現在Stepだけは片付ける（アクティブなまま残らないように）
        if (TryGetStep(CurrentPhaseIndex, CurrentStepIndex, out var step))
            SetActiveList(step.objectsToShow, false);

        TryUnsubscribeFromGameFlow();
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }

    // ===== GameFlow購読（複数インスタンス対応） =====
    private async UniTaskVoid SubscribeWhenGameFlowReady(CancellationToken token)
    {
        // GameFlow の生成待ち（生成済みなら即進む）
        while (!token.IsCancellationRequested && GameFlowController.instance == null)
            await UniTask.Yield(token);

        if (token.IsCancellationRequested) return;

        TrySubscribeToGameFlow();

        // 初回同期：GameFlow の現在フェーズに合わせて *即* 表示（待ちフレ無し）
        var current = GameFlowController.instance.CurrentPhase;
        SwitchPhaseAsync(current, null, invokeOldExit: false).Forget();
    }

    private void TrySubscribeToGameFlow()
    {
        if (_subscribedToGameFlow) return;
        if (GameFlowController.instance == null) return;

        GameFlowController.instance.PhaseChanged += OnGameFlowPhaseChanged;
        _subscribedToGameFlow = true;
    }

    private void TryUnsubscribeFromGameFlow()
    {
        if (!_subscribedToGameFlow) return;
        if (GameFlowController.instance != null)
            GameFlowController.instance.PhaseChanged -= OnGameFlowPhaseChanged;
        _subscribedToGameFlow = false;
    }

    // GameFlowController のイベントから呼ばれる
    public void OnGameFlowPhaseChanged(GameFlowController.Phase oldPhase, GameFlowController.Phase newPhase)
    {
        // newPhase の開始Step（startStepKey or 先頭）を即表示
        SwitchPhaseAsync(newPhase, null, invokeOldExit: true).Forget();
    }

    // ===== Public API =====

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

    /// <summary>同一フェーズ内で Step をキー指定で切替。現在と同じなら何もしない。</summary>
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

    /// <summary>フェーズ切替（非同期・戻り値あり）</summary>
    public async UniTask<bool> SwitchPhaseAsync(GameFlowController.Phase phase, string firstStepKey = null, bool invokeOldExit = true)
    {
        int newPi = FindPhaseIndex(phase);
        if (newPi < 0) { Debug.LogWarning($"Phase '{phase}' not found in list."); return false; }

        // === 同一フェーズ内での初期/指定Step決定 ===
        if (Equals(phase, CurrentPhase))
        {
            if (string.IsNullOrWhiteSpace(CurrentStepKey))
            {
                int siInit;
                if (!string.IsNullOrWhiteSpace(firstStepKey))
                {
                    siInit = FindStepIndex(newPi, firstStepKey);
                    if (siInit < 0) return false;
                }
                else if (!string.IsNullOrWhiteSpace(phases[newPi].startStepKey))
                {
                    siInit = FindStepIndex(newPi, phases[newPi].startStepKey);
                    if (siInit < 0) siInit = 0;
                }
                else
                {
                    siInit = 0;
                }
                return SwitchStepByIndex(newPi, Mathf.Clamp(siInit, 0, Mathf.Max(0, phases[newPi].steps.Count - 1)));
            }

            if (string.IsNullOrWhiteSpace(firstStepKey) ||
                string.Equals(firstStepKey, CurrentStepKey, StringComparison.Ordinal))
                return false;

            int siSame = FindStepIndex(newPi, firstStepKey);
            if (siSame < 0) return false;
            return SwitchStepByIndex(newPi, siSame);
        }

        // === フェーズが変わる場合 ===
        var oldPhase = CurrentPhase;
        var oldStep  = CurrentStepKey;

        if (invokeOldExit && TryGetStep(CurrentPhaseIndex, CurrentStepIndex, out var oldStepData))
        {
            onStepExited?.Invoke(oldStep);
            SetActiveList(oldStepData.objectsToShow, false);
        }
        if (invokeOldExit && phases.Count > 0)
            onPhaseExited?.Invoke(oldPhase);

        CurrentPhase = phases[newPi].phase;

        if (hardExclusiveShowAcrossPhases) HideAllObjects();

        int si;
        if (!string.IsNullOrWhiteSpace(firstStepKey))
            si = FindStepIndex(newPi, firstStepKey);
        else if (!string.IsNullOrWhiteSpace(phases[newPi].startStepKey))
            si = FindStepIndex(newPi, phases[newPi].startStepKey);
        else
            si = 0;

        // ※ 初期レース対策：待ちフレは無しで即Enter（以前の1フレ待ちを撤廃）
        onPhaseEntered?.Invoke(CurrentPhase);
        onPhaseChanged?.Invoke(oldPhase, CurrentPhase);

        return SwitchStepByIndex(newPi, Mathf.Clamp(si, 0, Mathf.Max(0, phases[newPi].steps.Count - 1)));
    }

    /// <summary>互換用：戻り値不要のFire-and-forget</summary>
    public void SwitchPhase(GameFlowController.Phase phase, string firstStepKey = null, bool invokeOldExit = true)
        => SwitchPhaseAsync(phase, firstStepKey, invokeOldExit).Forget();

    /// <summary>フェーズ＋ステップをキー指定で切替（非同期・戻り値あり）</summary>
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

    /// <summary>互換用：戻り値不要のFire-and-forget</summary>
    public void Switch(GameFlowController.Phase phase, string stepKey)
        => SwitchAsync(phase, stepKey).Forget();

    // ===== Internals =====

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
            SetActiveList(oldStep.objectsToShow, false);
        }

        CurrentStepKey = newStep.key;

        SetActiveList(newStep.objectsToShow, true);
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
                SetActiveList(st.objectsToShow, false);
    }

    private static void SetActiveList(List<GameObject> list, bool active)
    {
        if (list == null) return;
        foreach (var go in list)
            if (go && go.activeSelf != active) go.SetActive(active);
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

    // プロジェクトの UnityEvent2<> を想定
    [Serializable] public class UnityEventPhase   : UnityEvent2<GameFlowController.Phase> {}
    [Serializable] public class UnityEventPhase2  : UnityEvent2<GameFlowController.Phase, GameFlowController.Phase> {}
    [Serializable] public class UnityEventString  : UnityEvent2<string> {}
    [Serializable] public class UnityEventString2 : UnityEvent2<string, string> {}
}

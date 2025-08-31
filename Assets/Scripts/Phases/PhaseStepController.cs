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

        // 初期化はOnEnableで行う（1フレ待って各Awake/Startを待つ）
        InitializeOnEnableAsync().Forget();
    }

    private void OnDisable()
    {
        // 現在のステップの表示物を消す
        if (TryGetStep(CurrentPhaseIndex, CurrentStepIndex, out var step))
            SetActiveList(step.objectsToShow, false, this.gameObject);

        // イベント購読解除（nullガード）
        if (GameFlowController.instance)
            GameFlowController.instance.PhaseChanged -= OnGameFlowPhaseChanged;

        _subscribed = false;
        _autoStarted = false;
    }

    private async UniTask InitializeOnEnableAsync()
    {
        // 1フレ待って他シングルトンの初期化を待つ
        await UniTask.NextFrame();

        TrySubscribeGameFlow();

        if (!autoStartOnEnable || phases.Count == 0 || _autoStarted)
            return;

        // すでにGameFlowControllerが動いているならそれに同期、
        // まだなら startPhase / startStepKey から開始
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

    /// <summary>
    /// フェーズを切り替え（非同期・戻り値あり）
    /// </summary>
    public async UniTask<bool> SwitchPhaseAsync(GameFlowController.Phase phase, string firstStepKey = null, bool invokeOldExit = true)
    {
        int newPi = FindPhaseIndex(phase);
        if (newPi < 0) { Debug.LogWarning($"Phase '{phase}' not found in list."); return false; }

        // === 同一フェーズ要求の取り扱い ===
        if (Equals(phase, CurrentPhase))
        {
            // まだステップが未設定なら初期ステップへ
            if (string.IsNullOrWhiteSpace(CurrentStepKey))
            {
                if (!TryResolveFirstStepIndex(newPi, firstStepKey, out var siInit))
                    return false; // ステップが無い
                return SwitchStepByIndex(newPi, siInit);
            }

            // 既にステップが入っていて、かつ firstStepKey 未指定 or 同じなら何もしない
            if (string.IsNullOrWhiteSpace(firstStepKey) ||
                string.Equals(firstStepKey, CurrentStepKey, StringComparison.Ordinal))
                return false;

            // 同一フェーズ内で別ステップに飛びたいとき
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
            SetActiveList(oldStepData.objectsToShow, false, this.gameObject);
        }
        if (invokeOldExit && phases.Count > 0)
            onPhaseExited?.Invoke(oldPhase);

        CurrentPhase = phases[newPi].phase;

        if (hardExclusiveShowAcrossPhases) HideAllObjects();

        if (!TryResolveFirstStepIndex(newPi, firstStepKey, out var si))
            return false;

        // 1フレ待ってからEnter系（初期化順の保険）
        await UniTask.Yield();

        onPhaseEntered?.Invoke(CurrentPhase);
        onPhaseChanged?.Invoke(oldPhase, CurrentPhase);

        return SwitchStepByIndex(newPi, si);
    }

    /// <summary>
    /// 互換用：非同期をFire-and-forgetで叩きたいとき（戻り値いらない運用）
    /// </summary>
    public void SwitchPhase(GameFlowController.Phase phase, string firstStepKey = null, bool invokeOldExit = true)
        => SwitchPhaseAsync(phase, firstStepKey, invokeOldExit).Forget();

    /// <summary>
    /// フェーズ＋ステップをキー指定で切替（非同期・戻り値あり）
    /// </summary>
    public UniTask<bool> SwitchAsync(GameFlowController.Phase phase, string stepKey)
    {
        if (Equals(phase, CurrentPhase) && string.Equals(stepKey, CurrentStepKey, StringComparison.Ordinal))
            return UniTask.FromResult(false);

        int pi = FindPhaseIndex(phase);
        if (pi < 0) return UniTask.FromResult(false);
        int si = FindStepIndex(pi, stepKey);
        if (si < 0) return UniTask.FromResult(false);

        // SwitchPhaseAsync 内に同一フェーズ時の最適化がある
        return SwitchPhaseAsync(phase, stepKey);
    }

    /// <summary>
    /// 互換用：戻り値不要のFire-and-forget
    /// </summary>
    public void Switch(GameFlowController.Phase phase, string stepKey)
        => SwitchAsync(phase, stepKey).Forget();

    // ===== Internals =====

    // 初期ステップ解決を一本化（firstStepKey → PhaseBlock.startStepKey → 0番）
    private bool TryResolveFirstStepIndex(int phaseIndex, string preferredKey, out int stepIndex)
    {
        stepIndex = -1;
        if (!TryGetPhase(phaseIndex, out var ph) || ph.steps == null || ph.steps.Count == 0)
            return false;

        // 1) 引数で指定された firstStepKey を最優先
        if (!string.IsNullOrWhiteSpace(preferredKey))
            stepIndex = FindStepIndex(phaseIndex, preferredKey);

        // 2) PhaseBlock に設定された startStepKey
        if (stepIndex < 0 && !string.IsNullOrWhiteSpace(ph.startStepKey))
            stepIndex = FindStepIndex(phaseIndex, ph.startStepKey);

        // 3) どちらも見つからなければ 0 番
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

        // Debug.Log($"{CurrentPhase} / {CurrentStepKey} / {CurrentStepIndex}");
        return true;
    }

    private void HideAllObjects()
    {
        foreach (var ph in phases)
            foreach (var st in ph.steps)
                SetActiveList(st.objectsToShow, false, this.gameObject);
    }

    // 自分自身を誤ってSetActive(false)しないようにselfガード
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

    [Serializable] public class UnityEventPhase   : UnityEvent2<GameFlowController.Phase> {}
    [Serializable] public class UnityEventPhase2  : UnityEvent2<GameFlowController.Phase, GameFlowController.Phase> {}
    [Serializable] public class UnityEventString  : UnityEvent2<string> {}
    [Serializable] public class UnityEventString2 : UnityEvent2<string, string> {}
}

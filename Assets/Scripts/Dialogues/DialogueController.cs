using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class DialogueController : Singleton<DialogueController>
{
    [Title("Sequence Group Assets (ScriptableObject)")]
    [OdinSerialize] public List<DialogueSequenceGroupAsset> sequenceGroupAssets = new();

    // groupKey -> (seqId -> SequenceAsset)
    private Dictionary<string, Dictionary<string, DialogueSequenceAsset>> _lookup;

    [Title("UI")]
    [Tooltip("スピーカー名を包むボックス（スピーカーが空なら非表示）")]
    public GameObject speakerBox;
    public TextMeshProUGUI speakerText;

    [Tooltip("本文を包むボックス（本文が空なら非表示）")]
    public GameObject dialogueBox;
    public TextMeshProUGUI bodyText;

    public Transform choicesRoot;
    public Button choiceButtonPrefab;

    [Title("Typewriter")]
    public float charsPerSecond = 35f;
    public bool useUnscaledTime = true;

    [Title("Input Actions (名前)")]
    public string advanceActionName = "Submit"; // Press Only 推奨
    public string skipActionName    = "Skip";
    public string cancelActionName  = "Cancel";

    [Title("Double Click")]
    public float doubleClickSeconds = 0.30f;

    [Title("Flags (条件/演出分岐用)")]
    [OdinSerialize, DictionaryDrawerSettings(KeyLabel = "Key", ValueLabel = "Value")]
    public Dictionary<string, bool> flags = new();

    [Title("Events")]
    public UnityEvent onDialogueStarted;
    public UnityEvent onDialogueEnded;

    [Title("UI Switching")]
    [SerializeField] private UISwitcher uiSwitcher;
    [SerializeField] private bool switchUIOnStartAndEnd = true;

    // ==== 入力ポーズ / レイキャスト停止 ====
    [Title("Input Lock")]
    [SerializeField] private bool _pauseAdvanceInput = false;

    [Tooltip("このCanvas(または親Canvas)のGraphicRaycaster。Pause中にOFFにしてクリックを通す")]
    [SerializeField] private GraphicRaycaster dialogueRaycaster;

    /// <summary>クリック/スキップによる進行を一時停止/再開し、レイキャストも切り替える。</summary>
    public void PauseAdvance(bool pause = true)
    {
        _pauseAdvanceInput = pause;
        if (dialogueRaycaster) dialogueRaycaster.enabled = !pause;
    }
    public bool IsAdvancePaused => _pauseAdvanceInput;

    // ==== 内部 ====
    private PlayerInput playerInput;
    private InputAction advanceAction, skipAction, cancelAction;
    private CancellationTokenSource _runCts;
    private GameStateController gameStateController;

    private float lastClickTime = -999f;
    private float Now => useUnscaledTime ? Time.unscaledTime : Time.time;

    // 並走バグ対策 runId
    private int _runId = 0;
    private int _nextRunId = 1;

    private const int MaxGotoHops = 64; // ループ保護
    private const char _stepSep = '#';

    void OnValidate() { RebuildLookup(); }

    void Start()
    {
        RebuildLookup();

        if (speakerText) speakerText.text = "";
        if (bodyText) bodyText.text = "";
        if (speakerBox) speakerBox.SetActive(false);
        if (dialogueBox) dialogueBox.SetActive(false);

        uiSwitcher = UISwitcher.instance ?? uiSwitcher;
        gameStateController = GameStateController.instance ?? gameStateController;

        playerInput = GetComponent<PlayerInput>();
        if (!playerInput && PlayerStatus.instance) playerInput = PlayerStatus.instance.playerInput;

        if (playerInput != null)
        {
            var map = playerInput.actions;
            advanceAction = map.FindAction(advanceActionName);
            skipAction    = map.FindAction(skipActionName);
            cancelAction  = map.FindAction(cancelActionName);
        }
        else
        {
            Debug.LogWarning("[DialogueController] PlayerInput が見つかりません。");
        }

        if (!dialogueRaycaster)
        {
            dialogueRaycaster = GetComponent<GraphicRaycaster>();
            if (!dialogueRaycaster)
                dialogueRaycaster = GetComponentInParent<GraphicRaycaster>();
        }
    }

    void OnDisable() { StopCurrent(); }

    [Button("Rebuild Lookup")]
    public void RebuildLookup()
    {
        _lookup = new Dictionary<string, Dictionary<string, DialogueSequenceAsset>>(StringComparer.Ordinal);
        if (sequenceGroupAssets == null) return;

        foreach (var group in sequenceGroupAssets)
        {
            if (!group || string.IsNullOrWhiteSpace(group.groupKey)) continue;

            if (!_lookup.TryGetValue(group.groupKey, out var map))
            {
                map = new Dictionary<string, DialogueSequenceAsset>(StringComparer.Ordinal);
                _lookup[group.groupKey] = map;
            }

            if (group.sequences != null)
            {
                foreach (var seq in group.sequences)
                {
                    if (!seq || string.IsNullOrWhiteSpace(seq.id)) continue;
                    map[seq.id] = seq; // 後勝ち
                }
            }
        }
    }

    // ===== Public API =====

    public void StartDialogue(string groupKey, string sequenceId, int startStepId = 1)
        => StartDialogueAsync(groupKey, sequenceId, default, startStepId).Forget();

    public void StartDialogue(DialogueSequenceGroupAsset groupAsset, string sequenceId, int startStepId = 1)
    {
        if (!groupAsset) { Debug.LogWarning("GroupAsset is null"); return; }
        StartDialogue(groupAsset.groupKey, sequenceId, startStepId);
    }

    public async UniTask StartDialogueAsync(string groupKey, string sequenceId, CancellationToken externalToken = default, int startStepId = 1)
    {
        StopCurrent(); // 旧ラン停止
        int runId = _nextRunId++; _runId = runId;

        if (!TryGetSequence(groupKey, sequenceId, out var seq))
        {
            Debug.LogWarning($"[DialogueController] '{groupKey}:{sequenceId}' が見つかりません。");
            return;
        }

        int startIndex = Mathf.Max(0, (startStepId <= 0 ? 1 : startStepId) - 1);
        startIndex = (seq.steps != null && seq.steps.Count > 0) ? Mathf.Min(startIndex, seq.steps.Count - 1) : 0;

        _runCts = CancellationTokenSource.CreateLinkedTokenSource(externalToken);
        var token = _runCts.Token;

        try
        {
            ThrowIfStale(runId);

            if (switchUIOnStartAndEnd && uiSwitcher) uiSwitcher.SwitchTo(UISwitcher.UIKind.Dialogue);
            onDialogueStarted?.Invoke();
            if (gameStateController) gameStateController.ActionByModeSelect(GameStateController.GameState.MenuOnGamePlay, true);

            await RunSequenceAsync(runId, groupKey, sequenceId, token, 0, startIndex);
        }
        catch (OperationCanceledException) { }
        finally
        {
            if (runId == _runId)
            {
                CleanupUI();
                onDialogueEnded?.Invoke();
                if (switchUIOnStartAndEnd && uiSwitcher) uiSwitcher.SwitchTo(UISwitcher.UIKind.Player);
                if (gameStateController) gameStateController.ActionByModeSelect(GameStateController.GameState.Gameplay, false);
            }
            _runCts?.Dispose(); _runCts = null;
        }
    }

    public void StopCurrent()
    {
        _runId = _nextRunId++;
        if (_runCts != null && !_runCts.IsCancellationRequested) _runCts.Cancel();
        CleanupUI();
    }

    // ===== Flags =====
    public bool GetFlag(string key, bool def = false) => flags.TryGetValue(key, out var v) ? v : def;
    public void SetFlag(string key, bool val) => flags[key] = val;

    // ===== Core =====

    private async UniTask RunSequenceAsync(int runId, string groupKey, string seqId, CancellationToken token, int hop, int startStepIndex)
    {
        if (!TryGetSequence(groupKey, seqId, out var seq) || seq.steps == null || seq.steps.Count == 0)
        {
            Debug.LogWarning($"[DialogueController] '{groupKey}:{seqId}' が見つからない/空です。");
            return;
        }

        int start = Mathf.Clamp(startStepIndex, 0, seq.steps.Count - 1);

        for (int i = start; i < seq.steps.Count; i++)
        {
            token.ThrowIfCancellationRequested();
            ThrowIfStale(runId);

            var step = seq.steps[i];

            if (!CheckConditions(step.conditions)) continue;

            // 即時goto
            if (hop < MaxGotoHops && step.useStepGoto && !string.IsNullOrWhiteSpace(step.stepGotoId))
            {
                if (!TryParseTarget(step.stepGotoId.Trim(), groupKey, out string ng, out string ns, out int nStep))
                {
                    Debug.LogWarning($"[DialogueController] step goto 解析失敗 '{step.stepGotoId}'");
                    return;
                }
                if (!TryGetSequence(ng, ns, out _))
                {
                    Debug.LogWarning($"[DialogueController] step goto '{step.stepGotoId}' が見つかりません。");
                    return;
                }
                await RunSequenceAsync(runId, ng, ns, token, hop + 1, Mathf.Max(0, nStep - 1));
                return;
            }

            // 入場フラグ
            if (step.flagEditsOnEnter != null)
                foreach (var e in step.flagEditsOnEnter)
                    if (!string.IsNullOrWhiteSpace(e.key)) SetFlag(e.key, e.value);

            // Speaker 表示切替
            if (speakerText)
            {
                ThrowIfStale(runId);
                bool hasSpeaker = !string.IsNullOrWhiteSpace(step.speaker);
                if (speakerBox) speakerBox.SetActive(hasSpeaker);

                if (hasSpeaker)
                {
                    speakerText.text = step.speaker;
                    speakerText.enabled = true;
                }
                else
                {
                    speakerText.text = "";
                    speakerText.enabled = false;
                }
            }

            // 本文（空ならボックスを隠す）
            bool hasBody = !string.IsNullOrWhiteSpace(step.text);
            if (dialogueBox) dialogueBox.SetActive(hasBody);

            // 本文タイプ
            await TypeTextAsync(runId, step.text, token);
            ThrowIfStale(runId);

            // 選択肢
            if (step.choices != null && step.choices.Count > 0)
            {
                string gotoRaw = await ShowChoicesAndWaitAsync(runId, step.choices, token);
                ThrowIfStale(runId);
                if (gotoRaw == null) return;

                if (!TryParseTarget(gotoRaw.Trim(), groupKey, out string ng, out string ns, out int nStep))
                {
                    Debug.LogWarning($"[DialogueController] choice goto 解析失敗 '{gotoRaw}'");
                    return;
                }
                if (!TryGetSequence(ng, ns, out _))
                {
                    Debug.LogWarning($"[DialogueController] goto '{gotoRaw}' が見つかりません。");
                    return;
                }

                await RunSequenceAsync(runId, ng, ns, token, hop + 1, Mathf.Max(0, nStep - 1));
                return;
            }
            else
            {
                if (step.autoNext) await DelaySmartAsync(step.autoNextDelay, token);
                else               await WaitAdvanceAsync(runId, token);
                ThrowIfStale(runId);
            }
        }
    }

    private bool CheckConditions(List<FlagCondition> conds)
    {
        if (conds == null || conds.Count == 0) return true;
        foreach (var c in conds)
            if (GetFlag(c.key, false) != c.requiredValue) return false;
        return true;
    }

    // ===== Target 解析 =====
    private bool TryParseTarget(string raw, string currentGroup, out string group, out string seq, out int stepId)
    {
        group = currentGroup;
        seq = null;
        stepId = 1;

        if (string.IsNullOrWhiteSpace(raw)) return false;

        var tokens = raw.Split(':');
        for (int i = 0; i < tokens.Length; i++) tokens[i] = tokens[i].Trim();

        if (tokens.Length == 1)
        {
            seq = tokens[0];
            return !string.IsNullOrWhiteSpace(seq);
        }
        else if (tokens.Length == 2)
        {
            if (int.TryParse(tokens[1], out var n) && n >= 1)
            {
                seq = tokens[0];
                stepId = n;
                return !string.IsNullOrWhiteSpace(seq);
            }
            else
            {
                group = tokens[0];
                seq = tokens[1];
                return !string.IsNullOrWhiteSpace(group) && !string.IsNullOrWhiteSpace(seq);
            }
        }
        else if (tokens.Length >= 3)
        {
            group = tokens[0];
            seq = tokens[1];

            if (int.TryParse(tokens[^1], out var n) && n >= 1)
                stepId = n;

            return !string.IsNullOrWhiteSpace(group) && !string.IsNullOrWhiteSpace(seq);
        }

        return false;
    }

    // ===== Lookup =====
    private bool TryGetSequence(string groupKey, string seqId, out DialogueSequenceAsset asset)
    {
        asset = null;
        if (string.IsNullOrWhiteSpace(groupKey) || string.IsNullOrWhiteSpace(seqId)) return false;
        if (_lookup == null) RebuildLookup();
        if (!_lookup.TryGetValue(groupKey, out var map) || map == null) return false;
        return map.TryGetValue(seqId, out asset);
    }

    // ===== Typewriter =====
    private async UniTask TypeTextAsync(int runId, string content, CancellationToken token)
    {
        string full = content ?? "";
        bool hasBody = !string.IsNullOrWhiteSpace(full);

        // 本文ボックスの表示/非表示を最初に確定
        if (dialogueBox) dialogueBox.SetActive(hasBody);

        if (bodyText) bodyText.text = "";

        // テキストが空なら即終了
        if (!hasBody)
        {
            return;
        }

        await UniTask.Yield(PlayerLoopTiming.Update, token);
        ThrowIfStale(runId);

        int len = full.Length;
        int idx = 0;
        float secPerChar = 1f / Mathf.Max(1f, charsPerSecond);
        float acc = 0f;

        // 最初の1文字（任意）
        if (len > 0)
        {
            idx = 1;
            if (bodyText) bodyText.text = full.Substring(0, 1);
        }

        while (idx < len)
        {
            token.ThrowIfCancellationRequested();
            ThrowIfStale(runId);

            if (!_pauseAdvanceInput)
            {
                if (skipAction != null && skipAction.IsPressed())
                {
                    if (bodyText) bodyText.text = full;
                    break;
                }

                if (advanceAction != null && advanceAction.triggered)
                {
                    float now = Now;
                    if (now - lastClickTime <= doubleClickSeconds)
                    {
                        if (bodyText) bodyText.text = full;
                        lastClickTime = now;
                        break;
                    }
                    lastClickTime = now;
                }
            }

            float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            acc += dt;
            while (acc >= secPerChar && idx < len)
            {
                acc -= secPerChar;
                idx++;
                if (bodyText) bodyText.text = full.Substring(0, idx);
            }

            await UniTask.Yield(PlayerLoopTiming.Update, token);
            ThrowIfStale(runId);
        }
    }

    // ===== Wait / Delay =====
    private async UniTask WaitAdvanceAsync(int runId, CancellationToken token)
    {
        await UniTask.Yield(PlayerLoopTiming.Update, token);
        ThrowIfStale(runId);

        while (true)
        {
            token.ThrowIfCancellationRequested();
            ThrowIfStale(runId);

            if (cancelAction != null && cancelAction.triggered)
            { StopCurrent(); return; }

            if (!_pauseAdvanceInput && advanceAction != null && advanceAction.triggered)
            {
                lastClickTime = Now;
                return;
            }

            await UniTask.Yield(PlayerLoopTiming.Update, token);
            ThrowIfStale(runId);
        }
    }

    private async UniTask DelaySmartAsync(float seconds, CancellationToken token)
    {
        var delayType = useUnscaledTime ? DelayType.UnscaledDeltaTime : DelayType.DeltaTime;
        await UniTask.Delay(TimeSpan.FromSeconds(Mathf.Max(0f, seconds)), delayType, PlayerLoopTiming.Update, token);
    }

    // ===== Choices =====
    private async UniTask<string> ShowChoicesAndWaitAsync(int runId, List<DialogueChoice> choices, CancellationToken token)
    {
        ClearChoices();
        ThrowIfStale(runId);

        var usable = new List<(DialogueChoice, Button)>();
        foreach (var ch in choices)
        {
            if (!CheckConditions(ch.conditions)) continue;
            var btn = UnityEngine.Object.Instantiate(choiceButtonPrefab, choicesRoot);
            var label = btn.GetComponentInChildren<TextMeshProUGUI>();
            if (label) label.text = ch.text;
            btn.gameObject.SetActive(true);
            usable.Add((ch, btn));
        }

        if (usable.Count == 0) return null;

        string result = null; bool clicked = false;
        foreach (var (ch, btn) in usable)
        {
            string nextId = ch.gotoId;
            btn.onClick.AddListener(() =>
            {
                if (_runId != runId) return;
                result = nextId;
                clicked = true;
            });
        }

        while (!clicked)
        {
            token.ThrowIfCancellationRequested();
            ThrowIfStale(runId);

            if (cancelAction != null && cancelAction.triggered)
            { ClearChoices(); return null; }

            await UniTask.Yield(PlayerLoopTiming.Update, token);
            ThrowIfStale(runId);
        }

        ClearChoices();
        return result;
    }

    private void ClearChoices()
    {
        if (!choicesRoot) return;
        for (int i = choicesRoot.childCount - 1; i >= 0; i--)
        {
            var go = choicesRoot.GetChild(i).gameObject;
            if (Application.isPlaying) Destroy(go);
            else DestroyImmediate(go);
        }
    }

    private void CleanupUI()
    {
        ClearChoices();

        if (speakerText) { speakerText.text = ""; speakerText.enabled = false; }
        if (bodyText) { bodyText.text = ""; }

        if (speakerBox) speakerBox.SetActive(false);
        if (dialogueBox) dialogueBox.SetActive(false);

        lastClickTime = -999f;

        if (dialogueRaycaster && !dialogueRaycaster.enabled)
            dialogueRaycaster.enabled = true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ThrowIfStale(int runId)
    {
        if (runId != _runId) throw new OperationCanceledException("stale run");
    }
}

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
    public GameObject speakerBox;
    public TextMeshProUGUI speakerText;
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

    [Title("Layout Stabilizer")]
    [SerializeField] private LayoutElement dialogueBoxLayout;
    [SerializeField] private float minHeightPadding = 4f;
    [SerializeField] private bool lockHeightWhileTyping = true;

    [Title("Flags (条件/演出分岐用)")]
    [OdinSerialize, DictionaryDrawerSettings(KeyLabel = "Key", ValueLabel = "Value")]
    public Dictionary<string, bool> flags = new();

    [Title("Events")]
    public UnityEvent onDialogueStarted;
    public UnityEvent onDialogueEnded;

    [Title("UI Switching")]
    [SerializeField] private UISwitcher uiSwitcher;
    [SerializeField] private bool switchUIOnStartAndEnd = true;

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

    private const int MaxGotoHops = 64;        // ループ保護
    private static readonly char[] _groupSep = { ':', '/', '@' }; // グループ区切り（#は除外）
    private const char _stepSep = '#';         // ステップ区切り

    void OnValidate() { RebuildLookup(); }     // ★ Awakeでは呼ばない

    void Start()
    {
        RebuildLookup();                       // ★ Startで構築（要望どおり）

        if (speakerText) speakerText.text = "";
        if (bodyText) bodyText.text = "";

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
                    map[seq.id] = seq; // 後勝ち（被らない想定）
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
        Debug.Log("-------------3-------------");
        try
        {
            ThrowIfStale(runId);

            if (switchUIOnStartAndEnd && uiSwitcher) uiSwitcher.SwitchTo(UISwitcher.UIKind.Dialogue);
            onDialogueStarted?.Invoke();
            if (gameStateController) gameStateController.ActionByModeSelect(GameStateController.GameState.MenuOnGamePlay, true);
            Debug.Log("-------------5-------------");
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
        _runId = _nextRunId++; // 旧ランはUI更新禁止
        if (_runCts != null && !_runCts.IsCancellationRequested) _runCts.Cancel();
        CleanupUI();
    }

    // ===== Flags =====
    public bool GetFlag(string key, bool def = false) => flags.TryGetValue(key, out var v) ? v : def;
    public void SetFlag(string key, bool val) => flags[key] = val;

    // ===== Core =====

    private async UniTask RunSequenceAsync(int runId, string groupKey, string seqId, CancellationToken token, int hop, int startStepIndex)
    {
        Debug.Log("RunSequenceAsync");
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

            var step = seq.steps[i]; // ★ このステップの“自動ID”は i+1

            if (!CheckConditions(step.conditions)) continue;

            // ---- Step-level immediate goto（このステップに来た瞬間ジャンプ）----
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

            // ステップ入場時フラグ編集
            if (step.flagEditsOnEnter != null)
                foreach (var e in step.flagEditsOnEnter)
                    if (!string.IsNullOrWhiteSpace(e.key)) SetFlag(e.key, e.value);

            // Speaker
            // RunSequenceAsync 内の Speaker 処理を差し替え
            // --- ここから ---
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
            // --- ここまで ---

            // 本文タイプ
            await TypeTextAsync(runId, step.text, token);
            ThrowIfStale(runId);

            // 選択肢
            if (step.choices != null && step.choices.Count > 0)
            {
                string gotoRaw = await ShowChoicesAndWaitAsync(runId, step.choices, token);
                ThrowIfStale(runId);
                if (gotoRaw == null) return; // キャンセル

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
                return; // 分岐先から戻らない
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
    // 同グループ・先頭ステップ："Seq"

    // 同グループ・nステップ目："Seq:n"

    // 別グループ・先頭ステップ："Group:Seq"

    // 別グループ・nステップ目："Group:Seq:n"
    // 同グループの 3 ステップ目へ：
    // gotoId = "intro:3"

    // 別グループの 1 ステップ目へ：
    // gotoId = "MomEvent:intro"

    // 別グループの 5 ステップ目へ：
    // gotoId = "FriendTalk:greet:5"
    private bool TryParseTarget(string raw, string currentGroup, out string group, out string seq, out int stepId)
    {
        group = currentGroup;
        seq = null;
        stepId = 1;

        if (string.IsNullOrWhiteSpace(raw)) return false;

        var tokens = raw.Split(':'); // 1〜3個を想定
        for (int i = 0; i < tokens.Length; i++) tokens[i] = tokens[i].Trim();

        if (tokens.Length == 1)
        {
            // "Seq"
            seq = tokens[0];
            return !string.IsNullOrWhiteSpace(seq);
        }
        else if (tokens.Length == 2)
        {
            // 曖昧ケース: "Seq:n" か "Group:Seq"
            // 2番目が整数なら "Seq:n" とみなす。それ以外は "Group:Seq"
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
            // "Group:Seq:n"（3個以上来たら先頭2つを Group/Seq、最後を n とみなす）
            group = tokens[0];
            seq = tokens[1];

            // 末尾（tokens[^1]）が整数なら step、違えば 1
            if (int.TryParse(tokens[tokens.Length - 1], out var n) && n >= 1)
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
        if (bodyText) bodyText.text = "";

        // レイアウト高さロック
        RectTransform boxRT = null;
        if (lockHeightWhileTyping && dialogueBoxLayout)
        {
            boxRT = (RectTransform)dialogueBoxLayout.transform;
            Canvas.ForceUpdateCanvases();
            dialogueBoxLayout.minHeight = boxRT.rect.height + minHeightPadding;
        }

        // 入力フラッシュ
        await UniTask.Yield(PlayerLoopTiming.Update, token);
        ThrowIfStale(runId);

        int len = full.Length;
        int idx = 0;
        float secPerChar = 1f / Mathf.Max(1f, charsPerSecond);
        float acc = 0f;

        // ★ ここで1文字目を即表示
        if (len > 0)
        {
            idx = 1;
            if (bodyText) bodyText.text = full.Substring(0, 1);
        }

        while (idx < len)
        {
            token.ThrowIfCancellationRequested();
            ThrowIfStale(runId);

            // 押しっぱスキップ
            if (skipAction != null && skipAction.IsPressed())
            {
                if (bodyText) bodyText.text = full;
                break;
            }

            // ダブルクリック全文表示
            if (advanceAction != null && advanceAction.triggered)
            {
                float now = Now;
                if (now - lastClickTime <= doubleClickSeconds)
                {
                    if (bodyText) bodyText.text = full;
                    lastClickTime = now;
                    break;
                }
                lastClickTime = now; // 1発目
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

        // 高さロック解除
        if (lockHeightWhileTyping && dialogueBoxLayout)
        {
            dialogueBoxLayout.minHeight = 0f;
            if (boxRT) LayoutRebuilder.ForceRebuildLayoutImmediate(boxRT);
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

            if (advanceAction != null && advanceAction.triggered)
            {
                lastClickTime = Now; // 次行のダブル1発目
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
            string nextId = ch.gotoId; // "Seq" / "Seq#3" / "Group:Seq" / "Group:Seq#3"
            btn.onClick.AddListener(() =>
            {
                if (_runId != runId) return; // stale無視
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
        if (bodyText) bodyText.text = "";
        lastClickTime = -999f;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ThrowIfStale(int runId)
    {
        if (runId != _runId) throw new OperationCanceledException("stale run");
    }
}

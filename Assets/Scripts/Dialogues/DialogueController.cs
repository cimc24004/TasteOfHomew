using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 会話（ダイアログ）の再生を担当するシングルトン。
///
/// ・「グループキー → シーケンスID」で DialogueSequenceAsset を引いて再生する
/// ・1 ステップごとに「話者 → 本文（タイプライター表示）→ 選択肢 or 入力待ち」の順で進める
/// ・フラグによる表示条件と、goto によるシーケンス間ジャンプに対応
/// ・会話とは別に、一定時間だけ出す短いメッセージ（BriefMessage）も表示できる
/// </summary>
[DisallowMultipleComponent]
public class DialogueController : Singleton<DialogueController>
{
    /// <summary>goto の連鎖で無限ループしないための上限。</summary>
    private const int MaxGotoHops = 64;

    /// <summary>「まだ一度もクリックされていない」を表す lastClickTime の初期値。</summary>
    private const float NeverClicked = -999f;

    /// <summary>ダブルクリックで文字送りを飛ばした直後、その入力で次へ進んでしまわないための待ち時間。</summary>
    private const float SkipCooldownSeconds = 0.5f;

    /// <summary>選択肢ボタンを並べ始めるまでの待ち時間。</summary>
    private const float ChoiceAppearDelaySeconds = 0.1f;

    /// <summary>選択肢を並べてから、先頭のボタンを選択状態にするまでの待ち時間。</summary>
    private const float ChoiceFocusDelaySeconds = 0.5f;

    /// <summary>本文中のプレースホルダー。例: #{InputActionName("Click")}</summary>
    private static readonly Regex placeholderRegex = new Regex(@"\#\{(\w+)\(""([^""]*)""\)\}", RegexOptions.Compiled);

    // ===== Inspector =====

    [Title("Sequence Group Assets (ScriptableObject)")]
    [OdinSerialize] public List<DialogueSequenceGroupAsset> sequenceGroupAssets = new();

    [Title("UI (Default)")]
    [Tooltip("デフォルトのUI参照。オーバーライドされなかった場合に使われる。")]
    [SerializeField]
    private DefaultDialogueUIInfo defaultUIInfo;

    [Title("Typewriter")]
    public float charsPerSecond = 35f;
    public bool useUnscaledTime = true;

    [Title("Input Actions (名前)")]
    public string advanceActionName = "Click";
    public string skipActionName = "Skip";
    public string cancelActionName = "Cancel";

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

    [Title("Input Lock")]
    [SerializeField] private bool _pauseAdvanceInput = false;
    [Tooltip("このCanvas(または親Canvas)のGraphicRaycaster。Pause中にOFFにしてクリックを通す")]
    [SerializeField] private GraphicRaycaster dialogueRaycaster;

    // ===== 実行時の状態 =====

    /// <summary>groupKey → (sequenceId → シーケンス) の検索テーブル。</summary>
    private Dictionary<string, Dictionary<string, DialogueSequenceAsset>> sequenceLookup;

    /// <summary>いま表示に使っている UI。会話ごとに差し替えられる。</summary>
    private DialogueUIRefs currentUI;

    private PlayerInput playerInput;
    private InputAction advanceAction;
    private InputAction skipAction;
    private InputAction cancelAction;

    private GameStateController gameStateController;
    private PauseMenuController pauseMenuController;
    private InputDeviceDetector inputDeviceDetector;

    /// <summary>
    /// 再生中の会話を識別する ID。会話を開始・中断するたびに新しい値になる。
    /// 古い会話の非同期処理は <see cref="ThrowIfStale"/> で「自分はもう無効」と気づいて止まる。
    /// </summary>
    private int currentRunId = 0;
    private int nextRunId = 1;
    private CancellationTokenSource runCancellation;

    private Coroutine briefMessageCoroutine;
    private float lastClickTime = NeverClicked;

    private float Now => useUnscaledTime ? Time.unscaledTime : Time.time;
    private float DeltaTime => useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
    private DelayType DelayTiming => useUnscaledTime ? DelayType.UnscaledDeltaTime : DelayType.DeltaTime;
    private bool CancelRequested => cancelAction != null && cancelAction.triggered;

    public bool IsAdvancePaused => _pauseAdvanceInput;

    // ===== Unity ライフサイクル =====

    private void OnValidate()
    {
        RebuildLookup();
    }

    private void Start()
    {
        RebuildLookup();
        inputDeviceDetector = InputDeviceDetector.Instance;
        SceneManager.sceneLoaded += OnSceneLoaded;
        Init();

        if (!dialogueRaycaster)
        {
            dialogueRaycaster = GetComponent<GraphicRaycaster>();
            if (!dialogueRaycaster)
            {
                dialogueRaycaster = GetComponentInParent<GraphicRaycaster>();
            }
        }
    }

    private void OnDisable()
    {
        StopCurrent();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Init();
    }

    /// <summary>シーンが変わるたびに、そのシーンの UI・入力・各コントローラーへの参照を取り直す。</summary>
    private void Init()
    {
        CleanupUI();
        MergeFlagsFromDataShare();

        uiSwitcher = UISwitcher.instance ?? uiSwitcher;
        gameStateController = GameStateController.instance ?? gameStateController;
        pauseMenuController = PauseMenuController.instance;
        playerInput = PlayerDataHub.instance.CurrentplayerInput;

        BindDefaultUI();
        BindInputActions();
    }

    private void BindDefaultUI()
    {
        var sceneUIInfo = DefaultDialogueUIInfo.instance;
        if (sceneUIInfo != null)
        {
            defaultUIInfo = sceneUIInfo;
            currentUI = CreateDefaultUIRefs();
        }
        else if (defaultUIInfo == null)
        {
            Debug.LogWarning("[DialogueController] 新しいシーンで DialogueUIRefs が見つかりませんでした。UIが表示されない可能性があります。");
        }
    }

    private void BindInputActions()
    {
        if (playerInput == null)
        {
            Debug.LogWarning("[DialogueController] PlayerInput が見つかりません。");
            return;
        }

        var actions = playerInput.actions;
        advanceAction = actions.FindAction(advanceActionName);
        skipAction = actions.FindAction(skipActionName);
        cancelAction = actions.FindAction(cancelActionName);
    }

    /// <summary>エンディング共有フラグ（DataShareController）を会話用フラグへ取り込む。</summary>
    private void MergeFlagsFromDataShare()
    {
        var dataShare = DataShareController.instance;
        if (dataShare == null) return;

        foreach (var pair in dataShare.EndingShareDictionary)
        {
            if (!string.IsNullOrWhiteSpace(pair.Key))
            {
                flags[pair.Key] = pair.Value;
            }
        }
    }

    // ===== 公開 API: 会話の開始・停止 =====

    public void StartDialogue(string groupKey, string sequenceId, int startStepId = 1, bool changeGameState = true)
        => StartDialogueAsync(groupKey, sequenceId, null, default, startStepId, changeGameState).Forget();

    public void StartDialogue(DialogueSequenceGroupAsset groupAsset, string sequenceId, int startStepId = 1, bool changeGameState = true)
    {
        if (!groupAsset)
        {
            Debug.LogWarning("GroupAsset is null");
            return;
        }
        StartDialogue(groupAsset.groupKey, sequenceId, startStepId, changeGameState);
    }

    public void StartDialogue(string groupKey, string sequenceId, DialogueUIRefs overrideUIRefs, int startStepId = 1, bool changeGameState = true)
        => StartDialogueAsync(groupKey, sequenceId, overrideUIRefs, default, startStepId, changeGameState).Forget();

    public void StartDialogue(DialogueSequenceGroupAsset groupAsset, string sequenceId, DialogueUIRefs overrideUIRefs, int startStepId = 1, bool changeGameState = true)
    {
        if (!groupAsset)
        {
            Debug.LogWarning("GroupAsset is null");
            return;
        }
        StartDialogue(groupAsset.groupKey, sequenceId, overrideUIRefs, startStepId, changeGameState);
    }

    /// <summary>
    /// 会話を開始し、終わるまで待てる版。再生中の会話があれば先に中断する。
    /// </summary>
    /// <param name="overrideUIRefs">NPC の吹き出しなど、デフォルト以外の UI に表示したいときに渡す。</param>
    /// <param name="startStepId">開始ステップ（1 始まり）。</param>
    /// <param name="changeGameState">true なら会話中はゲーム状態をメニュー扱いにし、終了時にゲームプレイへ戻す。</param>
    public async UniTask StartDialogueAsync(string groupKey, string sequenceId, DialogueUIRefs overrideUIRefs = null, CancellationToken externalToken = default, int startStepId = 1, bool changeGameState = true)
    {
        StopCurrent();
        int runId = IssueRunId();

        currentUI = overrideUIRefs ?? CreateDefaultUIRefs();

        if (!TryGetSequence(groupKey, sequenceId, out var sequence))
        {
            Debug.LogWarning($"[DialogueController] '{groupKey}:{sequenceId}' が見つかりません。");
            return;
        }

        int startIndex = ToStartIndex(startStepId, sequence);

        runCancellation = CancellationTokenSource.CreateLinkedTokenSource(externalToken);
        var token = runCancellation.Token;

        try
        {
            ThrowIfStale(runId);
            EnterDialogueMode(changeGameState);
            await RunSequenceAsync(runId, groupKey, sequenceId, token, 0, startIndex);
        }
        catch (OperationCanceledException)
        {
            // 中断は正常系なので何もしない
        }
        finally
        {
            // 別の会話に割り込まれた場合、後始末はその新しい会話側に任せる
            if (runId == currentRunId)
            {
                ExitDialogueMode(changeGameState);
            }

            runCancellation?.Dispose();
            runCancellation = null;
            currentUI = CreateDefaultUIRefs();
        }
    }

    /// <summary>再生中の会話を中断して UI を片付ける。</summary>
    public void StopCurrent()
    {
        IssueRunId(); // 再生中の会話を「古い」扱いにする

        if (runCancellation != null && !runCancellation.IsCancellationRequested)
        {
            runCancellation.Cancel();
        }

        if (pauseMenuController != null)
        {
            pauseMenuController.PauseMenuOpenPermit = true;
        }

        CleanupUI();
    }

    /// <summary>「進む」入力を一時的に無効化する。無効中はダイアログ UI がクリックを遮らないようにする。</summary>
    public void PauseAdvance(bool pause = true)
    {
        _pauseAdvanceInput = pause;
        if (dialogueRaycaster) dialogueRaycaster.enabled = !pause;
    }

    // ===== 公開 API: フラグ・検索 =====

    public bool GetFlag(string key, bool def = false) => flags.TryGetValue(key, out var v) ? v : def;

    public void SetFlag(string key, bool val) => flags[key] = val;

    [Button("Rebuild Lookup")]
    public void RebuildLookup()
    {
        sequenceLookup = new Dictionary<string, Dictionary<string, DialogueSequenceAsset>>(StringComparer.Ordinal);
        if (sequenceGroupAssets == null) return;

        foreach (var group in sequenceGroupAssets)
        {
            if (!group || string.IsNullOrWhiteSpace(group.groupKey)) continue;

            if (!sequenceLookup.TryGetValue(group.groupKey, out var sequencesById))
            {
                sequencesById = new Dictionary<string, DialogueSequenceAsset>(StringComparer.Ordinal);
                sequenceLookup[group.groupKey] = sequencesById;
            }

            if (group.sequences == null) continue;

            foreach (var sequence in group.sequences)
            {
                if (!sequence || string.IsNullOrWhiteSpace(sequence.id)) continue;
                sequencesById[sequence.id] = sequence;
            }
        }
    }

    public bool TryGetSequence(string groupKey, string seqId, out DialogueSequenceAsset asset)
    {
        asset = null;
        if (string.IsNullOrWhiteSpace(groupKey) || string.IsNullOrWhiteSpace(seqId)) return false;

        if (sequenceLookup == null) RebuildLookup();
        if (!sequenceLookup.TryGetValue(groupKey, out var sequencesById) || sequencesById == null) return false;

        return sequencesById.TryGetValue(seqId, out asset);
    }

    // ===== 公開 API: 短いメッセージ表示 =====

    /// <summary>会話とは別枠で、speaker / body を duration 秒だけ表示する。空文字を渡すと非表示になる。</summary>
    public void ShowBriefMessage(DialogueUIRefs uiRefs, string speaker, string body, float duration)
    {
        if (briefMessageCoroutine != null)
        {
            StopCoroutine(briefMessageCoroutine);
        }
        briefMessageCoroutine = StartCoroutine(BriefMessageRoutine(uiRefs, speaker, body, duration));
    }

    /// <summary>ステップの文言を翻訳してから <see cref="ShowBriefMessage"/> で表示する（完了は待たない）。</summary>
    public void ShowBriefLocalizedStep(DialogueUIRefs uiRefs, DialogueStep step, float duration)
    {
        ShowBriefLocalizedStepAsync(uiRefs, step, duration).Forget();
    }

    private async UniTaskVoid ShowBriefLocalizedStepAsync(DialogueUIRefs uiRefs, DialogueStep step, float duration)
    {
        if (step == null)
        {
            ShowBriefMessage(uiRefs, "", "", duration);
            return;
        }

        string speaker = await ResolveTextAsync(step.localizedSpeaker, step.speaker, CancellationToken.None);
        string body = await ResolveTextAsync(step.localizedText, step.text, CancellationToken.None);

        ShowBriefMessage(uiRefs, ParseDialogueText(speaker), ParseDialogueText(body), duration);
    }

    private IEnumerator BriefMessageRoutine(DialogueUIRefs customUiRef, string speaker, string body, float duration)
    {
        DialogueUIRefs ui = customUiRef ?? CreateDefaultUIRefs();

        bool hasBody = !string.IsNullOrWhiteSpace(body);
        ui.bodyText.text = body;
        ui.dialogueBox.SetActive(hasBody);

        bool hasSpeaker = !string.IsNullOrWhiteSpace(speaker);
        if (ui.speakerBox) ui.speakerBox.SetActive(hasSpeaker);
        if (ui.speakerText) ui.speakerText.text = speaker;

        if (duration > 0)
        {
            yield return new WaitForSeconds(duration);
        }
        else
        {
            yield return null;
        }

        ui.dialogueBox.SetActive(false);
        if (ui.speakerBox) ui.speakerBox.SetActive(false);

        briefMessageCoroutine = null;
    }

    // ===== 会話の開始・終了処理 =====

    private void EnterDialogueMode(bool changeGameState)
    {
        if (switchUIOnStartAndEnd && uiSwitcher) uiSwitcher.SwitchTo(UISwitcher.UIKind.Dialogue);
        onDialogueStarted?.Invoke();

        if (pauseMenuController != null)
        {
            pauseMenuController.PauseMenuOpenPermit = false;
        }

        if (changeGameState && gameStateController)
        {
            gameStateController.ActionByModeSelect(GameStateController.GameState.MenuOnGamePlay, true);
        }
    }

    private void ExitDialogueMode(bool changeGameState)
    {
        CleanupUI();
        onDialogueEnded?.Invoke();

        if (pauseMenuController != null)
        {
            pauseMenuController.PauseMenuOpenPermit = true;
        }

        if (switchUIOnStartAndEnd && uiSwitcher) uiSwitcher.SwitchTo(UISwitcher.UIKind.Player);

        if (changeGameState && gameStateController)
        {
            gameStateController.ActionByModeSelect(GameStateController.GameState.Gameplay, false);
        }
    }

    /// <summary>1 始まりのステップ番号を、シーケンスの範囲に収まる 0 始まりのインデックスへ変換する。</summary>
    private static int ToStartIndex(int startStepId, DialogueSequenceAsset sequence)
    {
        if (sequence.steps == null || sequence.steps.Count == 0) return 0;

        int index = Mathf.Max(0, startStepId - 1);
        return Mathf.Min(index, sequence.steps.Count - 1);
    }

    // ===== シーケンスの再生 =====

    /// <summary>
    /// シーケンスを startStepIndex から順に再生する。
    /// goto / 選択肢で別のシーケンスへ移る場合は自分自身を呼び直す（hop は移動回数）。
    /// </summary>
    private async UniTask RunSequenceAsync(int runId, string groupKey, string seqId, CancellationToken token, int hop, int startStepIndex)
    {
        if (!TryGetSequence(groupKey, seqId, out var sequence) || sequence.steps == null || sequence.steps.Count == 0)
        {
            Debug.LogWarning($"[DialogueController] '{groupKey}:{seqId}' が見つからない/空です。");
            return;
        }

        int firstIndex = Mathf.Clamp(startStepIndex, 0, sequence.steps.Count - 1);
        for (int i = firstIndex; i < sequence.steps.Count; i++)
        {
            ThrowIfInterrupted(runId, token);

            var step = sequence.steps[i];
            if (!CheckConditions(step.conditions)) continue;

            // ジャンプ専用ステップ: 何も表示せずに移動先へ
            bool isJumpStep = hop < MaxGotoHops && step.useStepGoto && !string.IsNullOrWhiteSpace(step.stepGotoId);
            if (isJumpStep)
            {
                await JumpToAsync(runId, step.stepGotoId, groupKey, "step", token, hop);
                return;
            }

            ApplyFlagEdits(step.flagEditsOnEnter);

            bool hasSpeaker = HasText(step.localizedSpeaker, step.speaker);
            bool hasBody = HasText(step.localizedText, step.text);
            bool hasChoices = step.choices != null && step.choices.Count > 0;

            await ShowSpeakerAsync(runId, step, hasSpeaker, hasBody, hasChoices, token);
            await ShowBodyAsync(runId, step, hasBody, hasChoices, token);
            ThrowIfStale(runId);

            if (hasChoices)
            {
                string gotoTarget = await ShowChoicesAndWaitAsync(runId, step.choices, token);
                ThrowIfStale(runId);
                if (gotoTarget == null) return;

                await JumpToAsync(runId, gotoTarget, groupKey, "choice", token, hop);
                return;
            }

            if (step.autoNext)
            {
                await DelaySmartAsync(step.autoNextDelay, token);
            }
            else
            {
                await WaitAdvanceAsync(runId, token);
            }
            ThrowIfStale(runId);
        }
    }

    /// <summary>goto 文字列（<see cref="TryParseTarget"/> の形式）を解決し、そのシーケンスの再生へ移る。</summary>
    private async UniTask JumpToAsync(int runId, string rawTarget, string currentGroupKey, string sourceLabel, CancellationToken token, int hop)
    {
        if (!TryParseTarget(rawTarget.Trim(), currentGroupKey, out string groupKey, out string sequenceId, out int stepId))
        {
            Debug.LogWarning($"[DialogueController] {sourceLabel} goto 解析失敗 '{rawTarget}'");
            return;
        }

        if (!TryGetSequence(groupKey, sequenceId, out _))
        {
            Debug.LogWarning($"[DialogueController] {sourceLabel} goto '{rawTarget}' が見つかりません。");
            return;
        }

        await RunSequenceAsync(runId, groupKey, sequenceId, token, hop + 1, Mathf.Max(0, stepId - 1));
    }

    /// <summary>
    /// goto 文字列を分解する。対応する書式:
    ///   "sequence"            … 同じグループの sequence（ステップ 1 から）
    ///   "sequence:3"          … 同じグループの sequence のステップ 3
    ///   "group:sequence"      … 別グループの sequence
    ///   "group:sequence:3"    … 別グループの sequence のステップ 3
    /// </summary>
    private bool TryParseTarget(string raw, string currentGroup, out string group, out string sequenceId, out int stepId)
    {
        group = currentGroup;
        sequenceId = null;
        stepId = 1;
        if (string.IsNullOrWhiteSpace(raw)) return false;

        var parts = raw.Split(':');
        for (int i = 0; i < parts.Length; i++)
        {
            parts[i] = parts[i].Trim();
        }

        if (parts.Length == 1)
        {
            sequenceId = parts[0];
            return !string.IsNullOrWhiteSpace(sequenceId);
        }

        if (parts.Length == 2)
        {
            // 2 つ目が数字なら "sequence:step"、そうでなければ "group:sequence"
            if (int.TryParse(parts[1], out int step) && step >= 1)
            {
                sequenceId = parts[0];
                stepId = step;
                return !string.IsNullOrWhiteSpace(sequenceId);
            }

            group = parts[0];
            sequenceId = parts[1];
            return !string.IsNullOrWhiteSpace(group) && !string.IsNullOrWhiteSpace(sequenceId);
        }

        // 3 つ以上: "group:sequence:step"（末尾が数字でなければステップ 1 のまま）
        group = parts[0];
        sequenceId = parts[1];
        if (int.TryParse(parts[^1], out int lastStep) && lastStep >= 1)
        {
            stepId = lastStep;
        }
        return !string.IsNullOrWhiteSpace(group) && !string.IsNullOrWhiteSpace(sequenceId);
    }

    private bool CheckConditions(List<FlagCondition> conditions)
    {
        if (conditions == null || conditions.Count == 0) return true;

        foreach (var condition in conditions)
        {
            if (GetFlag(condition.key, false) != condition.requiredValue) return false;
        }
        return true;
    }

    private void ApplyFlagEdits(List<FlagEdit> edits)
    {
        if (edits == null) return;

        foreach (var edit in edits)
        {
            if (!string.IsNullOrWhiteSpace(edit.key))
            {
                SetFlag(edit.key, edit.value);
            }
        }
    }

    // ===== 話者・本文の表示 =====

    private async UniTask ShowSpeakerAsync(int runId, DialogueStep step, bool hasSpeaker, bool hasBody, bool hasChoices, CancellationToken token)
    {
        if (!currentUI.speakerText) return;
        ThrowIfStale(runId);

        if (hasSpeaker)
        {
            if (currentUI.speakerBox) currentUI.speakerBox.SetActive(true);

            string speaker = await ResolveTextAsync(step.localizedSpeaker, step.speaker, token);
            currentUI.speakerText.text = ParseDialogueText(speaker);
            currentUI.speakerText.enabled = true;
            return;
        }

        // 選択肢だけのステップでは、直前の話者表示をそのまま残す
        bool isChoiceOnlyStep = !hasBody && hasChoices;
        if (isChoiceOnlyStep) return;

        if (currentUI.speakerBox) currentUI.speakerBox.SetActive(false);
        currentUI.speakerText.text = "";
        currentUI.speakerText.enabled = false;
    }

    private async UniTask ShowBodyAsync(int runId, DialogueStep step, bool hasBody, bool hasChoices, CancellationToken token)
    {
        string rawBody = await ResolveTextAsync(step.localizedText, step.text, token);

        if (hasBody)
        {
            await TypeTextAsync(runId, ParseDialogueText(rawBody), token);
        }
        else if (hasChoices)
        {
            // 本文なしで選択肢だけ出す場合も、枠（ダイアログボックス）は表示しておく
            if (currentUI.dialogueBox && !currentUI.dialogueBox.activeSelf)
            {
                currentUI.dialogueBox.SetActive(true);
            }
        }
        else
        {
            // 本文も選択肢もない: 空文字として処理され、ダイアログボックスが閉じる
            await TypeTextAsync(runId, rawBody, token);
        }
    }

    /// <summary>本文を 1 文字ずつ表示する。スキップ入力（長押し or ダブルクリック）で全文を即表示する。</summary>
    private async UniTask TypeTextAsync(int runId, string content, CancellationToken token)
    {
        string fullText = content ?? "";
        bool hasBody = !string.IsNullOrWhiteSpace(fullText);

        if (currentUI.dialogueBox) currentUI.dialogueBox.SetActive(hasBody);
        SetBodyText("");
        if (!hasBody) return;

        await NextFrameAsync(runId, token);

        float secondsPerChar = 1f / Mathf.Max(1f, charsPerSecond);
        float elapsed = 0f;
        bool skippedByDoubleClick = false;

        // 1 文字目は待たずに出す
        int shownCount = 1;
        SetBodyText(fullText.Substring(0, 1));

        while (shownCount < fullText.Length)
        {
            ThrowIfInterrupted(runId, token);

            if (ShouldSkipTyping(out bool byDoubleClick))
            {
                SetBodyText(fullText);
                skippedByDoubleClick = byDoubleClick;
                break;
            }

            elapsed += DeltaTime;
            while (elapsed >= secondsPerChar && shownCount < fullText.Length)
            {
                elapsed -= secondsPerChar;
                shownCount++;
                SetBodyText(fullText.Substring(0, shownCount));
            }

            await NextFrameAsync(runId, token);
        }

        // ダブルクリックの 2 回目が「次へ進む」として拾われないよう、少し待つ
        if (skippedByDoubleClick)
        {
            await DelaySmartAsync(SkipCooldownSeconds, token);
        }
    }

    /// <summary>このフレームで文字送りを飛ばすべきか。スキップボタン押下中、または「進む」のダブルクリックで true。</summary>
    private bool ShouldSkipTyping(out bool byDoubleClick)
    {
        byDoubleClick = false;
        if (_pauseAdvanceInput) return false;

        if (skipAction != null && skipAction.IsPressed()) return true;

        if (advanceAction != null && advanceAction.triggered)
        {
            float now = Now;
            byDoubleClick = now - lastClickTime <= doubleClickSeconds;
            lastClickTime = now;
            return byDoubleClick;
        }

        return false;
    }

    private void SetBodyText(string text)
    {
        if (currentUI.bodyText) currentUI.bodyText.text = text;
    }

    // ===== 入力待ち =====

    /// <summary>「進む」入力（押して離すまで）を待つ。キャンセル入力があれば会話ごと中断する。</summary>
    private async UniTask WaitAdvanceAsync(int runId, CancellationToken token)
    {
        await NextFrameAsync(runId, token);

        // 1) 前のステップから押しっぱなしなら、いったん離されるまで待つ
        if (!_pauseAdvanceInput && advanceAction != null)
        {
            while (advanceAction.IsPressed())
            {
                ThrowIfInterrupted(runId, token);
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }
        }

        // 2) 押されるのを待つ
        while (true)
        {
            ThrowIfInterrupted(runId, token);

            if (CancelRequested)
            {
                StopCurrent();
                return;
            }

            if (!_pauseAdvanceInput && advanceAction != null && advanceAction.triggered)
            {
                lastClickTime = Now;
                break;
            }

            await NextFrameAsync(runId, token);
        }

        // 3) 離されるのを待つ
        while (true)
        {
            ThrowIfInterrupted(runId, token);

            if (!_pauseAdvanceInput && advanceAction != null && advanceAction.WasReleasedThisFrame()) return;

            if (CancelRequested)
            {
                StopCurrent();
                return;
            }

            await NextFrameAsync(runId, token);
        }
    }

    private async UniTask DelaySmartAsync(float seconds, CancellationToken token)
    {
        await UniTask.Delay(TimeSpan.FromSeconds(Mathf.Max(0f, seconds)), DelayTiming, PlayerLoopTiming.Update, token);
    }

    // ===== 選択肢 =====

    /// <summary>
    /// 条件を満たす選択肢をボタンとして並べ、選ばれた選択肢の gotoId を返す。
    /// 表示できる選択肢がない場合と、キャンセルされた場合は null。
    /// </summary>
    private async UniTask<string> ShowChoicesAndWaitAsync(int runId, List<DialogueChoice> choices, CancellationToken token)
    {
        ClearChoices();
        ThrowIfStale(runId);

        var delayType = DelayTiming;
        await UniTask.Delay(TimeSpan.FromSeconds(ChoiceAppearDelaySeconds), delayType, PlayerLoopTiming.Update, token);
        ThrowIfStale(runId);

        var shownChoices = new List<(DialogueChoice choice, Button button)>();
        foreach (var choice in choices)
        {
            if (!CheckConditions(choice.conditions)) continue;

            var button = Instantiate(currentUI.choiceButtonPrefab, currentUI.choicesRoot);
            var label = button.GetComponentInChildren<TextMeshProUGUI>();
            if (label)
            {
                string choiceText = await ResolveTextAsync(choice.localizedText, choice.text, token);
                label.text = ParseDialogueText(choiceText);
            }

            button.gameObject.SetActive(true);
            shownChoices.Add((choice, button));
        }

        if (shownChoices.Count == 0) return null;

        // キーボード/ゲームパッドで操作できるよう、少し待ってから先頭を選択状態にする
        if (EventSystem.current != null)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(ChoiceFocusDelaySeconds), delayType, PlayerLoopTiming.Update, token);
            EventSystem.current.SetSelectedGameObject(shownChoices[0].button.gameObject);
        }

        string selectedGotoId = null;
        bool clicked = false;
        foreach (var (choice, button) in shownChoices)
        {
            string gotoId = choice.gotoId;
            button.onClick.AddListener(() =>
            {
                if (currentRunId != runId) return;
                selectedGotoId = gotoId;
                clicked = true;
            });
        }

        while (!clicked)
        {
            ThrowIfInterrupted(runId, token);

            if (CancelRequested)
            {
                ClearChoices();
                return null;
            }

            await NextFrameAsync(runId, token);
        }

        ClearChoices();
        return selectedGotoId;
    }

    private void ClearChoices()
    {
        if (currentUI == null || !currentUI.choicesRoot) return;

        for (int i = currentUI.choicesRoot.childCount - 1; i >= 0; i--)
        {
            var child = currentUI.choicesRoot.GetChild(i).gameObject;
            if (Application.isPlaying) Destroy(child);
            else DestroyImmediate(child);
        }
    }

    // ===== UI の後始末 =====

    private void CleanupUI()
    {
        if (currentUI == null) return;

        ClearChoices();

        if (currentUI.speakerText)
        {
            currentUI.speakerText.text = "";
            currentUI.speakerText.enabled = false;
        }
        SetBodyText("");
        if (currentUI.speakerBox) currentUI.speakerBox.SetActive(false);
        if (currentUI.dialogueBox) currentUI.dialogueBox.SetActive(false);

        lastClickTime = NeverClicked;

        if (dialogueRaycaster && !dialogueRaycaster.enabled)
        {
            dialogueRaycaster.enabled = true;
        }
    }

    private DialogueUIRefs CreateDefaultUIRefs()
    {
        return new DialogueUIRefs(
            defaultUIInfo.speakerBox,
            defaultUIInfo.speakerText,
            defaultUIInfo.dialogueBox,
            defaultUIInfo.bodyText,
            defaultUIInfo.choicesRoot,
            defaultUIInfo.choiceButtonPrefab);
    }

    // ===== 文言の解決（ローカライズ / プレースホルダー） =====

    private static bool HasText(LocalizedString localized, string fallback)
    {
        return (localized != null && !localized.IsEmpty) || !string.IsNullOrWhiteSpace(fallback);
    }

    /// <summary>ローカライズ設定があれば翻訳済みの文字列を、なければ旧データ（生の文字列）を返す。</summary>
    private static async UniTask<string> ResolveTextAsync(LocalizedString localized, string fallback, CancellationToken token)
    {
        if (localized != null && !localized.IsEmpty)
        {
            return await localized.GetLocalizedStringAsync(token);
        }
        return fallback;
    }

    /// <summary>
    /// 文言中のプレースホルダー（<c>#{関数名("引数")}</c>）を実際の値に置き換える。
    /// 例: <c>#{InputActionName("Click")}</c> → 現在のデバイスでのキー/ボタン名。
    /// </summary>
    public string ParseDialogueText(string rawText)
    {
        if (string.IsNullOrEmpty(rawText) || rawText.IndexOf("#{") == -1) return rawText;

        return placeholderRegex.Replace(rawText, match =>
        {
            string functionName = match.Groups[1].Value;
            string argument = match.Groups[2].Value;

            if (functionName.Equals("InputActionName", StringComparison.OrdinalIgnoreCase))
            {
                return InputActionName(argument);
            }

            Debug.LogWarning($"[DialogueController] 不明なプレースホルダー関数: {functionName}");
            return match.Value;
        });
    }

    /// <summary>アクション名から、いま使っている入力デバイスでの表示名（キー/ボタン名）を求める。</summary>
    private string InputActionName(string actionName)
    {
        if (playerInput == null)
        {
            Debug.LogWarning("[DialogueController] PlayerInput が null のため、InputActionName を取得できません。");
            return "[入力設定なし]";
        }

        var action = playerInput.actions.FindAction(actionName);
        if (action == null)
        {
            Debug.LogWarning($"[DialogueController] アクション '{actionName}' が見つかりません。");
            return $"[不明なキー: {actionName}]";
        }

        if (inputDeviceDetector == null)
        {
            Debug.LogWarning("[DialogueController] InputDeviceDetector が見つかりません。デフォルトのキー表示を使います。");
            return StripDeviceSuffix(action.GetBindingDisplayString());
        }

        string schemeName = inputDeviceDetector.CurrentDeviceType == InputDeviceDetector.DeviceType.Gamepad
            ? inputDeviceDetector.gamepadSchemeName
            : inputDeviceDetector.keyboardMouseSchemeName;

        if (string.IsNullOrEmpty(schemeName))
        {
            Debug.LogError("[DialogueController] InputDeviceDetector に Scheme 名が設定されていません。");
            return "[Scheme名未設定]";
        }

        foreach (var binding in action.bindings)
        {
            if (binding.groups != null && binding.groups.Contains(schemeName))
            {
                return StripDeviceSuffix(binding.ToDisplayString());
            }
        }

        return $"[キー未設定: {actionName}]";
    }

    /// <summary>"E [Keyboard]" のような表示名から、末尾のデバイス名部分を取り除く。</summary>
    private static string StripDeviceSuffix(string displayName)
    {
        int bracketIndex = displayName.LastIndexOf('[');
        return bracketIndex > 0 ? displayName.Substring(0, bracketIndex).Trim() : displayName;
    }

    // ===== 実行 ID・中断チェック =====

    /// <summary>新しい実行 ID を発行して現在の ID にする（それまでの会話は古い扱いになる）。</summary>
    private int IssueRunId()
    {
        currentRunId = nextRunId++;
        return currentRunId;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ThrowIfStale(int runId)
    {
        if (runId != currentRunId) throw new OperationCanceledException("stale run");
    }

    private void ThrowIfInterrupted(int runId, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        ThrowIfStale(runId);
    }

    /// <summary>1 フレーム待ち、その間に会話が差し替わっていたら中断する。</summary>
    private async UniTask NextFrameAsync(int runId, CancellationToken token)
    {
        await UniTask.Yield(PlayerLoopTiming.Update, token);
        ThrowIfStale(runId);
    }
}

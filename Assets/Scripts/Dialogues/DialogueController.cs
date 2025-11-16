using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading; // ← 【追加】
using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems; 
using System.Text.RegularExpressions;
using UnityEngine.Localization; // ← 【追加】
using UnityEngine.Localization.Components; 

[DisallowMultipleComponent]
public class DialogueController : Singleton<DialogueController>
{
    // ... (Fields from sequenceGroupAssets to inputDeviceDetector are unchanged) ...
    [Title("Sequence Group Assets (ScriptableObject)")]
    [OdinSerialize] public List<DialogueSequenceGroupAsset> sequenceGroupAssets = new();

    private Dictionary<string, Dictionary<string, DialogueSequenceAsset>> _lookup;

    [Title("UI (Default)")]
    [Tooltip("デフォルトのUI参照。オーバーライドされなかった場合に使われる。")]
    [SerializeField]
    private DefaultDialogueUIInfo defaultUIInfo;

    private DialogueUIRefs _currentUIRefs;

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
    private Coroutine _briefMessageCoroutine;
    [SerializeField] private GraphicRaycaster dialogueRaycaster;

    public void PauseAdvance(bool pause = true)
    {
        _pauseAdvanceInput = pause;
        if (dialogueRaycaster) dialogueRaycaster.enabled = !pause;
    }
    public bool IsAdvancePaused => _pauseAdvanceInput;

    private PlayerInput playerInput;
    private InputAction advanceAction, skipAction, cancelAction;
    private CancellationTokenSource _runCts;
    private GameStateController gameStateController;
    private PauseMenuController pauseMenuController; 

    private float lastClickTime = -999f;
    private float Now => useUnscaledTime ? Time.unscaledTime : Time.time;
    private int _runId = 0;
    private int _nextRunId = 1;
    private const int MaxGotoHops = 64;
    private const char _stepSep = '#';
    private InputDeviceDetector inputDeviceDetector;
    
    private static readonly Regex placeholderRegex = new Regex(@"\#\{(\w+)\(""([^""]*)""\)\}", RegexOptions.Compiled);

    // ... (OnValidate, Start, SceneLoaded, Init, OnDisable, RebuildLookup are unchanged) ...
    void OnValidate() { RebuildLookup(); }

    void Start()
    {
        RebuildLookup();
        inputDeviceDetector = InputDeviceDetector.Instance;
        SceneManager.sceneLoaded += SceneLoaded;
        Init();
        if (!dialogueRaycaster)
        {
            dialogueRaycaster = GetComponent<GraphicRaycaster>();
            if (!dialogueRaycaster)
                dialogueRaycaster = GetComponentInParent<GraphicRaycaster>();
        }
    }
    private void SceneLoaded(Scene scene, LoadSceneMode mode) {
        Init();
    }
    private void Init()
    {
        CleanupUI();
        MergeFlagsFromDataShare(); 
        uiSwitcher = UISwitcher.instance ?? uiSwitcher;
        gameStateController = GameStateController.instance ?? gameStateController;
        pauseMenuController = PauseMenuController.instance; 
        playerInput = PlayerDataHub.instance.CurrentplayerInput;
        var currentSceneUIRefs = DefaultDialogueUIInfo.instance;
        if (currentSceneUIRefs != null)
        {
            defaultUIInfo = DefaultDialogueUIInfo.instance;
            _currentUIRefs = new DialogueUIRefs(defaultUIInfo.speakerBox, defaultUIInfo.speakerText, defaultUIInfo.dialogueBox,
                                                defaultUIInfo.bodyText, defaultUIInfo.choicesRoot, defaultUIInfo.choiceButtonPrefab);
        }
        else
        {
            if (defaultUIInfo == null) {
                Debug.LogWarning("[DialogueController] 新しいシーンで DialogueUIRefs が見つかりませんでした。UIが表示されない可能性があります。");
            }
        }
        if (playerInput != null)
        {
            var map = playerInput.actions;
            advanceAction = map.FindAction(advanceActionName);
            skipAction = map.FindAction(skipActionName);
            cancelAction = map.FindAction(cancelActionName);
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
                    map[seq.id] = seq;
                }
            }
        }
    }

    // ... (Public API StartDialogue methods are unchanged) ...
    public void StartDialogue(string groupKey, string sequenceId, int startStepId = 1, bool changeGameState = true)
        => StartDialogueAsync(groupKey, sequenceId, null, default, startStepId, changeGameState).Forget();

    public void StartDialogue(DialogueSequenceGroupAsset groupAsset, string sequenceId, int startStepId = 1, bool changeGameState = true)
    {
        if (!groupAsset) { Debug.LogWarning("GroupAsset is null"); return; }
        StartDialogue(groupAsset.groupKey, sequenceId, startStepId, changeGameState);
    }

    public void StartDialogue(string groupKey, string sequenceId, DialogueUIRefs overrideUIRefs, int startStepId = 1, bool changeGameState = true)
        => StartDialogueAsync(groupKey, sequenceId, overrideUIRefs, default, startStepId, changeGameState).Forget();
    
    public void StartDialogue(DialogueSequenceGroupAsset groupAsset, string sequenceId, DialogueUIRefs overrideUIRefs, int startStepId = 1, bool changeGameState = true)
    {
        if (!groupAsset) { Debug.LogWarning("GroupAsset is null"); return; }
        StartDialogue(groupAsset.groupKey, sequenceId, overrideUIRefs, startStepId, changeGameState);
    }
    
    // ... (StartDialogueAsync and StopCurrent are unchanged) ...
    public async UniTask StartDialogueAsync(string groupKey, string sequenceId, DialogueUIRefs overrideUIRefs = null, CancellationToken externalToken = default, int startStepId = 1, bool changeGameState = true)
    {
        StopCurrent(); // (StopCurrent内で PauseMenuOpenPermit = true が呼ばれる)
        int runId = _nextRunId++; _runId = runId;

        if (overrideUIRefs != null)
        {
            _currentUIRefs = overrideUIRefs;
        }
        else
        {
            _currentUIRefs = new DialogueUIRefs(defaultUIInfo.speakerBox, defaultUIInfo.speakerText, defaultUIInfo.dialogueBox,
                                                defaultUIInfo.bodyText, defaultUIInfo.choicesRoot, defaultUIInfo.choiceButtonPrefab);
        }
        
        if (_currentUIRefs == null)
        {
            Debug.LogError("[DialogueController] UI references are not set correctly!");
            return;
        }

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
            
            if (pauseMenuController != null)
                pauseMenuController.PauseMenuOpenPermit = false;

            if (changeGameState && gameStateController) gameStateController.ActionByModeSelect(GameStateController.GameState.MenuOnGamePlay, true);
            await RunSequenceAsync(runId, groupKey, sequenceId, token, 0, startIndex);
        }
        catch (OperationCanceledException) { }
        finally
        {
            if (runId == _runId)
            {
                CleanupUI();
                onDialogueEnded?.Invoke();
                
                if (pauseMenuController != null)
                    pauseMenuController.PauseMenuOpenPermit = true;

                if (switchUIOnStartAndEnd && uiSwitcher) uiSwitcher.SwitchTo(UISwitcher.UIKind.Player);
                if (changeGameState && gameStateController) gameStateController.ActionByModeSelect(GameStateController.GameState.Gameplay, false);
            }
            _runCts?.Dispose(); _runCts = null;
            _currentUIRefs = new DialogueUIRefs(defaultUIInfo.speakerBox, defaultUIInfo.speakerText, defaultUIInfo.dialogueBox,
                                                defaultUIInfo.bodyText, defaultUIInfo.choicesRoot, defaultUIInfo.choiceButtonPrefab);
        }
    }

    public void StopCurrent()
    {
        _runId = _nextRunId++;
        if (_runCts != null && !_runCts.IsCancellationRequested) _runCts.Cancel();
        
        if (pauseMenuController != null)
            pauseMenuController.PauseMenuOpenPermit = true;

        CleanupUI();
    }

    public bool GetFlag(string key, bool def = false) => flags.TryGetValue(key, out var v) ? v : def;
    public void SetFlag(string key, bool val) => flags[key] = val;

    // ... (Core RunSequenceAsync is unchanged, uses await localization) ...
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
            if (step.flagEditsOnEnter != null)
                foreach (var e in step.flagEditsOnEnter)
                    if (!string.IsNullOrWhiteSpace(e.key)) SetFlag(e.key, e.value);
            
            bool hasSpeaker = (step.localizedSpeaker != null && !step.localizedSpeaker.IsEmpty) || !string.IsNullOrWhiteSpace(step.speaker);
            bool hasBody = (step.localizedText != null && !step.localizedText.IsEmpty) || !string.IsNullOrWhiteSpace(step.text);
            bool hasChoices = step.choices != null && step.choices.Count > 0;

            if (_currentUIRefs.speakerText)
            {
                ThrowIfStale(runId);
                if (hasSpeaker)
                {
                    if (_currentUIRefs.speakerBox) _currentUIRefs.speakerBox.SetActive(true);
                    
                    string speakerText;
                    if (step.localizedSpeaker != null && !step.localizedSpeaker.IsEmpty)
                    {
                        // ▼▼▼ 【修正点 1】 ▼▼▼
                        // SmartFormat が #{...} を壊さないよう、Arguments を null にして無効化する
                        step.localizedSpeaker.Arguments = null;
                        speakerText = await step.localizedSpeaker.GetLocalizedStringAsync(token);
                        // ▲▲▲ 【修正点 1】 ▲▲▲
                    }
                    else
                    {
                        speakerText = step.speaker; 
                    }
                    _currentUIRefs.speakerText.text = ParseDialogueText(speakerText);
                    
                    _currentUIRefs.speakerText.enabled = true;
                }
                else if (!hasBody && hasChoices) { }
                else
                {
                    if (_currentUIRefs.speakerBox) _currentUIRefs.speakerBox.SetActive(false);
                    _currentUIRefs.speakerText.text = "";
                    _currentUIRefs.speakerText.enabled = false;
                }
            }
            
            string rawBodyText;
            if (step.localizedText != null && !step.localizedText.IsEmpty)
            {
                // ▼▼▼ 【修正点 2】 ▼▼▼
                // SmartFormat が #{...} を壊さないよう、Arguments を null にして無効化する
                step.localizedText.Arguments = null; 
                rawBodyText = await step.localizedText.GetLocalizedStringAsync(token);
                // ▲▲▲ 【修正点 2】 ▲▲▲
            }
            else
            {
                rawBodyText = step.text; 
            }
            string parsedBodyText = ParseDialogueText(rawBodyText);
            
            if (hasBody)
            {
                await TypeTextAsync(runId, parsedBodyText, token);
            }
            else if (hasChoices)
            {
                if (_currentUIRefs.dialogueBox && !_currentUIRefs.dialogueBox.activeSelf)
                {
                    _currentUIRefs.dialogueBox.SetActive(true);
                }
            }
            else
            {
                await TypeTextAsync(runId, rawBodyText, token); 
            }

            ThrowIfStale(runId);
            
            if (hasChoices)
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
                else await WaitAdvanceAsync(runId, token);
                ThrowIfStale(runId);
            }
        }
    }
    // ... (CheckConditions, TryParseTarget, TryGetSequence are unchanged) ...
    private bool CheckConditions(List<FlagCondition> conds)
    {
        if (conds == null || conds.Count == 0) return true;
        foreach (var c in conds)
            if (GetFlag(c.key, false) != c.requiredValue) return false;
        return true;
    }
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
    public bool TryGetSequence(string groupKey, string seqId, out DialogueSequenceAsset asset)
    {
        asset = null;
        if (string.IsNullOrWhiteSpace(groupKey) || string.IsNullOrWhiteSpace(seqId)) return false;
        if (_lookup == null) RebuildLookup();
        if (!_lookup.TryGetValue(groupKey, out var map) || map == null) return false;
        return map.TryGetValue(seqId, out asset);
    }

    // ... (Typewriter TypeTextAsync is unchanged) ...
    private async UniTask TypeTextAsync(int runId, string content, CancellationToken token)
    {
        string full = content ?? "";
        bool hasBody = !string.IsNullOrWhiteSpace(full);
        
        if (_currentUIRefs.dialogueBox) _currentUIRefs.dialogueBox.SetActive(hasBody);
        if (_currentUIRefs.bodyText) _currentUIRefs.bodyText.text = "";
        if (!hasBody) return;
        
        await UniTask.Yield(PlayerLoopTiming.Update, token);
        ThrowIfStale(runId);
        int len = full.Length;
        int idx = 0;
        float secPerChar = 1f / Mathf.Max(1f, charsPerSecond);
        float acc = 0f;
        
        bool skippedByDoubleClick = false;

        if (len > 0)
        {
            idx = 1;
            if (_currentUIRefs.bodyText) _currentUIRefs.bodyText.text = full.Substring(0, 1);
        }
        while (idx < len)
        {
            token.ThrowIfCancellationRequested();
            ThrowIfStale(runId);
            if (!_pauseAdvanceInput)
            {
                if (skipAction != null && skipAction.IsPressed())
                {
                    if (_currentUIRefs.bodyText) _currentUIRefs.bodyText.text = full;
                    break;
                }
                if (advanceAction != null && advanceAction.triggered)
                {
                    float now = Now;
                    if (now - lastClickTime <= doubleClickSeconds)
                    {
                        if (_currentUIRefs.bodyText) _currentUIRefs.bodyText.text = full;
                        lastClickTime = now;
                        skippedByDoubleClick = true; 
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
                if (_currentUIRefs.bodyText) _currentUIRefs.bodyText.text = full.Substring(0, idx);
            }
            await UniTask.Yield(PlayerLoopTiming.Update, token);
            ThrowIfStale(runId);
        }

        if (skippedByDoubleClick)
        {
            await DelaySmartAsync(0.5f, token);
        }
    }
    
    // ... (Wait/Delay methods are unchanged) ...
    private async UniTask WaitAdvanceAsync(int runId, CancellationToken token)
    {
        await UniTask.Yield(PlayerLoopTiming.Update, token);
        ThrowIfStale(runId);

        if (!_pauseAdvanceInput && advanceAction != null)
        {
            while (advanceAction.IsPressed()) 
            {
                token.ThrowIfCancellationRequested();
                ThrowIfStale(runId);
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }
        }
        
        bool inputPressed = false;
        while (true)
        {
            token.ThrowIfCancellationRequested();
            ThrowIfStale(runId);
            
            if (cancelAction != null && cancelAction.triggered)
            { StopCurrent(); return; }
            
            if (!_pauseAdvanceInput && advanceAction != null && advanceAction.triggered)
            {
                inputPressed = true;
                lastClickTime = Now;
                break; 
            }
            
            await UniTask.Yield(PlayerLoopTiming.Update, token);
            ThrowIfStale(runId);
        }

        while (inputPressed)
        {
            token.ThrowIfCancellationRequested();
            ThrowIfStale(runId);

            if (!_pauseAdvanceInput && advanceAction != null && advanceAction.WasReleasedThisFrame())
            {
                return; 
            }

            if (cancelAction != null && cancelAction.triggered)
            { StopCurrent(); return; }
            
            await UniTask.Yield(PlayerLoopTiming.Update, token);
            ThrowIfStale(runId);
        }
    }
    private async UniTask DelaySmartAsync(float seconds, CancellationToken token)
    {
        var delayType = useUnscaledTime ? DelayType.UnscaledDeltaTime : DelayType.DeltaTime;
        await UniTask.Delay(TimeSpan.FromSeconds(Mathf.Max(0f, seconds)), delayType, PlayerLoopTiming.Update, token);
    }

    // ... (ShowChoicesAndWaitAsync is unchanged, uses await localization) ...
    private async UniTask<string> ShowChoicesAndWaitAsync(int runId, List<DialogueChoice> choices, CancellationToken token)
    {
        ClearChoices();
        ThrowIfStale(runId);
        
        var delayType = useUnscaledTime ? DelayType.UnscaledDeltaTime : DelayType.DeltaTime;
        await UniTask.Delay(TimeSpan.FromSeconds(0.1f), delayType, PlayerLoopTiming.Update, token);
        ThrowIfStale(runId); 

        var usable = new List<(DialogueChoice, Button)>();
        foreach (var ch in choices)
        {
            if (!CheckConditions(ch.conditions)) continue;
            var btn = UnityEngine.Object.Instantiate(_currentUIRefs.choiceButtonPrefab, _currentUIRefs.choicesRoot);
            
            var label = btn.GetComponentInChildren<TextMeshProUGUI>();
            if (label)
            {
                string choiceText;
                if (ch.localizedText != null && !ch.localizedText.IsEmpty)
                {
                    choiceText = await ch.localizedText.GetLocalizedStringAsync(token);
                }
                else
                {
                    choiceText = ch.text; 
                }
                label.text = choiceText; 
            }

            btn.gameObject.SetActive(true);
            usable.Add((ch, btn));
        }
        
        if (usable.Count == 0) return null; 

        if (EventSystem.current != null)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(0.5f), delayType, PlayerLoopTiming.Update, token);
            EventSystem.current.SetSelectedGameObject(usable[0].Item2.gameObject);
        }

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

    // ... (ClearChoices and CleanupUI are unchanged) ...
    private void ClearChoices()
    {
        if (_currentUIRefs == null || !_currentUIRefs.choicesRoot) return;
        for (int i = _currentUIRefs.choicesRoot.childCount - 1; i >= 0; i--)
        {
            var go = _currentUIRefs.choicesRoot.GetChild(i).gameObject;
            if (Application.isPlaying) Destroy(go);
            else DestroyImmediate(go);
        }
    }
    private void CleanupUI()
    {
        if (_currentUIRefs == null) return;
        ClearChoices();
        if (_currentUIRefs.speakerText) { _currentUIRefs.speakerText.text = ""; _currentUIRefs.speakerText.enabled = false; }
        if (_currentUIRefs.bodyText) { _currentUIRefs.bodyText.text = ""; }
        if (_currentUIRefs.speakerBox) _currentUIRefs.speakerBox.SetActive(false);
        if (_currentUIRefs.dialogueBox) _currentUIRefs.dialogueBox.SetActive(false);
        lastClickTime = -999f;
        if (dialogueRaycaster && !dialogueRaycaster.enabled)
            dialogueRaycaster.enabled = true;
    }
    
    // ... (ShowBriefMessage and BriefMessageRoutine are unchanged) ...
    public void ShowBriefMessage(DialogueUIRefs uiRefs, string speaker, string body, float duration)
    {
        if (_briefMessageCoroutine != null)
        {
            StopCoroutine(_briefMessageCoroutine);
        }
        _briefMessageCoroutine = StartCoroutine(BriefMessageRoutine(uiRefs, speaker, body, duration));
    }

    private IEnumerator BriefMessageRoutine(DialogueUIRefs customUiRef, string speaker, string body, float duration)
    {
        DialogueUIRefs uiToUse = customUiRef ?? new DialogueUIRefs(defaultUIInfo.speakerBox, defaultUIInfo.speakerText, defaultUIInfo.dialogueBox,
                                                                    defaultUIInfo.bodyText, defaultUIInfo.choicesRoot, defaultUIInfo.choiceButtonPrefab);

        if (uiToUse == null)
        {
            Debug.LogError("[DialogueController] CRITICAL ERROR: 表示に使用できるUIがありません。'Default UI Refs'が設定されているか確認してください。");
            _briefMessageCoroutine = null;
            yield break; 
        }
        
        bool hasBody = !string.IsNullOrWhiteSpace(body);
        uiToUse.bodyText.text = body; 
        uiToUse.dialogueBox.SetActive(hasBody);

        bool hasSpeaker = !string.IsNullOrWhiteSpace(speaker);
        if(uiToUse.speakerBox) uiToUse.speakerBox.SetActive(hasSpeaker);
        if(uiToUse.speakerText) uiToUse.speakerText.text = speaker; 

        if (duration > 0)
        {
            yield return new WaitForSeconds(duration);
        }
        else
        {
            yield return null;
        }

        uiToUse.dialogueBox.SetActive(false);
        if(uiToUse.speakerBox) uiToUse.speakerBox.SetActive(false);
        
        _briefMessageCoroutine = null;
    }

    // ▼▼▼ 【(1) 新しいメソッドを追加】 ▼▼▼
    /// <summary>
    /// 翻訳が必要なステップ情報（LocalizedString）を受け取り、
    /// 翻訳後に BriefMessage を表示する非同期ラッパー。
    /// </summary>
    public void ShowBriefLocalizedStep(DialogueUIRefs uiRefs, DialogueStep step, float duration)
    {
        // 待機しない（.Forget()）
        ShowBriefLocalizedStepAsync(uiRefs, step, duration).Forget();
    }

    private async UniTaskVoid ShowBriefLocalizedStepAsync(DialogueUIRefs uiRefs, DialogueStep step, float duration)
    {
        if (step == null)
        {
            ShowBriefMessage(uiRefs, "", "", duration);
            return;
        }

        string speakerText;
        string bodyText;

        // 1. Localized Speaker 解決
        if (step.localizedSpeaker != null && !step.localizedSpeaker.IsEmpty)
        {
            // ▼▼▼ 【修正点 3】 ▼▼▼
            // SmartFormat が #{...} を壊さないよう、Arguments を null にして無効化する
            step.localizedSpeaker.Arguments = null;
            // このメソッドは .Forget() されるので、CancellationToken.None を使う
            speakerText = await step.localizedSpeaker.GetLocalizedStringAsync(CancellationToken.None);
            // ▲▲▲ 【修正点 3】 ▲▲▲
        }
        else
        {
            speakerText = step.speaker; // フォールバック
        }

        // 2. Localized Body 解決
        if (step.localizedText != null && !step.localizedText.IsEmpty)
        {
            // ▼▼▼ 【修正点 4】 ▼▼▼
            // SmartFormat が #{...} を壊さないよう、Arguments を null にして無効化する
  	        step.localizedText.Arguments = null;
            bodyText = await step.localizedText.GetLocalizedStringAsync(CancellationToken.None);
            // ▲▲▲ 【修正点 4】 ▲▲▲
        }
        else
        {
            bodyText = step.text; // フォールバック
        }

        // 3. 翻訳済みテキストで BriefMessage を呼び出す
        ShowBriefMessage(
            uiRefs,
            ParseDialogueText(speakerText), 
            ParseDialogueText(bodyText),    
            duration
        );
    }
    // ▲▲▲ 【(1) 追加ここまで】 ▲▲▲
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ThrowIfStale(int runId)
    {
        if (runId != _runId) throw new OperationCanceledException("stale run");
    }

    private void MergeFlagsFromDataShare()
    {
        var ds = DataShareController.instance;
        if (ds == null) return;

        foreach (var kv in ds.EndingShareDictionary)
        {
            if (!string.IsNullOrWhiteSpace(kv.Key))
            {
                flags[kv.Key] = kv.Value;
            }
        }
    }
    
    // ▼▼▼ 【(2) private から public に変更】 ▼▼▼
    public string ParseDialogueText(string rawText)
    // ▲▲▲ 【(2) 変更ここまで】 ▲▲▲
    {
        if (string.IsNullOrEmpty(rawText) || rawText.IndexOf("#{") == -1)
        {
            return rawText;
        }
        
        string processedText = placeholderRegex.Replace(rawText, (match) => 
        {
            string functionName = match.Groups[1].Value;
            string argument = match.Groups[2].Value;
            
            if (functionName.Equals("InputActionName", StringComparison.OrdinalIgnoreCase))
            {
                return InputActionName(argument);
            }
            else
            {
                Debug.LogWarning($"[DialogueController] 不明なプレースホルダー関数: {functionName}");
                return match.Value; 
            }
        });

        return processedText;
    }
    
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
            string fallbackName = action.GetBindingDisplayString();
            int deviceNameStartIndexFB = fallbackName.LastIndexOf('[');
            if (deviceNameStartIndexFB > 0)
            {
                fallbackName = fallbackName.Substring(0, deviceNameStartIndexFB).Trim();
            }
            return fallbackName; 
        }

        string targetSchemeName;
        if (inputDeviceDetector.CurrentDeviceType == InputDeviceDetector.DeviceType.Gamepad)
        {
            targetSchemeName = inputDeviceDetector.gamepadSchemeName;
        }
        else
        {
            targetSchemeName = inputDeviceDetector.keyboardMouseSchemeName;
        }
        
        if (string.IsNullOrEmpty(targetSchemeName))
        {
             Debug.LogError("[DialogueController] InputDeviceDetector に Scheme 名が設定されていません。");
             return "[Scheme名未設定]";
        }

        foreach (var binding in action.bindings)
        {
            if (binding.groups != null && binding.groups.Contains(targetSchemeName))
            {
                string bindingName = binding.ToDisplayString();
                
                int deviceNameStartIndex = bindingName.LastIndexOf('[');
                if (deviceNameStartIndex > 0)
                {
                    bindingName = bindingName.Substring(0, deviceNameStartIndex).Trim();
                }
                
                return bindingName;
            }
        }
        
        return $"[キー未設定: {actionName}]";
    }
}
// DialogueController.cs
using System;
using System.Collections;
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
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems; 

[DisallowMultipleComponent]
public class DialogueController : Singleton<DialogueController>
{
    [Title("Sequence Group Assets (ScriptableObject)")]
    [OdinSerialize] public List<DialogueSequenceGroupAsset> sequenceGroupAssets = new();

    private Dictionary<string, Dictionary<string, DialogueSequenceAsset>> _lookup;

    [Title("UI (Default)")]
    [Tooltip("デフォルトのUI参照。オーバーライドされなかった場合に使われる。")]
    [SerializeField]
    private DefaultDialogueUIInfo defaultUIInfo;

    // 実行時に使用するUI参照を動的に保持する
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
    private PauseMenuController pauseMenuController; // PauseMenuControllerへの参照

    private float lastClickTime = -999f;
    private float Now => useUnscaledTime ? Time.unscaledTime : Time.time;
    private int _runId = 0;
    private int _nextRunId = 1;
    private const int MaxGotoHops = 64;
    private const char _stepSep = '#';

    void OnValidate() { RebuildLookup(); }

    void Start()
    {
        RebuildLookup();
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

    // ===== Public API =====
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

    // ===== Core =====
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
            
            // ▼▼▼ ★★★ (ここから修正) ★★★ ▼▼▼

            // --- ステップのプロパティを事前チェック ---
            bool hasSpeaker = !string.IsNullOrWhiteSpace(step.speaker);
            bool hasBody = !string.IsNullOrWhiteSpace(step.text);
            bool hasChoices = step.choices != null && step.choices.Count > 0;

            // --- スピーカー(話者)のロジック ---
            if (_currentUIRefs.speakerText)
            {
                ThrowIfStale(runId);
                
                if (hasSpeaker)
                {
                    // このステップに話者が設定されている場合
                    if (_currentUIRefs.speakerBox) _currentUIRefs.speakerBox.SetActive(true);
                    _currentUIRefs.speakerText.text = step.speaker;
                    _currentUIRefs.speakerText.enabled = true;
                }
                else if (!hasBody && hasChoices)
                {
                    // このステップが「選択肢のみ」（本文なし）の場合
                    // → 前のステップの話者名を表示したままにする (何もしない)
                }
                else
                {
                    // このステップに話者がなく、かつ選択肢のみでもない場合
                    // (例: 本文だけ、またはフラグだけのステップ)
                    // → 話者ボックスを隠す
                    if (_currentUIRefs.speakerBox) _currentUIRefs.speakerBox.SetActive(false);
                    _currentUIRefs.speakerText.text = "";
                    _currentUIRefs.speakerText.enabled = false;
                }
            }

            // --- 本文(Body)のロジック ---
            if (hasBody)
            {
                // このステップに本文がある場合
                // → TypeTextAsync を呼び出す (内部でボックスが表示される)
                await TypeTextAsync(runId, step.text, token);
            }
            else if (hasChoices)
            {
                // このステップに本文がなく、選択肢だけがある場合
                // → TypeTextAsync を呼ばない (前のステップの本文が消えない)
                // (念のため、ボックスが非表示になっていたら表示する)
                if (_currentUIRefs.dialogueBox && !_currentUIRefs.dialogueBox.activeSelf)
                {
                    _currentUIRefs.dialogueBox.SetActive(true);
                }
            }
            else
            {
                // このステップに本文も選択肢もない場合
                // (例: フラグ操作のみのステップ、または空のステップ)
                // → TypeTextAsync に空の文字列を渡し、ボックスを非表示にする
                await TypeTextAsync(runId, step.text, token);
            }

            ThrowIfStale(runId);
            
            // --- 選択肢 / 次へのロジック ---
            if (hasChoices)
            {
                // (元のロジックと変更なし)
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
                // (元のロジックと変更なし)
                if (step.autoNext) await DelaySmartAsync(step.autoNextDelay, token);
                else await WaitAdvanceAsync(runId, token);
                ThrowIfStale(runId);
            }

            // ▲▲▲ ★★★ (ここまで修正) ★★★ ▲▲▲
        }
    }
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
        
        if (_currentUIRefs.dialogueBox) _currentUIRefs.dialogueBox.SetActive(hasBody);
        if (_currentUIRefs.bodyText) _currentUIRefs.bodyText.text = "";
        if (!hasBody) return;
        
        await UniTask.Yield(PlayerLoopTiming.Update, token);
        ThrowIfStale(runId);
        int len = full.Length;
        int idx = 0;
        float secPerChar = 1f / Mathf.Max(1f, charsPerSecond);
        float acc = 0f;
        
        // ▼▼▼ ★★★ (ここから追加) ★★★ ▼▼▼
        // ダブルクリックでスキップしたかを判定するフラグ
        bool skippedByDoubleClick = false;
        // ▲▲▲ ★★★ (ここまで追加) ★★★ ▲▲▲

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
                        
                        // ▼▼▼ ★★★ (ここから追加) ★★★ ▼▼▼
                        skippedByDoubleClick = true; // スキップしたことを記録
                        // ▲▲▲ ★★★ (ここまで追加) ★★★ ▲▲▲
                        
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

        // ▼▼▼ ★★★ (ここから追加) ★★★ ▼▼▼
        // ループ終了後（テキストが全部表示された後）
        if (skippedByDoubleClick)
        {
            // もしダブルクリックでスキップされていたら、
            // 0.5秒のクールダウンを入れ、次の WaitAdvanceAsync が即座に反応するのを防ぐ
            await DelaySmartAsync(0.5f, token);
        }
        // ▲▲▲ ★★★ (ここまで追加) ★★★ ▲▲▲
    }    
    // ===== Wait / Delay =====
// ===== Wait / Delay =====
    private async UniTask WaitAdvanceAsync(int runId, CancellationToken token)
    {
        // ▼▼▼ ★★★ (ここから修正) ★★★ ▼▼▼
        
        await UniTask.Yield(PlayerLoopTiming.Update, token);
        ThrowIfStale(runId);

        // (1) クールダウン処理:
        // もしこの時点で advanceAction が押されっぱなしなら (テキストスキップの入力)、
        // ユーザーが一度ボタンを離すまで待機する
        if (!_pauseAdvanceInput && advanceAction != null)
        {
            while (advanceAction.IsPressed()) // 押されている間
            {
                token.ThrowIfCancellationRequested();
                ThrowIfStale(runId);
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }
        }
        
        // (2) 新しい「押し込み」を待機
        bool inputPressed = false;
        while (true)
        {
            token.ThrowIfCancellationRequested();
            ThrowIfStale(runId);
            
            if (cancelAction != null && cancelAction.triggered)
            { StopCurrent(); return; }
            
            // ユーザーが「新しく」ボタンを押した瞬間 (triggered) を待つ
            if (!_pauseAdvanceInput && advanceAction != null && advanceAction.triggered)
            {
                inputPressed = true;
                lastClickTime = Now;
                break; // 押し込みを検知したので、(3) の「リリース待ち」に移行
            }
            
            await UniTask.Yield(PlayerLoopTiming.Update, token);
            ThrowIfStale(runId);
        }

        // (3) 「リリース」を待機
        // (inputPressed が true の場合のみ、ここに到達する)
        while (inputPressed)
        {
            token.ThrowIfCancellationRequested();
            ThrowIfStale(runId);

            // ユーザーがボタンを「離した」瞬間 (WasReleasedThisFrame) を待つ
            if (!_pauseAdvanceInput && advanceAction != null && advanceAction.WasReleasedThisFrame())
            {
                return; // ★ここで初めて次のステップに進む
            }

            // (もしキャンセルされたら、ここで while ループを抜ける)
            if (cancelAction != null && cancelAction.triggered)
            { StopCurrent(); return; }
            
            await UniTask.Yield(PlayerLoopTiming.Update, token);
            ThrowIfStale(runId);
        }
        // ▲▲▲ ★★★ (ここまで修正) ★★★ ▲▲▲
    }
    private async UniTask DelaySmartAsync(float seconds, CancellationToken token)
    {
        var delayType = useUnscaledTime ? DelayType.UnscaledDeltaTime : DelayType.DeltaTime;
        await UniTask.Delay(TimeSpan.FromSeconds(Mathf.Max(0f, seconds)), delayType, PlayerLoopTiming.Update, token);
    }

    // ===== Choices =====
    // ▼▼▼ ★★★ ここを修正 ★★★ ▼▼▼
    private async UniTask<string> ShowChoicesAndWaitAsync(int runId, List<DialogueChoice> choices, CancellationToken token)
    {
        ClearChoices();
        ThrowIfStale(runId);
        
        // ▼▼▼ ★★★ (ここから追加) 1秒待機 ★★★ ▼▼▼
        // useUnscaledTime フラグを考慮して1秒待機する
        var delayType = useUnscaledTime ? DelayType.UnscaledDeltaTime : DelayType.DeltaTime;
        await UniTask.Delay(TimeSpan.FromSeconds(0.5f), delayType, PlayerLoopTiming.Update, token);
        ThrowIfStale(runId); // 待機したのでStaleチェック
        // ▲▲▲ ★★★ (ここまで追加) ★★★ ▲▲▲

        var usable = new List<(DialogueChoice, Button)>();
        foreach (var ch in choices)
        {
            if (!CheckConditions(ch.conditions)) continue;
            var btn = UnityEngine.Object.Instantiate(_currentUIRefs.choiceButtonPrefab, _currentUIRefs.choicesRoot);
            var label = btn.GetComponentInChildren<TextMeshProUGUI>();
            if (label) label.text = ch.text;
            btn.gameObject.SetActive(true);
            usable.Add((ch, btn));
        }
        
        if (usable.Count == 0) return null; // 有効な選択肢がなければ終了

        // 最初のボタンを EventSystem で選択状態にし、コントローラー操作を可能にする
        if (EventSystem.current != null)
        {
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
        
        // ▼▼▼ ★★★ (1フレーム待機を削除) ★★★ ▼▼▼
        // (1秒待機を先頭に追加したため、この1フレーム待機は不要)
        // await UniTask.Yield(PlayerLoopTiming.Update, token);
        // ThrowIfStale(runId); 
        // ▲▲▲ ★★★ (削除) ★★★ ▲▲▲

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
    // ▲▲▲ ★★★ 修正ここまで ★★★ ▲▲▲

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
    /// <summary>
    /// 一つのメッセージを指定されたUIで、指定された時間だけ表示し、自動で消去します。
    /// </summary>
    /// <param name="uiRefs">使用するUIの参照。nullの場合はデフォルトUIが使用されます。</param>
    /// <param name="speaker">話者名</param>
    /// <param name="body">本文</param>
    /// <param name="duration">表示時間（秒）</param>
    public void ShowBriefMessage(DialogueUIRefs uiRefs, string speaker, string body, float duration)
    {
        // 以前のコルーチンが動いていたら、まず停止させる
        if (_briefMessageCoroutine != null)
        {
            StopCoroutine(_briefMessageCoroutine);
        }
        _briefMessageCoroutine = StartCoroutine(BriefMessageRoutine(uiRefs, speaker, body, duration));
    }

    private IEnumerator BriefMessageRoutine(DialogueUIRefs customUiRef, string speaker, string body, float duration)
    {
        // 使用するUIを決定する (指定がなければデフォルトUIにフォールバック)
        DialogueUIRefs uiToUse = customUiRef ?? new DialogueUIRefs(defaultUIInfo.speakerBox, defaultUIInfo.speakerText, defaultUIInfo.dialogueBox,
                                                                    defaultUIInfo.bodyText, defaultUIInfo.choicesRoot, defaultUIInfo.choiceButtonPrefab);

        // --- 1. UIの存在チェック ---
        if (uiToUse == null)
        {
            Debug.LogError("[DialogueController] CRITICAL ERROR: 表示に使用できるUIがありません。'Default UI Refs'が設定されているか確認してください。");
            _briefMessageCoroutine = null;
            yield break; // UIがないので処理を中断
        }
        // --- 2. 表示処理 ---
        bool hasBody = !string.IsNullOrWhiteSpace(body);
        uiToUse.bodyText.text = body;
        uiToUse.dialogueBox.SetActive(hasBody);

        bool hasSpeaker = !string.IsNullOrWhiteSpace(speaker);
        if(uiToUse.speakerBox) uiToUse.speakerBox.SetActive(hasSpeaker);
        if(uiToUse.speakerText) uiToUse.speakerText.text = speaker;
        // --- 3. 待機処理 ---
        if (duration > 0)
        {
            yield return new WaitForSeconds(duration);
        }
        else
        {
            // durationが0以下の場合は、一瞬だけ表示して消す
            yield return null;
        }

        // --- 4. 非表示処理 ---
        uiToUse.dialogueBox.SetActive(false);
        if(uiToUse.speakerBox) uiToUse.speakerBox.SetActive(false);
        
        _briefMessageCoroutine = null;
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ThrowIfStale(int runId)
    {
        if (runId != _runId) throw new OperationCanceledException("stale run");
    }


    /// <summary>
    /// DataShareControllerからフラグの値をマージ（同期）します。
    /// </summary>
    private void MergeFlagsFromDataShare()
    {
        var ds = DataShareController.instance;
        if (ds == null) return;

        // DataShareControllerのEndingShareDictionaryからフラグを読み込む
        // 既存のキーは上書きされ、新しいキーは追加されます。
        foreach (var kv in ds.EndingShareDictionary)
        {
            if (!string.IsNullOrWhiteSpace(kv.Key))
            {
                flags[kv.Key] = kv.Value;
                // Debug.Log($"[DialogueController] Merged flag from DataShare: {kv.Key} = {kv.Value}");
            }
        }
    }
}
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class DialogueController : Singleton<DialogueController>
{
    [Title("Database / UI")]
    public DialogueDatabase database;

    [Header("UI Refs")]
    public CanvasGroup canvasGroup;
    public TextMeshProUGUI speakerText;   // スピーカー名
    public TextMeshProUGUI bodyText;      // 本文
    public Transform choicesRoot;         // 選択肢ボタンの親
    public Button choiceButtonPrefab;     // TextMeshProUGUIを子に持つ前提

    [Title("Typewriter")]
    [Tooltip("1秒あたりの表示文字数（大きいほど速い）")]
    public float charsPerSecond = 35f;
    [Tooltip("timeScaleの影響を受けない（ポーズ中も進む）")]
    public bool useUnscaledTime = true;

    [Title("Input Actions (名前)")]
    [Tooltip("行送り（シングル）。Interactionは Press(Press Only) 推奨")]
    public string advanceActionName = "Submit";
    [Tooltip("押しっぱ高速スキップ（任意）")]
    public string skipActionName    = "Skip";
    public string cancelActionName  = "Cancel";

    [Title("ダブルクリック（自前判定）")]
    [Tooltip("2回タップをダブル判定する最大間隔秒")]
    public float doubleClickSeconds = 0.28f;
    [Tooltip("クリックを有効とみなす保持時間（行またぎ許容のため少し長め）")]
    public float clickBufferSeconds = 0.40f;
    [Tooltip("advanceAction.performed のデバウンス最小間隔秒（環境差対策）")]
    public float pressDebounceSeconds = 0.01f;

    [Title("レイアウト安定化（ContentSizeFitter/VerticalLayout用）")]
    [SerializeField] private LayoutElement dialogueBoxLayout; // DialogueBox に付けた LayoutElement
    [SerializeField] private float minHeightPadding = 4f;
    [SerializeField] private bool lockHeightWhileTyping = true;

    [Title("フラグ（条件分岐用）")]
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

    private bool isRunning;
    private bool isTyping;

    private CancellationTokenSource _runCts;
    private GameStateController gameStateController;

    // ==== クリックバッファ（自前ダブルクリック検知） ====
    private class ClickBuffer
    {
        private readonly List<float> times = new List<float>(4);

        public void Push(float t) => times.Add(t);

        public void PruneOlderThan(float minTime)
        {
            for (int i = times.Count - 1; i >= 0; i--)
                if (times[i] < minTime) times.RemoveAt(i);
        }

        // 直近2つ（どちらも有効期間内）が doubleWindow 以内なら両方消費
        public bool TryConsumeDouble(float now, float doubleWindow, float validWindow)
        {
            PruneOlderThan(now - validWindow);
            int n = times.Count;
            if (n < 2) return false;
            float a = times[n - 1];
            float b = times[n - 2];
            if (a - b <= doubleWindow)
            {
                times.RemoveAt(n - 1);
                times.RemoveAt(n - 2);
                return true;
            }
            return false;
        }

        // seed（前行で消費したクリック時刻）と、行開始後(minSecondTime以降)のクリックを組み合わせてダブル判定
        public bool TryConsumeDoubleWithSeed(float seedTime, float minSecondTime, float now, float doubleWindow, float validWindow)
        {
            if (seedTime < 0f) return false;
            if (seedTime < now - validWindow) return false; // 古すぎるseedは無効

            PruneOlderThan(now - validWindow);
            for (int i = 0; i < times.Count; i++)
            {
                float t = times[i];
                if (t < minSecondTime) continue;            // ★ 行開始前のクリックは無視
                if (t - seedTime <= doubleWindow)
                {
                    times.RemoveAt(i);
                    return true;
                }
            }
            return false;
        }

        // 単発を1つ消費（古い順）。消費した時刻を out で返す
        public bool TryConsumeSingle(float now, float validWindow, out float consumedTime)
        {
            PruneOlderThan(now - validWindow);
            if (times.Count == 0)
            {
                consumedTime = -1f;
                return false;
            }
            consumedTime = times[0];
            times.RemoveAt(0);
            return true;
        }

        public void Clear() => times.Clear();
    }

    private ClickBuffer _clicks = new ClickBuffer();

    // 前行で「行送り」に使ったクリックの時刻（次行のダブル判定用）
    private float _lastAdvanceConsumedTime = -1f;

    // 次行開始の時刻（これより前のクリックはダブル2発目として認めない）
    private float _lineStartTime = -1f;

    // 連続 performed のデバウンス
    private float _lastPushTime = -999f;

    private float NowTime => useUnscaledTime ? Time.unscaledTime : Time.time;

    void Start()
    {
        if (canvasGroup) SetCanvasVisible(false);
        if (speakerText) speakerText.text = "";
        if (bodyText) bodyText.text = "";

        uiSwitcher = UISwitcher.instance ?? uiSwitcher;
        gameStateController = GameStateController.instance ?? gameStateController;

        playerInput = GetComponent<PlayerInput>();
        if (!playerInput && PlayerStatus.instance) playerInput = PlayerStatus.instance.playerInput;

        if (playerInput != null)
        {
            advanceAction = playerInput.actions.FindAction(advanceActionName);
            skipAction    = playerInput.actions.FindAction(skipActionName);
            cancelAction  = playerInput.actions.FindAction(cancelActionName);

            if (advanceAction != null)
            {
                advanceAction.performed -= OnAdvancePerformed;
                advanceAction.performed += OnAdvancePerformed;
            }
        }
        else
        {
            Debug.LogWarning("[DialogueController] PlayerInput が見つかりません。");
        }
    }

    void OnDisable()
    {
        if (advanceAction != null) advanceAction.performed -= OnAdvancePerformed;
        StopCurrent();
    }

    // ========= 外部API =========

    public void StartDialogue(string id)
    {
        if (gameStateController != null)
            gameStateController.ActionByModeSelect(GameStateController.GameState.MenuOnGamePlay, true);

        StartDialogueAsync(id).Forget();
    }

    public async UniTask StartDialogueAsync(string id, CancellationToken externalToken = default)
    {
        StopCurrent();
        if (database == null || !database.TryGet(id, out var seq) || seq.steps.Count == 0)
        {
            Debug.LogWarning($"[DialogueController] Dialogue id '{id}' が見つからないか空です。", database);
            return;
        }

        _runCts = CancellationTokenSource.CreateLinkedTokenSource(externalToken);
        var token = _runCts.Token;

        try
        {
            isRunning = true;

            if (switchUIOnStartAndEnd && uiSwitcher != null)
                uiSwitcher.SwitchTo(UISwitcher.UIKind.Dialogue);

            onDialogueStarted?.Invoke();
            SetCanvasVisible(true);

            await RunSequenceAsync(id, token);
        }
        catch (OperationCanceledException) { }
        finally
        {
            CleanupUI();
            isRunning = false;
            onDialogueEnded?.Invoke();

            if (switchUIOnStartAndEnd && uiSwitcher != null)
                uiSwitcher.SwitchTo(UISwitcher.UIKind.Player);

            if (gameStateController != null)
                gameStateController.ActionByModeSelect(GameStateController.GameState.Gameplay, false);

            if (_runCts != null) { _runCts.Dispose(); _runCts = null; }
        }
    }

    public void StopCurrent()
    {
        if (_runCts != null && !_runCts.IsCancellationRequested)
            _runCts.Cancel();

        CleanupUI();
        isRunning = false;
    }

    public bool GetFlag(string key, bool defaultVal = false)
        => flags.TryGetValue(key, out var v) ? v : defaultVal;
    public void SetFlag(string key, bool val) => flags[key] = val;

    // ========= 本体処理 =========

    private async UniTask RunSequenceAsync(string id, CancellationToken token)
    {
        if (!database.TryGet(id, out var seq) || seq.steps.Count == 0)
            return;

        for (int i = 0; i < seq.steps.Count; i++)
        {
            token.ThrowIfCancellationRequested();
            var step = seq.steps[i];

            if (!CheckConditions(step.conditions))
                continue;

            // Speaker
            if (speakerText)
            {
                if (string.IsNullOrWhiteSpace(step.speaker))
                {
                    speakerText.text = "";
                    speakerText.enabled = false;
                }
                else
                {
                    speakerText.text = step.speaker;
                    speakerText.enabled = true;
                }
            }

            // 本文タイプ
            await TypeTextAsync(step.text, token);

            // 選択肢
            if (step.choices != null && step.choices.Count > 0)
            {
                string gotoId = await ShowChoicesAndWaitAsync(step.choices, token);
                if (gotoId == null) return; // キャンセル等

                await RunSequenceAsync(gotoId, token); // 分岐先へ
                return;
            }
            else
            {
                if (step.autoNext)
                    await DelaySmartAsync(step.autoNextDelay, token);
                else
                    await WaitAdvanceAsync(token); // シングル消費＆時刻キャリー
            }
        }
    }

    private bool CheckConditions(List<FlagCondition> conds)
    {
        if (conds == null || conds.Count == 0) return true;
        foreach (var c in conds)
        {
            bool v = GetFlag(c.key, false);
            if (v != c.requiredValue) return false;
        }
        return true;
    }

    // ========= Typewriter（毎フレーム進行＋自前ダブル＋行頭ガード） =========

    private async UniTask TypeTextAsync(string content, CancellationToken token)
    {
        isTyping = true;
        string full = content ?? "";
        if (bodyText) bodyText.text = "";

        // 行開始時刻を確定（この時刻より前のクリックは“次行の2発目”に使わない）
        _lineStartTime = NowTime;

        // 前行→次行の連打を活かすため、古いクリックだけ整理（全クリアはしない）
        _clicks.PruneOlderThan(_lineStartTime - clickBufferSeconds);

        // キャリー種の期限切れチェック
        if (_lastAdvanceConsumedTime >= 0f && _lineStartTime - _lastAdvanceConsumedTime > clickBufferSeconds)
            _lastAdvanceConsumedTime = -1f;

        // レイアウト高さロック（潰れ防止）
        float lockedMin = 0f;
        RectTransform boxRT = null;
        if (lockHeightWhileTyping && dialogueBoxLayout)
        {
            boxRT = (RectTransform)dialogueBoxLayout.transform;
            Canvas.ForceUpdateCanvases();
            lockedMin = boxRT.rect.height + minHeightPadding;
            dialogueBoxLayout.minHeight = lockedMin;
        }

        // 前フレームのperformedを逃がす
        await UniTask.Yield(PlayerLoopTiming.Update, token);

        int len = full.Length;
        int idx = 0;

        float cps = Mathf.Max(1f, charsPerSecond);
        float secPerChar = 1f / cps;
        float acc = 0f;

        while (idx < len)
        {
            token.ThrowIfCancellationRequested();

            // 押しっぱ Skip → 即全文
            if (skipAction != null && skipAction.IsPressed())
            {
                if (bodyText) bodyText.text = full;
                break;
            }

            // ダブルクリック：①バッファ内2連 or ②キャリー＋“行開始後”のクリック のどちらか
            bool doubleNow =
                _clicks.TryConsumeDouble(NowTime, doubleClickSeconds, clickBufferSeconds) ||
                _clicks.TryConsumeDoubleWithSeed(_lastAdvanceConsumedTime, _lineStartTime, NowTime, doubleClickSeconds, clickBufferSeconds);

            if (doubleNow)
            {
                if (bodyText) bodyText.text = full;
                _lastAdvanceConsumedTime = -1f; // キャリー消費
                break;
            }

            // 経過時間でまとめて進行
            float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            acc += dt;
            while (acc >= secPerChar && idx < len)
            {
                acc -= secPerChar;
                idx++;
                if (bodyText) bodyText.text = full.Substring(0, idx);
            }

            await UniTask.Yield(PlayerLoopTiming.Update, token);
        }

        // 高さロック解除
        if (lockHeightWhileTyping && dialogueBoxLayout)
        {
            dialogueBoxLayout.minHeight = 0f;
            if (boxRT) LayoutRebuilder.ForceRebuildLayoutImmediate(boxRT);
        }

        isTyping = false;
    }

    // ========= 入力待ち（シングルで進む＆時刻キャリー） =========

    private async UniTask WaitAdvanceAsync(CancellationToken token)
    {
        // 直前押下を逃がす
        await UniTask.Yield(PlayerLoopTiming.Update, token);

        while (true)
        {
            token.ThrowIfCancellationRequested();

            if (cancelAction != null && cancelAction.triggered)
            {
                StopCurrent();
                return;
            }

            // バッファから単発消費（消費時刻を保持 → 次行のダブル判定にキャリー）
            if (_clicks.TryConsumeSingle(NowTime, clickBufferSeconds, out float consumedTime))
            {
                _lastAdvanceConsumedTime = consumedTime; // ★ キモ：次行の2発目と組み合わせる
                return;
            }

            await UniTask.Yield(PlayerLoopTiming.Update, token);
        }
    }

    private async UniTask DelaySmartAsync(float seconds, CancellationToken token)
    {
        var delayType = useUnscaledTime ? DelayType.UnscaledDeltaTime : DelayType.DeltaTime;
        await UniTask.Delay(TimeSpan.FromSeconds(Mathf.Max(0f, seconds)), delayType, PlayerLoopTiming.Update, token);
    }

    // ========= 選択肢 =========

    private async UniTask<string> ShowChoicesAndWaitAsync(List<DialogueChoice> choices, CancellationToken token)
    {
        ClearChoices();

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

        string result = null;
        bool clicked = false;

        foreach (var (ch, btn) in usable)
        {
            string nextId = ch.gotoId;
            btn.onClick.AddListener(() =>
            {
                result = nextId;
                clicked = true;
            });
        }

        while (!clicked)
        {
            token.ThrowIfCancellationRequested();

            if (cancelAction != null && cancelAction.triggered)
            {
                ClearChoices();
                return null;
            }

            await UniTask.Yield(PlayerLoopTiming.Update, token);
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

    private void SetCanvasVisible(bool v)
    {
        if (!canvasGroup) return;
        canvasGroup.alpha = v ? 1f : 0f;
        canvasGroup.blocksRaycasts = v;
        canvasGroup.interactable = v;
    }

    private void CleanupUI()
    {
        ClearChoices();
        if (speakerText) { speakerText.text = ""; speakerText.enabled = false; }
        if (bodyText) bodyText.text = "";
        SetCanvasVisible(false);

        _clicks.Clear();
        _lastAdvanceConsumedTime = -1f;
        _lineStartTime = -1f;
        _lastPushTime = -999f;
    }

    // ========= Input: advance（Press）を時刻バッファへ（デバウンス付） =========

    private void OnAdvancePerformed(InputAction.CallbackContext ctx)
    {
        float now = NowTime;
        // 環境によって press/press+release 二重発火する場合の誤登録を抑止
        if (now - _lastPushTime < pressDebounceSeconds) return;
        _lastPushTime = now;

        _clicks.Push(now);
    }
}

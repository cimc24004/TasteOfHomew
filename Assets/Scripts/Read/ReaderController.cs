using System;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using UnityEngine.InputSystem; // ▼▼▼ ★★★ InputSystem を使うために追加 ★★★ ▼▼▼
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
public class ReaderController : Singleton<ReaderController>
{
    [Title("UI")]
    [SerializeField] private CanvasGroup root;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI bodyText;

    [LabelText("背景Image(UI)")]
    [SerializeField] private Image backgroundImage;

    [LabelText("前面Image(UI)")]
    [SerializeField] private Image foregroundImage;

    [SerializeField] private TextMeshProUGUI pageNumberText;

    [Title("Navigation")]
    [SerializeField] private Button nextButton;
    [SerializeField] private Button prevButton;
    [SerializeField] private Button closeButton;

    [Tooltip("ボタンではなく『表示用の矢印画像』を隠したい場合に割り当て（未指定ならボタンを隠します）")]
    [SerializeField] private GameObject nextIndicator;  // 例: Next の Image/矢印GO
    [SerializeField] private GameObject prevIndicator;  // 例: Prev の Image/矢印GO

    [Title("UI切替")]
    [SerializeField] private UISwitcher uiSwitcher;
    [SerializeField] private bool switchUIOnOpenClose = true;
    [SerializeField] private UISwitcher.UIKind uiKindOnOpen = UISwitcher.UIKind.Archive;

    [Title("イベント")]
    public UnityEvent onOpened;
    public UnityEvent onClosed;
    public UnityEvent<int> onPageChanged;

    // ==== 内部 ====
    private ReadableAsset _now;
    private int _page; // 0-based
    private AudioSource _audio;
    private static readonly System.Collections.Generic.HashSet<string> _readFlags = new();
    private GameStateController gameStateController;
    private bool _isCloseAllowed = true; // ▼▼▼ ★★★ (ここから追加) 閉じる操作が許可されているか ★★★ ▼▼▼

    // ▼▼▼ ★★★ (ここから修正) InputSystem用 ★★★ ▼▼▼
    private PlayerInput playerInput;
    private InputAction nextAction;      // 'E'
    private InputAction prevAction;      // 'Q'
    private InputAction closeAction;     // 'RightClick' (新規追加)
    // ▲▲▲ ★★★ (ここまで修正) ★★★ ▲▲▲

    // ページ情報
    public int TotalPages => _now?.pages != null ? _now.pages.Count : 0;
    public int CurrentPage1 => _now == null ? 0 : (_page + 1);
    public string PageCounterText => $"{(TotalPages > 0 ? CurrentPage1 : 0)}/{TotalPages}";

    [Title("デバッグ/挙動オプション")]
    [SerializeField] private bool clearSpriteWhenHidden = false;
    [SerializeField] private bool logApplyImage = false;

    [Title("ページ番号表示")]
    [SerializeField] private GameObject pageNumberRoot;
    private AudioClip _turnSEOverride;

    // ▼▼▼ ★★★ (ここから追加) OnEnable ★★★ ▼▼▼
    void OnEnable()
    {
        // Start() で playerInput が取得済みの場合、イベントを購読
        SubscribeInputEvents();
    }
    // ▲▲▲ ★★★ (ここまで追加) ★★★ ▲▲▲

    void Start()
    {
        if (!root) Debug.LogWarning("ReaderController: CanvasGroup(root) 未割当");
        SetActive(false);
        gameStateController = GameStateController.instance;
        uiSwitcher = UISwitcher.instance ?? uiSwitcher;

        if (nextButton) nextButton.onClick.AddListener(NextPage);
        if (prevButton) prevButton.onClick.AddListener(PrevPage);
        
        // ▼▼▼ ★★★ (ここから修正) force=false で Close を呼ぶラムダ式に変更 ★★★ ▼▼▼
        if (closeButton) closeButton.onClick.AddListener(() => Close(false));
        // ▲▲▲ ★★★ (ここまで修正) ★★★ ▲▲▲

        _audio = GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();
        _audio.playOnAwake = false;
        
        // ▼▼▼ ★★★ (ここから修正) InputSystemの初期化 ★★★ ▼▼▼
        if (PlayerDataHub.instance != null && PlayerDataHub.instance.CurrentplayerInput != null)
        {
            this.playerInput = PlayerDataHub.instance.CurrentplayerInput;
            // Start() での最初の購読
            SubscribeInputEvents(); 
        }
        else
        {
            Debug.LogWarning("[ReaderController] PlayerInput が PlayerDataHub から取得できません。", this);
        }
        // ▲▲▲ ★★★ (ここまで修正) ★★★ ▲▲▲
    }

    void OnDisable()
    {
        // ▼▼▼ ★★★ (ここから修正) force=true で Close を呼ぶ (強制クローズ) ★★★ ▼▼▼
        if (_now != null) Close(true);
        // ▲▲▲ ★★★ (ここまで修正) ★★★ ▲▲▲
        
        // ▼▼▼ ★★★ (ここから修正) イベント解除 ★★★ ▼▼▼
        // OnDisableではイベントを「解除」する
        UnsubscribeInputEvents();
        // ▲▲▲ ★★★ (ここまで修正) ★★★ ▲▲▲
    }
    
    // ▼▼▼ ★★★ (ここから修正) InputSystem関連メソッド ★★★ ▼▼▼
    
    /// <summary>
    /// InputSystem のイベントを購読する
    /// </summary>
    private void SubscribeInputEvents()
    {
        // playerInput が (Start()で) 取得できていない場合は何もしない
        if (playerInput == null || playerInput.actions == null)
        {
            return;
        }

        // 念のため、多重登録を防ぐために一度解除してから登録する
        UnsubscribeInputEvents(); 

        // ConfigController と同じ 'E' と 'Q' のアクション名を使う
        nextAction = playerInput.actions.FindAction("E");
        prevAction = playerInput.actions.FindAction("Q");
        // "RightClick" アクションを取得
        closeAction = playerInput.actions.FindAction("RightClick");

        if (nextAction != null)
        {
            nextAction.performed += HandleNextPage;
        }
        else
        {
            Debug.LogWarning("[ReaderController] 'E' アクションが見つかりません。");
        }
        
        if (prevAction != null)
        {
            prevAction.performed += HandlePrevPage;
        }
        else
        {
            Debug.LogWarning("[ReaderController] 'Q' アクションが見つかりません。");
        }
        
        // (新規追加)
        if (closeAction != null)
        {
            closeAction.performed += HandleClose;
        }
        else
        {
            Debug.LogWarning("[ReaderController] 'RightClick' アクションが見つかりません。");
        }
    }

    /// <summary>
    /// InputSystem のイベント購読を解除する
    /// </summary>
    private void UnsubscribeInputEvents()
    {
        if (nextAction != null)
        {
            nextAction.performed -= HandleNextPage;
        }
        if (prevAction != null)
        {
            prevAction.performed -= HandlePrevPage;
        }
        // (新規追加)
        if (closeAction != null)
        {
            closeAction.performed -= HandleClose;
        }
    }
    
    /// <summary>
    /// "E" アクションが実行された (Next Page)
    /// </summary>
    private void HandleNextPage(InputAction.CallbackContext context)
    {
        // リーダーが開かれていない (_now == null) 場合は何もしない
        if (_now == null) return;

        // 既存のページ送り処理を呼ぶ
        TryNextPage();
    }

    /// <summary>
    /// "Q" アクションが実行された (Previous Page)
    /// </summary>
    private void HandlePrevPage(InputAction.CallbackContext context)
    {
        // リーダーが開かれていない (_now == null) 場合は何もしない
        if (_now == null) return;

        // 既存のページ送り処理を呼ぶ
        PrevPage();
    }
    
    // (新規追加)
    /// <summary>
    /// "RightClick" アクションが実行された (Close Reader)
    /// </summary>
    private void HandleClose(InputAction.CallbackContext context)
    {
        // リーダーが開かれていない (_now == null) 場合は何もしない
        if (_now == null) return;
        
        // ▼▼▼ ★★★ (ここから修正) 閉じる処理を呼ぶ (force=false) ★★★ ▼▼▼
        Close(false);
        // ▲▲▲ ★★★ (ここまで修正) ★★★ ▲▲▼
    }
    
    // ▲▲▲ ★★★ (ここまで修正) ★★★ ▲▲▲


    [Button("Open(Test)")]
    public void Open(ReadableAsset asset, int startPage1 = -1, AudioClip overrideTurnSE = null)
    {
        if (!asset) return;
        EventSystem.current.SetSelectedGameObject(null);
        gameStateController.ActionByModeSelect(GameStateController.GameState.MenuOnGamePlay, true);
        _now = asset;

        int total = Math.Max(0, TotalPages);
        int start1 = (startPage1 > 0 ? startPage1 : Mathf.Max(1, asset.defaultStartPage));
        _page = (total > 0) ? Mathf.Clamp(start1 - 1, 0, total - 1) : 0;
        _turnSEOverride = overrideTurnSE;
        if (switchUIOnOpenClose && uiSwitcher) uiSwitcher.SwitchTo(uiKindOnOpen);

        SetActive(true);
        onOpened?.Invoke();
        AudioClip openSound = (overrideTurnSE != null) ? overrideTurnSE : asset.openSE;
        PlaySE(openSound);
        if (!string.IsNullOrWhiteSpace(asset.id)) _readFlags.Add(asset.id);

        RefreshPage();
    }

    // ▼▼▼ ★★★ (ここから修正) force パラメータを追加 ★★★ ▼▼▼
    public void Close(bool force = false)
    {
        // ▼▼▼ ★★★ (ここから追加) 閉じる許可フラグをチェック ★★★ ▼▼▼
        // force=false (通常) の場合のみ、閉じる許可フラグをチェックする
        if (!force && !_isCloseAllowed)
        {
            return; // 許可されていないため処理を中断
        }
        // ▲▲▲ ★★★ (ここまで追加) ★★★ ▲▲▲

        if (_now == null) return;
        EventSystem.current.SetSelectedGameObject(null);
        gameStateController.ActionByModeSelect(GameStateController.GameState.Gameplay, false);
        PlaySE(_now.closeSE);
        SetActive(false);
        if (switchUIOnOpenClose && uiSwitcher) uiSwitcher.SwitchTo(UISwitcher.UIKind.Player);
        onClosed?.Invoke();
        _now = null;
    }
    // ▲▲▲ ★★★ (ここまで修正) ★★★ ▲▲▲

    public bool HasRead(string readableId) => _readFlags.Contains(readableId);

    public void NextPage() => TryNextPage();

    private bool TryNextPage()
    {
        if (_now == null) return false;
        int total = TotalPages;
        if (total <= 0) return false;

        if (_page < total - 1)
        {
            _page++;
            RefreshPage();
            PlaySE(_turnSEOverride ? _turnSEOverride : _now.turnSE);
            return true;
        }
        return false;
    }

    public void PrevPage()
    {
        if (_now == null) return;

        if (_page > 0)
        {
            _page--;
            RefreshPage();
            
            // ▼▼▼ ★★★ 修正 ★★★ ▼▼▼
            // _turnSEOverride が設定されていればそちらを優先する
            PlaySE(_turnSEOverride ? _turnSEOverride : _now.turnSE);
            // ▲▲▲ ★★★ 修正 ★★★ ▲▲▲
        }
    }

    private void RefreshPage()
    {
        if (!_now) return;

        string text = "";
        if (TotalPages > 0 && _page < TotalPages)
            text = _now.pages[_page] ?? "";

        if (titleText) titleText.text = _now.title ?? "";
        if (bodyText) bodyText.text = text ?? "";
        
        var bg = GetSpriteSafely(_now.backgroundImages, _page);
        var fg = GetSpriteSafely(_now.foregroundImages, _page);
        ApplyImageGo(backgroundImage, bg, "BG");
        ApplyImageGo(foregroundImage, fg, "FG");

        // ナビ表示更新
        bool hasPrev = TotalPages > 0 && _page > 0;
        bool hasNext = TotalPages > 0 && _page < TotalPages - 1;
        
        // ▼▼▼ ★★★ (ここから修正) 閉じるボタンと許可フラグのロジック ★★★ ▼▼▼
        // 閉じる操作を許可するかどうかを決定する
        _isCloseAllowed = true; // デフォルトは許可 (リストにないページなど)
        if (_now.showCloseButtonOnPages != null && _page >= 0 && _page < _now.showCloseButtonOnPages.Count)
        {
            _isCloseAllowed = _now.showCloseButtonOnPages[_page];
        }

        if (closeButton)
        {
            // 閉じる許可フラグに基づいてボタンの表示/非表示を決定
            closeButton.gameObject.SetActive(_isCloseAllowed);
        }
        // ▲▲▲ ★★★ (ここまで修正) ★★★ ▲▲▲

        if (prevIndicator) prevIndicator.SetActive(hasPrev);
        else if (prevButton) prevButton.gameObject.SetActive(hasPrev);

        if (nextIndicator) nextIndicator.SetActive(hasNext);
        else if (nextButton) nextButton.gameObject.SetActive(hasNext);

        // ページ番号表示
        bool showPageNumber = TotalPages > 1;
        SetPageNumberActive(showPageNumber);
        if (showPageNumber && pageNumberText)
        {
            pageNumberText.text = PageCounterText;
        }

        // ここでイベントを発火させることで、購読しているReadablePickupに通知がいく
        onPageChanged?.Invoke(CurrentPage1);
    }

    private static Sprite GetSpriteSafely(System.Collections.Generic.List<Sprite> list, int index)
    {
        if (list == null || index < 0 || index >= list.Count) return null;
        return list[index];
    }

    private void SetActive(bool on)
    {
        if (!root) return;
        root.alpha = on ? 1f : 0f;
        root.blocksRaycasts = on;
        root.interactable = on;

        if (_now != null && _now.pauseWhileOpen)
            Time.timeScale = on ? 0f : 1f;
    }

    private void ApplyImageGo(Image img, Sprite spr, string tagForLog)
    {
        if (!img) return;

        if (spr)
        {
            img.sprite = spr;
            if (!img.gameObject.activeSelf) img.gameObject.SetActive(true);
            if (logApplyImage)
                Debug.Log($"[ReaderController] Show {tagForLog}: go={img.gameObject.name}, sprite={(spr ? spr.name : "null")}, activeSelf=true");
        }
        else
        {
            if (clearSpriteWhenHidden) img.sprite = null;
            if (img.gameObject.activeSelf) img.gameObject.SetActive(false);
            if (logApplyImage)
                Debug.Log($"[ReaderController] Hide {tagForLog}: go={img.gameObject.name}, spriteWas={(img.sprite ? img.sprite.name : "null")}, activeSelf=false");
        }
    }

    private void PlaySE(AudioClip clip)
    {
        if (!_audio || !clip) return;
        _audio.PlayOneShot(clip);
    }

    private void SetPageNumberActive(bool on)
    {
        var go = pageNumberRoot ? pageNumberRoot : (pageNumberText ? pageNumberText.gameObject : null);
        if (!go) return;
        if (go.activeSelf != on) go.SetActive(on);
    }
}
using System;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using UnityEngine.InputSystem; 
using UnityEngine.EventSystems;
using UnityEngine.Localization; // ← 【追加】
using UnityEngine.Localization.Components; // ← 【追加】

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

    // ▼▼▼ 【(3) 追加】 LocalizeStringEvent への参照 ▼▼▼
    [Header("Localization References (NEW)")]
    [Tooltip("タイトルのTMPについている Localize String Event")]
    [SerializeField] private LocalizeStringEvent titleLocalizeEvent;
    [Tooltip("本文のTMPについている Localize String Event")]
    [SerializeField] private LocalizeStringEvent bodyLocalizeEvent;
    // ▲▲▲ 【(3) ここまで】 ▲▲▲

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
    private bool _isCloseAllowed = true; 

    private PlayerInput playerInput;
    private InputAction nextAction;      // 'E'
    private InputAction prevAction;      // 'Q'
    private InputAction closeAction;     // 'RightClick' (新規追加)

    // ページ情報
    public int TotalPages
    {
        get
        {
            if (_now == null) return 0;
            // (優先) 新しいリストのカウント
            if (_now.localizedPages != null && _now.localizedPages.Count > 0)
                return _now.localizedPages.Count;
            // (フォールバック) 古いリストのカウント
            if (_now.pages != null)
                return _now.pages.Count;
            return 0;
        }
    }
    public int CurrentPage1 => _now == null ? 0 : (_page + 1);
    public string PageCounterText => $"{(TotalPages > 0 ? CurrentPage1 : 0)}/{TotalPages}";

    [Title("デバッグ/挙動オプション")]
    [SerializeField] private bool clearSpriteWhenHidden = false;
    [SerializeField] private bool logApplyImage = false;

    [Title("ページ番号表示")]
    [SerializeField] private GameObject pageNumberRoot;
    private AudioClip _turnSEOverride;

    void OnEnable()
    {
        SubscribeInputEvents();
    }

    void Start()
    {
        if (!root) Debug.LogWarning("ReaderController: CanvasGroup(root) 未割当");
        SetActive(false);
        gameStateController = GameStateController.instance;
        uiSwitcher = UISwitcher.instance ?? uiSwitcher;

        if (nextButton) nextButton.onClick.AddListener(NextPage);
        if (prevButton) prevButton.onClick.AddListener(PrevPage);
        if (closeButton) closeButton.onClick.AddListener(() => Close(false));

        _audio = GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();
        _audio.playOnAwake = false;
        
        if (PlayerDataHub.instance != null && PlayerDataHub.instance.CurrentplayerInput != null)
        {
            this.playerInput = PlayerDataHub.instance.CurrentplayerInput;
            SubscribeInputEvents(); 
        }
        else
        {
            Debug.LogWarning("[ReaderController] PlayerInput が PlayerDataHub から取得できません。", this);
        }
    }

    void OnDisable()
    {
        if (_now != null) Close(true);
        UnsubscribeInputEvents();
    }
    
    // --- (InputSystem関連のメソッドは変更なし) ---
    private void SubscribeInputEvents()
    {
        if (playerInput == null || playerInput.actions == null) return;
        UnsubscribeInputEvents(); 

        nextAction = playerInput.actions.FindAction("E");
        prevAction = playerInput.actions.FindAction("Q");
        closeAction = playerInput.actions.FindAction("RightClick");

        if (nextAction != null) nextAction.performed += HandleNextPage;
        else Debug.LogWarning("[ReaderController] 'E' アクションが見つかりません。");
        
        if (prevAction != null) prevAction.performed += HandlePrevPage;
        else Debug.LogWarning("[ReaderController] 'Q' アクションが見つかりません。");
        
        if (closeAction != null) closeAction.performed += HandleClose;
        else Debug.LogWarning("[ReaderController] 'RightClick' アクションが見つかりません。");
    }

    private void UnsubscribeInputEvents()
    {
        if (nextAction != null) nextAction.performed -= HandleNextPage;
        if (prevAction != null) prevAction.performed -= HandlePrevPage;
        if (closeAction != null) closeAction.performed -= HandleClose;
    }
    
    private void HandleNextPage(InputAction.CallbackContext context)
    {
        if (_now == null) return;
        TryNextPage();
    }

    private void HandlePrevPage(InputAction.CallbackContext context)
    {
        if (_now == null) return;
        PrevPage();
    }
    
    private void HandleClose(InputAction.CallbackContext context)
    {
        if (_now == null) return;
        Close(false);
    }
    // --- (InputSystem関連ここまで) ---


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

    public void Close(bool force = false)
    {
        if (!force && !_isCloseAllowed)
        {
            return; 
        }

        if (_now == null) return;
        EventSystem.current.SetSelectedGameObject(null);
        gameStateController.ActionByModeSelect(GameStateController.GameState.Gameplay, false);
        PlaySE(_now.closeSE);
        SetActive(false);
        if (switchUIOnOpenClose && uiSwitcher) uiSwitcher.SwitchTo(UISwitcher.UIKind.Player);
        onClosed?.Invoke();
        _now = null;
    }

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
            PlaySE(_turnSEOverride ? _turnSEOverride : _now.turnSE);
        }
    }

    private void RefreshPage()
    {
        if (!_now) return;

        // ▼▼▼ 【(4) 移行ロジック（ここが最重要）】 ▼▼▼

        // --- (A) 新しい LocalizedString を取得試行 ---
        LocalizedString locTitle = _now.localizedTitle;
        LocalizedString locText = null;
        if (_now.localizedPages != null && _page < _now.localizedPages.Count)
        {
            locText = _now.localizedPages[_page];
        }
        
        // --- (B) 古い string を取得（フォールバック用） ---
        string oldTitle = _now.title ?? "";
        string oldText = "";
        if (_now.pages != null && _page < _now.pages.Count)
        {
            oldText = _now.pages[_page] ?? "";
        }

        // --- (C) タイトルの適用 ---
        if (titleLocalizeEvent)
        {
            // (優先) 新しい LocalizedString が設定されていれば、それを使う
            if (locTitle != null && !locTitle.IsEmpty)
            {
                titleLocalizeEvent.StringReference = locTitle;
            }
            else
            {
                // (フォールバック) 新しいのが空なら、古い string を直接表示（翻訳されない）
                titleLocalizeEvent.StringReference = null; // LocalizeEvent を無効化
                if (titleText) titleText.text = oldTitle; // 古い text に戻す
            }
        }
        else if(titleText)
        {
             titleText.text = oldTitle; // LocalizeEventが未設定なら古い方だけ使う
        }

        // --- (D) 本文の適用 ---
        if (bodyLocalizeEvent)
        {
            // (優先) 新しい LocalizedString が設定されていれば、それを使う
            if (locText != null && !locText.IsEmpty)
            {
                bodyLocalizeEvent.StringReference = locText;
            }
            else
            {
                // (フォールバック) 新しいのが空なら、古い string を直接表示（翻訳されない）
                bodyLocalizeEvent.StringReference = null; // LocalizeEvent を無効化
                if (bodyText) bodyText.text = oldText; // 古い text に戻す
            }
        }
        else if (bodyText)
        {
             bodyText.text = oldText; // LocalizeEventが未設定なら古い方だけ使う
        }
        // ▲▲▲ 【(4) 移行ロジックここまで】 ▲▲▲
        
        
        var bg = GetSpriteSafely(_now.backgroundImages, _page);
        var fg = GetSpriteSafely(_now.foregroundImages, _page);
        ApplyImageGo(backgroundImage, bg, "BG");
        ApplyImageGo(foregroundImage, fg, "FG");

        // ナビ表示更新
        bool hasPrev = TotalPages > 0 && _page > 0;
        bool hasNext = TotalPages > 0 && _page < TotalPages - 1;
        
        _isCloseAllowed = true; // デフォルトは許可 (リストにないページなど)
        if (_now.showCloseButtonOnPages != null && _page >= 0 && _page < _now.showCloseButtonOnPages.Count)
        {
            _isCloseAllowed = _now.showCloseButtonOnPages[_page];
        }

        if (closeButton)
        {
            closeButton.gameObject.SetActive(_isCloseAllowed);
        }

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
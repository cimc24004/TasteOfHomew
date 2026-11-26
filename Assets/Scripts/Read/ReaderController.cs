using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Localization;
using UnityEngine.UI;

/// <summary>
/// 手紙やメモなどの「読み物」（ReadableAsset）を全画面で表示するシングルトン。
///
/// ・ページ送り / 戻し / 閉じる（ボタンとショートカット入力の両方に対応）
/// ・ページごとの背景画像・前面画像の切り替え
/// ・一度開いた読み物の ID を覚えておく（<see cref="HasRead"/>）
/// </summary>
[DisallowMultipleComponent]
public class ReaderController : Singleton<ReaderController>
{
    // ショートカットに使う InputAction 名
    private const string NextPageActionName = "E";
    private const string PrevPageActionName = "Q";
    private const string CloseActionName = "RightClick";

    /// <summary>これまでに開いた読み物の ID（シーンをまたいで保持する）。</summary>
    private static readonly HashSet<string> readIds = new();

    // ===== Inspector =====

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
    [SerializeField] private GameObject nextIndicator;
    [SerializeField] private GameObject prevIndicator;

    [Title("UI切替")]
    [SerializeField] private UISwitcher uiSwitcher;
    [SerializeField] private bool switchUIOnOpenClose = true;
    [SerializeField] private UISwitcher.UIKind uiKindOnOpen = UISwitcher.UIKind.Archive;

    [Title("イベント")]
    public UnityEvent onOpened;
    public UnityEvent onClosed;
    public UnityEvent<int> onPageChanged;

    [Title("デバッグ/挙動オプション")]
    [SerializeField] private bool clearSpriteWhenHidden = false;
    [SerializeField] private bool logApplyImage = false;

    [Title("ページ番号表示")]
    [SerializeField] private GameObject pageNumberRoot;

    // ===== 実行時の状態 =====

    /// <summary>いま開いている読み物。閉じているときは null。</summary>
    private ReadableAsset currentAsset;

    /// <summary>表示中のページ（0 始まり）。</summary>
    private int pageIndex;

    /// <summary>このページで「閉じる」操作を許可するか（読み物側でページごとに設定できる）。</summary>
    private bool isCloseAllowed = true;

    private AudioClip turnSEOverride;
    private AudioSource audioSource;
    private GameStateController gameStateController;

    private PlayerInput playerInput;
    private InputAction nextAction;
    private InputAction prevAction;
    private InputAction closeAction;

    // ===== ページ情報 =====

    public int TotalPages
    {
        get
        {
            if (currentAsset == null) return 0;

            // ローカライズ済みのページがあればそちらを優先する
            if (currentAsset.localizedPages != null && currentAsset.localizedPages.Count > 0)
            {
                return currentAsset.localizedPages.Count;
            }
            if (currentAsset.pages != null)
            {
                return currentAsset.pages.Count;
            }
            return 0;
        }
    }

    /// <summary>表示中のページ番号（1 始まり）。閉じているときは 0。</summary>
    public int CurrentPage1 => currentAsset == null ? 0 : (pageIndex + 1);

    public string PageCounterText => $"{(TotalPages > 0 ? CurrentPage1 : 0)}/{TotalPages}";

    // ===== Unity ライフサイクル =====

    private void OnEnable()
    {
        SubscribeInputEvents();
    }

    private void Start()
    {
        if (!root) Debug.LogWarning("ReaderController: CanvasGroup(root) 未割当");
        SetVisible(false);

        gameStateController = GameStateController.instance;
        uiSwitcher = UISwitcher.instance ?? uiSwitcher;

        if (nextButton) nextButton.onClick.AddListener(NextPage);
        if (prevButton) prevButton.onClick.AddListener(PrevPage);
        if (closeButton) closeButton.onClick.AddListener(() => Close(false));

        audioSource = GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;

        if (PlayerDataHub.instance != null && PlayerDataHub.instance.CurrentplayerInput != null)
        {
            playerInput = PlayerDataHub.instance.CurrentplayerInput;
            SubscribeInputEvents();
        }
        else
        {
            Debug.LogWarning("[ReaderController] PlayerInput が PlayerDataHub から取得できません。", this);
        }
    }

    private void OnDisable()
    {
        if (currentAsset != null) Close(true);
        UnsubscribeInputEvents();
    }

    // ===== 公開 API =====

    /// <summary>
    /// 読み物のタイトルと全ページの翻訳テキストを先読みする。
    /// <see cref="Open"/> は翻訳済みテキストを同期的に取り出すので、開く前に呼んでおくこと。
    /// </summary>
    public async UniTask PreloadReadableAssetAsync(ReadableAsset asset)
    {
        if (asset == null) return;

        var tasks = new List<UniTask>();

        if (asset.localizedTitle != null && !asset.localizedTitle.IsEmpty)
        {
            tasks.Add(asset.localizedTitle.GetLocalizedStringAsync().Task.AsUniTask());
        }

        if (asset.localizedPages != null)
        {
            foreach (var localizedPage in asset.localizedPages)
            {
                if (localizedPage != null && !localizedPage.IsEmpty)
                {
                    tasks.Add(localizedPage.GetLocalizedStringAsync().Task.AsUniTask());
                }
            }
        }

        await UniTask.WhenAll(tasks);
    }

    /// <summary>読み物を開く。</summary>
    /// <param name="startPage1">開始ページ（1 始まり）。0 以下なら読み物側の defaultStartPage を使う。</param>
    /// <param name="overrideTurnSE">指定すると、開く音とページめくり音をこのクリップに差し替える。</param>
    [Button("Open(Test)")]
    public void Open(ReadableAsset asset, int startPage1 = -1, AudioClip overrideTurnSE = null)
    {
        if (!asset) return;

        EventSystem.current.SetSelectedGameObject(null);
        gameStateController.ActionByModeSelect(GameStateController.GameState.MenuOnGamePlay, true);
        currentAsset = asset;

        int total = Math.Max(0, TotalPages);
        int startPage = startPage1 > 0 ? startPage1 : Mathf.Max(1, asset.defaultStartPage);
        pageIndex = total > 0 ? Mathf.Clamp(startPage - 1, 0, total - 1) : 0;
        turnSEOverride = overrideTurnSE;

        // 画面を見せる前に中身を入れておく
        RefreshPage();

        if (switchUIOnOpenClose && uiSwitcher) uiSwitcher.SwitchTo(uiKindOnOpen);

        SetVisible(true);
        onOpened?.Invoke();
        PlaySE(overrideTurnSE != null ? overrideTurnSE : asset.openSE);

        if (!string.IsNullOrWhiteSpace(asset.id)) readIds.Add(asset.id);
    }

    /// <summary>読み物を閉じる。</summary>
    /// <param name="force">true なら「閉じる」が許可されていないページでも閉じる。</param>
    public void Close(bool force = false)
    {
        if (!force && !isCloseAllowed) return;
        if (currentAsset == null) return;

        EventSystem.current.SetSelectedGameObject(null);
        gameStateController.ActionByModeSelect(GameStateController.GameState.Gameplay, false);
        PlaySE(currentAsset.closeSE);
        SetVisible(false);

        if (switchUIOnOpenClose && uiSwitcher) uiSwitcher.SwitchTo(UISwitcher.UIKind.Player);

        onClosed?.Invoke();
        currentAsset = null;
    }

    public bool HasRead(string readableId) => readIds.Contains(readableId);

    public void NextPage() => TryNextPage();

    public void PrevPage()
    {
        if (currentAsset == null) return;
        if (pageIndex <= 0) return;

        pageIndex--;
        RefreshPage();
        PlayTurnSE();
    }

    private bool TryNextPage()
    {
        if (currentAsset == null) return false;

        int total = TotalPages;
        if (total <= 0) return false;
        if (pageIndex >= total - 1) return false;

        pageIndex++;
        RefreshPage();
        PlayTurnSE();
        return true;
    }

    // ===== ショートカット入力 =====

    private void SubscribeInputEvents()
    {
        if (playerInput == null || playerInput.actions == null) return;

        // 多重登録を防ぐため、一度解除してから登録する
        UnsubscribeInputEvents();

        nextAction = playerInput.actions.FindAction(NextPageActionName);
        prevAction = playerInput.actions.FindAction(PrevPageActionName);
        closeAction = playerInput.actions.FindAction(CloseActionName);

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

    // ショートカットは読み物を開いている間だけ反応させる

    private void HandleNextPage(InputAction.CallbackContext context)
    {
        if (currentAsset == null) return;
        TryNextPage();
    }

    private void HandlePrevPage(InputAction.CallbackContext context)
    {
        if (currentAsset == null) return;
        PrevPage();
    }

    private void HandleClose(InputAction.CallbackContext context)
    {
        if (currentAsset == null) return;
        Close(false);
    }

    // ===== ページ表示の更新 =====

    private void RefreshPage()
    {
        if (!currentAsset) return;

        RefreshTexts();
        RefreshImages();
        RefreshNavigation();
        RefreshPageNumber();

        onPageChanged?.Invoke(CurrentPage1);
    }

    private void RefreshTexts()
    {
        if (titleText)
        {
            titleText.text = ResolveText(currentAsset.localizedTitle, currentAsset.title ?? "");
        }

        if (bodyText)
        {
            LocalizedString localizedPage = null;
            if (currentAsset.localizedPages != null && pageIndex < currentAsset.localizedPages.Count)
            {
                localizedPage = currentAsset.localizedPages[pageIndex];
            }

            string plainPage = "";
            if (currentAsset.pages != null && pageIndex < currentAsset.pages.Count)
            {
                plainPage = currentAsset.pages[pageIndex] ?? "";
            }

            bodyText.text = ResolveText(localizedPage, plainPage);
        }
    }

    /// <summary>
    /// ローカライズ設定があれば翻訳済みの文字列を、なければ旧データ（生の文字列）を返す。
    /// 翻訳は PreloadReadableAssetAsync で先読み済みの前提で、同期的に取り出す。
    /// </summary>
    private static string ResolveText(LocalizedString localized, string fallback)
    {
        if (localized != null && !localized.IsEmpty)
        {
            return localized.GetLocalizedString();
        }
        return fallback;
    }

    private void RefreshImages()
    {
        ApplyImage(backgroundImage, GetSpriteSafely(currentAsset.backgroundImages, pageIndex), "BG");
        ApplyImage(foregroundImage, GetSpriteSafely(currentAsset.foregroundImages, pageIndex), "FG");
    }

    /// <summary>前後ページの矢印と「閉じる」ボタンの表示を、現在のページに合わせる。</summary>
    private void RefreshNavigation()
    {
        bool hasPrev = TotalPages > 0 && pageIndex > 0;
        bool hasNext = TotalPages > 0 && pageIndex < TotalPages - 1;

        // 「閉じる」はページごとに禁止できる（設定がないページは許可）
        isCloseAllowed = true;
        var closeFlags = currentAsset.showCloseButtonOnPages;
        if (closeFlags != null && pageIndex >= 0 && pageIndex < closeFlags.Count)
        {
            isCloseAllowed = closeFlags[pageIndex];
        }

        if (closeButton) closeButton.gameObject.SetActive(isCloseAllowed);

        // 矢印画像（Indicator）が割り当てられていればそちらを、なければボタン自体を出し入れする
        if (prevIndicator) prevIndicator.SetActive(hasPrev);
        else if (prevButton) prevButton.gameObject.SetActive(hasPrev);

        if (nextIndicator) nextIndicator.SetActive(hasNext);
        else if (nextButton) nextButton.gameObject.SetActive(hasNext);
    }

    /// <summary>ページ番号は 2 ページ以上あるときだけ表示する。</summary>
    private void RefreshPageNumber()
    {
        bool showPageNumber = TotalPages > 1;
        SetPageNumberActive(showPageNumber);

        if (showPageNumber && pageNumberText)
        {
            pageNumberText.text = PageCounterText;
        }
    }

    private void SetPageNumberActive(bool active)
    {
        GameObject target = pageNumberRoot ? pageNumberRoot : (pageNumberText ? pageNumberText.gameObject : null);
        if (!target) return;

        if (target.activeSelf != active) target.SetActive(active);
    }

    private static Sprite GetSpriteSafely(List<Sprite> sprites, int index)
    {
        if (sprites == null || index < 0 || index >= sprites.Count) return null;
        return sprites[index];
    }

    /// <summary>スプライトがあれば Image に設定して表示し、なければ Image を非表示にする。</summary>
    private void ApplyImage(Image image, Sprite sprite, string tagForLog)
    {
        if (!image) return;

        if (sprite)
        {
            image.sprite = sprite;
            if (!image.gameObject.activeSelf) image.gameObject.SetActive(true);

            if (logApplyImage)
            {
                Debug.Log($"[ReaderController] Show {tagForLog}: go={image.gameObject.name}, sprite={(sprite ? sprite.name : "null")}, activeSelf=true");
            }
        }
        else
        {
            if (clearSpriteWhenHidden) image.sprite = null;
            if (image.gameObject.activeSelf) image.gameObject.SetActive(false);

            if (logApplyImage)
            {
                Debug.Log($"[ReaderController] Hide {tagForLog}: go={image.gameObject.name}, spriteWas={(image.sprite ? image.sprite.name : "null")}, activeSelf=false");
            }
        }
    }

    // ===== 表示 / 音 =====

    /// <summary>画面全体の表示を切り替える。読み物の設定によっては、開いている間ゲームを一時停止する。</summary>
    private void SetVisible(bool visible)
    {
        if (!root) return;

        root.alpha = visible ? 1f : 0f;
        root.blocksRaycasts = visible;
        root.interactable = visible;

        if (currentAsset != null && currentAsset.pauseWhileOpen)
        {
            Time.timeScale = visible ? 0f : 1f;
        }
    }

    private void PlayTurnSE()
    {
        PlaySE(turnSEOverride ? turnSEOverride : currentAsset.turnSE);
    }

    private void PlaySE(AudioClip clip)
    {
        if (!audioSource || !clip) return;
        audioSource.PlayOneShot(clip);
    }
}

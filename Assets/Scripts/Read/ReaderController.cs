using System;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class ReaderController : Singleton<ReaderController>
{
    [Title("UI")]
    [SerializeField] private CanvasGroup root;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI bodyText;
    [SerializeField] private Image pageImage;
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
    public UnityEvent onPageChanged;

    // ==== 内部 ====
    private ReadableAsset _now;
    private int _page; // 0-based
    private AudioSource _audio;
    private static readonly System.Collections.Generic.HashSet<string> _readFlags = new();
    private GameStateController gameStateController;
    // ページ情報
    public int TotalPages   => _now?.pages != null ? _now.pages.Count : 0;
    public int CurrentPage1 => _now == null ? 0 : (_page + 1);
    public string PageCounterText => $"{(TotalPages > 0 ? CurrentPage1 : 0)}/{TotalPages}";

    void Start()
    {
        if (!root) Debug.LogWarning("ReaderController: CanvasGroup(root) 未割当");
        SetActive(false);
        gameStateController = GameStateController.instance;
        uiSwitcher = UISwitcher.instance ?? uiSwitcher;

        if (nextButton)  nextButton.onClick.AddListener(NextPage);
        if (prevButton)  prevButton.onClick.AddListener(PrevPage);
        if (closeButton) closeButton.onClick.AddListener(Close);

        _audio = GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();
        _audio.playOnAwake = false;
    }

    void OnDisable()
    {
        if (_now != null) Close();
    }

    [Button("Open(Test)")]
    public void Open(ReadableAsset asset, int startPage1 = -1)
    {
        if (!asset) return;
        gameStateController.ActionByModeSelect(GameStateController.GameState.MenuOnGamePlay, true);
        _now = asset;

        int total = Math.Max(0, TotalPages);
        int start1 = (startPage1 > 0 ? startPage1 : Mathf.Max(1, asset.defaultStartPage));
        _page = (total > 0) ? Mathf.Clamp(start1 - 1, 0, total - 1) : 0;

        if (switchUIOnOpenClose && uiSwitcher) uiSwitcher.SwitchTo(uiKindOnOpen);

        SetActive(true);
        onOpened?.Invoke();
        PlaySE(asset.openSE);

        if (!string.IsNullOrWhiteSpace(asset.id)) _readFlags.Add(asset.id);

        RefreshPage();
    }

    public void Close()
    {
        if (_now == null) return;
        gameStateController.ActionByModeSelect(GameStateController.GameState.Gameplay, false);
        PlaySE(_now.closeSE);
        SetActive(false);
        onClosed?.Invoke();

        if (switchUIOnOpenClose && uiSwitcher) uiSwitcher.SwitchTo(UISwitcher.UIKind.Player);

        _now = null;
    }

    public bool HasRead(string readableId) => _readFlags.Contains(readableId);

    // ==== ページ操作（ボタンのみ） ====
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
            PlaySE(_now.turnSE);
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
            PlaySE(_now.turnSE);
        }
    }

    // ==== 表示更新 ====
    private void RefreshPage()
    {
        if (!_now) return;

        string text = "";
        Sprite spr = null;

        if (TotalPages > 0 && _page < TotalPages)
            text = _now.pages[_page] ?? "";

        if (_now.pageImages != null && _page < _now.pageImages.Count)
            spr = _now.pageImages[_page];

        if (titleText)      titleText.text  = _now.title ?? "";
        if (bodyText)       bodyText.text   = text ?? "";
        if (pageImage)
        {
            pageImage.sprite  = spr;
            pageImage.enabled = spr != null;
        }

        if (pageNumberText) pageNumberText.text = PageCounterText;

        // === ここを SetActive に切替 ===
        bool hasPrev = TotalPages > 0 && _page > 0;
        bool hasNext = TotalPages > 0 && _page < TotalPages - 1;

        // 表示対象の優先度：Indicator 指定があればそれ → 無ければ Button 本体
        if (prevIndicator)  prevIndicator.SetActive(hasPrev);
        else if (prevButton) prevButton.gameObject.SetActive(hasPrev);

        if (nextIndicator)  nextIndicator.SetActive(hasNext);
        else if (nextButton) nextButton.gameObject.SetActive(hasNext);

        onPageChanged?.Invoke();
    }

    private void SetActive(bool on)
    {
        if (!root) return;
        root.alpha = on ? 1f : 0f;
        root.blocksRaycasts = on;
        root.interactable   = on;

        if (_now && _now.pauseWhileOpen)
            Time.timeScale = on ? 0f : 1f;
    }

    private void PlaySE(AudioClip clip)
    {
        if (!_audio || !clip) return;
        _audio.PlayOneShot(clip);
    }
}

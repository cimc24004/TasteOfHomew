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
    public UnityEvent onPageChanged;

    // ==== 内部 ====
    private ReadableAsset _now;
    private int _page; // 0-based
    private AudioSource _audio;
    private static readonly System.Collections.Generic.HashSet<string> _readFlags = new();
    private GameStateController gameStateController;

    // ページ情報
    public int TotalPages => _now?.pages != null ? _now.pages.Count : 0;
    public int CurrentPage1 => _now == null ? 0 : (_page + 1);
    public string PageCounterText => $"{(TotalPages > 0 ? CurrentPage1 : 0)}/{TotalPages}";
    // ReaderController のフィールドに追加
    [Title("デバッグ/挙動オプション")]
    [SerializeField] private bool clearSpriteWhenHidden = false;        // ← 非表示時に sprite=null にもするか
    [SerializeField] private bool logApplyImage = false;                // ← ログ出力
    [Title("ページ番号表示")]
    [SerializeField] private GameObject pageNumberRoot; // ← 未指定なら pageNumberText.gameObject を使う

    void Start()
    {
        if (!root) Debug.LogWarning("ReaderController: CanvasGroup(root) 未割当");
        SetActive(false);
        gameStateController = GameStateController.instance;
        uiSwitcher = UISwitcher.instance ?? uiSwitcher;

        if (nextButton) nextButton.onClick.AddListener(NextPage);
        if (prevButton) prevButton.onClick.AddListener(PrevPage);
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
        if (switchUIOnOpenClose && uiSwitcher) uiSwitcher.SwitchTo(UISwitcher.UIKind.Player);
        onClosed?.Invoke();
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
    // ReaderController.RefreshPage() の画像反映部分を差し替え
    private void RefreshPage()
    {
        if (!_now) return;

        // 本文テキスト系は従来通り
        string text = "";
        if (TotalPages > 0 && _page < TotalPages)
            text = _now.pages[_page] ?? "";

        if (titleText) titleText.text = _now.title ?? "";
        if (bodyText) bodyText.text = text ?? "";
        if (pageNumberText) pageNumberText.text = PageCounterText;

        // RefreshPage() の画像取得～反映部分を差し替え
        // 画像（背景 / 前面）
        var bg = GetSpriteSafely(_now.backgroundImages, _page);
        var fg = GetSpriteSafely(_now.foregroundImages, _page);

        // ★ SetActive で切替（必要なら sprite もクリア）
        ApplyImageGo(backgroundImage, bg, "BG");
        ApplyImageGo(foregroundImage, fg, "FG");

        // ナビ表示更新（従来通り）
        bool hasPrev = TotalPages > 0 && _page > 0;
        bool hasNext = TotalPages > 0 && _page < TotalPages - 1;

        if (prevIndicator) prevIndicator.SetActive(hasPrev);
        else if (prevButton) prevButton.gameObject.SetActive(hasPrev);

        if (nextIndicator) nextIndicator.SetActive(hasNext);
        else if (nextButton) nextButton.gameObject.SetActive(hasNext);
        // RefreshPage() の該当箇所を差し替え
        // 本文やタイトルの更新のあとに：
        bool showPageNumber = TotalPages > 1;

        // 表示/非表示だけ先に適用
        SetPageNumberActive(showPageNumber);

        // 表示する場合だけテキスト更新（0/0や1/1を出さない）
        if (showPageNumber && pageNumberText)
        {
            pageNumberText.text = PageCounterText; // e.g. "2/5"
        }

        onPageChanged?.Invoke();
    }

    private static Sprite GetSpriteSafely(System.Collections.Generic.List<Sprite> list, int index)
    {
        if (list == null) return null;
        if (index < 0 || index >= list.Count) return null;
        return list[index]; // 無ければ null を返す想定
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
    // ReaderController 内に追加：画像適用用ヘルパー
    // 画像適用ヘルパーを差し替え
    private void ApplyImageGo(Image img, Sprite spr, string tagForLog)
    {
        if (!img) return;

        if (spr)
        {
            img.sprite = spr; // ここは常に差し替え（古い見た目が残らないように）
            if (!img.gameObject.activeSelf) img.gameObject.SetActive(true);
            if (logApplyImage)
                Debug.Log($"[ReaderController] Show {tagForLog}: go={img.gameObject.name}, sprite={(spr ? spr.name : "null")}, activeSelf=true");
        }
        else
        {
            if (clearSpriteWhenHidden) img.sprite = null; // 任意：完全クリアしたい時だけ
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
    // ReaderController 内ヘルパーを追加
    private void SetPageNumberActive(bool on)
    {
        var go = pageNumberRoot ? pageNumberRoot : (pageNumberText ? pageNumberText.gameObject : null);
        if (!go) return;
        if (go.activeSelf != on) go.SetActive(on);
    }

}

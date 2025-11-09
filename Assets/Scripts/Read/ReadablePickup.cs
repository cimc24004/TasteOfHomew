using UnityEngine;
using Sirenix.OdinInspector;
using UnityEngine.Events;
using UnityEngine.Localization; // ← 【(1) 追加】

public class ReadablePickup : SerializedMonoBehaviour, IInteractable
{
    [Required] public ReadableAsset readable;

    [Tooltip("一度だけ読める（閉じ終わったら消費）")]
    public bool once = false;

    // ▼▼▼ 【(2) 修正】 デフォルトキーのフィールドを追加 ▼▼▼
    [Title("Localization")]
    [Tooltip("（オプション）「調べる」など、デフォルト以外のキーを個別に指定する場合")]
    [SerializeField] private LocalizedString interactText;

    [Tooltip("↑の interactText が空の場合に使われる、デフォルトの翻訳キー（String Table内）")]
    [SerializeField] private string defaultInteractKey = "ui_interact_read"; // ← ★★★ これを追加 ★★★
    // ▲▲▲ 【(2) 修正ここまで】 ▲▲▲

    [Title("Events")]
    [SerializeField, LabelText("開いた（毎回）")] public UnityEvent onOpenedEveryTime;
    [SerializeField, LabelText("開いた（最初の1回だけ）")] public UnityEvent onOpenedOnce; 
    [SerializeField, LabelText("ページ変更時")] public UnityEvent<int> onPageChanged;
    [SerializeField, LabelText("閉じた（毎回）")] public UnityEvent onClosedEveryTime;
    [SerializeField, LabelText("閉じた（最初の1回だけ）")] private UnityEvent onClosedOnce;
    
    public AudioClip overrideTurnSE;

    private bool consumed = false;

    // このピックアップから開いた読書がまだ閉じていない時 true
    private bool _openedByThis = false;
    private bool _subscribed = false;

    // 各「最初の1回だけ」イベントの発火済みフラグ
    private bool _openedOnceFired = false; 
    private bool _closedOnceFired = false;
    public int pageNow;
    
    public void Interact()
    {
        if (once && consumed) return;

        var ctrl = ReaderController.instance;
        if (!ctrl) { Debug.LogError("ReaderController.instance が見つかりません"); return; }

        // イベントを購読
        if (!_subscribed)
        {
            ctrl.onClosed.AddListener(OnReaderClosed);
            ctrl.onPageChanged.AddListener(OnReaderPageChanged);
            _subscribed = true;
        }

        // ---- 開いた時のイベントを発火 ----
        onOpenedEveryTime?.Invoke();

        if (!_openedOnceFired) 
        {
            onOpenedOnce?.Invoke();
            _openedOnceFired = true;
        }

        _openedByThis = true;
        ctrl.Open(readable, -1, overrideTurnSE);
    }

    // ReaderController側の onPageChanged から呼ばれる
    private void OnReaderPageChanged(int pageNow)
    {
        if (!_openedByThis) return;
        onPageChanged?.Invoke(pageNow);
        this.pageNow = pageNow;
    }

    // ReaderController 側の Close() から呼ばれる
    private void OnReaderClosed()
    {
        if (!_openedByThis) return;
        _openedByThis = false;

        // ---- 閉じた時のイベントを発火 ----
        onClosedEveryTime?.Invoke();
        if (!_closedOnceFired)
        {
            onClosedOnce?.Invoke();
            _closedOnceFired = true;
        }

        if (once) consumed = true;

        // イベント購読を解除
        var ctrl = ReaderController.instance;
        if (ctrl && _subscribed)
        {
            ctrl.onClosed.RemoveListener(OnReaderClosed);
            ctrl.onPageChanged.RemoveListener(OnReaderPageChanged);
            _subscribed = false;
        }
    }

    private void OnDestroy()
    {
        // 安全のため購読解除
        var ctrl = ReaderController.instance;
        if (ctrl && _subscribed)
        {
            ctrl.onClosed.RemoveListener(OnReaderClosed);
            ctrl.onPageChanged.RemoveListener(OnReaderPageChanged);
            _subscribed = false;
        }
    }

    // ▼▼▼ 【(3) 修正】 GetDisplayText がフォールバックするように変更 ▼▼▼
    public string GetDisplayText()
    {
        // (優先) "interactText" が設定されていれば、そのキーを返す
        if (interactText != null && !interactText.IsEmpty)
        {
            return interactText.TableEntryReference.Key;
        }
        
        // (フォールバック) "interactText" が空なら、デフォルトのキーを返す
        return defaultInteractKey; 
    }
    // ▲▲▲ 【(3) 修正ここまで】 ▲▲▲
    
    public bool ShouldHighlight() => !once || !consumed;

    [Button("テストで開く")]
    private void TestOpen()
    {
        var ctrl = ReaderController.instance;
        if (ctrl && readable) Interact();
    }
}
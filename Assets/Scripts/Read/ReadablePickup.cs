using UnityEngine;
using Sirenix.OdinInspector;
using UnityEngine.Events;

public class ReadablePickup : SerializedMonoBehaviour, IInteractable
{
    [Required] public ReadableAsset readable;

    [Tooltip("一度だけ読める（閉じ終わったら消費）")]
    public bool once = false;

    [Title("Events")]
    [SerializeField, LabelText("開いた（毎回）")] public UnityEvent onOpenedEveryTime;
    [SerializeField, LabelText("開いた（最初の1回だけ）")] public UnityEvent onOpenedOnce; // ◀◀◀【追加】
    [SerializeField, LabelText("ページ変更時")] private UnityEvent onPageChanged;
    [SerializeField, LabelText("閉じた（毎回）")] private UnityEvent onClosedEveryTime;
    [SerializeField, LabelText("閉じた（最初の1回だけ）")] private UnityEvent onClosedOnce;
    
    public AudioClip overrideTurnSE;

    private bool consumed = false;

    // このピックアップから開いた読書がまだ閉じていない時 true
    private bool _openedByThis = false;
    private bool _subscribed = false;

    // 各「最初の1回だけ」イベントの発火済みフラグ
    private bool _openedOnceFired = false; // ◀◀◀【追加】
    private bool _closedOnceFired = false;

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

        if (!_openedOnceFired) // ◀◀◀【追加】
        {
            onOpenedOnce?.Invoke();
            _openedOnceFired = true;
        }

        _openedByThis = true;
        ctrl.Open(readable, -1, overrideTurnSE);
    }

    // ReaderController側の onPageChanged から呼ばれる
    private void OnReaderPageChanged()
    {
        if (!_openedByThis) return;
        onPageChanged?.Invoke();
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

    public string GetDisplayText() => "読む";
    public bool ShouldHighlight() => !once || !consumed;

    [Button("テストで開く")]
    private void TestOpen()
    {
        var ctrl = ReaderController.instance;
        if (ctrl && readable) Interact();
    }
}
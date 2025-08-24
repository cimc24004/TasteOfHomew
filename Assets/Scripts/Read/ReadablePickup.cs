using UnityEngine;
using Sirenix.OdinInspector;
using UnityEngine.Events;

public class ReadablePickup : SerializedMonoBehaviour, IInteractable
{
    [Required] public ReadableAsset readable;

    [Tooltip("一度だけ読める（閉じ終わったら消費）")]
    public bool once = false;

    [Title("Events")]
    [SerializeField, LabelText("閉じた（毎回）")] private UnityEvent2 onClosedEveryTime;
    [SerializeField, LabelText("閉じた（最初の1回だけ）")] private UnityEvent2 onClosedOnce;

    // 互換用（既存配線があれば引き続き呼ぶ）
    [SerializeField, LabelText("閉じた（旧）")]
    [InfoBox("非推奨：今後は『毎回』or『最初の1回だけ』を使ってね", InfoMessageType.Warning)]
    private UnityEvent2 onClosed;

    private bool consumed = false;

    // このピックアップから開いた読書がまだ閉じていない時 true
    private bool _openedByThis = false;
    private bool _subscribed   = false;

    // 「最初の1回だけ」発火済みフラグ
    private bool _closedOnceFired = false;

    public void Interact()
    {
        if (once && consumed) return;

        var ctrl = ReaderController.instance;
        if (!ctrl) { Debug.LogError("ReaderController.instance が見つかりません"); return; }

        // 閉じイベントを一度だけ購読（遅延解除）
        if (!_subscribed)
        {
            ctrl.onClosed.AddListener(OnReaderClosed);
            _subscribed = true;
        }

        _openedByThis = true;
        ctrl.Open(readable);
        // ※ 「once」の消費は“閉じた後”に行う（読み切りで消費）
    }

    // ReaderController 側の Close() から呼ばれる
    private void OnReaderClosed()
    {
        // 他のソースから開かれて閉じた場合は無視
        if (!_openedByThis) return;

        _openedByThis = false;

        // ---- 新イベントたち ----
        onClosedEveryTime?.Invoke();

        if (!_closedOnceFired)
        {
            onClosedOnce?.Invoke();
            _closedOnceFired = true;
        }

        // ---- 互換用（旧）----
        onClosed?.Invoke();

        if (once) consumed = true;

        // もう購読不要なら解除（必要なら複数回読む挙動に合わせてここを外す）
        var ctrl = ReaderController.instance;
        if (ctrl && _subscribed)
        {
            ctrl.onClosed.RemoveListener(OnReaderClosed);
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

    private void OnDestroy()
    {
        // 安全のため購読解除
        var ctrl = ReaderController.instance;
        if (ctrl && _subscribed)
        {
            ctrl.onClosed.RemoveListener(OnReaderClosed);
            _subscribed = false;
        }
    }
}

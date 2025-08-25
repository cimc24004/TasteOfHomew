using UnityEngine;
using UnityEngine.Events;
using Sirenix.OdinInspector;

/// <summary>
/// 「Interact されたら用意した UnityEvent を実行するだけ」
/// どこでも使える汎用コンポーネント。
/// </summary>
[DisallowMultipleComponent]
public class InteractEventRelay : SerializedMonoBehaviour, IInteractable
{
    [Title("Interact イベント")]
    [Tooltip("Interact() が呼ばれたときに実行する処理")]
    public UnityEvent onInteract;

    [Title("表示/ハイライト")]
    [LabelText("プロンプト文字列")] public string displayText = "Interact";
    [LabelText("ハイライト可")] public bool highlightWhenAvailable = true;

    [Title("オプション")]
    [Tooltip("1回だけ有効（2回目以降は無視）")]
    public bool once = false;

    [Tooltip("実行後にこの GameObject を無効化する")]
    public bool disableGameObjectAfterInvoke = false;

    private bool _consumed = false;

    // ===== IInteractable =====
    public void Interact()
    {
        if (once && _consumed) return;

        onInteract?.Invoke();

        if (once) _consumed = true;
        if (disableGameObjectAfterInvoke) gameObject.SetActive(false);
    }

    public string GetDisplayText() => (!once || !_consumed) ? displayText : string.Empty;

    public bool ShouldHighlight() => (!once || !_consumed) && highlightWhenAvailable;

    // ===== 便利系 =====
    [Button("Reset Consumed")]
    public void ResetConsumed() => _consumed = false;

    [Button("Test Interact")]
    private void TestInteract() => Interact();
}

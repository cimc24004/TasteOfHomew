using UnityEngine;
using Sirenix.OdinInspector;

public class ReadablePickup : SerializedMonoBehaviour, IInteractable
{
    [Required] public ReadableAsset readable;

    [Tooltip("一度だけ読める（2回目は無効）")]
    public bool once = false;

    private bool consumed = false;

    public void Interact()
    {
        if (once && consumed) return;
        var ctrl = ReaderController.instance;
        if (!ctrl) { Debug.LogError("ReaderController.instance が見つかりません"); return; }

        ctrl.Open(readable);
        if (once) consumed = true;
    }

    public string GetDisplayText() => "読む";
    public bool ShouldHighlight() => !once || !consumed;

    [Button("テストで開く")]
    private void TestOpen()
    {
        var ctrl = ReaderController.instance;
        if (ctrl && readable) ctrl.Open(readable);
    }
}

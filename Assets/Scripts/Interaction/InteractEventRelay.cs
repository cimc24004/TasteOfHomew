using UnityEngine;
using UnityEngine.Events;
using Sirenix.OdinInspector;
using UnityEngine.Localization; // ← 【(1) using を追加】

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
    [LabelText("プロンプト文字列 (フォールバック)")]
    [Tooltip("下の 'Localized Display Text' が None の場合に、この文字列が「そのまま」表示されます。")]
    public string displayText = "Interact"; // ← ★★★ これが "fawfwafaw"
    [LabelText("ハイライト可")] public bool highlightWhenAvailable = true;
    [SerializeField] private string defaultInteractKey = ""; // ← ★★★ これを追加 ★★★
    // ▲▲▲ 【(2) 追加ここまで】 ▲▲▲

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

    // ▼▼▼ 【(3) 修正】 GetDisplayText のロジックを修正 ▼▼▼
    public string GetDisplayText()
    {
        if (once && _consumed)
        {
            return string.Empty; // 実行済みなら空を返す
        }

        if(defaultInteractKey != null)
        {
            // (優先) "localizedDisplayText" が設定されていれば、その「キー名」を返す
            return defaultInteractKey;
        }

        // (フォールバック) "localizedDisplayText" が None なら、
        // 古い "displayText" の「生の文字列」("fawfwafaw"など) をそのまま返す
        return displayText; 
    }
    // ▲▲▲ 【(3) 修正ここまで】 ▲▲▲

    public bool ShouldHighlight() => (!once || !_consumed) && highlightWhenAvailable;

    // ===== 便利系 =====
    [Button("Reset Consumed")]
    public void ResetConsumed() => _consumed = false;

    [Button("Test Interact")]
    private void TestInteract() => Interact();
}
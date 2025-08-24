using UnityEngine;
using Sirenix.OdinInspector;
public class UISwitcher : Singleton<UISwitcher>
{
    [Header("参照するUIルート")]
    [SerializeField] private GameObject PlayerUIs;
    [SerializeField] private GameObject ArchiveUIs;   // 「ArchieveUis」ならここをその名前に
    [SerializeField] private GameObject DialogueUIs;

    public enum UIKind { Player, Archive, Dialogue, None }

    /// <summary>
    /// 指定したUIだけをActiveにして、他は非表示にする。
    /// </summary>
    [Button]
    public void SwitchTo(UIKind kind)
    {
        SetActiveSafe(PlayerUIs, kind == UIKind.Player);
        SetActiveSafe(ArchiveUIs, kind == UIKind.Archive);
        SetActiveSafe(DialogueUIs, kind == UIKind.Dialogue);

    }

    // ボタンクリック等のUnityEvent用（引数をintで受けてもOK）
    public void SwitchToInt(int kind) => SwitchTo((UIKind)kind);

    // 文字列指定したい時用（"Player" "Archive" "Dialogue"）
    public void SwitchToByName(string name)
    {
        if (System.Enum.TryParse(name, true, out UIKind kind))
            SwitchTo(kind);
        else
            Debug.LogWarning($"UISwitcher: 不明なUI名 '{name}'");
    }

    private static void SetActiveSafe(GameObject go, bool active)
    {
        if (!go) return;
        if (go.activeSelf != active) go.SetActive(active);
    }
}

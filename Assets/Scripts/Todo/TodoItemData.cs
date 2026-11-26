using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;
using UnityEngine.Localization;

/// <summary>
/// ToDoリストのアイテム情報を保持するためのデータクラス
/// </summary>
[CreateAssetMenu(fileName = "TodoItemData", menuName = "ToDo/TodoItemData", order = 1)]
[System.Serializable]
public class TodoItemData : ScriptableObject
{
    public string key;

    [Title("--- OLD DATA (for Migration) ---")]
    [TextArea(1, 2)]
    public string text;

    [Title("--- LOCALIZATION (NEW) ---")]
    [Tooltip("こちらを設定すると、上記の古い Text より優先されます")]
    public LocalizedString localizedText;
}

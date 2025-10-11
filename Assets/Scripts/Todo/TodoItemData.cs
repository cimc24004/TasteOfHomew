// TodoItemData.cs

using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;

/// <summary>
/// ToDoリストのアイテム情報を保持するためのデータクラス
/// </summary>
[CreateAssetMenu(fileName = "TodoItemData", menuName = "ToDo/TodoItemData", order = 1)]
[System.Serializable]
public class TodoItemData : ScriptableObject
{
    public string key;
    public string text;
}
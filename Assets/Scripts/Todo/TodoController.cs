using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector; // ★ Odin

public class TodoController : MonoBehaviour
{
    [Title("生成設定")]
    [SerializeField] private Transform todoLists;
    [SerializeField] private TodoBox todoBoxPrefab;

    // 内部
    private readonly Dictionary<string, TodoBox> _byId = new();
    private readonly Dictionary<string, string> _keyToId = new();

    // ====== Runtime API ======

    public string Add(string text)
    {
        if (!ValidateSetup()) return null;
        var box = Instantiate(todoBoxPrefab, todoLists, false);
        var id = System.Guid.NewGuid().ToString("N");
        box.Setup(this, id, text);
        _byId[id] = box;
        return id;
    }

    public string AddWithKey(string key, string text)
    {
        if (string.IsNullOrWhiteSpace(key)) return Add(text);
        if (_keyToId.TryGetValue(key, out var existId) && _byId.TryGetValue(existId, out var exist))
        {
            exist.SetText(text);
            return existId;
        }
        var id = Add(text);
        if (id != null) _keyToId[key] = id;
        return id;
    }

    public void SetCheckedById(string id, bool isChecked)
    {
        if (string.IsNullOrEmpty(id)) return;
        if (_byId.TryGetValue(id, out var box) && box) box.SetChecked(isChecked);
    }

    public void SetCheckedByKey(string key, bool isChecked)
    {
        if (string.IsNullOrEmpty(key)) return;
        if (_keyToId.TryGetValue(key, out var id)) SetCheckedById(id, isChecked);
    }

    public void ToggleCheckedByKey(string key)
    {
        if (string.IsNullOrEmpty(key)) return;
        if (_keyToId.TryGetValue(key, out var id) && _byId.TryGetValue(id, out var box) && box)
            box.SetChecked(!box.IsChecked);
    }

    public void ClearAll()
    {
        foreach (var kv in _byId)
            if (kv.Value) Destroy(kv.Value.gameObject);
        _byId.Clear();
        _keyToId.Clear();
    }

    private bool ValidateSetup()
    {
        if (todoLists == null || todoBoxPrefab == null)
        {
            Debug.LogWarning("[Todo] todoLists or todoBoxPrefab 未設定");
            return false;
        }
        return true;
    }

    // ====== Odin Buttons for Debug ======

    [Title("Odin Debug Tools")]
    [GUIColor(0.8f, 1f, 0.8f)]
    [Button("Add Todo")]
    public void _Add([GUIColor(1f,1f,0.6f)] string text = "新しいタスク")
    {
        Add(text);
    }

    [GUIColor(0.6f, 0.9f, 1f)]
    [Button("Add With Key")]
    public void _AddWithKey(string key = "task1", string text = "キー付きタスク")
    {
        AddWithKey(key, text);
    }

    [GUIColor(1f, 0.9f, 0.6f)]
    [Button("Toggle Check By Key")]
    public void _ToggleByKey(string key = "task1")
    {
        ToggleCheckedByKey(key);
    }

    [GUIColor(1f, 0.6f, 0.6f)]
    [Button("Clear All Todos")]
    private void _ClearAll()
    {
        ClearAll();
    }
}

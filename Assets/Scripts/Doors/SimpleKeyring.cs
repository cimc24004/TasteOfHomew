using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

public class SimpleKeyring : SerializedMonoBehaviour, IKeyring
{
    [LabelText("起動時に Current として登録")] public bool setAsGlobal = true;
    [LabelText("初期所持鍵")] public string[] initialKeys;

    private readonly HashSet<string> _keys = new();
    public AudioSource keygetSound;
    void OnEnable()
    {
        _keys.Clear();
        if (initialKeys != null)
            foreach (var k in initialKeys) if (!string.IsNullOrEmpty(k)) _keys.Add(k);
        if (setAsGlobal) KeyringService.Current = this;
    }

    public void AddKey(string keyId) { if (!string.IsNullOrEmpty(keyId)) _keys.Add(keyId); }
    public void RemoveKey(string keyId) { if (!string.IsNullOrEmpty(keyId)) _keys.Remove(keyId); }
    public bool HasKey(string keyId) => !string.IsNullOrEmpty(keyId) && _keys.Contains(keyId);
    public bool ConsumeKey(string keyId)
    {
        if (HasKey(keyId)) { _keys.Remove(keyId); return true; }
        return false;
    }
}

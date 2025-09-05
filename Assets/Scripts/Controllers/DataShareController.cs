using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;

/// <summary>
/// 文字列キー -> bool のフラグを共有するシンプルなコントローラー（シングルトン）
/// EndingShareDictionary … エンディング到達等のフラグ用
/// FlagEventRunnerDictionary … 一度きりのイベント実行管理等のフラグ用
/// </summary>
public class DataShareController : SerializedMonoBehaviour
{
    public static DataShareController Instance { get; private set; }

    // ★ リネーム: shareDictionary → EndingShareDictionary
    public Dictionary<string, bool> EndingShareDictionary = new Dictionary<string, bool>();

    // ★ 追加: FlagEventRunnerDictionary
    public Dictionary<string, bool> FlagEventRunnerDictionary = new Dictionary<string, bool>();

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this.gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(this.gameObject);
    }

    // ===== EndingShareDictionary 用（既存API互換名のまま中身をこちらに寄せる） =====
    public void SetFlagToTrue(string key)
    {
        if (string.IsNullOrEmpty(key)) return;
        EndingShareDictionary[key] = true;
    }

    public void SetFlag(string key, bool value)
    {
        if (string.IsNullOrEmpty(key)) return;
        EndingShareDictionary[key] = value;
    }

    public bool GetFlag(string key)
    {
        if (string.IsNullOrEmpty(key)) return false;
        if (EndingShareDictionary.TryGetValue(key, out bool val)) return val;
        return false;
    }

    public bool TryGetFlag(string key, out bool value)
    {
        if (string.IsNullOrEmpty(key))
        {
            value = false;
            return false;
        }
        return EndingShareDictionary.TryGetValue(key, out value);
    }

    public bool HasFlag(string key)
    {
        if (string.IsNullOrEmpty(key)) return false;
        return EndingShareDictionary.ContainsKey(key);
    }

    public bool RemoveFlag(string key)
    {
        if (string.IsNullOrEmpty(key)) return false;
        return EndingShareDictionary.Remove(key);
    }

    public void ToggleFlag(string key)
    {
        if (string.IsNullOrEmpty(key)) return;
        bool current = GetFlag(key);
        EndingShareDictionary[key] = !current;
    }

    public void ClearAllEndingFlags()
    {
        EndingShareDictionary.Clear();
    }

    // ===== FlagEventRunnerDictionary 用のAPI =====
    public void SetFlagEventRunner(string key, bool value)
    {
        if (string.IsNullOrEmpty(key)) return;
        FlagEventRunnerDictionary[key] = value;
    }

    public bool GetFlagEventRunner(string key)
    {
        if (string.IsNullOrEmpty(key)) return false;
        if (FlagEventRunnerDictionary.TryGetValue(key, out bool val)) return val;
        return false;
    }

    public bool TryGetFlagEventRunner(string key, out bool value)
    {
        if (string.IsNullOrEmpty(key))
        {
            value = false;
            return false;
        }
        return FlagEventRunnerDictionary.TryGetValue(key, out value);
    }

    public bool HasFlagEventRunner(string key)
    {
        if (string.IsNullOrEmpty(key)) return false;
        return FlagEventRunnerDictionary.ContainsKey(key);
    }

    public bool RemoveFlagEventRunner(string key)
    {
        if (string.IsNullOrEmpty(key)) return false;
        return FlagEventRunnerDictionary.Remove(key);
    }
    public void SetTrueEventRunner(string key)
    {
        if (string.IsNullOrEmpty(key)) return;
        FlagEventRunnerDictionary[key] = true;
    }
    public void ToggleFlagEventRunner(string key)
    {
        if (string.IsNullOrEmpty(key)) return;
        bool current = GetFlagEventRunner(key);
        FlagEventRunnerDictionary[key] = !current;
    }

    public void ClearAllFlagEventRunners()
    {
        FlagEventRunnerDictionary.Clear();
    }

    // ===== 互換: まとめて全消去 =====
    public void ClearAll()
    {
        EndingShareDictionary.Clear();
        FlagEventRunnerDictionary.Clear();
    }
}

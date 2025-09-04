using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;
/// <summary>
/// 文字列キー -> bool のフラグを共有するシンプルなコントローラー（シングルトン）
/// 使い方： DataShareController.Instance.SetFlag("KeyName", true);
/// </summary>
public class DataShareController : SerializedMonoBehaviour
{
    // グローバルアクセス用シングルトン
    public static DataShareController Instance { get; private set; }

    // 実際の辞書（Inspectorには表示されない）
    public Dictionary<string, bool> shareDictionary = new Dictionary<string, bool>();

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
    public void SetFlagToTrue(string key)
    {
        if (string.IsNullOrEmpty(key)) return;
        shareDictionary[key] = true;
    }

    // フラグをセット（存在しなければ追加）
    public void SetFlag(string key, bool value)
    {
        if (string.IsNullOrEmpty(key)) return;
        shareDictionary[key] = value;
    }

    // フラグを取得（キーが無ければ false を返す）
    public bool GetFlag(string key)
    {
        if (string.IsNullOrEmpty(key)) return false;
        if (shareDictionary.TryGetValue(key, out bool val)) return val;
        return false;
    }

    // 存在チェック＆値取得（キーが無ければ false を out に入れて false を返す）
    public bool TryGetFlag(string key, out bool value)
    {
        if (string.IsNullOrEmpty(key))
        {
            value = false;
            return false;
        }
        return shareDictionary.TryGetValue(key, out value);
    }

    // キーが存在するか
    public bool HasFlag(string key)
    {
        if (string.IsNullOrEmpty(key)) return false;
        return shareDictionary.ContainsKey(key);
    }

    // 削除
    public bool RemoveFlag(string key)
    {
        if (string.IsNullOrEmpty(key)) return false;
        return shareDictionary.Remove(key);
    }

    // トグル（キーが無ければ true にセットする）
    public void ToggleFlag(string key)
    {
        if (string.IsNullOrEmpty(key)) return;
        bool current = GetFlag(key);
        shareDictionary[key] = !current;
    }

    // 全削除
    public void ClearAll()
    {
        shareDictionary.Clear();
    }
}

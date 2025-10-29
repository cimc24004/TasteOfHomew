using UnityEngine;
using System; // Exceptionクラスのために必要

/// <summary>
/// PlayerPrefs を使った汎用セーブ・ロードコントローラー
/// 継承元（Singleton<T>）がシングルトンとDontDestroyOnLoadを管理することを前提としています。
/// </summary>
public class SaveDataBaseController : Singleton<SaveDataBaseController> 
    // public class SaveDataBaseController : MonoBehaviour から変更
{
    // --- シングルトン管理 ---
    // 継承元の Singleton<T> が Instance プロパティと
    // Awake() でのインスタンス管理（DontDestroyOnLoadも含む）を
    // 行うため、このクラスでの実装はすべて不要。
    
    // --- セーブ（基本データ型） ---

    public void SaveInt(string key, int value)
    {
        PlayerPrefs.SetInt(key, value);
    }

    public void SaveFloat(string key, float value)
    {
        PlayerPrefs.SetFloat(key, value);
    }

    public void SaveString(string key, string value)
    {
        PlayerPrefs.SetString(key, value);
    }
    
    /// <summary>
    /// bool値をint（1 or 0）として保存します
    /// </summary>
    public void SaveBool(string key, bool value)
    {
        PlayerPrefs.SetInt(key, value ? 1 : 0);
    }

    // --- ロード（基本データ型） ---

    public int LoadInt(string key, int defaultValue = 0)
    {
        return PlayerPrefs.GetInt(key, defaultValue);
    }

    public float LoadFloat(string key, float defaultValue = 0f)
    {
        return PlayerPrefs.GetFloat(key, defaultValue);
    }

    public string LoadString(string key, string defaultValue = "")
    {
        return PlayerPrefs.GetString(key, defaultValue);
    }
    
    /// <summary>
    /// int（1 or 0）をbool値としてロードします
    /// </summary>
    public bool LoadBool(string key, bool defaultValue = false)
    {
        int defaultIntValue = defaultValue ? 1 : 0;
        return PlayerPrefs.GetInt(key, defaultIntValue) == 1;
    }

    // --- セーブ＆ロード（JSON / カスタムオブジェクト） ---

    /// <summary>
    /// 任意の[Serializable]なクラス/構造体をJSON文字列として保存します
    /// </summary>
    public void SaveData<T>(string key, T dataObject)
    {
        try
        {
            string json = JsonUtility.ToJson(dataObject);
            PlayerPrefs.SetString(key, json);
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveData] '{key}' のJSONシリアライズに失敗: {e.Message}");
        }
    }

    /// <summary>
    /// 保存されたJSON文字列をデシリアライズしてオブジェクトを復元します
    /// </summary>
    public T LoadData<T>(string key)
    {
        string json = PlayerPrefs.GetString(key, null);

        if (string.IsNullOrEmpty(json))
        {
            return default(T);
        }

        try
        {
            return JsonUtility.FromJson<T>(json);
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveData] '{key}' のJSONデシリアライズに失敗: {e.Message}");
            return default(T);
        }
    }

    // --- ユーティリティ ---

    /// <summary>
    /// 指定したキーのデータを削除します
    /// </summary>
    public void DeleteKey(string key)
    {
        PlayerPrefs.DeleteKey(key);
    }

    /// <summary>
    /// PlayerPrefsに保存されている全てのデータを削除します
    /// </summary>
    public void DeleteAllData()
    {
        PlayerPrefs.DeleteAll();
        Debug.LogWarning("[SaveData] すべてのPlayerPrefsデータが削除されました。");
    }

    /// <summary>
    /// 指定したキーのデータが存在するか確認します
    /// </summary>
    public bool HasKey(string key)
    {
        return PlayerPrefs.HasKey(key);
    }
    
    /// <summary>
    /// すべての変更をディスクに書き込みます（通常はOnApplicationQuitで自動実行されます）
    /// </summary>
    public void SaveAllChanges()
    {
        PlayerPrefs.Save();
    }
}
using UnityEngine;

/// <summary>
/// SaveDataBaseController へのアクセスを仲介（ラップ）するMonoBehaviour。
/// 他のゲームロジックやUI管理スクリプトから呼び出されることを想定しています。
/// UIコンポーネントには一切依存しません。
/// </summary>
public class SaveDataBaseCaller : MonoBehaviour
{
    // --- セーブ（基本データ型） ---

    /// <summary>
    /// Int値を保存します
    /// </summary>
    public void SaveInt(string key, int value)
    {
        SaveDataBaseController.instance.SaveInt(key, value);
        Debug.Log($"[Save] Key: {key}, Value: {value} (Int)");
    }

    /// <summary>
    /// Float値を保存します
    /// </summary>
    public void SaveFloat(string key, float value)
    {
        SaveDataBaseController.instance.SaveFloat(key, value);
        Debug.Log($"[Save] Key: {key}, Value: {value} (Float)");
    }

    /// <summary>
    /// String値を保存します
    /// </summary>
    public void SaveString(string key, string value)
    {
        SaveDataBaseController.instance.SaveString(key, value);
        Debug.Log($"[Save] Key: {key}, Value: {value} (String)");
    }

    /// <summary>
    /// Bool値を保存します
    /// </summary>
    public void SaveBool(string key, bool value)
    {
        SaveDataBaseController.instance.SaveBool(key, value);
        Debug.Log($"[Save] Key: {key}, Value: {value} (Bool)");
    }

    // --- ロード（基本データ型） ---

    public int LoadInt(string key, int defaultValue = 0)
    {
        return SaveDataBaseController.instance.LoadInt(key, defaultValue);
    }

    public float LoadFloat(string key, float defaultValue = 0f)
    {
        return SaveDataBaseController.instance.LoadFloat(key, defaultValue);
    }

    public string LoadString(string key, string defaultValue = "")
    {
        return SaveDataBaseController.instance.LoadString(key, defaultValue);
    }

    public bool LoadBool(string key, bool defaultValue = false)
    {
        return SaveDataBaseController.instance.LoadBool(key, defaultValue);
    }

    // --- セーブ＆ロード（JSON / カスタムオブジェクト） ---

    /// <summary>
    /// 任意の[Serializable]なクラス/構造体をJSON文字列として保存します
    /// </summary>
    public void SaveData<T>(string key, T dataObject)
    {
        SaveDataBaseController.instance.SaveData<T>(key, dataObject);
        Debug.Log($"[Save] Key: {key} (JSON)");
    }

    /// <summary>
    /// 保存されたJSON文字列をデシリアライズしてオブジェクトを復元します
    /// </summary>
    public T LoadData<T>(string key)
    {
        return SaveDataBaseController.instance.LoadData<T>(key);
    }

    // --- ユーティリティ ---

    public void DeleteKey(string key)
    {
        SaveDataBaseController.instance.DeleteKey(key);
        Debug.LogWarning($"[Delete] Key: {key}");
    }

    public void DeleteAllData()
    {
        SaveDataBaseController.instance.DeleteAllData();
    }

    public bool HasKey(string key)
    {
        return SaveDataBaseController.instance.HasKey(key);
    }

    public void SaveAllChanges()
    {
        SaveDataBaseController.instance.SaveAllChanges();
    }
}

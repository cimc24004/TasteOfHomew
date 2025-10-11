using UnityEngine;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
public class TeleportGlobalController : Singleton<TeleportGlobalController>
{
    // TeleportDataController のデータを参照して、プレイヤーのテレポートを実行する
    private TeleportDataController teleportDataController;
    public Dictionary<string, Transform> teleportTransformData = new Dictionary<string, Transform>();
    private bool isUse;
    private PlayerStatus playerStatus;
    private void Start()
    {
        teleportByKeyCheck();
        // SceneManager.sceneLoaded += OnSceneLoaded;
    }
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        teleportByKeyCheck();
    }
    private void Teleport(Transform _transform)
    {
        playerStatus.transform.position = _transform.position;
    }
    public void teleportTransformDataAdd(string keyName, Transform targetTransform)
    {
        if (!teleportTransformData.ContainsKey(keyName))
        {
            teleportTransformData.Add(keyName, targetTransform);
        }
    }
    private void teleportByKeyCheck()
    {
        playerStatus = PlayerStatus.instance;
        teleportDataController = TeleportDataController.instance;

        // nullチェックを追加して安全性を高める
        if (playerStatus == null || teleportDataController == null) return;

        // ★★★ 修正点 ★★★
        // Dictionaryのキーを先に別のリストにコピーする
        var teleportKeys = new List<string>(teleportDataController.teleportData.Keys);

        // コピーしたキーのリストをループで回す
        foreach (var key in teleportKeys)
        {
            isUse = teleportDataController.TeleportIsUseCheckFromData(key);
            if (isUse)
            {
                // teleportTransformDataにキーが存在するかチェックすると、より安全
                if (teleportTransformData.ContainsKey(key))
                {
                    Teleport(teleportTransformData[key]);
                    // 元のDictionaryを変更しても、ループはコピーしたリストを
                    // 見ているのでエラーにならない
                    teleportDataController.TeleportDataUse(key, false);
                }
            }
        }
    }
}
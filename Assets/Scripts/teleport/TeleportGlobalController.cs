using UnityEngine;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
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
        Debug.Log("Teleport関数内です。");
        // プレイヤーにアタッチされている CharacterController を取得
        CharacterController cc = playerStatus.GetComponent<CharacterController>();

        if (cc != null)
        {
            // ★★★ CharacterController がある場合のテレポート方法 ★★★
            Debug.Log("CharacterControllerを無効化してテレポートします。");
            cc.enabled = false; // 一時的に無効化
            playerStatus.transform.position = _transform.position; // 位置を強制変更
            cc.enabled = true;  // 再度有効化
            Debug.Log("テレポート完了。");
        }
        else
        {
            // CharacterController がない場合（通常）
            Debug.Log("transform.position を直接変更します。");
            playerStatus.transform.position = _transform.position;
        }
    }
    public void teleportTransformDataAdd(string keyName, Transform targetTransform)
    {
        if (!teleportTransformData.ContainsKey(keyName))
        {
            teleportTransformData.Add(keyName, targetTransform);
        }else
        {
            // ★★★ 修正点 ★★★
            // キーが既に存在する場合 (古いシーンのTransformが入っている) は、
            // 新しいシーンのTransformで上書きする
            Debug.Log($"キー '{keyName}' を新しいTransformで上書き更新します。");
            teleportTransformData[keyName] = targetTransform;
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
                    Debug.Log("テレポート成功手レポートします");
                    Teleport(teleportTransformData[key]);
                    // 元のDictionaryを変更しても、ループはコピーしたリストを
                    // 見ているのでエラーにならない
                    teleportDataController.TeleportDataUse(key, false);
                }
            }
        }
    }
}
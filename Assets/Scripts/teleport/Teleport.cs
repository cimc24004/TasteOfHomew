using UnityEngine;
using Sirenix.OdinInspector;
using System.Collections.Generic;
public class Teleport : MonoBehaviour
{
    [Header("シーン読み込み時、テレポート(シーン間の移動管理)")]
    [SerializeField]
    private bool TeleportSceneManage;
    [ShowIf("TeleportSceneManage")]
    public string teleportKeyName;

    void Awake()
    {
        if (TeleportSceneManage)
        {
            TeleportDataController.instance.teleportDataAdd(teleportKeyName);
            TeleportGlobalController.instance.teleportTransformDataAdd(teleportKeyName, this.transform);
        }
    }
    // Timeline の SignalReceiver から呼ぶ用（引数なし）
    public void TeleportAction(Transform teleportTarget)
    {
        this.transform.position = teleportTarget.position;
    }
}
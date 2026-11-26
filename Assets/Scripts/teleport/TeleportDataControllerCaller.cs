using UnityEngine;

public class TeleportDataControllerCaller : MonoBehaviour
{
    private TeleportDataController teleportDataController;
    void Start()
    {
        teleportDataController = TeleportDataController.instance;
    }
    public void TeleportDataUse(string keyName, bool isUse)
    {
        teleportDataController.TeleportDataUse(keyName, isUse);
    }
    public void TeleportDataAdd(string keyName)
    {
        teleportDataController.teleportDataAdd(keyName);
    }
}

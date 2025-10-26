using UnityEngine;

public class TeleportDataControllerCaller : MonoBehaviour
{
    private TeleportDataController teleportDataController;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
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

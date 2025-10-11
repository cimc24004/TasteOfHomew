using System.Collections.Generic;
using UnityEngine;
public class TeleportDataController : Singleton<TeleportDataController>
{
    public bool isUse;
    private PlayerStatus playerStatus;
    private void Start()
    {
        playerStatus = PlayerStatus.instance;
    }
    public Dictionary<string, bool> teleportData = new Dictionary<string, bool>();
    public void teleportDataAdd(string keyName)
    {

        if (!teleportData.ContainsKey(keyName))
        {
            teleportData.Add(keyName, false);
        }
    }

    public bool TeleportIsUseCheckFromData(string keyName)
    {
        if (teleportData.ContainsKey(keyName))
        {
            bool isUse = teleportData[keyName];
            return isUse;
        }
        return false;
    }
    public void TeleportDataUse(string keyName, bool isUse)
    {
        if (teleportData.ContainsKey(keyName))
        {
            teleportData[keyName] = isUse; // ← ここが問題！
        }
    }

}
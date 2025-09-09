using UnityEngine;
using System.Collections.Generic;
using Sirenix.OdinInspector; // Odin Inspectorを使用している場合

public class DoorsController : MonoBehaviour
{
    // シーン内の全てのDoorControllerを格納するリスト
    private List<DoorController> allDoors = new List<DoorController>();

    void Awake()
    {
        // "Door"タグが付いている全てのGameObjectを検索します
        GameObject[] doorObjects = GameObject.FindGameObjectsWithTag("Door");

        // 見つかった各GameObjectからDoorControllerコンポーネントを取得し、リストに追加します
        foreach (GameObject doorObject in doorObjects)
        {
            DoorController doorController = doorObject.GetComponent<DoorController>();
            if (doorController != null)
            {
                allDoors.Add(doorController);
            }
        }
    }

    [Button("全てのドアを即座に開ける", ButtonSizes.Large)]
    [GUIColor(0.4f, 0.8f, 1f)]
    public void OpenAllDoors()
    {
        if (allDoors.Count == 0)
        {
            Debug.LogWarning("操作対象のドアが見つかりません。");
            return;
        }

        foreach (DoorController door in allDoors)
        {
            door.ForceOpen();
        }
        Debug.Log($"{allDoors.Count}個のドアを即座に開きました。");
    }

    [Button("全てのドアを即座に閉める", ButtonSizes.Large)]
    [GUIColor(1f, 0.6f, 0.6f)]
    public void CloseAllDoors()
    {
        if (allDoors.Count == 0)
        {
            Debug.LogWarning("操作対象のドアが見つかりません。");
            return;
        }

        foreach (DoorController door in allDoors)
        {
            door.ForceClose();
        }
        Debug.Log($"{allDoors.Count}個のドアを即座に閉めました。");
    }
}
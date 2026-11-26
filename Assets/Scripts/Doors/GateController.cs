using UnityEngine;
using DG.Tweening;
using System.Collections.Generic;
using Sirenix.OdinInspector; // Odin Inspectorが不要なためコメントアウトまたは削除

// GateDoorクラスは変更なし
[System.Serializable]
public class GateDoor
{
    public Transform doorTransform;
    public float openAngle;
    public float closeAngle;

    public GateDoor(Transform transform, float openAngle, float closeAngle)
    {
        this.doorTransform = transform;
        this.openAngle = openAngle;
        this.closeAngle = closeAngle;
    }
}

// Dictionaryの代わりとしてInspectorに表示するためのキーと値のペアを持つクラス
[System.Serializable]
public class GateDoorPair
{
    public string key;
    public GateDoor value;
}

// MonoBehaviourを継承（SerializedMonoBehaviourから変更）
public class GateController : MonoBehaviour
{
    [SerializeField]
    private float openDuration;

    // Inspectorで編集するためのリスト
    [SerializeField]
    private List<GateDoorPair> gateDoorList = new List<GateDoorPair>();

    // 実行時に使用するDictionary（Inspectorには表示しない）
    private Dictionary<string, GateDoor> gateDoors = new Dictionary<string, GateDoor>();

    void Awake()
    {
        // ゲーム開始時にリストの内容をDictionaryに変換する
        // これにより、Inspectorでの編集のしやすさと、実行時の高速なアクセスを両立できる
        foreach (var pair in gateDoorList)
        {
            if (!gateDoors.ContainsKey(pair.key))
            {
                gateDoors.Add(pair.key, pair.value);
            }
        }
    }

    [Button("Gate Open")] // Inspectorのコンポーネントのケバブメニューから実行できるようにする
    public void GateOpen()
    {
        foreach (GateDoor door in gateDoors.Values)
        {
            door.doorTransform.DOLocalRotate(new Vector3(0, door.openAngle, 0), openDuration);
        }
    }

    [Button("Gate Close")] // 同上
    public void GateClose()
    {
        foreach (GateDoor door in gateDoors.Values)
        {
            door.doorTransform.DOLocalRotate(new Vector3(0, door.closeAngle, 0), openDuration);
        }
    }
}

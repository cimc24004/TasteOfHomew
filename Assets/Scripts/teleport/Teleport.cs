using UnityEngine;

public class Teleport : MonoBehaviour
{
    // Timeline の SignalReceiver から呼ぶ用（引数なし）
    public void TeleportAction(Transform teleportTarget)
    {
        this.transform.position = teleportTarget.position;
    }
}
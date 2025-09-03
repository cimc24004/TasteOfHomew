using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public class CollisionOrTriggerEvent : MonoBehaviour
{
    [Header("どのタグと反応するか（空なら無条件）")]
    public string targetTag = "";

    [Header("イベント")]
    public UnityEvent onEnter;
    public UnityEvent onExit;

    [Header("レイヤーマスク（デフォルト: 全部）")]
    public LayerMask layerMask = ~0; // -1 と同じ。全部のレイヤー

    // ==== Trigger ====
    private void OnTriggerEnter(Collider other)
    {
        if (IsInLayerMask(other.gameObject))
        {
            if (string.IsNullOrEmpty(targetTag) || other.CompareTag(targetTag))
                onEnter?.Invoke();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (IsInLayerMask(other.gameObject))
        {
            if (string.IsNullOrEmpty(targetTag) || other.CompareTag(targetTag))
                onExit?.Invoke();
        }
    }

    // ==== Collision ====
    private void OnCollisionEnter(Collision collision)
    {
        Debug.Log("OnCollisionEnter: " + collision.gameObject);
        if (IsInLayerMask(collision.gameObject))
        {
            if (string.IsNullOrEmpty(targetTag) || collision.collider.CompareTag(targetTag))
                onEnter?.Invoke();
        }
    }

    private void OnCollisionExit(Collision collision)
    {
        if (IsInLayerMask(collision.gameObject))
        {
            if (string.IsNullOrEmpty(targetTag) || collision.collider.CompareTag(targetTag))
                onExit?.Invoke();
        }
    }

    // ==== 共通: レイヤーマスク判定 ====
    private bool IsInLayerMask(GameObject obj)
    {
        return (layerMask.value & (1 << obj.layer)) != 0;
    }
}

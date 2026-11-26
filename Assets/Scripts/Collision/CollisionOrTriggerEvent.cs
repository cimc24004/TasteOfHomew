using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public class CollisionOrTriggerEvent : MonoBehaviour
{
    [Header("どのタグと反応するか（空なら無条件）")]
    public string targetTag = "";

    [Header("イベント")]
    public UnityEvent onEnter;
    public UnityEvent2 onEnterByUnityEvent2;
    public UnityEvent onExit;
    public UnityEvent2 onExitByUnityEvent2;

    [Header("レイヤーマスク（デフォルト: 全部）")]
    public LayerMask layerMask = ~0; // -1 と同じ。全部のレイヤー

    // ==== Trigger ====
    private void OnTriggerEnter(Collider other)
    {
        Debug.Log("OntriggerEnter: " + other.gameObject);
        if (IsInLayerMask(other.gameObject))
        {
        Debug.Log("OntriggerEnter2 : " + other.gameObject);
            if (string.IsNullOrEmpty(targetTag) || other.CompareTag(targetTag))
            {
                Debug.Log("OntriggerEnter3 : " + other.gameObject);
                onEnter?.Invoke();
                onEnterByUnityEvent2.Invoke();
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (IsInLayerMask(other.gameObject))
        {
            if (string.IsNullOrEmpty(targetTag) || other.CompareTag(targetTag))
            {
                onExit?.Invoke();
                onExitByUnityEvent2?.Invoke();
            }
        }
    }

    // ==== Collision ====
    private void OnCollisionEnter(Collision collision)
    {
        Debug.Log("OnCollisionEnter: " + collision.gameObject);
        if (IsInLayerMask(collision.gameObject))
        {
            if (string.IsNullOrEmpty(targetTag) || collision.collider.CompareTag(targetTag))
            {
                onEnter?.Invoke();
                onEnterByUnityEvent2?.Invoke();
            }
        }
    }

    private void OnCollisionExit(Collision collision)
    {
        if (IsInLayerMask(collision.gameObject))
        {
            if (string.IsNullOrEmpty(targetTag) || collision.collider.CompareTag(targetTag))
            {
                onExit?.Invoke();
                onExitByUnityEvent2?.Invoke();
            }
        }
    }

    // ==== 共通: レイヤーマスク判定 ====
    private bool IsInLayerMask(GameObject obj)
    {
        return (layerMask.value & (1 << obj.layer)) != 0;
    }
}

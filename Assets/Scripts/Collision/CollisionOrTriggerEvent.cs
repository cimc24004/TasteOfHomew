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

    // ==== Trigger ====
    private void OnTriggerEnter(Collider other)
    {
        if (string.IsNullOrEmpty(targetTag) || other.CompareTag(targetTag))
            onEnter?.Invoke();
    }

    private void OnTriggerExit(Collider other)
    {
        if (string.IsNullOrEmpty(targetTag) || other.CompareTag(targetTag))
            onExit?.Invoke();
    }

    // ==== Collision ====
    private void OnCollisionEnter(Collision collision)
    {
        if (string.IsNullOrEmpty(targetTag) || collision.collider.CompareTag(targetTag))
            onEnter?.Invoke();
    }

    private void OnCollisionExit(Collision collision)
    {
        if (string.IsNullOrEmpty(targetTag) || collision.collider.CompareTag(targetTag))
            onExit?.Invoke();
    }
}

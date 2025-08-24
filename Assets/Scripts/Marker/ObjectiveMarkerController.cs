using UnityEngine;

[DisallowMultipleComponent]
public class ObjectiveMarkerController : MonoBehaviour
{
    public enum MarkerType
    {
        Trigger,
        Manual
    }

    [Header("基本設定")]
    [SerializeField] private MarkerType markerType = MarkerType.Trigger;
    [SerializeField] private GameObject targetCanvas;

    [Header("Trigger 設定")]
    [SerializeField] private string playerTag = "Player";
    void Awake()
    {
        Hide();
    }
    // ====== Triggerモード ======
    private void OnTriggerEnter(Collider other)
    {
        if (markerType != MarkerType.Trigger) return;
        if (other.CompareTag(playerTag) && targetCanvas != null)
        {
            targetCanvas.SetActive(true);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (markerType != MarkerType.Trigger) return;
        if (other.CompareTag(playerTag) && targetCanvas != null)
        {
            targetCanvas.SetActive(false);
        }
    }

    // ====== Manualモード ======
    public void Show()
    {
        if (targetCanvas != null)
            targetCanvas.SetActive(true);
    }

    public void Hide()
    {
        if (targetCanvas != null)
            targetCanvas.SetActive(false);
    }

    public void Toggle()
    {
        if (targetCanvas != null)
            targetCanvas.SetActive(!targetCanvas.activeSelf);
    }
}

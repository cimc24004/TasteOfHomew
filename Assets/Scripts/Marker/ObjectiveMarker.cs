using UnityEngine;
using UnityEngine.UI;

public class ObjectiveMarker : MonoBehaviour
{
    [SerializeField] private Transform targetPoint; // 行くべき場所
    [SerializeField] private Canvas canvas; // Screen Space Overlay推奨
    [SerializeField] private RectTransform markerUI; // 矢印UI
    [SerializeField] private float hideDistance = 2f; // 近づいたら消える距離

    private Camera mainCamera;
    private Transform player;

    void Start()
    {
        mainCamera = Camera.main;
        player = GameObject.FindGameObjectWithTag("Player").transform;
    }

    void Update()
    {
        if (targetPoint == null) return;

        // ワールド座標をスクリーン座標に変換
        Vector3 screenPos = mainCamera.WorldToScreenPoint(targetPoint.position);

        // カメラの前にあるときだけ表示
        if (screenPos.z > 0)
        {
            markerUI.position = screenPos;
        }
        else
        {
            canvas.gameObject.SetActive(false);
        }
    }

    // 進行に応じて呼び出す
    public void SetTarget(Transform newTarget)
    {
        targetPoint = newTarget;
    }
}

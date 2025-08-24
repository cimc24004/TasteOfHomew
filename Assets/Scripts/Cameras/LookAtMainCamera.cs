using UnityEngine;

[DisallowMultipleComponent]
public class LookAtMainCamera : MonoBehaviour
{
    private Transform camTransform;
    void Start()
    {
        camTransform = Camera.main.transform;
    }
    void LateUpdate()
    {
        // MainCameraがまだキャッシュされてない or 消えたら再取得
        if (camTransform == null)
        {
            if (Camera.main != null)
                camTransform = Camera.main.transform;
            else
                return; // MainCameraがシーンにない場合は何もしない
        }

        // カメラの方向を向く
        transform.LookAt(camTransform);
    }
}

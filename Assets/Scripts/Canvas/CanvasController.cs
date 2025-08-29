using UnityEngine;
using UnityEngine.UI;

public class CanvasScalerController : MonoBehaviour
{
    [SerializeField] private CanvasScaler scaler;

    // Signalから呼び出す関数
    public void SetScaleMode(CanvasScaler.ScaleMode mode)
    {
        if (scaler != null)
        {
            scaler.uiScaleMode = mode;
        }
    }

    public void SetReferenceResolution(Vector2 resolution)
    {
        if (scaler != null)
        {
            scaler.referenceResolution = resolution;
        }
    }

    public void SetScaleFactor(float factor)
    {
        if (scaler != null)
        {
            scaler.scaleFactor = factor;
        }
    }
}

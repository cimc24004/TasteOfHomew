using UnityEngine;
using UnityEngine.UI;

public class CrosshairUI : MonoBehaviour
{
    public Color defaultColor = Color.white;
    public Color interactColor = Color.red;

    private Image horiz;
    private Image vert;

    private void Start()
    {
        var canvasGO = new GameObject("CrosshairCanvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasGO.AddComponent<CanvasScaler>();
        canvasGO.AddComponent<GraphicRaycaster>();

        horiz = CreateLine("Horizontal", new Vector2(20, 2), canvas.transform);
        vert = CreateLine("Vertical", new Vector2(2, 20), canvas.transform);

        SetInteractable(false);
    }

    private Image CreateLine(string name, Vector2 size, Transform parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent);
        var img = go.AddComponent<Image>();
        img.color = defaultColor;
        var rect = img.rectTransform;
        rect.sizeDelta = size;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        return img;
    }

    public void SetInteractable(bool canInteract)
    {
        Color c = canInteract ? interactColor : defaultColor;
        if (horiz != null) horiz.color = c;
        if (vert != null) vert.color = c;
    }
}

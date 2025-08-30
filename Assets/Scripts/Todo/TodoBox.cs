using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TodoBox : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TextMeshProUGUI labelTMP;
    [SerializeField] private Text labelUGUI;     // TMP 無い人向けフォールバック
    [SerializeField] private Image checkImage;   // ← チェック画像（ONで表示）

    [Header("見た目オプション")]
    [SerializeField, Range(0f,1f)] private float uncheckedAlpha = 1f;
    [SerializeField, Range(0f,1f)] private float checkedAlpha = 0.6f; // 完了時ちょい薄くする

    public string Id { get; private set; }
    public bool IsChecked { get; private set; }

    private TodoController _owner;
    private CanvasGroup _cg;

    public void Setup(TodoController owner, string id, string text)
    {
        _owner = owner;
        Id = id;
        if (!_cg) _cg = gameObject.GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
        SetText(text);
        SetChecked(false, true);
    }

    public void SetText(string text)
    {
        var t = text ?? "";
        if (labelTMP)  labelTMP.text = t;
        if (labelUGUI) labelUGUI.text = t;
    }

    public void SetChecked(bool isChecked) => SetChecked(isChecked, false);

    private void SetChecked(bool isChecked, bool instant)
    {
        IsChecked = isChecked;

        if (checkImage) checkImage.enabled = isChecked;

        // うっすら薄くする（画像だけでOKならここを消してもいい）
        if (!_cg) _cg = gameObject.GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
        _cg.alpha = isChecked ? checkedAlpha : uncheckedAlpha;
    }
}

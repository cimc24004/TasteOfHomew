using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Localization; // ← 【追加】
using UnityEngine.Localization.Components; // ← 【追加】

public class TodoBox : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TextMeshProUGUI labelTMP;
    [SerializeField] private Text labelUGUI;     // TMP 無い人向けフォールバック
    [SerializeField] private Image checkImage;   // ← チェック画像（ONで表示）

    // ▼▼▼ 【追加】 LocalizeStringEvent への参照 ▼▼▼
    [Header("Localization (NEW)")]
    [Tooltip("テキストのTMP/Labelについている Localize String Event")]
    [SerializeField] private LocalizeStringEvent labelLocalizeEvent;
    // ▲▲▲ 【追加ここまで】 ▲▲▲

    [Header("見た目オプション")]
    [SerializeField, Range(0f, 1f)] private float uncheckedAlpha = 1f;
    [SerializeField, Range(0f, 1f)] private float checkedAlpha = 0.6f; // 完了時ちょい薄くする

    public string Id { get; private set; }
    public bool IsChecked { get; private set; }

    private TodoController _owner;
    private CanvasGroup _cg;

    /// <summary>
    /// (古い) 生の string で初期化
    /// </summary>
    public void Setup(TodoController owner, string id, string text)
    {
        _owner = owner;
        Id = id;
        if (!_cg) _cg = gameObject.GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
        SetText(text); // 古い SetText を呼ぶ
        SetChecked(false, true);
    }

    /// <summary>
    /// (新しい) LocalizedString で初期化
    /// </summary>
    public void SetupLocalized(TodoController owner, string id, LocalizedString locText)
    {
        _owner = owner;
        Id = id;
        if (!_cg) _cg = gameObject.GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
        SetLocalizedText(locText); // 新しい SetLocalizedText を呼ぶ
        SetChecked(false, true);
    }

    /// <summary>
    /// (古い) 生の string をセット（フォールバック用）
    /// </summary>
    public void SetText(string text)
    {
        // LocalizeEvent がある場合は無効化する
        if (labelLocalizeEvent) labelLocalizeEvent.StringReference = null;
        
        var t = text ?? "";
        if (labelTMP) labelTMP.text = t;
        if (labelUGUI) labelUGUI.text = t;
    }

    /// <summary>
    /// (新しい) LocalizedString をセット
    /// </summary>
    public void SetLocalizedText(LocalizedString locText)
    {
        if (labelLocalizeEvent)
        {
            labelLocalizeEvent.StringReference = locText;
        }
        else
        {
            // LocalizeStringEvent が Prefab にない場合の最終フォールバック
            //（非同期になるため推奨されないが、動かなくはない）
            Debug.LogWarning("[TodoBox] labelLocalizeEvent が設定されていません。非同期でテキストを設定します。", this);
            SetText("[Loading...]");
            locText.GetLocalizedStringAsync().Completed += (handle) =>
            {
                if (this != null && labelTMP != null)
                    labelTMP.text = handle.Result;
            };
        }
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
using UnityEngine;
using UnityEngine.UI; // ★ Selectable を使うために追加

public class UISelectSoundManagerCaller : MonoBehaviour
{
    private Selectable targetSelectable; // ★ 追加

    void Start() // AwakeかStartで取得
    {
        targetSelectable = GetComponent<Selectable>();
    }

    public void UISelect()
    {
        // ★ 追加：無効なら鳴らさない
        if (targetSelectable != null && !targetSelectable.interactable) return;

        if (UISelectSoundManager.instance != null)
            UISelectSoundManager.instance.SelectPlaySound();
    }

    public void UISubmit()
    {
        // ★ 追加：無効なら鳴らさない
        if (targetSelectable != null && !targetSelectable.interactable) return;

        if (UISelectSoundManager.instance != null)
            UISelectSoundManager.instance.SubmitPlaySound();
    }
}
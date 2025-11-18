using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI; // ★ Selectable を使うために追加
using Cysharp.Threading.Tasks;

public class HoverToSelect : MonoBehaviour, IPointerEnterHandler
{
    private InputDeviceDetector inputDeviceDetector;
    private Selectable targetSelectable; // ★ 追加：自身のSelectable(Button)を取得用

    void Start()
    {
        inputDeviceDetector = InputDeviceDetector.Instance;
        targetSelectable = GetComponent<Selectable>(); // ★ ButtonもSelectableの一種なのでこれで取れる
    }

    public async void OnPointerEnter(PointerEventData eventData)
    {
        // ★ 追加：ボタンが無効(interactable = false)なら何もしない
        if (targetSelectable != null && !targetSelectable.interactable)
        {
            return; 
        }

        if (inputDeviceDetector.CurrentDeviceType != InputDeviceDetector.DeviceType.KeyboardMouse)
            return;

        if (EventSystem.current.currentSelectedGameObject != this.gameObject)
        {
            await UniTask.DelayFrame(1);

            // 1フレーム待機中に状況が変わっている可能性があるので再チェック
            if (this == null || (targetSelectable != null && !targetSelectable.interactable)) return;

            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(this.gameObject);
            }
        }
    }
}
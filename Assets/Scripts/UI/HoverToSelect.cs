using UnityEngine;
using UnityEngine.EventSystems;
using Cysharp.Threading.Tasks; // ★ UniTaskをusingする

/// <summary>
/// マウスが乗った時に、(競合を避けて) 1フレーム遅れてSelectする
/// </summary>
public class HoverToSelect : MonoBehaviour, IPointerEnterHandler
{
    private InputDeviceDetector inputDeviceDetector;
    void Start()
    {
        inputDeviceDetector = InputDeviceDetector.Instance;
    }
    // ▼▼▼ ★★★ async void に変更 ★★★ ▼▼▼
    public async void OnPointerEnter(PointerEventData eventData)
    {
        if (inputDeviceDetector.CurrentDeviceType != InputDeviceDetector.DeviceType.KeyboardMouse)
            return;
        // もし「現在選択されているオブジェクト」が「このオブジェクト」で *ない* 場合のみ実行
        if (EventSystem.current.currentSelectedGameObject != this.gameObject)
        {
            // ★★★ 1フレーム待機する ★★★
            // これにより、マウス移動によるナビゲーション誤動作と
            // SetSelectedGameObjectの呼び出しが競合するのを防ぐ
            await UniTask.DelayFrame(1);

            // 1フレーム待った後、(万が一nullになっていないかチェックしつつ)
            // このオブジェクトを選択状態にする
            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(this.gameObject);
            }
        }
    }
}
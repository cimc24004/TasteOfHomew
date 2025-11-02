using UnityEngine;

/// <summary>
/// InputDeviceDetector からの通知を受け取り、
/// デバイスに応じて GamePadIcon/KeyIcon の表示(SetActive)を切り替える。
/// (InstructionControl オブジェクトにアタッチする)
/// </summary>
public class InputVisualSwitcher : MonoBehaviour
{
    [Header("Icon GameObjects")]
    [Tooltip("ゲームパッド使用時に表示するGameObject")]
    [SerializeField]
    private GameObject gamePadIcon;

    [Tooltip("キーボード/マウス使用時に表示するGameObject")]
    [SerializeField]
    private GameObject keyIcon;

    
    private void Start()
    {
        if (InputDeviceDetector.Instance == null)
        {
            Debug.LogError("InputDeviceDetector がシーンに存在しません！", this);
            return;
        }

        // 1. マネージャーのイベント（OnDeviceChanged）を購読（リッスン）する
        InputDeviceDetector.Instance.OnDeviceChanged += UpdateVisuals;

        // 2. 起動時の現在のデバイスタイプに基づいて、初期表示を設定する
        UpdateVisuals(InputDeviceDetector.Instance.CurrentDeviceType);
    }

    private void OnDestroy()
    {
        // 3. このオブジェクトが破棄される時、イベントの購読を解除する
        if (InputDeviceDetector.Instance != null)
        {
            InputDeviceDetector.Instance.OnDeviceChanged -= UpdateVisuals;
        }
    }

    /// <summary>
    /// InputDeviceDetector からイベントで呼び出される
    /// </summary>
    private void UpdateVisuals(InputDeviceDetector.DeviceType deviceType)
    {
        if (deviceType == InputDeviceDetector.DeviceType.Gamepad)
        {
            // --- アイコンの切り替え ---
            if (gamePadIcon != null)
            {
                gamePadIcon.SetActive(true);
            }
            if (keyIcon != null)
            {
                keyIcon.SetActive(false);
            }
        }
        else // KeyboardMouse
        {
            // --- アイコンの切り替え ---
            if (gamePadIcon != null)
            {
                gamePadIcon.SetActive(false);
            }
            if (keyIcon != null)
            {
                keyIcon.SetActive(true);
            }
        }
    }
}
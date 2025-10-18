using UnityEngine;
using UnityEngine.UI;
using TMPro; // TextMeshProのDropdownを使う場合

/// <summary>
/// オプション画面のUIコンポーネントを管理し、
/// ユーザーの入力をConfigControllerに伝える。
/// </summary>
public class ConfigMenuUIController : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Slider masterVolumeSlider;
    [SerializeField] private TMP_Dropdown qualityDropdown;

    [Header("Controller Reference")]
    [SerializeField] private ConfigController configController;

    // --- InspectorのOnValueChanged()から呼び出すメソッド群 ---

    public void OnMasterVolumeChanged(float value)
    {
        configController.SetMasterVolume(value);
    }

    public void OnQualityChanged(int index)
    {
        configController.SetQuality(index);
    }

    public void OnVsyncChanged(bool isOn)
    {
        configController.SetVsync(isOn);
    }

    // --- ConfigControllerから呼び出されるメソッド ---

    /// <summary>
    /// ConfigControllerからロードした設定値をUIに反映させる
    /// </summary>
    public void SetUIValues(float volume, int qualityIndex, bool vsync)
    {
        // UIのイベントが発火しないように、一時的にnullにする（より安全な方法）
        // ※単純に代入するだけでも動作はしますが、値のセット時にOnValueChangedが
        //   呼ばれてしまい、二重でConfigControllerのSetメソッドが呼ばれる可能性があるため。
        
        masterVolumeSlider.onValueChanged.RemoveAllListeners();
        qualityDropdown.onValueChanged.RemoveAllListeners();

        // 値をUIにセット
        masterVolumeSlider.value = volume;
        qualityDropdown.value = qualityIndex;

        // イベントリスナーを再登録
        masterVolumeSlider.onValueChanged.AddListener(OnMasterVolumeChanged);
        qualityDropdown.onValueChanged.AddListener(OnQualityChanged);
    }

    // Start()やAwake()でリスナーをAddListenerする方法もありますが、
    // 起動時にUIの値をセットする際の処理(SetUIValues)と競合しやすいため、
    // 上記のようにInspectorから設定するか、SetUIValues内で管理する方が安全です。
    // ここではSetUIValues内で管理する方法を採用しています。
}
using UnityEngine;
using UnityEngine.Audio; // AudioMixerを使うため
using UnityEngine.Rendering.Universal; // URPの設定（今回はQualitySettingsを使用）

/// <summary>
/// ゲームの設定値を管理し、保存・読み込み・適用を行う。
/// ConfigMenuUIControllerと連携して動作する。
/// </summary>
public class ConfigController : MonoBehaviour
{
    [Header("UI Controller")]
    [SerializeField] private ConfigMenuUIController uiController;

    [Header("Game System References")]
    [SerializeField] private AudioMixer masterMixer;
    // [SerializeField] private UniversalRenderPipelineAsset urpAsset; // URPアセットを直接いじる場合
    // ※今回はより簡単なQualitySettingsを使います

    // PlayerPrefsに保存するためのキー
    private const string PREFS_VOLUME = "MasterVolume";
    private const string PREFS_QUALITY = "QualityIndex";
    private const string PREFS_VSYNC = "Vsync";

    // 現在の設定値（内部保持用）
    private float currentVolume;
    private int currentQualityIndex;
    private bool currentVsync;

    void Start()
    {
        LoadSettings();
    }

    /// <summary>
    /// PlayerPrefsから設定を読み込む
    /// </summary>
    private void LoadSettings()
    {
        // 値を読み込む（見つからなければデフォルト値を設定）
        currentVolume = PlayerPrefs.GetFloat(PREFS_VOLUME, 0.8f);
        currentQualityIndex = PlayerPrefs.GetInt(PREFS_QUALITY, 1); // 0:Low, 1:Medium, 2:High
        currentVsync = PlayerPrefs.GetInt(PREFS_VSYNC, 1) == 1; // 1 = true

        // 読み込んだ設定をゲームに適用
        ApplyAllSettings();

        // 読み込んだ設定をUIに反映させる
        if (uiController != null)
        {
            uiController.SetUIValues(currentVolume, currentQualityIndex, currentVsync);
        }
    }

    /// <summary>
    /// 現在の設定値をPlayerPrefsに保存する
    /// </summary>
    private void SaveSettings()
    {
        PlayerPrefs.SetFloat(PREFS_VOLUME, currentVolume);
        PlayerPrefs.SetInt(PREFS_QUALITY, currentQualityIndex);
        PlayerPrefs.SetInt(PREFS_VSYNC, currentVsync ? 1 : 0);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// すべての設定をゲームに即時適用する
    /// </summary>
    private void ApplyAllSettings()
    {
        ApplyMasterVolume();
        ApplyQuality();
        ApplyVsync();
    }

    // --- UIControllerから呼び出されるメソッド群 ---

    public void SetMasterVolume(float value)
    {
        currentVolume = value;
        ApplyMasterVolume();
        SaveSettings(); // 値が変更されたら即保存
    }

    public void SetQuality(int index)
    {
        currentQualityIndex = index;
        ApplyQuality();
        SaveSettings();
    }

    public void SetVsync(bool isOn)
    {
        currentVsync = isOn;
        ApplyVsync();
        SaveSettings();
    }

    // --- 実際の設定適用処理 ---

    private void ApplyMasterVolume()
    {
        if (masterMixer == null) return;
        
        // AudioMixerのボリュームはデシベル(dB)で、リニア(0-1)ではない
        // 0.0001 -> -80dB (ミュート), 1.0 -> 0dB (最大)
        float db = Mathf.Log10(Mathf.Max(currentVolume, 0.0001f)) * 20f;
        masterMixer.SetFloat("MasterVolumeParam", db); // "MasterVolumeParam"はMixerで公開した変数名
    }

    private void ApplyQuality()
    {
        // Project Settings > Quality で設定した品質レベルをインデックスで切り替える
        // URPの場合、各品質レベルにURPアセットを割り当てておく
        QualitySettings.SetQualityLevel(currentQualityIndex, true);
    }

    private void ApplyVsync()
    {
        // VSync(垂直同期) 0:OFF, 1:ON
        QualitySettings.vSyncCount = currentVsync ? 1 : 0;
    }
}
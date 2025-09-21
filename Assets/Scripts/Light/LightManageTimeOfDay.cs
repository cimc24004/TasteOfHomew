using UnityEngine;
using Sirenix.OdinInspector;

/// <summary>
/// TimeOfDayRendererの状態に応じて、アタッチされたLightコンポーネントのプロパティを制御します。
/// </summary>
[RequireComponent(typeof(Light))]
public class LightManageTimeOfDay : SerializedMonoBehaviour
{
    // 各時間帯のライト設定をまとめたクラス
    [System.Serializable]
    public class LightSettingsProfile
    {
        [Tooltip("この時間帯にライトを有効にするか")]
        public bool isEnabled = true;

        [ShowIf("isEnabled")]
        public float intensity = 1f;

        [ShowIf("isEnabled")]
        [ColorUsage(true, true), Tooltip("ライトの色（HDR対応）")]
        public Color color = Color.white;
    }

    [Title("必須コンポーネント")]
    [SerializeField, Required("シーン内のTimeOfDayRendererを指定してください")]
    private TimeOfDayRenderer timeOfDayRenderer;

    // このGameObjectにアタッチされているLightコンポーネント
    private Light controlledLight;

    [Title("時間帯ごとのライト設定")]
    [BoxGroup("Morning", showLabel: false)]
    public LightSettingsProfile morningSettings = new LightSettingsProfile();

    [BoxGroup("Evening", showLabel: false)]
    public LightSettingsProfile eveningSettings = new LightSettingsProfile();
    
    [BoxGroup("Night", showLabel: false)]
    public LightSettingsProfile nightSettings = new LightSettingsProfile { isEnabled = true }; // 夜はデフォルトでON

    // 前回のフレームでの時間帯を記憶しておく変数
    private TimeOfDayRenderer.TimeOfDay? previousState = null;

    // コンポーネントがリセットされた時や最初に追加された時に呼ばれる
    private void Reset()
    {
        // 参照を自動で取得
        controlledLight = GetComponent<Light>();
    }

    private void Awake()
    {
        // Lightコンポーネントを確実に取得
        controlledLight = GetComponent<Light>();
    }

    // ゲーム開始時の最初のフレームで呼ばれる
    void Start()
    {
        timeOfDayRenderer = TimeOfDayRenderer.instance;
        timeOfDayRenderer.ApplingEvent.AddListener((TimeOfDayRenderer.TimeOfDay timeofDay) =>
        {
                CheckAndUpdateLight();
        });
        // ゲーム開始時に現在の時間帯に合わせた設定を即時適用
    }


    /// <summary>
    /// TimeOfDayRendererの状態を監視し、変更があればライトに設定を適用する
    /// </summary>
    private void CheckAndUpdateLight()
    {

        TimeOfDayRenderer.TimeOfDay currentState = timeOfDayRenderer.Current;

        // 状態が前回から変わっていなければ何もしない（効率化）
        // if (previousState.HasValue && previousState.Value == currentState)
        // {
        //     return;
        // }

        // 状態が変更されたので、現在の状態に応じた設定を適用
        switch (currentState)
        {
            case TimeOfDayRenderer.TimeOfDay.Morning:
                ApplySettings(morningSettings);
                break;
            case TimeOfDayRenderer.TimeOfDay.Evening:
                ApplySettings(eveningSettings);
                break;
            case TimeOfDayRenderer.TimeOfDay.Night:
                ApplySettings(nightSettings);
                break;
        }

        // 現在の状態を「前回の状態」として記憶
        previousState = currentState;
    }

    /// <summary>
    /// 指定されたプロファイルの設定をLightコンポーネントに適用する
    /// </summary>
    /// <param name="profile">適用する設定プロファイル</param>
    private void ApplySettings(LightSettingsProfile profile)
    {
        controlledLight.enabled = profile.isEnabled;
        
        // ライトが有効な場合のみ、他のプロパティも設定
        if (profile.isEnabled)
        {
            controlledLight.intensity = profile.intensity;
            controlledLight.color = profile.color;
        }
    }
}
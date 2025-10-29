// ---------------------------------------------------------------------
// ■■■ 必須設定 ■■■
// 使用するアセットの行のコメント（//）を削除して有効化してください。
// ---------------------------------------------------------------------

// --- FronkonGames.Glitches 名前空間直下のエフェクト ---
// #define GLITCH_COLORBLINDNESS
// #define GLITCH_CORRUPTMEMORY
#define GLITCH_INTERFERENCES
#define GLITCH_VHS
#define GLITCH_CHEAPCAMERA
// #define GLITCH_BADGPU
#define GLITCH_ARTIFACTS
#define GLITCH_HACKED
// #define GLITCH_BROKENLCD

// ---------------------------------------------------------------------
// ■■■ スクリプト本体（ここより下は編集不要です） ■■■
// ---------------------------------------------------------------------

using UnityEngine;
using UnityEngine.SceneManagement; // <-- シーン管理のために追加

// #define で指定されたアセットの 'using' のみコンパイル対象になります
#if GLITCH_COLORBLINDNESS
using FronkonGames.Glitches.ColorBlindness;
#endif
#if GLITCH_CORRUPTMEMORY
using FronkonGames.Glitches.CorruptMemory;
#endif
#if GLITCH_INTERFERENCES
using FronkonGames.Glitches.Interferences;
#endif
#if GLITCH_VHS
using FronkonGames.Glitches.VHS;
#endif
#if GLITCH_CHEAPCAMERA
using FronkonGames.Glitches.CheapCamera;
#endif
#if GLITCH_BADGPU
using FronkonGames.Glitches.BadGPU;
#endif
#if GLITCH_ARTIFACTS
using FronkonGames.Glitches.Artifacts;
#endif
#if GLITCH_HACKED
using FronkonGames.Glitches.Hacked;
#endif
#if GLITCH_BROKENLCD
using FronkonGames.Glitches.BrokenLCD;
#endif


/// <summary>
/// Fronkon Games Glitches の Render Feature 自体を SetActive(bool) で有効/無効にします。
/// シーン移動時にリセットする機能、エディタ停止時にリセットする機能を含みます。
/// </summary>
public class URPFxController : Singleton<URPFxController>
{
    [Header("Settings")]
    [SerializeField]
    [Tooltip("trueの場合、シーンがロードされるたびにエフェクトの状態を起動時の状態にリセットします。")]
    private bool resetOnSceneChange = true;

    // --- Instanceキャッシュ ---
#if GLITCH_COLORBLINDNESS
    private ColorBlindness instanceColorBlindness;
    private bool initialActiveColorBlindness; // 元の状態(bool)を保存
#endif
#if GLITCH_CORRUPTMEMORY
    private CorruptMemory instanceCorruptMemory;
    private bool initialActiveCorruptMemory;
#endif
#if GLITCH_INTERFERENCES
    private Interferences instanceInterferences;
    private bool initialActiveInterferences;
#endif
#if GLITCH_VHS
    private VHS instanceVHS;
    private bool initialActiveVHS;
#endif
#if GLITCH_CHEAPCAMERA
    private CheapCamera instanceCheapCamera;
    private bool initialActiveCheapCamera;
#endif
#if GLITCH_BADGPU
    private BadGPU instanceBadGPU;
    private bool initialActiveBadGPU;
#endif
#if GLITCH_ARTIFACTS
    private Artifacts instanceArtifacts;
    private bool initialActiveArtifacts;
#endif
#if GLITCH_HACKED
    private Hacked instanceHacked;
    private bool initialActiveHacked;
#endif
#if GLITCH_BROKENLCD
    private BrokenLCD instanceBrokenLCD;
    private bool initialActiveBrokenLCD;
#endif


    void Start()
    {
        // 各エフェクトの Instance と、起動時の Active 状態を取得・保存します。
#if GLITCH_COLORBLINDNESS
        instanceColorBlindness = ColorBlindness.Instance;
        if (instanceColorBlindness != null)
            initialActiveColorBlindness = instanceColorBlindness.isActive;
#endif
#if GLITCH_CORRUPTMEMORY
        instanceCorruptMemory = CorruptMemory.Instance;
        if (instanceCorruptMemory != null)
            initialActiveCorruptMemory = instanceCorruptMemory.isActive;
#endif
#if GLITCH_INTERFERENCES
        instanceInterferences = Interferences.Instance;
        if (instanceInterferences != null)
            initialActiveInterferences = instanceInterferences.isActive;
#endif
#if GLITCH_VHS
        instanceVHS = VHS.Instance;
        if (instanceVHS != null)
            initialActiveVHS = instanceVHS.isActive;
#endif
#if GLITCH_CHEAPCAMERA
        instanceCheapCamera = CheapCamera.Instance;
        if (instanceCheapCamera != null)
            initialActiveCheapCamera = instanceCheapCamera.isActive;
#endif
#if GLITCH_BADGPU
        instanceBadGPU = BadGPU.Instance;
        if (instanceBadGPU != null)
            initialActiveBadGPU = instanceBadGPU.isActive;
#endif
#if GLITCH_ARTIFACTS
        instanceArtifacts = Artifacts.Instance;
        if (instanceArtifacts != null)
            initialActiveArtifacts = instanceArtifacts.isActive;
#endif
#if GLITCH_HACKED
        instanceHacked = Hacked.Instance;
        if (instanceHacked != null)
            initialActiveHacked = instanceHacked.isActive;
#endif
#if GLITCH_BROKENLCD
        instanceBrokenLCD = BrokenLCD.Instance;
        if (instanceBrokenLCD != null)
            initialActiveBrokenLCD = instanceBrokenLCD.isActive;
#endif
        
        // 【追加】シーンがロードされた時のイベントに関数を登録
        SceneManager.activeSceneChanged += OnActiveSceneChanged;
    }

    /// <summary>
    /// 【追加】全てのFronkonエフェクトを、Start()時に記憶した初期状態に戻します。
    /// </summary>
    private void ResetAllEffectsToInitialState()
    {
#if GLITCH_COLORBLINDNESS
        if (instanceColorBlindness != null) instanceColorBlindness.SetActive(initialActiveColorBlindness);
#endif
#if GLITCH_CORRUPTMEMORY
        if (instanceCorruptMemory != null) instanceCorruptMemory.SetActive(initialActiveCorruptMemory);
#endif
#if GLITCH_INTERFERENCES
        if (instanceInterferences != null) instanceInterferences.SetActive(initialActiveInterferences);
#endif
#if GLITCH_VHS
        if (instanceVHS != null) instanceVHS.SetActive(initialActiveVHS);
#endif
#if GLITCH_CHEAPCAMERA
        if (instanceCheapCamera != null) instanceCheapCamera.SetActive(initialActiveCheapCamera);
#endif
#if GLITCH_BADGPU
        if (instanceBadGPU != null) instanceBadGPU.SetActive(initialActiveBadGPU);
#endif
#if GLITCH_ARTIFACTS
        if (instanceArtifacts != null) instanceArtifacts.SetActive(initialActiveArtifacts);
#endif
#if GLITCH_HACKED
        if (instanceHacked != null) instanceHacked.SetActive(initialActiveHacked);
#endif
#if GLITCH_BROKENLCD
        if (instanceBrokenLCD != null) instanceBrokenLCD.SetActive(initialActiveBrokenLCD);
#endif
    }

    /// <summary>
    /// 【追加】シーンが変更されたときに呼び出される関数
    /// </summary>
    private void OnActiveSceneChanged(Scene current, Scene next)
    {
        // bool が true に設定されている場合のみ、リセット処理を呼ぶ
        if (resetOnSceneChange)
        {
            ResetAllEffectsToInitialState();
        }
    }

    /// <summary>
    /// プレイモード終了時（またはオブジェクト破棄時）に呼ばれます。
    /// </summary>
    void OnDestroy()
    {
        // 【変更】イベントの登録を解除 (メモリリーク防止)
        SceneManager.activeSceneChanged -= OnActiveSceneChanged;

        // 【変更】エディタ実行時のみ、エディタ停止時に値を戻す (ResetAllEffects...を呼ぶように変更)
        if (Application.isEditor)
        {
            ResetAllEffectsToInitialState();
        }
    }


    void Update()
    {
        // UIボタンや他のスクリプトから下記のSet関数を呼び出してください。
    }

    // --- On/Off 切り替え関数 (変更なし) ---

#if GLITCH_COLORBLINDNESS
    public void SetColorBlindness(bool isOn)
    {
        if (instanceColorBlindness != null)
            instanceColorBlindness.SetActive(isOn);
    }
#endif

#if GLITCH_CORRUPTMEMORY
    public void SetCorruptMemory(bool isOn)
    {
        if (instanceCorruptMemory != null)
            instanceCorruptMemory.SetActive(isOn);
    }
#endif

#if GLITCH_INTERFERENCES
    public void SetInterferences(bool isOn)
    {
        if (instanceInterferences != null)
            instanceInterferences.SetActive(isOn);
    }
#endif

#if GLITCH_VHS
    public void SetVHS(bool isOn)
    {
        if (instanceVHS != null)
            instanceVHS.SetActive(isOn);
    }
#endif

#if GLITCH_CHEAPCAMERA
    public void SetCheapCamera(bool isOn)
    {
        if (instanceCheapCamera != null)
            instanceCheapCamera.SetActive(isOn);
    }
#endif

#if GLITCH_BADGPU
    public void SetBadGPU(bool isOn)
    {
        if (instanceBadGPU != null)
            instanceBadGPU.SetActive(isOn);
    }
#endif

#if GLITCH_ARTIFACTS
    public void SetArtifacts(bool isOn)
    {
        if (instanceArtifacts != null)
            instanceArtifacts.SetActive(isOn);
    }
#endif

#if GLITCH_HACKED
    public void SetHacked(bool isOn)
    {
        if (instanceHacked != null)
            instanceHacked.SetActive(isOn);
    }
#endif

#if GLITCH_BROKENLCD
    public void SetBrokenLCD(bool isOn)
    {
        if (instanceBrokenLCD != null)
            instanceBrokenLCD.SetActive(isOn);
    }
#endif
}
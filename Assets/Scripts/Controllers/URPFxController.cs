// ---------------------------------------------------------------------
// ■■■ 必須設定 ■■■
// 使用するアセットの行のコメント（//）を削除して有効化してください。
// ---------------------------------------------------------------------

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
// ■■■ スクリプト本体 ■■■
// ---------------------------------------------------------------------

using UnityEngine;
using System.Collections;

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

public class URPFxController : Singleton<URPFxController>
{
    [Header("Settings")]
    [SerializeField]
    [Tooltip("trueの場合、このスクリプトが開始されたタイミングで全エフェクトを強制的にOFFにします。")]
    private bool forceResetOnStart = true;

    // --- Instanceキャッシュ ---
#if GLITCH_COLORBLINDNESS
    private ColorBlindness instanceColorBlindness;
#endif
#if GLITCH_CORRUPTMEMORY
    private CorruptMemory instanceCorruptMemory;
#endif
#if GLITCH_INTERFERENCES
    private Interferences instanceInterferences;
#endif
#if GLITCH_VHS
    private VHS instanceVHS;
#endif
#if GLITCH_CHEAPCAMERA
    private CheapCamera instanceCheapCamera;
#endif
#if GLITCH_BADGPU
    private BadGPU instanceBadGPU;
#endif
#if GLITCH_ARTIFACTS
    private Artifacts instanceArtifacts;
#endif
#if GLITCH_HACKED
    private Hacked instanceHacked;
#endif
#if GLITCH_BROKENLCD
    private BrokenLCD instanceBrokenLCD;
#endif

    public void Start()
    {
        // インスタンスを取得
        InitializeInstances();

        // シーン開始時に強制リセット（DontDestroyでないなら、ここを通るたびにリセットをかければOK）
        if (forceResetOnStart)
        {
            StartCoroutine(ForceResetCoroutine());
        }
    }

    /// <summary>
    /// 各FeatureのInstanceを取得する
    /// </summary>
    private void InitializeInstances()
    {
#if GLITCH_COLORBLINDNESS
        instanceColorBlindness = ColorBlindness.Instance;
#endif
#if GLITCH_CORRUPTMEMORY
        instanceCorruptMemory = CorruptMemory.Instance;
#endif
#if GLITCH_INTERFERENCES
        instanceInterferences = Interferences.Instance;
#endif
#if GLITCH_VHS
        instanceVHS = VHS.Instance;
#endif
#if GLITCH_CHEAPCAMERA
        instanceCheapCamera = CheapCamera.Instance;
#endif
#if GLITCH_BADGPU
        instanceBadGPU = BadGPU.Instance;
#endif
#if GLITCH_ARTIFACTS
        instanceArtifacts = Artifacts.Instance;
#endif
#if GLITCH_HACKED
        instanceHacked = Hacked.Instance;
#endif
#if GLITCH_BROKENLCD
        instanceBrokenLCD = BrokenLCD.Instance;
#endif
    }

    /// <summary>
    /// 1フレーム待ってから強制的にOFFにするコルーチン
    /// </summary>
    private IEnumerator ForceResetCoroutine()
    {
        // URPの初期化や前シーンの残存処理と競合しないよう、念のため1フレーム待つ
        yield return null;

        ForceDisableAll();
    }

    /// <summary>
    /// 全エフェクトを強制的に無効化(false)にする
    /// </summary>
    private void ForceDisableAll()
    {
#if GLITCH_COLORBLINDNESS
        if (instanceColorBlindness != null) instanceColorBlindness.SetActive(false);
#endif
#if GLITCH_CORRUPTMEMORY
        if (instanceCorruptMemory != null) instanceCorruptMemory.SetActive(false);
#endif
#if GLITCH_INTERFERENCES
        if (instanceInterferences != null) instanceInterferences.SetActive(false);
#endif
#if GLITCH_VHS
        if (instanceVHS != null) instanceVHS.SetActive(false);
#endif
#if GLITCH_CHEAPCAMERA
        if (instanceCheapCamera != null) instanceCheapCamera.SetActive(false);
#endif
#if GLITCH_BADGPU
        if (instanceBadGPU != null) instanceBadGPU.SetActive(false);
#endif
#if GLITCH_ARTIFACTS
        if (instanceArtifacts != null) instanceArtifacts.SetActive(false);
#endif
#if GLITCH_HACKED
        if (instanceHacked != null) instanceHacked.SetActive(false);
#endif
#if GLITCH_BROKENLCD
        if (instanceBrokenLCD != null) instanceBrokenLCD.SetActive(false);
#endif
        // Debug.Log("[URPFxController] All glitches have been reset to OFF.");
    }

    // --- On/Off 切り替え関数（外部からの操作用） ---

#if GLITCH_COLORBLINDNESS
    public void SetColorBlindness(bool isOn) { if (instanceColorBlindness != null) instanceColorBlindness.SetActive(isOn); }
#endif
#if GLITCH_CORRUPTMEMORY
    public void SetCorruptMemory(bool isOn) { if (instanceCorruptMemory != null) instanceCorruptMemory.SetActive(isOn); }
#endif
#if GLITCH_INTERFERENCES
    public void SetInterferences(bool isOn) { if (instanceInterferences != null) instanceInterferences.SetActive(isOn); }
#endif
#if GLITCH_VHS
    public void SetVHS(bool isOn) { if (instanceVHS != null) instanceVHS.SetActive(isOn); }
#endif
#if GLITCH_CHEAPCAMERA
    public void SetCheapCamera(bool isOn) { if (instanceCheapCamera != null) instanceCheapCamera.SetActive(isOn); }
#endif
#if GLITCH_BADGPU
    public void SetBadGPU(bool isOn) { if (instanceBadGPU != null) instanceBadGPU.SetActive(isOn); }
#endif
#if GLITCH_ARTIFACTS
    public void SetArtifacts(bool isOn) { if (instanceArtifacts != null) instanceArtifacts.SetActive(isOn); }
#endif
#if GLITCH_HACKED
    public void SetHacked(bool isOn) { if (instanceHacked != null) instanceHacked.SetActive(isOn); }
#endif
#if GLITCH_BROKENLCD
    public void SetBrokenLCD(bool isOn) { if (instanceBrokenLCD != null) instanceBrokenLCD.SetActive(isOn); }
#endif
}

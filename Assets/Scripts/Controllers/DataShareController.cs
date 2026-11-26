using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;
using UnityEngine.SceneManagement; // SceneManager.sceneLoaded を使うために必要

/// <summary>
/// 文字列キー -> bool のフラグを共有するシンプルなコントローラー（シングルトン）
/// EndingShareDictionary … エンディング到達等のフラグ用
/// FlagEventRunnerDictionary … 一度きりのイベント実行管理等のフラグ用
/// </summary>
public class DataShareController : Singleton<DataShareController>
{
    [Header("共有フラグ辞書")]
    // リネーム: shareDictionary → EndingShareDictionary
    public Dictionary<string, bool> EndingShareDictionary = new Dictionary<string, bool>();

    // FlagEventRunnerDictionary
    public Dictionary<string, bool> FlagEventRunnerDictionary = new Dictionary<string, bool>();

    [Header("共有ゲーム進行データ")]
    public string CurrentPhaseStep = "1";

    // GameFlowControllerが見つからない場合、コメントアウトを解除して下の行を使用してください
    public GameFlowController.Phase CurrentPhase = GameFlowController.Phase.Morning;
    // public int CurrentPhase = 0; // GameFlowController.Phase.Morning;

    [Header("シーン連動設定")]
    [Tooltip("（値リセット）このリストのシーンに移動した際、全フラグの値を false にリセットします（キーは残ります）")]
    public List<string> resetValuesOnLoadScenes = new List<string>();

    // テレポート連携 追加 (1/3)
    [Header("テレポート連携")]
    [Tooltip("（実行予約）次にシーンをロードした際、このキー名でテレポートを実行します。実行後、この値はクリアされます。")]
    [ReadOnly] // インスペクターからは変更させず、メソッド経由でのみ設定
    public string NextTeleportKey = "";

    // OnEnable / OnDisable 修正 (2/3)
    // AwakeではなくOnEnableで登録することが推奨されます
    private void OnEnable()
    {
        // シーンがロードされた時に OnSceneLoaded メソッドを呼ぶように登録
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    // OnEnableで登録したイベントは、OnDisableで解除します
    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    /// <summary>
    /// シーンがロードされたときに呼び出されるメソッド
    /// </summary>
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // --- 1. 値リセットチェック ---
        if (resetValuesOnLoadScenes.Contains(scene.name))
        {
            Debug.Log($"[{gameObject.name}] 値リセットシーン '{scene.name}' に移動したため、全てのフラグ値を false にします。");
            ResetAllValuesToFalse();
        }

        // テレポート連携 修正 (3/3)
        // --- 2. テレポート実行予約チェック ---
        // NextTeleportKeyにキー名がセットされていたら
        if (!string.IsNullOrEmpty(NextTeleportKey))
        {
            // TeleportDataControllerが（このロードされたシーンに）存在するか確認
            if (TeleportDataController.instance != null)
            {
                Debug.Log($"[{gameObject.name}] シーンロードにより、テレポート '{NextTeleportKey}' を予約実行します。");

                // TeleportDataControllerに「キー名」と「true (使用する)」を渡す
                TeleportDataController.instance.TeleportDataUse(NextTeleportKey, true);

                // 予約を実行したので、キーをクリアする
                NextTeleportKey = "";
            }
            else
            {
                // テレポートキーは予約されていたが、移動先のシーンにTeleportDataControllerがなかった場合
                Debug.LogWarning($"[{gameObject.name}] テレポートキー '{NextTeleportKey}' が予約されていましたが、" +
                                 $"シーン '{scene.name}' に TeleportDataController.instance が見つかりません。");

                // ※キーはクリアせず、次のシーンロードまで持ち越すことも可能ですが、
                // 意図しないテレポートを防ぐため、ここではクリアします。
                NextTeleportKey = "";
            }
        }
    }

    /// <summary>
    /// すべてのDictionaryの値を false にリセットします。
    /// </summary>
    private void ResetAllValuesToFalse()
    {
        // EndingShareDictionary
        if (EndingShareDictionary != null && EndingShareDictionary.Count > 0)
        {
            List<string> keys = new List<string>(EndingShareDictionary.Keys);
            foreach (string key in keys)
            {
                EndingShareDictionary[key] = false;
            }
        }

        // FlagEventRunnerDictionary
        if (FlagEventRunnerDictionary != null && FlagEventRunnerDictionary.Count > 0)
        {
            List<string> keys = new List<string>(FlagEventRunnerDictionary.Keys);
            foreach (string key in keys)
            {
                FlagEventRunnerDictionary[key] = false;
            }
        }

        Debug.Log($"[{gameObject.name}] 全てのフラグの値が false にリセットされました。");
    }

    // ===== テレポート連携API =====

    /// <summary>
    /// 次回シーンロード時に使用するテレポートキーを予約（セット）します。
    /// （例：スタート画面のContinueControllerから呼び出す）
    /// </summary>
    /// <param name="keyName">Teleport.csのteleportKeyNameと一致するキー</param>
    public void SetNextTeleportKey(string keyName)
    {
        Debug.Log($"[{gameObject.name}] 次回テレポートキーを予約: {keyName}");
        NextTeleportKey = keyName;
    }

    // ===== EndingShareDictionary 用API =====
    public void SetFlagToTrue(string key)
    {
        if (string.IsNullOrEmpty(key)) return;
        EndingShareDictionary[key] = true;
    }

    public void SetFlag(string key, bool value)
    {
        if (string.IsNullOrEmpty(key)) return;
        EndingShareDictionary[key] = value;
    }
    public void ChangePhaseStep(string _step)
    {
        this.CurrentPhaseStep = _step;
    }
    public bool GetFlag(string key)
    {
        if (string.IsNullOrEmpty(key)) return false;
        if (EndingShareDictionary.TryGetValue(key, out bool val)) return val;
        return false;
    }

    public bool TryGetFlag(string key, out bool value)
    {
        if (string.IsNullOrEmpty(key))
        {
            value = false;
            return false;
        }
        return EndingShareDictionary.TryGetValue(key, out value);
    }

    public bool HasFlag(string key)
    {
        if (string.IsNullOrEmpty(key)) return false;
        return EndingShareDictionary.ContainsKey(key);
    }

    public bool RemoveFlag(string key)
    {
        if (string.IsNullOrEmpty(key)) return false;
        return EndingShareDictionary.Remove(key);
    }

    public void ToggleFlag(string key)
    {
        if (string.IsNullOrEmpty(key)) return;
        bool current = GetFlag(key);
        EndingShareDictionary[key] = !current;
    }

    public void ClearAllEndingFlags()
    {
        EndingShareDictionary.Clear();
    }

    // ===== FlagEventRunnerDictionary 用API =====
    public void SetFlagEventRunner(string key, bool value)
    {
        if (string.IsNullOrEmpty(key)) return;
        FlagEventRunnerDictionary[key] = value;
    }

    public bool GetFlagEventRunner(string key)
    {
        if (string.IsNullOrEmpty(key)) return false;
        if (FlagEventRunnerDictionary.TryGetValue(key, out bool val)) return val;
        return false;
    }

    public bool TryGetFlagEventRunner(string key, out bool value)
    {
        if (string.IsNullOrEmpty(key))
        {
            value = false;
            return false;
        }
        return FlagEventRunnerDictionary.TryGetValue(key, out value);
    }

    public bool HasFlagEventRunner(string key)
    {
        if (string.IsNullOrEmpty(key)) return false;
        return FlagEventRunnerDictionary.ContainsKey(key);
    }

    public bool RemoveFlagEventRunner(string key)
    {
        if (string.IsNullOrEmpty(key)) return false;
        return FlagEventRunnerDictionary.Remove(key);
    }
    public void SetTrueEventRunner(string key)
    {
        if (string.IsNullOrEmpty(key)) return;
        FlagEventRunnerDictionary[key] = true;
    }
    public void ToggleFlagEventRunner(string key)
    {
        if (string.IsNullOrEmpty(key)) return;
        bool current = GetFlagEventRunner(key);
        FlagEventRunnerDictionary[key] = !current;
    }

    public void ClearAllFlagEventRunners()
    {
        FlagEventRunnerDictionary.Clear();
    }

    // ===== 互換: まとめて全消去 =====
    public void ClearAll()
    {
        EndingShareDictionary.Clear();
        FlagEventRunnerDictionary.Clear();
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement; // ← 追加

[AddComponentMenu("Game/Flags/Flag Event Runner (Rise Edge)")]
public class FlagEventRunner : MonoBehaviour
{
    [Serializable]
    public class FlagSlot
    {
        [Tooltip("このフラグの現在値。false→trueに変わった瞬間に onBecameTrue が実行される")]
        public bool value;

        [Tooltip("value が false→true に変化した瞬間に実行されるイベント")]
        public UnityEvent2 onBecameTrue = new UnityEvent2();

        [Tooltip("実行後に value を false に戻す（1回こっきり実行したい時）")]
        public bool resetAfterInvoke = false;
    }

    [Serializable] public struct Entry { public string key; public FlagSlot slot; }

    [Header("初期データ（インスペクタ編集用）")]
    public List<Entry> initialFlags = new();

    [Header("起動時の実行")]
    [Tooltip("Startで、false→true の“立ち上がり”だけ初回として実行する")]
    public bool invokeIfTrueOnStart = true;

    [Header("外部変更の監視（任意）")]
    [Tooltip("インスペクタや他スクリプトから value を直接変更する運用のために、毎フレーム差分を検出して瞬間実行する")]
    public bool watchExternalChanges = false;

    /// ランタイム辞書
    public Dictionary<string, FlagSlot> flags { get; private set; } = new();

    /// 直近の値（立ち上がり検出用）
    private readonly Dictionary<string, bool> _lastValues = new();


    void Awake()
    {
        BuildDictionary();
        // SnapshotLastValues(); ← これ消す
        // 初期prevは未登録（=false）として扱わせる
    }

    void Start()
    {
        MergeFromDataShare();

        if (invokeIfTrueOnStart)
        {
            // 起動時は false→true の“立ち上がり”だけ実行
            foreach (var kv in flags)
            {
                var key  = kv.Key;
                var prev = _lastValues.TryGetValue(key, out var p) ? p : false;
                var cur  = kv.Value.value;

                if (!prev && cur)
                {
                    InvokeAndMaybeReset(key, kv.Value);
                }
            }
        }

        SnapshotLastValues(); // 以後の監視と整合
    }

    void Update()
    {
        if (!watchExternalChanges) return;

        foreach (var kv in flags)
        {
            var key  = kv.Key;
            var prev = _lastValues.TryGetValue(key, out var p) ? p : false;
            var cur  = kv.Value.value;

            if (!prev && cur)
            {
                InvokeAndMaybeReset(key, kv.Value);
                cur = flags[key].value; // resetAfterInvoke 対応
            }

            _lastValues[key] = cur;
        }
    }

    // ===== 初期構築 =====
    public void BuildDictionary()
    {
        flags.Clear();
        foreach (var e in initialFlags)
        {
            if (string.IsNullOrWhiteSpace(e.key)) continue;
            flags[e.key] = new FlagSlot
            {
                value = e.slot.value,
                onBecameTrue = e.slot.onBecameTrue,
                resetAfterInvoke = e.slot.resetAfterInvoke
            };
        }
    }

    private void SnapshotLastValues()
    {
        _lastValues.Clear();
        foreach (var kv in flags) _lastValues[kv.Key] = kv.Value.value;
    }

    // ===== API経由の更新（ミラー書き込みはしない：シーンロード時のみ） =====
    public void SetFlag(string key, bool newValue)
    {
        if (string.IsNullOrWhiteSpace(key)) return;

        if (!flags.TryGetValue(key, out var slot)) slot = new FlagSlot();

        bool prev = slot.value;
        slot.value = newValue;
        flags[key] = slot;

        if (!prev && newValue)
        {
            InvokeAndMaybeReset(key, slot);
        }

        bool finalVal = flags[key].value;
        _lastValues[key] = finalVal;
    }
    // ★ DataShare から FlagEventRunnerDictionary の実値を取り込む
    private void MergeFromDataShare()
    {
        var ds = DataShareController.instance;
        if (ds == null) return;

        var dict = ds.FlagEventRunnerDictionary;
        if (dict == null || dict.Count == 0) return;

        foreach (var kv in dict)
        {
            // Invoke せずに “値だけ” 同期（このあと Start 側の立ち上がり判定で発火させる）
            ForceValueWithoutInvoke(kv.Key, kv.Value);
            // Debug.Log($"[FlagEventRunner] Import from DataShare: {kv.Key}={kv.Value}");
        }
    }
    // Invokeせずに値だけ反映（初期同期用）
    private void ForceValueWithoutInvoke(string key, bool value)
    {
        if (string.IsNullOrWhiteSpace(key)) return;

        if (!flags.TryGetValue(key, out var slot)) slot = new FlagSlot();
        slot.value = value;
        flags[key] = slot;
        // _lastValues は Start 終端で Snapshot を取り直すので今は触らない
    }
    public void SetTrue(string key) => SetFlag(key, true);
    public void SetFalse(string key) => SetFlag(key, false);

    public bool ToggleFlag(string key)
    {
        bool cur = GetFlag(key);
        SetFlag(key, !cur);
        return !cur;
    }

    public bool GetFlag(string key) =>
        flags.TryGetValue(key, out var slot) && slot.value;

    // 実行＋必要ならリセット
    private void InvokeAndMaybeReset(string key, FlagSlot slot)
    {
        try { slot.onBecameTrue?.Invoke(); }
        catch (Exception ex) { Debug.LogError($"[FlagEventRunner] {key} 実行中に例外: {ex}"); }

        if (slot.resetAfterInvoke)
        {
            slot.value = false;
            flags[key] = slot;
        }
    }

#if UNITY_EDITOR
    [ContextMenu("Rebuild From InitialFlags")] private void _Ctx_Rebuild() => BuildDictionary();
    [ContextMenu("Snapshot LastValues")]      private void _Ctx_Snapshot() => SnapshotLastValues();
#endif
}

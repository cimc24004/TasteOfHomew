using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// ゲーム全体の進行を管理（朝→友達→夕→家探索→母帰宅→不在→外→小屋→END）。
/// フェーズ遷移時に内部フラグをブロードキャストします。
/// 他システムは C#イベント or UnityEvent へ購読してください。
/// </summary>
[DisallowMultipleComponent]
public class GameFlowController : Singleton<GameFlowController>
{
    // ===== フェーズ列挙 =====
    public enum Phase
    {
        Morning,        // 朝
        Friend,         // 友達
        Evening,        // 夕
        HomeSearch,     // 家探索
        MotherReturn,   // 母帰宅
        Absence,        // 不在
        Outside,        // 外
        Shed,           // 小屋
        End             // END
    }

    // ===== インスペクタ設定 =====
    [Title("Flow & Options")]
    [Tooltip("進行順序（必要なら並べ替え可）")]
    public List<Phase> flowOrder = new()
    {
        Phase.Morning, Phase.Friend, Phase.Evening, Phase.HomeSearch,
        Phase.MotherReturn, Phase.Absence, Phase.Outside, Phase.Shed, Phase.End
    };

    [Tooltip("Awake時に最初のフェーズへ遷移する")]
    public bool autoStartOnAwake = true;

    [Tooltip("ブロードキャストするフラグ名の接頭辞（phase.XXX）")]
    public string phaseFlagPrefix = "phase";

    [Tooltip("DialogueController のフラグへもミラー書き込みする（存在する場合のみ）")]
    public bool mirrorToDialogueFlags = true;

    [Title("Unity Events")]
    public UnityEventPhase onPhaseEntered;
    public UnityEventPhase onPhaseExited;
    [Tooltip("フラグが変更された際の通知（key, value）")]
    public StringBoolEvent onFlagChanged;

    // ===== C# イベント =====
    public event Action<Phase, Phase> PhaseChanged;          // (old, @new)
    public event Action<string, bool> FlagChanged;           // (key, value)

    // ===== 実行時状態 =====
    [ShowInInspector, ReadOnly]
    public Phase CurrentPhase;

    [ShowInInspector, ReadOnly]
    public int CurrentIndex { get; private set; } = 0;

    // 内部フラグ管理（参照用）
    private readonly Dictionary<string, bool> _flags = new();

    // 以前のフェーズフラグ（false に戻す用）
    private string _lastPhaseFlagKey = null;

    // ===== ライフサイクル =====
    protected override void Awake()
    {
        base.Awake();
        DedupFlowOrder();
        if (autoStartOnAwake && flowOrder.Count > 0)
        {
            SwitchTo(flowOrder[0], invokeExitOfOld:false);
        }
    }

    // ===== パブリック API =====
    public void NextPhaseForUnityEvent() => NextPhase();

    [Button("→ Next Phase")]
    public bool NextPhase()
    {
        if (flowOrder.Count == 0) return false;
        int idx = IndexOf(CurrentPhase);
        int next = (idx < 0) ? 0 : idx + 1;
        if (next >= flowOrder.Count) return false;
        SwitchTo(flowOrder[next]);
        return true;
    }

    [Button("Jump To")]
    public void JumpTo(Phase target) => SwitchTo(target);

    public bool IsLastPhase => flowOrder.Count > 0 && IndexOf(CurrentPhase) == flowOrder.Count - 1;

    public void SetFlag(string key, bool value)
    {
        if (string.IsNullOrWhiteSpace(key)) return;

        _flags[key] = value;

        onFlagChanged?.Invoke(key, value);
        FlagChanged?.Invoke(key, value);

        if (mirrorToDialogueFlags && DialogueController.instance)
        {
            DialogueController.instance.SetFlag(key, value);
        }
    }

    public bool GetFlag(string key, bool defaultValue = false)
        => _flags.TryGetValue(key, out var v) ? v : defaultValue;

    public string GetCurrentPhaseFlagKey() => MakePhaseFlagKey(CurrentPhase);

    // ===== 内部実装 =====
    private void SwitchTo(Phase newPhase, bool invokeExitOfOld = true)
    {
        var old = CurrentPhase;
        bool wasInitialized = CurrentIndex >= 0;

        if (wasInitialized && invokeExitOfOld)
            onPhaseExited?.Invoke(old);

        CurrentPhase = newPhase;
        CurrentIndex = IndexOf(newPhase); // flowOrderに無い場合は -1 のまま

        // 1) フェーズフラグの更新
        if (!string.IsNullOrEmpty(_lastPhaseFlagKey))
            SetFlag(_lastPhaseFlagKey, false);

        var newKey = MakePhaseFlagKey(newPhase);
        SetFlag(newKey, true);
        _lastPhaseFlagKey = newKey;

        // 2) イベント発火
        onPhaseEntered?.Invoke(newPhase);
        PhaseChanged?.Invoke(old, newPhase);
    }

    private string MakePhaseFlagKey(Phase p)
        => string.IsNullOrWhiteSpace(phaseFlagPrefix)
            ? p.ToString()
            : $"{phaseFlagPrefix}.{p}";

    private int IndexOf(Phase p) => flowOrder.FindIndex(x => x == p);

    private void DedupFlowOrder()
    {
        if (flowOrder == null) flowOrder = new List<Phase>();
        var seen = new HashSet<Phase>();
        var list = new List<Phase>(flowOrder.Count);
        foreach (var p in flowOrder)
        {
            if (seen.Add(p)) list.Add(p);
        }
        flowOrder = list;
    }

#if UNITY_EDITOR
    [Button("Broadcast sample flags")]
    private void DebugBroadcastSample()
    {
        SetFlag("example.started", true);
        SetFlag("example.canTalkFriend", CurrentPhase == Phase.Friend);
    }
#endif

    // ===== ユーティリティ型 =====
    [Serializable]
    public class UnityEventPhase : UnityEngine.Events.UnityEvent<Phase> {}

    [Serializable]
    public class StringBoolEvent : UnityEngine.Events.UnityEvent<string, bool> {}
}

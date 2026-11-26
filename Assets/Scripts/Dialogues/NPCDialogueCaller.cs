using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;
using Cysharp.Threading.Tasks;
using UnityEngine.Events;
using UnityEngine.Localization;

public class NpcDialogueCaller : SerializedMonoBehaviour, IInteractable
{
    public enum StartMode { Single, RandomFromList }
    public enum GroupRefMode { ByKey, ByAsset }

    [Title("Group")]
    [EnumToggleButtons] public GroupRefMode groupRef = GroupRefMode.ByAsset;
    [ShowIf(nameof(groupRef), GroupRefMode.ByKey)] [LabelText("Group Key")] public string groupKey;
    [ShowIf(nameof(groupRef), GroupRefMode.ByAsset)] [LabelText("Group Asset")] public DialogueSequenceGroupAsset groupAsset;

    [Title("Start Mode")]
    [EnumToggleButtons] public StartMode mode = StartMode.Single;
    [ShowIf(nameof(mode), StartMode.Single)] [LabelText("Sequence ID")] public string singleSequenceId;
    [ShowIf(nameof(mode), StartMode.RandomFromList)] [LabelText("候補 Sequence IDs"), ListDrawerSettings(Expanded = true)]
    public List<string> candidateSequenceIds = new();

    [Title("Options")]
    [Tooltip("一度だけ起動して以降は無効にする")] public bool once = false;
    [Tooltip("このNPCで会話進行中は新たなTalkを無視（推奨）")] public bool blockWhileTalking = true;
    [Tooltip("話しかけたときにプレイヤーの方向を向くか。")] public bool LookAtPlayerY;

    // デフォルトキーのフィールドを追加
    [Title("Localization")]
    [Tooltip("（オプション）「調べる」など、デフォルト以外のキーを個別に指定する場合")]
    [SerializeField] private LocalizedString interactText;

    [Tooltip("↑の interactText が空の場合に使われる、デフォルトの翻訳キー（String Table内）")]
    [SerializeField] private string defaultInteractKey = "ui_interact_talk";

    [Title("Local Events (このCaller専用)")]
    public UnityEvent onDialogueStarted;
    public UnityEvent onDialogueClosed;   // 会話が閉じた直後にこのCallerだけで発火
    private bool consumed = false;
    private bool localBusy = false;

    // IInteractable
    public void Interact() => Talk().Forget();

    // GetDisplayText がフォールバックするように変更
    public string GetDisplayText()
    {
        // (優先) "interactText" が設定されていれば、そのキーを返す
        if (interactText != null && !interactText.IsEmpty)
        {
            return interactText.TableEntryReference.Key;
        }

        // (フォールバック) "interactText" が空なら、デフォルトのキーを返す
        return defaultInteractKey;
    }

    public bool ShouldHighlight() => (!blockWhileTalking || !localBusy) && (!once || !consumed);

    [Button("Talk()")]
    public void TalkButton() => Talk().Forget();
    [ShowIf("LookAtPlayerY")]
    [Required] [SerializeField] private NPCActions npcActions;
    public async UniTaskVoid Talk()
    {
        if (once && consumed) return;
        if (blockWhileTalking && localBusy) return;

        if (!TryResolveGroup(out string gKey))
        {
            Debug.LogWarning($"{name}: Group が未設定です。");
            return;
        }
        string seqId = ResolveSequenceId();
        if (string.IsNullOrWhiteSpace(seqId))
        {
            Debug.LogWarning($"{name}: 有効な sequenceId がありません。");
            return;
        }

        var ctrl = DialogueController.instance;
        if (!ctrl) { Debug.LogError("DialogueController.instance が見つかりません。"); return; }

        localBusy = true;
        try
        {
            onDialogueStarted?.Invoke();              // このCallerの開始イベント
            if(LookAtPlayerY)npcActions.LookAtPlayerY();
            await ctrl.StartDialogueAsync(gKey, seqId);  // （ここが戻ってきたら閉じた合図）
        }
        finally
        {
            onDialogueClosed?.Invoke();               // このCallerの終了イベント（常に自分だけ）
            if(LookAtPlayerY)npcActions.LookAtDefaultY();
            if (once) consumed = true;
            localBusy = false;
        }
    }

    public void TalkWith(string explicitGroupKey, string sequenceId)
        => TalkWithAsync(explicitGroupKey, sequenceId).Forget();

    private async UniTaskVoid TalkWithAsync(string explicitGroupKey, string sequenceId)
    {
        if (once && consumed) return;
        if (blockWhileTalking && localBusy) return;
        if (string.IsNullOrWhiteSpace(explicitGroupKey) || string.IsNullOrWhiteSpace(sequenceId))
        { Debug.LogWarning($"{name}: TalkWith に不正な引数。"); return; }

        var ctrl = DialogueController.instance;
        if (!ctrl) { Debug.LogError("DialogueController.instance が見つかりません。"); return; }

        localBusy = true;
        try
        {
            onDialogueStarted?.Invoke();
            await ctrl.StartDialogueAsync(explicitGroupKey, sequenceId);
        }
        finally
        {
            onDialogueClosed?.Invoke();
            if (once) consumed = true;
            localBusy = false;
        }
    }

    // helpers
    private bool TryResolveGroup(out string resolved)
    {
        if (groupRef == GroupRefMode.ByAsset)
        {
            if (groupAsset && !string.IsNullOrWhiteSpace(groupAsset.groupKey)) { resolved = groupAsset.groupKey; return true; }
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(groupKey)) { resolved = groupKey; return true; }
        }
        resolved = null; return false;
    }

    private string ResolveSequenceId()
    {
        switch (mode)
        {
            case StartMode.Single: return string.IsNullOrWhiteSpace(singleSequenceId) ? null : singleSequenceId;
            case StartMode.RandomFromList:
                if (candidateSequenceIds == null || candidateSequenceIds.Count == 0) return null;
                var pool = new List<string>();
                foreach (var s in candidateSequenceIds) if (!string.IsNullOrWhiteSpace(s)) pool.Add(s);
                if (pool.Count == 0) return null;
                int idx = Random.Range(0, pool.Count); return pool[idx];
        }
        return null;
    }

#if UNITY_EDITOR
    [Button("候補IDsをGroupAssetから反映"), ShowIf(nameof(groupRef), GroupRefMode.ByAsset)]
    private void FillCandidatesFromGroupAsset()
    {
        if (!groupAsset || groupAsset.sequences == null) return;
        candidateSequenceIds = new List<string>();
        foreach (var seq in groupAsset.sequences)
            if (seq && !string.IsNullOrWhiteSpace(seq.id))
                candidateSequenceIds.Add(seq.id);
        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif
}

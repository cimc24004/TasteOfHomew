using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

[CreateAssetMenu(menuName = "Dialogue/Dialogue Database")]
public class DialogueDatabase : SerializedScriptableObject
{
    // 手動でIDを振る。辞書はランタイムで内部生成
    [TableList(AlwaysExpanded = true)]
    public List<DialogueSequence> sequences = new();

    // 生成された索引（実行時）
    [NonSerialized] private Dictionary<string, DialogueSequence> index;
    [NonSerialized] private bool built;

    // 外部API
    public bool TryGet(string id, out DialogueSequence seq)
    {
        EnsureBuilt();
        return index.TryGetValue(id, out seq);
    }

    public IReadOnlyList<string> GetAllIds()
    {
        EnsureBuilt();
        return _ids;
    }

    // ====== 索引生成/検証 ======
    [Button("Rebuild & Validate Index")]
    public void RebuildAndValidate()
    {
        BuildIndex(logIssues: true);
    }

    void OnEnable()  { built = false; }   // 再ロード時に再構築させる
    void OnValidate(){ built = false; }   // 編集時の変更にも追従

    private List<string> _ids = new();

    private void EnsureBuilt()
    {
        if (!built) BuildIndex(logIssues: false);
    }

    private void BuildIndex(bool logIssues)
    {
        built = true;
        index = new Dictionary<string, DialogueSequence>(StringComparer.Ordinal);
        _ids.Clear();

        if (sequences == null) return;

        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < sequences.Count; i++)
        {
            var seq = sequences[i];
            if (seq == null) continue;

            string id = seq.id?.Trim() ?? "";
            if (string.IsNullOrEmpty(id))
            {
                if (logIssues) Debug.LogWarning($"[DialogueDatabase] 空IDのSequenceがあります（index:{i}）。idを手動で設定してね。", this);
                continue;
            }

            if (seen.Contains(id))
            {
                if (logIssues) Debug.LogError($"[DialogueDatabase] 重複ID '{id}' が見つかったため最初のものを使用。index:{i}", this);
                continue; // 最初のを優先
            }

            seen.Add(id);
            index[id] = seq;
            _ids.Add(id);
        }
    }
}

[Serializable]
public class DialogueSequence
{
    [LabelText("ID (手動)")]
    public string id;  // ★ 手動で割り当てる

    [TableList(AlwaysExpanded = true)]
    public List<DialogueStep> steps = new();
}

[Serializable]
public class DialogueStep
{
    [LabelText("Speaker")]
    public string speaker;

    [LabelText("Text"), TextArea(3, 8)]
    public string text;

    [FoldoutGroup("Flow"), LabelText("入力待ち")]
    public bool waitForInput = true;

    [FoldoutGroup("Flow"), LabelText("自動で次へ")]
    public bool autoNext = false;

    [FoldoutGroup("Flow"), ShowIf(nameof(autoNext))]
    public float autoNextDelay = 0.8f;

    [FoldoutGroup("Conditions"), LabelText("この行を表示する条件(AND)")]
    public List<FlagCondition> conditions = new();

    [FoldoutGroup("Choices"), TableList]
    public List<DialogueChoice> choices = new();
}

[Serializable]
public class DialogueChoice
{
    [TableColumnWidth(260)]
    public string text;

    [Tooltip("選ぶとジャンプする先の Dialogue ID（手動入力）")]
    public string gotoId;

    [TableColumnWidth(140)]
    public List<FlagCondition> conditions = new();
}

[Serializable]
public class FlagCondition
{
    public string key;
    public bool requiredValue = true;
}

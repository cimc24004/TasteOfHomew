using System;
using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
// Unity純正 Timeline を明示
using UTimelineAsset = UnityEngine.Timeline.TimelineAsset;

[CreateAssetMenu(fileName = "PTL_", menuName = "Timeline/Phase Timeline Asset")]
public class PhaseTimelineAsset : SerializedScriptableObject
{
    [LabelText("このアセットの対象フェーズ")]
    public GameFlowController.Phase phase;

    [LabelText("ノート"), TextArea(1, 3)]
    public string note;

    [Serializable]
    public class TimelineEntry
    {
        [HorizontalGroup("row", Width = 160)]
        [LabelText("ID"), GUIColor(0.85f, 0.95f, 1f)]
        public string id;

        [HorizontalGroup("row")]
        [LabelText("Timeline"), AssetsOnly, Required, InlineEditor(InlineEditorObjectFieldModes.Boxed)]
        public UTimelineAsset asset;

        [LabelText("メモ"), TextArea(1, 3)]
        public string memo;
    }

    [LabelText("Timelines")]
    [ListDrawerSettings(Expanded = true, DraggableItems = true, ShowIndexLabels = true, NumberOfItemsPerPage = 40)]
    public List<TimelineEntry> timelines = new();

    public UTimelineAsset FindById(string id)
    {
        if (string.IsNullOrEmpty(id) || timelines == null) return null;
        for (int i = 0; i < timelines.Count; i++)
        {
            var t = timelines[i];
            if (t != null && t.id == id) return t.asset;
        }
        return null;
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        // 重複IDを軽く警告
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var t in timelines)
        {
            if (t == null || string.IsNullOrEmpty(t.id)) continue;
            if (!seen.Add(t.id))
                Debug.LogWarning($"[PhaseTimelineAsset:{name}] Duplicate id '{t.id}'");
        }
    }
#endif
}

using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Dialogue/Sequence Group", fileName = "SeqGroup_New")]
public class DialogueSequenceGroupAsset : ScriptableObject
{
    [Tooltip("グループキー（例: 母_会話 / 友_イベント など）")]
    public string groupKey;
    public List<DialogueSequenceAsset> sequences = new List<DialogueSequenceAsset>();
}

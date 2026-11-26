using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;
[CreateAssetMenu(menuName = "Dialogue/Sequence", fileName = "Seq_New")]
public class DialogueSequenceAsset : SerializedScriptableObject
{
    [Tooltip("一意のシーケンスID")] public string id;
    [TextArea] public string memo;
    public List<DialogueStep> steps = new List<DialogueStep>();
    // Sequence-level Goto
    [Header("Sequence Goto (optional)")]
    public bool useGoto;

    [ShowIf(nameof(useGoto))]
    [LabelText("Goto (group:sequence or sequence)")]
    public string gotoId;
    #if UNITY_EDITOR
    private void OnValidate()
    {
        if (steps == null) return;
        for (int i = 0; i < steps.Count; i++)
        {
            if (steps[i] != null)
                steps[i].displayStepId = i + 1; // 1始まりで可視化
        }
    }
    #endif
}

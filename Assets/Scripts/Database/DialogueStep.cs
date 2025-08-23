using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

[System.Serializable]
public class FlagCondition
{
    [Tooltip("true/false を照会するキー名")] public string key;
    [Tooltip("この値に一致したときだけ通過")] public bool requiredValue = true;
}

[System.Serializable]
public class DialogueChoice
{
    [TextArea] public string text;
    [Tooltip("同グループ: \"id\" / 別グループ: \"group:id\"")]
    public string gotoId;
    [Tooltip("この選択肢を表示する条件")] public List<FlagCondition> conditions = new();
}

[System.Serializable]
public class DialogueStep
{
    // --- 表示用の自動StepId（エディタ表示のみ） ---
    [Sirenix.OdinInspector.ReadOnly, Sirenix.OdinInspector.LabelText("Step Id")]
    public int displayStepId;  // 1始まり・実行時に使わない表示用

    [Tooltip("話者名。空なら非表示")] public string speaker;
    [TextArea(2, 6)] public string text;

    [Tooltip("条件を満たすときだけこのステップを再生")]
    public List<FlagCondition> conditions = new();

    [Header("自動送り")]
    public bool autoNext = false;
    public float autoNextDelay = 0.25f;


    [Header("選択肢（空なら通常の次へ）")]
    public List<DialogueChoice> choices = new();

    [Header("このステップ突入時にフラグを変更（任意）")]
    public List<FlagEdit> flagEditsOnEnter = new();
        // DialogueStep に追記
    [Header("Step Goto (immediate)")]
    public bool useStepGoto;

    [ShowIf(nameof(useStepGoto))]
    [LabelText("Goto (group:sequence or sequence)　同グループならsequenceIdのみ ")]
    public string stepGotoId;

}

[System.Serializable]
public class FlagEdit
{
    public string key;
    public bool value;
}

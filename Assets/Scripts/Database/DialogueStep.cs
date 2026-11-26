using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;
using UnityEngine.Localization;

// ### DialogueStep ###
[System.Serializable]
public class DialogueStep
{
    [ReadOnly, LabelWidth(40)] public int displayStepId = 0;

    [Header("Conditions")]
    public List<FlagCondition> conditions = new();

    // 古いデータ（移行用）
    [Title("--- OLD DATA (for Migration) ---")]
    [Header("Content (Old)")]
    [Tooltip("話者名。空欄OK")]
    public string speaker;

    [Tooltip("本文。空欄OK")]
    [TextArea(3, 8)]
    public string text;

    // 新しいローカライズ用データ
    [Title("--- LOCALIZATION (NEW) ---")]
    [Header("Content (Localized)")]
    [Tooltip("話者名 (Localized)")]
    public LocalizedString localizedSpeaker;

    [Tooltip("本文 (Localized)")]
    [TextArea(3, 8)]
    public LocalizedString localizedText;

    [Header("Choices")]
    [Tooltip("このステップで表示する選択肢")]
    public List<DialogueChoice> choices = new();

    [Header("Flow Control")]
    public bool autoNext = false;
    [ShowIf(nameof(autoNext))] public float autoNextDelay = 1f;

    [Header("Step Goto (optional)")]
    public bool useStepGoto;
    [ShowIf(nameof(useStepGoto))]
    [LabelText("Goto (group:sequence:step or sequence:step or :step)")]
    public string stepGotoId;

    [Header("Flag Edits (on enter)")]
    public List<FlagEdit> flagEditsOnEnter = new();
}

// ### DialogueChoice ###
[System.Serializable]
public class DialogueChoice
{
    // 古いデータ（移行用）
    [Tooltip("選択肢のテキスト (Old)")]
    [TextArea(1, 2)]
    public string text;

    // 新しいローカライズ用データ
    [Tooltip("選択肢のテキスト (Localized)")]
    public LocalizedString localizedText;

    [Tooltip("ジャンプ先 (group:sequence or sequence)")]
    public string gotoId;

    [Tooltip("表示条件")]
    public List<FlagCondition> conditions = new();
}

// ### FlagCondition ###
[System.Serializable]
public class FlagCondition
{
    public string key;
    public bool requiredValue = true;
}

// ### FlagEdit ###
[System.Serializable]
public class FlagEdit
{
    public string key;
    public bool value = true;
}

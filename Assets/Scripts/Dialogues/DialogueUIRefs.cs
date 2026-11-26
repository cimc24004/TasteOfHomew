using TMPro;
using UnityEngine;
using UnityEngine.UI;

[System.Serializable]
public class DialogueUIRefs
{
    [Tooltip("スピーカー名を包むボックス")]
    public GameObject speakerBox;
    public TextMeshProUGUI speakerText;

    [Tooltip("本文を包むボックス")]
    public GameObject dialogueBox;
    public TextMeshProUGUI bodyText;

    [Tooltip("選択肢の親Transform")]
    public Transform choicesRoot;
    [Tooltip("選択肢ボタンのPrefab")]
    public Button choiceButtonPrefab;

    public DialogueUIRefs(GameObject speakerBox, TextMeshProUGUI speakerText, GameObject dialogueBox,
                             TextMeshProUGUI bodyText
                            , Transform choicesRoot, Button choiceButtonPrefab)
    {
        this.speakerBox = speakerBox;
        this.speakerText = speakerText;
        this.dialogueBox = dialogueBox;
        this.bodyText = bodyText;
        this.choicesRoot = choicesRoot;
        this.choiceButtonPrefab = choiceButtonPrefab;
    }
}

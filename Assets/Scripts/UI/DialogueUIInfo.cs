// DialogueUIRefs.cs
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[System.Serializable]
public class DefaultDialogueUIInfo : Singleton<DefaultDialogueUIInfo>
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
}
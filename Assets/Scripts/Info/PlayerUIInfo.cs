using TMPro;
using Unity.VisualScripting;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization.Components;

public class PlayerUIInfo : MonoBehaviour
{
    [SerializeField] public TextMeshProUGUI promptText;         // 例: 画面中央下に配置
    [SerializeField] public GameObject InteractBox;
    [SerializeField] public CanvasGroup InteractBoxCanvasGroup;
    [SerializeField] public GameObject InteractImage;
    [SerializeField] public GameObject crosshair;           // あればフェード表示
    [SerializeField] public StringTableCollection defaultInteractTable;           // あればフェード表示
    [SerializeField] public LocalizeStringEvent promptLocalizeEvent;           // あればフェード表示
    
}

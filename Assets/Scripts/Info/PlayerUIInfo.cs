using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Localization.Components;


public class PlayerUIInfo : MonoBehaviour
{
    [SerializeField] public TextMeshProUGUI promptText;         
    [SerializeField] public GameObject InteractBox;
    [SerializeField] public CanvasGroup InteractBoxCanvasGroup;
    [SerializeField] public GameObject InteractImage;
    [SerializeField] public GameObject crosshair;          
    
    // ▼▼▼ 【(1) 修正】 アセット参照(NG) から「テーブル名」(string) に変更 ▼▼▼
    [Tooltip("参照する String Table Collection の「名前」を入力 (例: Interaction)")]
    [SerializeField] public string defaultInteractTableName; 
    // ▲▲▲ 【(1) 修正ここまで】 ▲▲▲
    
    [SerializeField] public LocalizeStringEvent promptLocalizeEvent;      
}
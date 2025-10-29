using TMPro;
using Unity.VisualScripting;
using UnityEngine;

public class PlayerUIInfo : MonoBehaviour
{
    [SerializeField] public TextMeshProUGUI promptText;         // 例: 画面中央下に配置
    [SerializeField] public GameObject InteractBox;
    [SerializeField] public CanvasGroup InteractBoxCanvasGroup;
    [SerializeField] public GameObject InteractImage;
    [SerializeField] public GameObject crosshair;           // あればフェード表示
    
}

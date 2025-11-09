using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.Localization; // LocalizedString のために必要
using UnityEngine.Localization.Components; // LocalizeStringEvent のために必要
using UnityEngine.Localization.Tables;
using UnityEditor.Localization; // ★★★ StringTableCollection のためにこれが必要 ★★★

public class CrossHairController : Singleton<CrossHairController>
{
    [Title("Ray 設定")]
    [SerializeField, Min(0.01f)] private float maxDistance = 2.0f;
    [SerializeField] private LayerMask targetLayerMask = ~0;
    private Camera mainCamera;

    [Title("当てやすさ")]
    [SerializeField] private bool useSphereCast = false;
    [ShowIf(nameof(useSphereCast))] [SerializeField, Min(0f)] private float sphereRadius = 0.12f;

    [Title("UI")]
    [SerializeField] public TextMeshProUGUI promptText;
    [SerializeField] public GameObject InteractBox;
    [SerializeField] public CanvasGroup InteractBoxCanvasGroup;
    
    [Title("Localization (NEW)")]
    [Tooltip("インタラクト用テキスト「話す」「読む」などが入った String Table Collection アセット")]
    [SerializeField] private StringTableCollection defaultInteractTable; // ← ★★★ この型
    
    [Tooltip("promptText にアタッチされている Localize String Event")]
    [SerializeField] private LocalizeStringEvent promptLocalizeEvent;

    [Title("Animator")]
    private Animator crosshairAnimator;

    private bool isHighlight;
    private IInteractable currentInteractable;

    private PlayerInput playerInput;
    private InputAction interactAction;
    private GameController gameController;
    private PlayerUIInfo playerUIInfo;
    void Start()
    {
        if (!mainCamera) mainCamera = Camera.main;
        SetHighlight(false);

        playerInput = PlayerDataHub.instance.CurrentplayerInput;
        interactAction = playerInput.actions.FindAction("InteractAction");
        gameController = GameController.instance;
        playerUIInfo = gameController.playerUIInfo;
        crosshairAnimator = playerUIInfo.crosshair.GetComponent<Animator>();
        promptText = playerUIInfo.promptText;
        InteractBox = playerUIInfo.InteractBox;
        InteractBoxCanvasGroup = playerUIInfo.InteractBoxCanvasGroup;
        promptLocalizeEvent = playerUIInfo.promptLocalizeEvent;
        defaultInteractTable = playerUIInfo.defaultInteractTable;
        if (promptLocalizeEvent == null)
        {
            Debug.LogError("[CrossHairController] promptLocalizeEvent が設定されていません！翻訳できません。");
        }
        if (defaultInteractTable == null)
        {
            Debug.LogError("[CrossHairController] defaultInteractTable が設定されていません！翻訳できません。");
        }
    }

    void Update()
    {
        if (!mainCamera) return;

        Ray ray = mainCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        RaycastHit hit;
        bool hitAny = useSphereCast
            ? Physics.SphereCast(ray, sphereRadius, out hit, maxDistance, targetLayerMask)
            : Physics.Raycast(ray, out hit, maxDistance, targetLayerMask);

        if (hitAny && hit.collider)
        {
            var interactable = hit.collider.GetComponentInParent<IInteractable>();
            if (interactable != null)
            {
                SetHighlight(interactable.ShouldHighlight());

                // "ui_interact_talk" などの「キー」または「""」を取得
                string displayKey = interactable.GetDisplayText();

                // 翻訳を実行
                if (promptLocalizeEvent != null && defaultInteractTable != null)
                {
                    if (!string.IsNullOrEmpty(displayKey))
                    {
                        // (A) LocalizeEvent に「テーブル名」と「キー」をセット
                        // ※ .TableReference は (string) TableCollectionName を直接受け取れます
                        promptLocalizeEvent.StringReference.TableReference = defaultInteractTable.TableCollectionName;
                        promptLocalizeEvent.StringReference.TableEntryReference = displayKey;
                        
                        playerUIInfo.InteractBox.SetActive(true);
                    }
                    else
                    {
                        // (B) キーが空なら、InteractBoxを非表示
                        promptLocalizeEvent.StringReference.TableEntryReference = null; // キーをクリア
                        playerUIInfo.InteractBox.SetActive(false);
                    }
                }
                else if (promptText) // (フォールバック) LocalizeEvent が設定されてない場合
                {
                    promptText.text = displayKey; // 生のキーが表示されてしまう
                    playerUIInfo.InteractBox.SetActive(!string.IsNullOrEmpty(displayKey));
                }
                
                currentInteractable = interactable;

                if (interactAction != null && interactAction.triggered)
                {
                    if (interactable is DoorController door)
                    {
                        door.Interact(playerInput.gameObject);
                    }
                    else
                    {
                        interactable.Interact();
                    }
                }
                return;
            }
        }

        // 何もヒットしなかった場合
        SetHighlight(false);
        if (promptLocalizeEvent != null)
        {
             promptLocalizeEvent.StringReference.TableEntryReference = null; // キーをクリア
        }
        if (playerUIInfo.InteractBox.activeSelf) playerUIInfo.InteractBox.SetActive(false);
        currentInteractable = null;
    }

    void SetHighlight(bool on)
    {
        if (isHighlight == on) return;
        isHighlight = on;
        if (crosshairAnimator)
        {
            crosshairAnimator.SetBool("HighLight", isHighlight);
        }
    }
}
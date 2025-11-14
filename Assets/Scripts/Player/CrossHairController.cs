using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.Localization;
using UnityEngine.Localization.Tables;
using UnityEngine.Localization.Components;

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
    // ▼▼▼ 【(1) 修正】 アセット参照(NG) から「テーブル名」(string) に変更 ▼▼▼
    [Tooltip("「話す」「読む」など、翻訳が必要なキーが入ったテーブルの名前 (例: Interaction)")]
    [SerializeField] private string defaultInteractTableName;
    // ▲▲▲ 【(1) 修正ここまで】 ▲▲▲
    
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

        // ▼▼▼ 【(2) 修正】 PlayerUIInfo から string を読み込む ▼▼▼
        if (playerUIInfo != null)
        {
            // PlayerUIInfo の設定を優先する
            if (playerUIInfo.promptLocalizeEvent != null)
            {
                this.promptLocalizeEvent = playerUIInfo.promptLocalizeEvent;
            }
            if (!string.IsNullOrEmpty(playerUIInfo.defaultInteractTableName))
            {
                this.defaultInteractTableName = playerUIInfo.defaultInteractTableName;
            }
        }
        // ▲▲▲ 【(2) 修正ここまで】 ▲▲▲
        
        // (フォールバック)
        if (promptText != null && promptLocalizeEvent == null)
        {
            promptLocalizeEvent = promptText.GetComponent<LocalizeStringEvent>();
        }
        if (promptLocalizeEvent == null)
        {
            Debug.LogError("[CrossHairController] promptLocalizeEvent が設定されていません！翻訳できません。");
        }
        if (string.IsNullOrEmpty(defaultInteractTableName))
        {
            Debug.LogError("[CrossHairController] defaultInteractTableName が設定されていません！翻訳できません。");
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
                string displayString = interactable.GetDisplayText();

                // ▼▼▼ ★★★ (これが正しいロジック) ★★★ ▼▼▼
                
                // (A) 「翻訳が必要なクラス」かどうかを「型」で判定する
                bool needsLocalization = true;
                    // (↑ 今後、常に翻訳が必要なクラスが増えたら、ここに追加する)

                if (string.IsNullOrEmpty(displayString))
                {
                    // (1) 文字列が空なら、常に非表示
                    if (promptLocalizeEvent) promptLocalizeEvent.StringReference.TableEntryReference = null;
                    if (promptText) promptText.text = ""; 
                    playerUIInfo.InteractBox.SetActive(false);
                }
                else if (needsLocalization)
                {
                    // (2) 翻訳が必要なクラス (NpcDialogueCallerなど) の場合
                    // → "displayString" を「キー」として LocalizeEvent に渡す
                    if (promptLocalizeEvent != null && !string.IsNullOrEmpty(defaultInteractTableName))
                    {
                        promptLocalizeEvent.StringReference.TableReference = defaultInteractTableName;
                        promptLocalizeEvent.StringReference.TableEntryReference = displayString; // "ui_interact_talk"
                        playerUIInfo.InteractBox.SetActive(true);
                    }
                }
                else
                {
                    // (3) 翻訳が不要なクラス (InteractEventRelay, PlayerableCall) の場合
                    // → "displayString" を「生のテキスト」として .text に直接書き込む
                    
                    // まず LocalizeEvent を無効化する
                    if (promptLocalizeEvent) promptLocalizeEvent.StringReference.TableEntryReference = null; 
                    
                    if (promptText != null)
                    {
                        promptText.text = displayString; // "fawfwafaw" をそのまま表示
                    }
                    playerUIInfo.InteractBox.SetActive(true);
                }
                
                // ▲▲▲ ★★★ (ここまで) ★★★ ▲▲▲
                
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
        if (promptText) promptText.text = "";
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
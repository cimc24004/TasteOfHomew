// CrossHairController.cs

using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

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
                // クロスヘア強調
                SetHighlight(interactable.ShouldHighlight());

                // テキスト表示
                if (promptText)
                {
                    string display = interactable.GetDisplayText();
                    if (!string.IsNullOrEmpty(display))
                    {
                        promptText.text = display;
                        playerUIInfo.InteractBox.SetActive(true);
                    }
                    else
                    {
                        playerUIInfo.InteractBox.SetActive(false);
                    }
                }

                currentInteractable = interactable;

                // 入力チェック
                if (interactAction != null && interactAction.triggered)
                {
                    // ▼▼▼ ここから修正 ▼▼▼
                    
                    // 見つけた相手が DoorController かどうかをチェック
                    if (interactable is DoorController door)
                    {
                        // DoorController なら、引数付きの Interact(interactor) を呼ぶ
                        door.Interact(playerInput.gameObject);
                    }
                    else
                    {
                        // PlayerableCall や他のすべてのクラスはこちら
                        // 既存の Interact() を呼ぶ
                        interactable.Interact();
                    }
                    
                    // ▲▲▲ 修正ここまで ▲▲▲
                }
                return;
            }
        }

        // 何もヒットしなかった場合
        SetHighlight(false);
        if (promptText) playerUIInfo.InteractBox.SetActive(false);
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
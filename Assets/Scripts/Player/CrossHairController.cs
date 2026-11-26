using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Localization.Components;

/// <summary>
/// 画面中央のクロスヘア。
/// 毎フレーム、視線の先にインタラクト可能なもの（IInteractable）があるかを調べ、
/// あれば操作ガイド（「話す」「読む」など）を表示し、入力があればインタラクトを実行する。
/// </summary>
public class CrossHairController : Singleton<CrossHairController>
{
    private const string InteractActionName = "InteractAction";
    private const string HighlightAnimatorParameter = "HighLight";

    // ===== Inspector =====

    [Title("Ray 設定")]
    [SerializeField, Min(0.01f)] private float maxDistance = 2.0f;
    [SerializeField] private LayerMask targetLayerMask = ~0;

    [Title("当てやすさ")]
    [SerializeField] private bool useSphereCast = false;
    [ShowIf(nameof(useSphereCast))] [SerializeField, Min(0f)] private float sphereRadius = 0.12f;

    [Title("UI")]
    [SerializeField] public TextMeshProUGUI promptText;
    [SerializeField] public GameObject InteractBox;
    [SerializeField] public CanvasGroup InteractBoxCanvasGroup;

    [Title("Localization (NEW)")]
    [Tooltip("「話す」「読む」など、翻訳が必要なキーが入ったテーブルの名前 (例: Interaction)")]
    [SerializeField] private string defaultInteractTableName;

    [Tooltip("promptText にアタッチされている Localize String Event")]
    [SerializeField] private LocalizeStringEvent promptLocalizeEvent;

    // ===== 実行時の状態 =====

    private Camera mainCamera;
    private Animator crosshairAnimator;
    private PlayerInput playerInput;
    private InputAction interactAction;

    // ===== Unity ライフサイクル =====

    private void Start()
    {
        if (!mainCamera) mainCamera = Camera.main;
        SetHighlight(false);

        if (PlayerDataHub.instance != null)
        {
            playerInput = PlayerDataHub.instance.CurrentplayerInput;
            if (playerInput != null) interactAction = playerInput.actions.FindAction(InteractActionName);
        }

        if (GameController.instance != null)
        {
            ApplyPlayerUIInfo(GameController.instance.playerUIInfo);
        }

        if (promptText != null && promptLocalizeEvent == null)
        {
            promptLocalizeEvent = promptText.GetComponent<LocalizeStringEvent>();
        }

        if (promptLocalizeEvent == null)
        {
            Debug.LogError("[CrossHairController] promptLocalizeEvent が設定されていません！翻訳機能が動作しません。");
        }
        if (string.IsNullOrEmpty(defaultInteractTableName))
        {
            Debug.LogError("[CrossHairController] defaultInteractTableName が設定されていません！翻訳機能が動作しません。");
        }
    }

    private void Update()
    {
        if (!mainCamera) return;

        IInteractable interactable = FindInteractableUnderCrosshair();
        if (interactable == null)
        {
            SetHighlight(false);
            HidePrompt();
            return;
        }

        SetHighlight(interactable.ShouldHighlight());
        ShowPrompt(interactable.GetDisplayText());

        if (interactAction != null && interactAction.triggered)
        {
            Interact(interactable);
        }
    }

    // ===== 初期化 =====

    /// <summary>シーンの PlayerUIInfo が持つ UI 参照で、Inspector の設定を上書きする。</summary>
    private void ApplyPlayerUIInfo(PlayerUIInfo playerUIInfo)
    {
        if (playerUIInfo == null) return;

        if (playerUIInfo.crosshair != null)
        {
            crosshairAnimator = playerUIInfo.crosshair.GetComponent<Animator>();
        }

        promptText = playerUIInfo.promptText;
        InteractBox = playerUIInfo.InteractBox;
        InteractBoxCanvasGroup = playerUIInfo.InteractBoxCanvasGroup;

        if (playerUIInfo.promptLocalizeEvent != null)
        {
            promptLocalizeEvent = playerUIInfo.promptLocalizeEvent;
        }
        if (!string.IsNullOrEmpty(playerUIInfo.defaultInteractTableName))
        {
            defaultInteractTableName = playerUIInfo.defaultInteractTableName;
        }
    }

    // ===== 視線の先の判定とインタラクト =====

    /// <summary>画面中央から Ray を飛ばし、最初に当たったものが持つ IInteractable を返す（なければ null）。</summary>
    private IInteractable FindInteractableUnderCrosshair()
    {
        Ray ray = mainCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        RaycastHit hit;
        bool hitAny = useSphereCast
            ? Physics.SphereCast(ray, sphereRadius, out hit, maxDistance, targetLayerMask)
            : Physics.Raycast(ray, out hit, maxDistance, targetLayerMask);

        if (!hitAny || !hit.collider) return null;

        return hit.collider.GetComponentInParent<IInteractable>();
    }

    private void Interact(IInteractable interactable)
    {
        // ドアだけは「誰が開けたか」が必要なので、プレイヤーを渡す
        if (interactable is DoorController door)
        {
            if (playerInput != null) door.Interact(playerInput.gameObject);
        }
        else
        {
            interactable.Interact();
        }
    }

    // ===== 操作ガイドの表示 =====

    /// <summary>操作ガイドを表示する。displayKey はローカライズテーブルのキーとして扱い、翻訳して表示する。</summary>
    private void ShowPrompt(string displayKey)
    {
        if (string.IsNullOrEmpty(displayKey))
        {
            HidePrompt();
            return;
        }

        if (promptLocalizeEvent != null && !string.IsNullOrEmpty(defaultInteractTableName))
        {
            promptLocalizeEvent.StringReference.TableReference = defaultInteractTableName;
            promptLocalizeEvent.StringReference.TableEntryReference = displayKey;

            if (InteractBoxCanvasGroup) InteractBoxCanvasGroup.alpha = 1f;
        }
    }

    private void HidePrompt()
    {
        if (promptLocalizeEvent != null)
        {
            promptLocalizeEvent.StringReference.TableEntryReference = null;
        }
        if (promptText) promptText.text = "";

        if (InteractBoxCanvasGroup != null && InteractBoxCanvasGroup.alpha > 0f)
        {
            InteractBoxCanvasGroup.alpha = 0f;
        }
    }

    private void SetHighlight(bool on)
    {
        if (crosshairAnimator)
        {
            crosshairAnimator.SetBool(HighlightAnimatorParameter, on);
        }
    }
}

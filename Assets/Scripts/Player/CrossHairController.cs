using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem; // PlayerInput用に必要
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
    [Tooltip("「話す」「読む」など、翻訳が必要なキーが入ったテーブルの名前 (例: Interaction)")]
    [SerializeField] private string defaultInteractTableName;

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

        // シングルトンや他のクラスへの参照取得
        if (PlayerDataHub.instance != null)
        {
            playerInput = PlayerDataHub.instance.CurrentplayerInput;
            if (playerInput != null) interactAction = playerInput.actions.FindAction("InteractAction");
        }

        if (GameController.instance != null)
        {
            gameController = GameController.instance;
            playerUIInfo = gameController.playerUIInfo;

            if (playerUIInfo != null)
            {
                if (playerUIInfo.crosshair != null)
                    crosshairAnimator = playerUIInfo.crosshair.GetComponent<Animator>();

                promptText = playerUIInfo.promptText;
                InteractBox = playerUIInfo.InteractBox;
                InteractBoxCanvasGroup = playerUIInfo.InteractBoxCanvasGroup;

                // PlayerUIInfo の設定を優先して上書き
                if (playerUIInfo.promptLocalizeEvent != null)
                {
                    this.promptLocalizeEvent = playerUIInfo.promptLocalizeEvent;
                }
                if (!string.IsNullOrEmpty(playerUIInfo.defaultInteractTableName))
                {
                    this.defaultInteractTableName = playerUIInfo.defaultInteractTableName;
                }
            }
        }

        // (フォールバック) コンポーネント取得
        if (promptText != null && promptLocalizeEvent == null)
        {
            promptLocalizeEvent = promptText.GetComponent<LocalizeStringEvent>();
        }

        // エラーチェック
        if (promptLocalizeEvent == null)
        {
            Debug.LogError("[CrossHairController] promptLocalizeEvent が設定されていません！翻訳機能が動作しません。");
        }
        if (string.IsNullOrEmpty(defaultInteractTableName))
        {
            Debug.LogError("[CrossHairController] defaultInteractTableName が設定されていません！翻訳機能が動作しません。");
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

                // 型判定ロジック

                // (A) 基本は「翻訳が必要(true)」とする
                bool needsLocalization = true;

                // もし「翻訳不要なクラス（生のテキストを表示するクラス）」なら false にする
                // ※ ここに実際のクラス名を入れてください。例としてコメントにあった名前を使用しています。
                // if (interactable is InteractEventRelay || interactable is PlayerableCall)
                // {
                //     needsLocalization = false;
                // }

                // ※もし逆のロジック（特定のクラスだけ翻訳する）が良い場合は以下のように書き換えてください
                /*
                bool needsLocalization = false;
                if (interactable is NpcDialogueCaller) needsLocalization = true;
                */

                // ---------------------------------------------------------

                if (string.IsNullOrEmpty(displayString))
                {
                    // (1) 文字列が空なら、常に非表示
                    if (promptLocalizeEvent) promptLocalizeEvent.StringReference.TableEntryReference = null;
                    if (promptText) promptText.text = "";
                    if (InteractBoxCanvasGroup) InteractBoxCanvasGroup.alpha = 0.0f;
                }
                else if (needsLocalization)
                {
                    // (2) 翻訳が必要なクラスの場合 (キーとして扱う)
                    if (promptLocalizeEvent != null && !string.IsNullOrEmpty(defaultInteractTableName))
                    {
                        // テーブル名とキーを設定して、翻訳システムに任せる
                        promptLocalizeEvent.StringReference.TableReference = defaultInteractTableName;
                        promptLocalizeEvent.StringReference.TableEntryReference = displayString;

                        if (InteractBoxCanvasGroup) InteractBoxCanvasGroup.alpha = 1f;
                    }
                }
                else
                {
                    // (3) 翻訳が不要なクラスの場合 (生のテキストとして扱う)

                    // 重要: LocalizeEventが生きていると上書きされるため、参照を切る
                    if (promptLocalizeEvent) promptLocalizeEvent.StringReference.TableEntryReference = null;

                    if (promptText != null)
                    {
                        promptText.text = displayString; // displayString をそのまま表示
                    }
                    if (InteractBoxCanvasGroup) InteractBoxCanvasGroup.alpha = 1f;
                }

                currentInteractable = interactable;

                if (interactAction != null && interactAction.triggered)
                {
                    // DoorController など特定の型に対する処理があればここに
                    if (interactable is DoorController door)
                    {
                        if(playerInput != null) door.Interact(playerInput.gameObject);
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

        // 表示を消す処理
        if (promptLocalizeEvent != null)
        {
             promptLocalizeEvent.StringReference.TableEntryReference = null; // キーをクリア
        }
        if (promptText) promptText.text = "";

        // CanvasGroupで消す
        if (InteractBoxCanvasGroup != null && InteractBoxCanvasGroup.alpha > 0f)
        {
            InteractBoxCanvasGroup.alpha = 0f;
        }

        currentInteractable = null;
    }

    void SetHighlight(bool on)
    {
        // ハイライト状態が変わった時だけ叩くガードを入れても良いが、Animatorの仕様による
        // if (isHighlight == on) return;

        isHighlight = on;
        if (crosshairAnimator)
        {
            crosshairAnimator.SetBool("HighLight", isHighlight);
        }
    }
}

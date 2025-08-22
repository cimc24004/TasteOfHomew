using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [Header("移動")]
    public float speed = 5f;
    public float gravity = -9.81f;

    [Header("Cinemachine3 ターゲット")]
    [Tooltip("CinemachineのFollow/LookAtに設定しているターゲット(子)")]
    public Transform cameraRoot;

    [Header("しゃがみ(カメラのみ)")]
    [Tooltip("立ち時の cameraRoot の localY。0ならStart時に自動取得")]
    public float cameraStandLocalY = 0f;
    [Tooltip("しゃがみ時の cameraRoot の localY。0なら自動計算(standY - crouchDrop)")]
    public float cameraCrouchLocalY = 0f;
    [Tooltip("立ち→しゃがみの落差(自動計算用)")]
    public float crouchDrop = 0.5f;
    [Tooltip("カメラY補間速度")]
    public float cameraLerpSpeed = 12f;
    [Tooltip("トグル式(ON) / ホールド式(OFF)")]
    public bool crouchToggle = true;

    [Header("オプション: コリジョンも低くする? (デフォルトOFF)")]
    public bool applyControllerCrouch = false;
    public float standHeight = 1.8f;
    public float crouchHeight = 1.2f;
    public float crouchSpeedMultiplier = 0.6f;
    public LayerMask ceilingMask = ~0;

    [Header("Input System (Action名)")]
    public string moveActionName = "Move";
    public string crouchActionName = "Crouch";

    private CharacterController controller;
    private Vector3 velocity;
    private Camera mainCamera;

    private PlayerInput playerInput;
    private InputAction moveAction;
    private InputAction crouchAction;

    // 状態
    private bool isCrouching = false;
    private float targetCameraLocalY;
    private float moveSpeedScale => (isCrouching && applyControllerCrouch) ? crouchSpeedMultiplier : 1f;

    // CC初期
    private float defaultHeight;
    private Vector3 defaultCenter;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        mainCamera = Camera.main;

        // PlayerInputの取得（PlayerStatus経由があるならそっちでもOK）
        playerInput = GetComponent<PlayerInput>();
        if (!playerInput && PlayerStatus.instance != null)
            playerInput = PlayerStatus.instance.playerInput;

        moveAction   = playerInput.currentActionMap.FindAction(string.IsNullOrEmpty(moveActionName)   ? "Move"   : moveActionName);
        crouchAction = playerInput.currentActionMap.FindAction(string.IsNullOrEmpty(crouchActionName) ? "Crouch" : crouchActionName);

        if (!cameraRoot)
            Debug.LogWarning("cameraRoot 未設定。Cinemachineのターゲット(子)を割り当ててね。");

        // カメラYの初期化
        if (cameraRoot)
        {
            var lp = cameraRoot.localPosition;
            if (Mathf.Approximately(cameraStandLocalY, 0f)) cameraStandLocalY = lp.y;
            if (Mathf.Approximately(cameraCrouchLocalY, 0f)) cameraCrouchLocalY = cameraStandLocalY - Mathf.Abs(crouchDrop);
            targetCameraLocalY = cameraStandLocalY;
            cameraRoot.localPosition = new Vector3(lp.x, targetCameraLocalY, lp.z);
        }

        // CC初期値
        defaultHeight = controller.height;
        defaultCenter = controller.center;
        if (standHeight <= 0f) standHeight = defaultHeight;
        if (crouchHeight <= 0f) crouchHeight = Mathf.Max(0.8f, standHeight * 0.65f);

        // 立ち姿勢適用（ただし applyControllerCrouch がONのときのみCC変更）
        if (applyControllerCrouch) ApplyControllerStand();
    }

    void Update()
    {
        // ===== 移動 =====
        Vector2 input = moveAction != null ? moveAction.ReadValue<Vector2>() : Vector2.zero;

        Vector3 basisRight = mainCamera ? mainCamera.transform.right : transform.right;
        Vector3 basisFwd   = mainCamera ? mainCamera.transform.forward : transform.forward;
        basisRight.y = 0f; basisFwd.y = 0f;
        basisRight.Normalize(); basisFwd.Normalize();

        Vector3 move = (basisRight * input.x + basisFwd * input.y).normalized;
        controller.Move(move * speed * moveSpeedScale * Time.deltaTime);

        // ===== 重力 =====
        if (controller.isGrounded && velocity.y < 0) velocity.y = -2f;
        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);

        // ===== しゃがみ入力 =====
        HandleCrouchInput();

        // ===== カメラLerp（cameraRootだけ動かす）=====
        if (cameraRoot)
        {
            var lp = cameraRoot.localPosition;
            float newY = Mathf.Lerp(lp.y, targetCameraLocalY, Time.deltaTime * cameraLerpSpeed);
            cameraRoot.localPosition = new Vector3(lp.x, newY, lp.z);
        }
    }

    void HandleCrouchInput()
    {
        if (crouchAction == null) return;

        if (crouchToggle)
        {
            if (crouchAction.triggered)
            {
                if (isCrouching)
                {
                    // 立てるか判定（CC変更しないなら常にOK）
                    if (!applyControllerCrouch || CanStandUp())
                        SetCrouch(false);
                }
                else
                {
                    SetCrouch(true);
                }
            }
        }
        else
        {
            bool pressed = crouchAction.IsPressed();
            if (pressed)
            {
                if (!isCrouching) SetCrouch(true);
            }
            else
            {
                if (isCrouching && (!applyControllerCrouch || CanStandUp()))
                    SetCrouch(false);
            }
        }
    }

    void SetCrouch(bool crouch)
    {
        if (isCrouching == crouch) return;
        isCrouching = crouch;

        // cameraRootだけ上下させる（Cinemachineはターゲット追従なのでOK）
        targetCameraLocalY = isCrouching ? cameraCrouchLocalY : cameraStandLocalY;

        // 必要ならコリジョンも変更
        if (applyControllerCrouch)
        {
            if (isCrouching) ApplyControllerCrouch();
            else             ApplyControllerStand();
        }
    }

    // --- ここから下は「CCも低くする」オプション用 ---
    void ApplyControllerStand()
    {
        controller.height = standHeight;
        controller.center = new Vector3(defaultCenter.x, standHeight * 0.5f, defaultCenter.z);
    }

    void ApplyControllerCrouch()
    {
        controller.height = crouchHeight;
        controller.center = new Vector3(defaultCenter.x, crouchHeight * 0.5f, defaultCenter.z);
    }

    bool CanStandUp()
    {
        float radius = controller.radius * 0.98f;
        Vector3 p1 = transform.position + Vector3.up * radius;
        Vector3 p2 = transform.position + Vector3.up * (standHeight - radius);
        bool blocked = Physics.CheckCapsule(p1, p2, radius, ceilingMask, QueryTriggerInteraction.Ignore);
        return !blocked;
    }
}

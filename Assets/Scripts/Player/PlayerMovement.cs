using UnityEngine;
using UnityEngine.InputSystem;
using Sirenix.OdinInspector;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : SerializedMonoBehaviour
{
    [Header("移動")]
    public float speed = 5f;
    public float runSpeed = 8f;
    public float crouchSpeed = 2f;
    public float gravity = -9.81f;

    [Header("Cinemachine3 ターゲット")]
    [Tooltip("CinemachineのFollow/LookAtに設定しているターゲット(子)")]
    public Transform cameraRoot;

    [Header("頭のTransform (任意)")]
    [Tooltip("モデルのHeadボーンなど。cameraRootと同じように上下移動する")]
    public Transform head;

    [Header("しゃがみ(カメラ&Head)")]
    public float cameraStandLocalY = 0f;
    public float cameraCrouchLocalY = 0f;
    public float crouchDrop = 0.5f;
    public float cameraLerpSpeed = 12f;
    public bool crouchToggle = true;

    [Header("オプション: コリジョンも低くする?")]
    public bool applyControllerCrouch = false;
    public float standHeight = 1.8f;
    public float crouchHeight = 1.2f;
    public LayerMask ceilingMask = ~0;

    [Header("Input System (Action名)")]
    public string moveActionName = "Move";
    public string sprintActionName = "Sprint";
    public string crouchActionName = "Crouch";

    [Header("参照コンポーネント")]
    [SerializeField] private FootStepController playerFootStepController;
    [SerializeField] private GroundCheck groundCheck;

    private CharacterController controller;
    private Vector3 velocity;
    private Camera mainCamera;

    private PlayerInput playerInput;
    private InputAction moveAction;
    private InputAction sprintAction;
    private InputAction crouchAction;

    // 状態
    private bool isCrouching = false;
    private bool isRunning = false;
    private float targetCameraLocalY;
    private float targetHeadLocalY;

    private float defaultHeight;
    private Vector3 defaultCenter;
    private float headStandLocalY;
    private float headCrouchLocalY;
    private PlayerStatus playerStatus;
    void OnDisable()
    {
        playerFootStepController.CurrentState = MovementState.Idle;
        playerStatus.crossHairController.enabled = false;
    }
    void OnEnable()
    {
        if (playerStatus != null)
        {
            playerStatus.crossHairController.enabled = true;
        }
    }
    void Start()
    {
        playerStatus = PlayerStatus.instance;
        controller = GetComponent<CharacterController>();
        mainCamera = Camera.main;

        playerInput = GetComponent<PlayerInput>();
        if (!playerInput && PlayerStatus.instance != null)
            playerInput = PlayerStatus.instance.playerInput;

        moveAction   = playerInput.currentActionMap.FindAction(string.IsNullOrEmpty(moveActionName) ? "Move" : moveActionName);
        sprintAction = playerInput.currentActionMap.FindAction(string.IsNullOrEmpty(sprintActionName) ? "Sprint" : sprintActionName);
        crouchAction = playerInput.currentActionMap.FindAction(string.IsNullOrEmpty(crouchActionName) ? "Crouch" : crouchActionName);

        if (cameraRoot)
        {
            var lp = cameraRoot.localPosition;
            if (Mathf.Approximately(cameraStandLocalY, 0f)) cameraStandLocalY = lp.y;
            if (Mathf.Approximately(cameraCrouchLocalY, 0f)) cameraCrouchLocalY = cameraStandLocalY - Mathf.Abs(crouchDrop);
            targetCameraLocalY = cameraStandLocalY;
            cameraRoot.localPosition = new Vector3(lp.x, targetCameraLocalY, lp.z);
        }

        if (head)
        {
            var lp = head.localPosition;
            headStandLocalY = lp.y;
            headCrouchLocalY = headStandLocalY - Mathf.Abs(crouchDrop);
            targetHeadLocalY = headStandLocalY;
            head.localPosition = new Vector3(lp.x, targetHeadLocalY, lp.z);
        }

        defaultHeight = controller.height;
        defaultCenter = controller.center;
        if (standHeight <= 0f) standHeight = defaultHeight;
        if (crouchHeight <= 0f) crouchHeight = Mathf.Max(0.8f, standHeight * 0.65f);

        if (applyControllerCrouch) ApplyControllerStand();
    }

    void Update()
    {
        // ===== 移動 =====
        isRunning = sprintAction != null ? sprintAction.IsPressed() : false;
        Vector2 input = moveAction != null ? moveAction.ReadValue<Vector2>() : Vector2.zero;

        Vector3 basisRight = mainCamera ? mainCamera.transform.right : transform.right;
        Vector3 basisFwd   = mainCamera ? mainCamera.transform.forward : transform.forward;
        basisRight.y = 0f; basisFwd.y = 0f;
        basisRight.Normalize(); basisFwd.Normalize();

        float moveSpd;
        if (isCrouching)
        {
            moveSpd = crouchSpeed;
        }
        else if (isRunning)
        {
            moveSpd = runSpeed;
        }
        else
        {
            moveSpd = speed;
        }

        Vector3 move = (basisRight * input.x + basisFwd * input.y).normalized;
        controller.Move(move * moveSpd * Time.deltaTime);

        // ===== 重力 =====
        if (groundCheck.IsGrounded && velocity.y < 0)
        {
            velocity.y = -2f;
        }
        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);

        // ===== しゃがみ入力 =====
        HandleCrouchInput();

        // ===== カメラ & Head Lerp =====
        if (cameraRoot)
        {
            var lp = cameraRoot.localPosition;
            float newY = Mathf.Lerp(lp.y, targetCameraLocalY, Time.deltaTime * cameraLerpSpeed);
            cameraRoot.localPosition = new Vector3(lp.x, newY, lp.z);
        }
        if (head)
        {
            var lp = head.localPosition;
            float newY = Mathf.Lerp(lp.y, targetHeadLocalY, Time.deltaTime * cameraLerpSpeed);
            head.localPosition = new Vector3(lp.x, newY, lp.z);
        }
        
        // ===== 足音コントローラーの状態を更新 =====
        UpdateFootstepState();
    }
    
    void UpdateFootstepState()
    {
        if (playerFootStepController == null || groundCheck == null) return;

        bool isGrounded = groundCheck.IsGrounded;
        bool isMoving = moveAction.ReadValue<Vector2>().magnitude > 0;
        
        if (isGrounded)
        {
            if (isCrouching && isMoving)
            {
                playerFootStepController.CurrentState = MovementState.Crouching;
            }
            else if (isMoving && isRunning)
            {
                playerFootStepController.CurrentState = MovementState.Running;
            }
            else if (isMoving)
            {
                playerFootStepController.CurrentState = MovementState.Walking;
            }
            else
            {
                playerFootStepController.CurrentState = MovementState.Idle;
            }
        }
        else
        {
            playerFootStepController.CurrentState = MovementState.Idle;
        }
    }

    void HandleCrouchInput()
    {
        if (crouchAction == null) return;

        if (isCrouching && isRunning)
        {
            if (!applyControllerCrouch || CanStandUp())
            {
                SetCrouch(false);
            }
            return;
        }
        
        if (crouchToggle)
        {
            if (crouchAction.triggered)
            {
                if (isCrouching)
                {
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

        targetCameraLocalY = isCrouching ? cameraCrouchLocalY : cameraStandLocalY;
        if (head) targetHeadLocalY = isCrouching ? headCrouchLocalY : headStandLocalY;

        if (applyControllerCrouch)
        {
            if (isCrouching) ApplyControllerCrouch();
            else ApplyControllerStand();
        }
    }

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
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// プレイヤーの移動（歩き / 走り / しゃがみ）と重力を処理する。
///
/// ・カメラの向きを基準に移動する
/// ・走れるのは屋外にいて、しゃがんでいないときだけ
/// ・しゃがむとカメラと頭の位置が下がる（オプションで当たり判定も低くする）
/// ・移動状態を FootStepController に伝える（足音と、ステルス中の見張りの聴覚に使われる）
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : SerializedMonoBehaviour
{
    /// <summary>接地中に下向きへかけ続ける速度。地面から浮かないよう、少しだけ押し付ける。</summary>
    private const float GroundedStickVelocity = -2f;

    // ===== Inspector =====

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
    [SerializeField] public GroundCheck groundCheck;

    // ===== 実行時の状態 =====

    private CharacterController controller;
    private Camera mainCamera;
    private PlayerStatus playerStatus;

    private PlayerInput playerInput;
    private InputAction moveAction;
    private InputAction sprintAction;
    private InputAction crouchAction;

    private Vector3 velocity; // 重力による落下速度（y だけ使う）
    private bool isCrouching = false;
    private bool isRunning = false;

    // しゃがみ/立ちで切り替わる、カメラと頭の高さ（ローカル Y）
    private float targetCameraLocalY;
    private float targetHeadLocalY;
    private float headStandLocalY;
    private float headCrouchLocalY;

    private Vector3 defaultCenter;

    // ===== Unity ライフサイクル =====

    private void OnEnable()
    {
        if (playerStatus == null) playerStatus = PlayerStatus.instance;
        if (playerStatus == null) return;

        playerStatus.crossHairController.enabled = true;
    }

    /// <summary>移動を止めている間（会話中など）は、足音とクロスヘアも止める。</summary>
    private void OnDisable()
    {
        if (playerStatus == null) playerStatus = PlayerStatus.instance;
        if (playerStatus == null) return;

        playerFootStepController.CurrentState = MovementState.Idle;

        var crossHair = playerStatus.crossHairController;
        crossHair.enabled = false;
        if (crossHair.InteractBoxCanvasGroup != null)
        {
            crossHair.InteractBoxCanvasGroup.alpha = 0;
        }
    }

    private void Start()
    {
        playerStatus = PlayerStatus.instance;
        controller = GetComponent<CharacterController>();
        mainCamera = Camera.main;

        playerInput = PlayerDataHub.instance.CurrentplayerInput;
        moveAction = FindAction(moveActionName, "Move");
        sprintAction = FindAction(sprintActionName, "Sprint");
        crouchAction = FindAction(crouchActionName, "Crouch");
        if (moveAction == null) Debug.LogError("moveActionがありません");

        InitializeCameraHeights();
        InitializeHeadHeights();

        defaultCenter = controller.center;
        if (standHeight <= 0f) standHeight = controller.height;
        if (crouchHeight <= 0f) crouchHeight = Mathf.Max(0.8f, standHeight * 0.65f);

        if (applyControllerCrouch) ApplyControllerStand();
    }

    private void Update()
    {
        UpdateRunState();
        Move();
        ApplyGravity();
        HandleCrouchInput();
        UpdateCameraAndHeadHeight();
        UpdateFootstepState();
    }

    // ===== 初期化 =====

    private InputAction FindAction(string actionName, string defaultName)
    {
        return playerInput.actions.FindAction(string.IsNullOrEmpty(actionName) ? defaultName : actionName);
    }

    /// <summary>カメラの立ち/しゃがみの高さを決める。Inspector で 0 のままの値は、現在の位置から自動で決める。</summary>
    private void InitializeCameraHeights()
    {
        if (!cameraRoot) return;

        if (Mathf.Approximately(cameraStandLocalY, 0f)) cameraStandLocalY = cameraRoot.localPosition.y;
        if (Mathf.Approximately(cameraCrouchLocalY, 0f)) cameraCrouchLocalY = cameraStandLocalY - Mathf.Abs(crouchDrop);

        targetCameraLocalY = cameraStandLocalY;
        SetLocalY(cameraRoot, targetCameraLocalY);
    }

    private void InitializeHeadHeights()
    {
        if (!head) return;

        headStandLocalY = head.localPosition.y;
        headCrouchLocalY = headStandLocalY - Mathf.Abs(crouchDrop);

        targetHeadLocalY = headStandLocalY;
        SetLocalY(head, targetHeadLocalY);
    }

    // ===== 毎フレームの処理 =====

    /// <summary>走れるのは「屋外にいる」かつ「しゃがんでいない」ときだけ。</summary>
    private void UpdateRunState()
    {
        bool canRun = PlayerLocation.instance != null && PlayerLocation.instance.currentLocation == PlayerLocation.Location.Outside;
        isRunning = canRun && !isCrouching && (sprintAction != null && sprintAction.IsPressed());
    }

    /// <summary>入力方向へ移動する。前後左右はカメラの向き（水平成分のみ）を基準にする。</summary>
    private void Move()
    {
        Vector2 input = moveAction != null ? moveAction.ReadValue<Vector2>() : Vector2.zero;

        Vector3 right = mainCamera ? mainCamera.transform.right : transform.right;
        Vector3 forward = mainCamera ? mainCamera.transform.forward : transform.forward;
        right.y = 0f;
        forward.y = 0f;
        right.Normalize();
        forward.Normalize();

        // 斜め入力で速くならないよう、長さを 1 までに制限する
        Vector3 direction = Vector3.ClampMagnitude(right * input.x + forward * input.y, 1.0f);
        controller.Move(direction * GetMoveSpeed() * Time.deltaTime);
    }

    private float GetMoveSpeed()
    {
        if (isCrouching) return crouchSpeed;
        if (isRunning) return runSpeed;
        return speed;
    }

    private void ApplyGravity()
    {
        if (groundCheck.IsGrounded && velocity.y < 0)
        {
            velocity.y = GroundedStickVelocity;
        }

        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }

    private void UpdateCameraAndHeadHeight()
    {
        if (cameraRoot) LerpLocalY(cameraRoot, targetCameraLocalY);
        if (head) LerpLocalY(head, targetHeadLocalY);
    }

    private void UpdateFootstepState()
    {
        if (playerFootStepController == null || groundCheck == null) return;

        bool isGrounded = groundCheck.IsGrounded;
        bool isMoving = moveAction.ReadValue<Vector2>().magnitude > 0;
        playerFootStepController.CurrentState = GetMovementState(isGrounded, isMoving);
    }

    private MovementState GetMovementState(bool isGrounded, bool isMoving)
    {
        if (!isGrounded || !isMoving) return MovementState.Idle;
        if (isCrouching) return MovementState.Crouching;
        if (isRunning) return MovementState.Running;
        return MovementState.Walking;
    }

    // ===== しゃがみ =====

    private void HandleCrouchInput()
    {
        if (crouchAction == null) return;
        if (isRunning) return; // 走っている間はしゃがめない

        bool wantsCrouch;
        if (crouchToggle)
        {
            // トグル式: 押すたびに切り替え
            if (!crouchAction.triggered) return;
            wantsCrouch = !isCrouching;
        }
        else
        {
            // ホールド式: 押している間だけしゃがむ
            wantsCrouch = crouchAction.IsPressed();
        }

        if (wantsCrouch == isCrouching) return;

        if (wantsCrouch)
        {
            SetCrouch(true);
        }
        else if (!applyControllerCrouch || CanStandUp())
        {
            // 当たり判定も低くしている場合は、頭上に空間があるときだけ立てる
            SetCrouch(false);
        }
    }

    private void SetCrouch(bool crouch)
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

    private void ApplyControllerStand()
    {
        SetControllerHeight(standHeight);
    }

    private void ApplyControllerCrouch()
    {
        SetControllerHeight(crouchHeight);
    }

    /// <summary>当たり判定の高さを変える。足元の位置が変わらないよう、中心も合わせて動かす。</summary>
    private void SetControllerHeight(float height)
    {
        controller.height = height;
        controller.center = new Vector3(defaultCenter.x, height * 0.5f, defaultCenter.z);
    }

    /// <summary>立ち上がったときの当たり判定が、天井などにぶつからないか。</summary>
    private bool CanStandUp()
    {
        float radius = controller.radius * 0.98f;
        Vector3 bottom = transform.position + Vector3.up * radius;
        Vector3 top = transform.position + Vector3.up * (standHeight - radius);
        bool blocked = Physics.CheckCapsule(bottom, top, radius, ceilingMask, QueryTriggerInteraction.Ignore);
        return !blocked;
    }

    // ===== 小さなヘルパー =====

    private static void SetLocalY(Transform target, float y)
    {
        Vector3 localPosition = target.localPosition;
        target.localPosition = new Vector3(localPosition.x, y, localPosition.z);
    }

    private void LerpLocalY(Transform target, float targetY)
    {
        float newY = Mathf.Lerp(target.localPosition.y, targetY, Time.deltaTime * cameraLerpSpeed);
        SetLocalY(target, newY);
    }
}

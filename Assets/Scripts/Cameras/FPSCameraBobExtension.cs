using UnityEngine;
using Unity.Cinemachine;

/// <summary>
/// CMFPSオブジェクトにアタッチ。
/// 速度を位置座標から直接計算することで、あらゆる移動スクリプトに対応させた強固な版。
/// </summary>
public class FPSCameraBobExtension : CinemachineExtension
{
    [Header("References")]
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private CMFPSController cmController;

    [Header("Bob Settings")]
    [SerializeField] private float walkSpeed = 10f;
    [SerializeField] private float walkAmount = 0.05f;
    [SerializeField] private float runSpeed = 14f;
    [SerializeField] private float runAmount = 0.1f;
    [Range(0f, 1f)]
    [SerializeField] private float horizontalMultiplier = 0.5f;
    [SerializeField] private float smoothTime = 8f;

    [Header("Sensitivity")]
    [SerializeField] private float movingThreshold = 0.1f;
    [Tooltip("チェックを入れると画面左上に現在の判定状況を表示します")]
    [SerializeField] private bool showDebugStatus = true; // うまくいったらチェックを外してください
    public bool BobEnabled = true; // 将来的にオンオフ切り替えを実装する場合用
    // 内部変数
    private float _timer = 0f;
    private Vector3 _currentBobOffset = Vector3.zero;
    private Vector3 _lastPosition; // 前フレームの位置
    private float _currentSpeed;   // 計算した速度

    void Start()
    {
        cmController = GetComponent<CMFPSController>();

        if (PlayerStatus.instance != null)
        {
            playerMovement = PlayerStatus.instance.GetComponent<PlayerMovement>();
        }

        if (playerMovement != null)
        {
            _lastPosition = playerMovement.transform.position;
        }
    }

    protected override void PostPipelineStageCallback(
        CinemachineVirtualCameraBase vcam,
        CinemachineCore.Stage stage,
        ref CameraState state,
        float deltaTime)
    {
        if (stage != CinemachineCore.Stage.Aim) return;
        if (playerMovement == null) return;

        // --- 1. 正確な速度計算 (CCに頼らず位置の差分で計算) ---
        Vector3 currentPos = playerMovement.transform.position;
        Vector3 delta = currentPos - _lastPosition;
        // 水平成分のみ抽出
        delta.y = 0f;

        // 瞬間移動などでdeltaTimeが0の時は計算しない
        if (deltaTime > 0.0001f)
        {
            // Lerpを使って速度数値を安定させる
            float rawSpeed = delta.magnitude / deltaTime;
            _currentSpeed = Mathf.Lerp(_currentSpeed, rawSpeed, deltaTime * 10f);
        }
        _lastPosition = currentPos;

        // --- 2. 判定 ---
        bool shouldBob = CheckShouldBob(out bool isRunning);

        // --- 3. 計算 ---
        float targetAmp = 0f;
        float targetFreq = 0f;

        if (shouldBob)
        {
            targetAmp = isRunning ? runAmount : walkAmount;
            targetFreq = isRunning ? runSpeed : walkSpeed;
        }

        if (targetAmp > 0 || _currentBobOffset.sqrMagnitude > 0.0001f)
        {
            _timer += deltaTime * targetFreq;
        }
        else
        {
            _timer = 0f;
        }

        float bobY = Mathf.Sin(_timer) * targetAmp;
        float bobX = Mathf.Cos(_timer * 0.5f) * targetAmp * horizontalMultiplier;

        Vector3 targetOffset = new Vector3(bobX, bobY, 0);

        // --- 4. 適用 ---
        _currentBobOffset = Vector3.Lerp(_currentBobOffset, targetOffset, deltaTime * smoothTime);
        state.PositionCorrection += state.RawOrientation * _currentBobOffset;
    }

    private bool CheckShouldBob(out bool isRunning)
    {
        isRunning = false;

        // ロック判定
        if (cmController != null && cmController.IsLocked) return false;

        // 接地判定
        // publicにした groundCheck を使用
        if (playerMovement.groundCheck != null)
        {
            if (!playerMovement.groundCheck.IsGrounded) return false;
        }
        // 予備：CCの接地判定（ただしMove2回呼び出し問題で不安定な可能性あり）
        else
        {
             var cc = playerMovement.GetComponent<CharacterController>();
             if (cc != null && !cc.isGrounded) return false;
        }

        // 速度判定（自前計算した _currentSpeed を使用）
        if (_currentSpeed < movingThreshold) return false;

        // 走り判定
        if (_currentSpeed > 6.0f) isRunning = true;

        return true;
    }
}

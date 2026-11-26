using UnityEngine;
using Unity.Cinemachine;
using UnityEngine.InputSystem;

/// <summary>
/// CM3 の FPS リグ操作用コントローラ。
/// - 視点ロック/解除（Input無効化＋PanTilt停止で角度固定）
/// - 向きスナップ（任意Transformを向く）
/// - Followターゲットの差し替え
/// </summary>
[DisallowMultipleComponent]
public class CMFPSController : Singleton<CMFPSController>
{
    [Header("References (同じオブジェクトに付いている想定)")]
    [Tooltip("CM3の仮想カメラ本体 (CinemachineCamera)")]
    [SerializeField] public CinemachineCamera cmCamera;

    [Tooltip("視点入力を読み取るプロバイダ (CinemachineInputAxisController)")]
    [SerializeField] private CinemachineInputAxisController inputProvider;

    [Tooltip("Yaw/Pitch を駆動する PanTilt")]
    [SerializeField] private CinemachinePanTilt panTilt;

    [Tooltip("プレイヤー位置を追従する Follow")]
    [SerializeField] private CinemachineFollow follow;

    [Header("Lock 挙動オプション")]
    [Tooltip("ロック中は PanTilt を無効化して角度を完全固定")]
    [SerializeField] private bool disablePanTiltWhileLocked = true;

    [Tooltip("ロック中に Follow まで止めたい場合は有効化（通常は false 推奨）")]
    [SerializeField] private bool disableFollowWhileLocked = false;

    // 状態保存
    private bool _wasInputEnabled;
    private bool _wasPanTiltEnabled;
    private bool _wasFollowEnabled;
    private Quaternion _lockedRotation;

    public bool IsLocked { get; private set; }

    void Reset()
    {
        // 自動参照
        if (!cmCamera)      cmCamera      = GetComponent<CinemachineCamera>();
        if (!inputProvider) inputProvider = GetComponent<CinemachineInputAxisController>();
        if (!panTilt)       panTilt       = GetComponent<CinemachinePanTilt>();
        if (!follow)        follow        = GetComponent<CinemachineFollow>();
    }

    void Start()
    {
        // 足りない参照を保険で拾う
        Reset();
    }

    /// <summary>
    /// 毎フレーム、ConfigControllerから設定を読み込み、InputProviderに適用する
    /// </summary>
    void Update()
    {
        if (Gamepad.current == null) return;

        Vector2 rs = Gamepad.current.rightStick.ReadValue();
        float rt = Gamepad.current.rightTrigger.ReadValue();

        Debug.Log($"RightStick: {rs}, RT: {rt}");
        // 視点がロックされている間は、inputProvider自体が無効なので処理不要
        if (IsLocked) return;

        // (inputProvider が無い場合も処理不要)
        if (inputProvider == null) return;

        // ConfigController (金庫) が見つからなければ何もしない
        if (ConfigController.instance == null) return;

        // ConfigControllerから最新の設定を取得して適用する
        ApplySettingsToProvider(ConfigController.instance.CurrentSettings);
    }

    /// <summary>
    /// 取得した設定値をInputProviderの各プロパティに適用する
    /// </summary>
    private void ApplySettingsToProvider(GameSettings settings)
    {
        if (settings == null) return;

        // X軸（左右）に感度(Gain)を適用
        inputProvider.Controllers[0].Input.Gain = settings.mouseSensitivity;

        // Y軸（上下）に感度(Gain)を適用
        inputProvider.Controllers[1].Input.Gain = -settings.mouseSensitivity;
    }

    // ====== Public API ======

    /// <summary>視点ロック（入力とPanTiltを止めて現在角度のまま固定）</summary>
    public void LockView()
    {
        if (IsLocked) return;

        // 現在の状態を保存
        _wasInputEnabled  = inputProvider ? inputProvider.enabled  : false;
        _wasPanTiltEnabled= panTilt       ? panTilt.enabled        : false;
        _wasFollowEnabled = follow        ? follow.enabled         : false;

        // 現在角度を保持
        if (cmCamera) _lockedRotation = cmCamera.transform.rotation;

        // 入力停止
        if (inputProvider) inputProvider.enabled = false;

        // 角度固定：PanTiltを止める（必要ならFollowも）
        if (disablePanTiltWhileLocked && panTilt) panTilt.enabled = false;
        if (disableFollowWhileLocked  && follow)  follow.enabled  = false;

        // 念のため角度を再適用（PanTiltの最後の更新でズレないように）
        if (cmCamera) cmCamera.transform.rotation = _lockedRotation;

        IsLocked = true;
    }

    /// <summary>視点ロック解除（保存しておいた各コンポーネント有効状態に戻す）</summary>
    public void UnlockView()
    {
        if (!IsLocked) return;

        // 回転は保持されたままでOK。入力とコンポーネントの有効状態だけ復帰
        if (inputProvider) inputProvider.enabled = _wasInputEnabled;
        if (panTilt)       panTilt.enabled       = _wasPanTiltEnabled;
        if (follow)        follow.enabled        = _wasFollowEnabled;

        // Unlockした直後に、現在の設定を即座に再適用する（オプションだが推奨）
        // （Update()でも適用されるが、1フレームの遅延を防ぐ）
        if (ConfigController.instance != null)
        {
            ApplySettingsToProvider(ConfigController.instance.CurrentSettings);
        }

        IsLocked = false;
    }

    /// <summary>ロック/アンロックをまとめて切替</summary>
    public void SetViewLocked(bool locked)
    {
        if (locked) LockView();
        else        UnlockView();
    }

    /// <summary>即座に指定方向を向かせる（ロック中でも反映）。</summary>
    public void SnapLookAt(Transform worldTarget)
    {
        if (!cmCamera || !worldTarget) return;

        var camTf = cmCamera.transform;
        var dir = (worldTarget.position - camTf.position);
        if (dir.sqrMagnitude < 0.0001f) return;

        var rot = Quaternion.LookRotation(dir.normalized, Vector3.up);

        // PanTiltが有効だと次の更新で上書きされる場合があるので、一瞬止めてから回転適用→元に戻す
        bool ptWasEnabled = panTilt && panTilt.enabled;
        if (ptWasEnabled) panTilt.enabled = false;

        camTf.rotation = rot;

        if (!IsLocked && ptWasEnabled && panTilt) panTilt.enabled = true;

        // ロック中は固定回転も更新しておく
        if (IsLocked) _lockedRotation = rot;
    }

    /// <summary>Follow のターゲットを変更（null で解除）。</summary>
    public void SetFollowTarget(Transform target)
    {
        if (!cmCamera) return;
        cmCamera.Follow = target;
    }
    /// <summary>PanTilt の LookAt ターゲットを変えたい場合（任意）。
    /// 通常 FPS はカメラ自身で向きを作るので不要。</summary>
    public void SetLookAtTarget(Transform target)
    {
        if (!cmCamera) return;
        // CM3では LookAt は CinemachineCamera のプロパティ
        cmCamera.LookAt = target;
    }
}

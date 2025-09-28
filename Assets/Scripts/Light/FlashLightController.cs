// FlashLightController.cs
using UnityEngine;
using UnityEngine.InputSystem;
using Sirenix.OdinInspector;

public class FlashLightController : SerializedMonoBehaviour
{
    [Title("References")]
    private PlayerInput playerInput;            // Inspectorで指定するか同GameObjectから自動取得
    [Required] public Light flashlightLight;             // 点け消しするライト
    [Required] public AudioClip toggleClip;              // 点け/消すときに鳴らす音（同じ音）
    [InfoBox("AudioSourceが未設定なら自動追加されます")]
    public AudioSource audioSource;                      // 任意。無ければ自動追加

    [Title("Follow (Camera.main を基準に追従)")]
    [Tooltip("カメラ基準のローカルオフセット（右上前など）")]
    public Vector3 positionOffset = new Vector3(0f, -0.12f, 0.5f);
    public Vector3 rotationOffset = Vector3.zero;       // euler offset
    [Tooltip("位置のスムースタイム（小さいほど追従が早い）")]
    [Range(0.001f, 0.5f)] public float positionSmoothTime = 0.05f;
    [Tooltip("回転の補間速度（大きいほど早い）")]
    public float rotationLerpSpeed = 20f;

    // 内部
    Transform cam;
    InputAction flashAction;
    Vector3 velocity = Vector3.zero;
    bool isOn;

    void Start()
    {
        // Camera.main を取得（要件）
        cam = Camera.main?.transform;
        Debug.Assert(cam != null, "FlashLightController: Camera.main が見つかりません。MainCamera に Tag='MainCamera' を設定してね。");
        playerInput = PlayerStatus.instance.playerInput;
        Debug.Assert(playerInput != null, "FlashLightController: PlayerInput が見つかりません。インスペクタで設定してね。");

        flashAction = playerInput?.actions?.FindAction("LightSwitchAction");
        Debug.Assert(flashAction != null, "FlashLight アクションが見つかりません。InputAction の名前を 'FlashLight' にしてね。");

        // 押下でトグル（performed）
        flashAction.performed += OnFlashPerformed;

        // AudioSource 用意（なければ追加）
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();

        // Light が未指定なら子から探す
        if (flashlightLight == null) flashlightLight = GetComponentInChildren<Light>();
        Debug.Assert(flashlightLight != null, "Light コンポーネントが見つかりません。");

        isOn = flashlightLight.enabled;
    }

    void OnDisable()
    {
        if (flashAction != null) flashAction.performed -= OnFlashPerformed;
    }

    void OnFlashPerformed(InputAction.CallbackContext ctx)
    {
        ToggleFlashlight();
    }

    [Button]
    public void ToggleFlashlight()
    {
        flashlightLight.enabled = !flashlightLight.enabled;
        isOn = flashlightLight.enabled;

        if (toggleClip != null && audioSource != null)
            audioSource.PlayOneShot(toggleClip);
    }

    void LateUpdate()
    {
        if (cam == null) return; // カメラ消失時の最低限のガード

        // 位置追従（カメラ基準のオフセットをワールドに変換して追従）
        Vector3 targetPos = cam.position + cam.TransformDirection(positionOffset);
        transform.position = Vector3.SmoothDamp(transform.position, targetPos, ref velocity, positionSmoothTime);

        // 回転追従（カメラ回転にオフセットをかけて補間）
        Quaternion targetRot = cam.rotation * Quaternion.Euler(rotationOffset);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Mathf.Clamp01(Time.deltaTime * rotationLerpSpeed));
    }
}

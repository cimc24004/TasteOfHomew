using UnityEngine;
using UnityEngine.InputSystem;
using Sirenix.OdinInspector;

public class FlashLightController : SerializedMonoBehaviour
{
    [Title("References")]
    private PlayerInput playerInput;
    [Required] public Light flashlightLight;
    [Required] public AudioClip toggleClip;
    [InfoBox("AudioSourceが未設定なら自動追加されます")]
    public AudioSource audioSource;

    [Title("Settings")]
    [Tooltip("外部要因（イベント等）でライトの切り替えを許可するかどうか")]
    public bool permitSwitchLight = true;

    [Tooltip("家の中にいるときライトを強制的に消すかどうか")]
    public bool autoTurnOffInHouse = true;

    [Title("Follow (Camera.main を基準に追従)")]
    public Vector3 positionOffset = new Vector3(0f, -0.12f, 0.5f);
    public Vector3 rotationOffset = Vector3.zero;
    [Range(0.001f, 0.5f)] public float positionSmoothTime = 0.05f;
    public float rotationLerpSpeed = 20f;

    // 内部
    Transform cam;
    InputAction flashAction;
    Vector3 velocity = Vector3.zero;
    bool isOn;

    void Awake()
    {
        cam = Camera.main?.transform;

        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();

        if (flashlightLight == null) flashlightLight = GetComponentInChildren<Light>();

        isOn = flashlightLight.enabled;
    }
    void Start()
    {
        flashAction = PlayerDataHub.instance.CurrentplayerInput.actions.FindAction("LightSwitchAction");
        if (flashAction != null)
        {
            flashAction.performed += OnFlashPerformed;
        }
    }
    void OnEnable()
    {
    }

    void OnDisable()
    {
        if (flashAction != null)
        {
            flashAction.performed -= OnFlashPerformed;
        }
    }

    void OnFlashPerformed(InputAction.CallbackContext ctx)
    {
        ToggleFlashlight();
    }

    [Button]
    public void ToggleFlashlight()
    {
        if (autoTurnOffInHouse && (PlayerLocation.instance.currentLocation != PlayerLocation.Location.Outside || !permitSwitchLight))
        {
            return;
        }

        flashlightLight.enabled = !flashlightLight.enabled;
        isOn = flashlightLight.enabled;

        if (toggleClip != null && audioSource != null)
            audioSource.PlayOneShot(toggleClip);
    }

    void LateUpdate()
    {
        // カメラ追従処理
        if (cam != null)
        {
            Vector3 targetPos = cam.position + cam.TransformDirection(positionOffset);
            transform.position = Vector3.SmoothDamp(transform.position, targetPos, ref velocity, positionSmoothTime);

            Quaternion targetRot = cam.rotation * Quaternion.Euler(rotationOffset);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Mathf.Clamp01(Time.deltaTime * rotationLerpSpeed));
        }

        // 家の中での自動消灯
        if (autoTurnOffInHouse &&
            PlayerLocation.instance != null &&
            PlayerLocation.instance.currentLocation == PlayerLocation.Location.InHouse &&
            flashlightLight.enabled)
        {
            audioSource.PlayOneShot(toggleClip);
            flashlightLight.enabled = false;
            isOn = false;
        }
    }

    /// <summary>
    /// 外部からライトの切り替え許可を操作します。
    /// </summary>
    public void ChangePermitSwitchLight(bool permit)
    {
        permitSwitchLight = permit;
    }
}

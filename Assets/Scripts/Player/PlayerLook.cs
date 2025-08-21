using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerLook : MonoBehaviour
{
    public Transform playerBody;
    public Transform cameraTransform;
    public float sensitivity = 100f;
    public float bobFrequency = 4f;
    public float bobAmplitude = 0.05f;

    private float xRotation = 0f;
    private Vector3 cameraDefaultPos;
    private float bobTimer;

    private void Start()
    {
        if (cameraTransform == null)
            cameraTransform = Camera.main.transform;
        cameraDefaultPos = cameraTransform.localPosition;
        Cursor.lockState = CursorLockMode.Locked;
    }

    private void Update()
    {
        if (Mouse.current == null) return;

        Vector2 mouseDelta = Mouse.current.delta.ReadValue();
        mouseDelta *= sensitivity * Time.deltaTime;

        xRotation -= mouseDelta.y;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);
        cameraTransform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        playerBody.Rotate(Vector3.up * mouseDelta.x);

        Vector3 horiz = playerBody.GetComponent<CharacterController>().velocity;
        horiz.y = 0f;
        if (horiz.magnitude > 0.1f)
        {
            bobTimer += Time.deltaTime * bobFrequency;
            float bobOffset = Mathf.Sin(bobTimer) * bobAmplitude;
            cameraTransform.localPosition = cameraDefaultPos + new Vector3(0, bobOffset, 0);
        }
        else
        {
            bobTimer = 0f;
            cameraTransform.localPosition = Vector3.Lerp(
                cameraTransform.localPosition,
                cameraDefaultPos,
                Time.deltaTime * bobFrequency);
        }
    }
}

using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Camera))]
[RequireComponent(typeof(CrosshairUI))]
public class PlayerInteraction : MonoBehaviour
{
    public float interactDistance = 3f;
    public LayerMask interactMask = ~0;

    private Camera cam;
    private CrosshairUI crosshair;

    private void Awake()
    {
        cam = GetComponent<Camera>();
        crosshair = GetComponent<CrosshairUI>();
    }

    private void Update()
    {
        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        bool hasTarget = false;

        if (Physics.Raycast(ray, out RaycastHit hit, interactDistance, interactMask))
        {
            var interactable = hit.collider.GetComponent<IInteractable>();
            if (interactable != null)
            {
                hasTarget = true;
                if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
                {
                    interactable.Interact();
                }
            }
        }

        crosshair.SetInteractable(hasTarget);
    }
}

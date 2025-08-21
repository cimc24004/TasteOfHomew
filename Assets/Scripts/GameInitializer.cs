using UnityEngine;

public class GameInitializer : MonoBehaviour
{
    private void Start()
    {
        var cam = GetComponent<Camera>();
        if (cam == null)
            cam = Camera.main;
        if (cam == null)
            return;

        GameObject camGO = cam.gameObject;

        var player = new GameObject("Player");
        player.transform.position = camGO.transform.position;
        camGO.transform.SetParent(player.transform);
        camGO.transform.localPosition = new Vector3(0f, 0.8f, 0f);

        var controller = player.AddComponent<CharacterController>();
        player.AddComponent<PlayerMovement>();

        var look = player.AddComponent<PlayerLook>();
        look.playerBody = player.transform;
        look.cameraTransform = cam.transform;

        camGO.AddComponent<PlayerInteraction>();
        camGO.AddComponent<CrosshairUI>();

        var door = GameObject.CreatePrimitive(PrimitiveType.Cube);
        door.transform.position = player.transform.position + player.transform.forward * 3f;
        door.name = "HingedDoor";
        door.AddComponent<HingedDoor>();

        Destroy(this);
    }
}

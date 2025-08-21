using System.Collections;
using UnityEngine;

public class HingedDoor : MonoBehaviour, IInteractable
{
    public Transform door;
    public float openAngle = 90f;
    public float speed = 2f;

    private bool isOpen;
    private Quaternion closedRot;
    private Quaternion openRot;

    private void Awake()
    {
        if (door == null) door = transform;
        closedRot = door.localRotation;
        openRot = Quaternion.Euler(door.localEulerAngles + new Vector3(0f, openAngle, 0f));
    }

    public void Interact()
    {
        isOpen = !isOpen;
        StopAllCoroutines();
        StartCoroutine(RotateDoor(isOpen ? openRot : closedRot));
    }

    private IEnumerator RotateDoor(Quaternion target)
    {
        while (Quaternion.Angle(door.localRotation, target) > 0.1f)
        {
            door.localRotation = Quaternion.Slerp(door.localRotation, target, Time.deltaTime * speed);
            yield return null;
        }
        door.localRotation = target;
    }
}

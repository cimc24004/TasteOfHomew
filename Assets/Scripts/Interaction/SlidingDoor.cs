using System.Collections;
using UnityEngine;

public class SlidingDoor : MonoBehaviour, IInteractable
{
    public Transform leftDoor;
    public Transform rightDoor;
    public float slideDistance = 1f;
    public float speed = 2f;

    private bool isOpen;
    private Vector3 leftClosed;
    private Vector3 rightClosed;

    private void Awake()
    {
        if (leftDoor != null) leftClosed = leftDoor.localPosition;
        if (rightDoor != null) rightClosed = rightDoor.localPosition;
    }

    public void Interact()
    {
        isOpen = !isOpen;
        StopAllCoroutines();
        StartCoroutine(SlideDoors(isOpen));
    }

    private IEnumerator SlideDoors(bool open)
    {
        Vector3 leftTarget = leftClosed + (open ? Vector3.left * slideDistance : Vector3.zero);
        Vector3 rightTarget = rightClosed + (open ? Vector3.right * slideDistance : Vector3.zero);

        while (leftDoor != null && rightDoor != null &&
               (Vector3.Distance(leftDoor.localPosition, leftTarget) > 0.01f ||
                Vector3.Distance(rightDoor.localPosition, rightTarget) > 0.01f))
        {
            if (leftDoor != null)
                leftDoor.localPosition = Vector3.Lerp(leftDoor.localPosition, leftTarget, Time.deltaTime * speed);
            if (rightDoor != null)
                rightDoor.localPosition = Vector3.Lerp(rightDoor.localPosition, rightTarget, Time.deltaTime * speed);
            yield return null;
        }

        if (leftDoor != null) leftDoor.localPosition = leftTarget;
        if (rightDoor != null) rightDoor.localPosition = rightTarget;
    }
}

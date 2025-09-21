using UnityEngine;

public class InHouseTracker : MonoBehaviour
{
    private PlayerLocation playerLocation;
    [SerializeField] private CollisionOrTriggerEvent collisionOrTriggerEvent;
    void Start()
    {
        playerLocation = PlayerLocation.instance;
        collisionOrTriggerEvent.onEnter.AddListener(() =>
        {
            playerLocation.SetLocation(PlayerLocation.Location.InHouse);
        });
    }
}

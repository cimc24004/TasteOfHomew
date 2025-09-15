using UnityEngine;

public class InHouseTracker : MonoBehaviour
{
    private EnvironmentBGMController environmentBGMController;
    [SerializeField] private CollisionOrTriggerEvent collisionOrTriggerEvent;
    void Start()
    {
        environmentBGMController = EnvironmentBGMController.instance;
        collisionOrTriggerEvent.onEnter.AddListener(() =>
        {
            environmentBGMController.SetLocation(PlayerLocation.InHouse);
        });
    }
}

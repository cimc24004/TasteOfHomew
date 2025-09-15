using UnityEngine;

public class OutsideTracker : MonoBehaviour
{
    private EnvironmentBGMController environmentBGMController;
    [SerializeField] private CollisionOrTriggerEvent collisionOrTriggerEvent;
    void Start()
    {
        environmentBGMController = EnvironmentBGMController.instance;
        collisionOrTriggerEvent.onEnter.AddListener(() =>
        {
            environmentBGMController.SetLocation(PlayerLocation.Outside);
        });
    }
}

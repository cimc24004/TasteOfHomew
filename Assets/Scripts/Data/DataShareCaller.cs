using UnityEngine;

public class DataShareCaller : MonoBehaviour
{
    private DataShareController dataShareController;
    void Start()
    {
        dataShareController = DataShareController.instance;
    }
    public void SetFlagToTrue(string key)
    {
        dataShareController.SetFlagToTrue(key);
    }
    public void ChangePhaseStep(string _step)
    {
        dataShareController.ChangePhaseStep(_step);
    }
}

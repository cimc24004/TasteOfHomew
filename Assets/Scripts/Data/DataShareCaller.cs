using UnityEngine;

public class DataShareCaller : MonoBehaviour
{
    private DataShareController dataShareController;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        dataShareController = DataShareController.Instance;
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

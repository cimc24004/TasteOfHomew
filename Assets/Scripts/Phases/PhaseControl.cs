using UnityEngine;
using UnityEngine.SceneManagement;

public class PhaseControl : MonoBehaviour
{
    private GameFlowController gameFlowController;
    private PhaseStepController phaseStepController;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        Init();
        SceneManager.sceneLoaded += OnSceneLoaded;
    }
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Init();
    }
    private void Init()
    {
        gameFlowController = GameFlowController.instance;
        phaseStepController = PhaseStepController.instance;
    }
    // Update is called once per frame
    // void Update()
    // {

    // }
    public void NextFlowPhase()
    {
        gameFlowController.NextPhase();
    }
}

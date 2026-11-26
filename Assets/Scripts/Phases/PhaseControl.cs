using UnityEngine;
using UnityEngine.SceneManagement;

public class PhaseControl : MonoBehaviour
{
    private GameFlowController gameFlowController;
    private PhaseStepController phaseStepController;

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
    public void NextFlowPhase()
    {
        gameFlowController.NextPhase();
    }
}

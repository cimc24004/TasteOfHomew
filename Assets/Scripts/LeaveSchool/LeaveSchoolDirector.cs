using UnityEngine;
using UnityEngine.SceneManagement;
public class LeaveSchoolDirector : MonoBehaviour
{
    private GameFlowController gameFlowController;
    private DialogueController dialogueController;
    private PlayerDataHub playerDataHub;
    void Start()
    {
        SceneManager.sceneUnloaded += OnSceneUnLoaded;
        playerDataHub = PlayerDataHub.instance;
        gameFlowController = GameFlowController.instance;
        dialogueController = DialogueController.instance;
    }

    void Update()
    {
    }

    private void OnSceneUnLoaded(Scene scene)
    {
        // Destroy(playerDataHub.gameObject);
        // Destroy(gameFlowController.gameObject);
        // Destroy(dialogueController.gameObject);
    }
}

using UnityEngine;
using UnityEngine.SceneManagement;
public class LeaveSchoolDirector : MonoBehaviour
{
    private GameFlowController gameFlowController;
    private DialogueController dialogueController;
    private PlayerDataHub playerDataHub;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        SceneManager.sceneUnloaded += OnSceneUnLoaded;
        playerDataHub = PlayerDataHub.instance;
        gameFlowController = GameFlowController.instance;
        dialogueController = DialogueController.instance;
    }

    // Update is called once per frame
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

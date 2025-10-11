using UnityEngine;

public class DialogueControllerCaller : MonoBehaviour
{
    private DialogueController dialogueController;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        dialogueController = DialogueController.instance;
    }
    public void SetFlagToTrue(string flagName)
    {
        dialogueController.SetFlag(flagName, true);
    }
}

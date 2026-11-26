using UnityEngine;

public class DialogueControllerCaller : MonoBehaviour
{
    private DialogueController dialogueController;
    void Start()
    {
        dialogueController = DialogueController.instance;
    }
    public void SetFlagToTrue(string flagName)
    {
        dialogueController.SetFlag(flagName, true);
    }
}

using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class CreditController : MonoBehaviour
{
    private PlayerInput playerInput;
    private SceneManagerController sceneManagerController;
    [SerializeField] private Button backToStartButton;
    private bool once;
    void Start()
    {
        playerInput = PlayerDataHub.instance.CurrentplayerInput;
        sceneManagerController = SceneManagerController.Instance;
        backToStartButton.onClick.AddListener(SceneMove);
    }

    void Update()
    {
        if (playerInput.actions["RightClick"].triggered)
        {
            if (once) return;
            SceneMove();
            once = true;
        }
    }

    private void SceneMove()
    {
        sceneManagerController.LoadScene("Start");
    }
}

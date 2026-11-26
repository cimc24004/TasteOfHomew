using UnityEngine;

public class SceneManagerCaller : MonoBehaviour
{
    private SceneManagerController sceneManagerController;
    void Start()
    {
        sceneManagerController = SceneManagerController.Instance;
    }
    public void LoadScene(string _sceneName)
    {
        sceneManagerController.LoadScene(_sceneName);
    }
}

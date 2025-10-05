using UnityEngine;

public class SceneManagerCaller : MonoBehaviour
{
    private SceneManagerController sceneManagerController;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        sceneManagerController = SceneManagerController.Instance;
    }
    public void LoadScene(string _sceneName)
    {
        sceneManagerController.LoadScene(_sceneName);
    }
}

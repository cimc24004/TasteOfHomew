using UnityEngine;
using UnityEngine.SceneManagement;

public class GameController : Singleton<GameController>
{
    public CursorLockMode DefaultCursorLockMode;
    public string NowSceneName;
    private GameStateController game_state_controller;
    public GameStateController.GameState game_state;
    public bool CursorVisible;
    public PlayerUIInfo playerUIInfo;
    //カーソルのロックはステージごとに違う可能性がある(GameOverやGameStart画面などではLockはしておかない)
    //なのでステージごとに配置するコントローラーを分けている。

    // Start is called before the first frame update
    void Start()
    {
        game_state_controller = GameStateController.instance;
        if (SceneManager.GetActiveScene().name == "End") return;
        game_state_controller.ActionByModeSelect(game_state, CursorVisible);
        NowSceneName = SceneManager.GetActiveScene().name;
    }

}

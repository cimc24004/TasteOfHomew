using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class GameStateController : Singleton<GameStateController>
{
    public enum GameState
    {
        Gameplay,
        MenuOnGamePlay,
        Menu
    }

    [Header("現在のゲームの状態 メニュー画面だったり、スタート画面ならUIが選択")]
    public GameState gameState;
    private PlayerInput playerInput;
    private PlayerStatus playerStatus;
    private CMFPSController cMFPSController;
    void Start()
    {
        playerStatus = PlayerStatus.instance;
        this.playerInput = playerStatus.playerInput;
        playerInput.SwitchCurrentActionMap("Player");
        cMFPSController = CMFPSController.instance;

        SceneManager.sceneLoaded += OnSceneLoaded;
    }
    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        playerStatus = PlayerStatus.instance;
        this.playerInput = playerStatus.playerInput;
        playerInput.SwitchCurrentActionMap("Player");
        cMFPSController = CMFPSController.instance;
    }
    public void ActionByModeSelect(GameState game_state, bool cursor_display)
    {
        if (game_state == GameState.Gameplay)
        {
            GameplayMode();
        }
        else if (game_state == GameState.MenuOnGamePlay)
        {
            MenuOnGamePlayMode(cursor_display);
        }
    }

    public void GameplayMode()
    {
        //ゲームをプレイしてるとき
        cMFPSController.UnlockView();
        playerInput.actions.FindActionMap("MenuOnGamePlay").Disable();
        playerInput.actions.FindActionMap("ReadEvent").Disable();
        playerInput.actions.FindActionMap("Player").Enable();
        playerInput.actions.FindActionMap("GamePlayAndMenuCommon").Enable();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        Debug.Log("GameplayModeGameplayModeGameplayModeGameplayModeGameplayMode");
        EventSystem.current.sendNavigationEvents = true;
    }

    public void MenuOnGamePlayMode(bool visibleCursor)
    {
        //ゲームプレイ中にメニューを開いたとき readableEventや dialogue時など
        cMFPSController.LockView();
        playerInput.actions.FindActionMap("MenuOnGamePlay").Enable();
        playerInput.actions.FindActionMap("Player").Disable();
        playerInput.actions.FindActionMap("ReadEvent").Disable();
        playerInput.actions.FindActionMap("GamePlayAndMenuCommon").Enable();
        if (visibleCursor)
        {
            //カーソルは表示
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        
        EventSystem.current.sendNavigationEvents = false;
    }

    public void ReadEventMode(bool visibleCursor)
    {
        playerInput.actions.FindActionMap("Player").Disable();
        playerInput.actions.FindActionMap("MenuOnGamePlay").Disable();
        playerInput.actions.FindActionMap("ReadEvent").Enable();

        playerInput.actions.FindActionMap("GamePlayAndMenuCommon").Disable();
        if (visibleCursor)
        {
            //カーソルは表示
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        EventSystem.current.sendNavigationEvents = false;
    }

}
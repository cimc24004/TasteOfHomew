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
    private PlayerDataHub playerDataHub;
    void Start()
    {
        playerDataHub = PlayerDataHub.instance;
        if(playerDataHub.CurrentplayerInput != null){
            playerDataHub.CurrentplayerInput.SwitchCurrentActionMap("Player");
        }
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
        playerDataHub.CurrentCMFPSController.UnlockView();
        playerDataHub.CurrentplayerInput.actions.FindActionMap("MenuOnGamePlay").Disable();
        playerDataHub.CurrentplayerInput.actions.FindActionMap("ReadEvent").Disable();
        playerDataHub.CurrentplayerInput.actions.FindActionMap("Player").Enable();
        playerDataHub.CurrentplayerInput.actions.FindActionMap("GamePlayAndMenuCommon").Enable();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        Debug.Log("GameplayModeGameplayModeGameplayModeGameplayModeGameplayMode");
        EventSystem.current.sendNavigationEvents = true;
    }

    public void MenuOnGamePlayMode(bool visibleCursor)
    {
        //ゲームプレイ中にメニューを開いたとき readableEventや dialogue時など
        playerDataHub.CurrentCMFPSController.LockView();
        playerDataHub.CurrentplayerInput.actions.FindActionMap("MenuOnGamePlay").Enable();
        playerDataHub.CurrentplayerInput.actions.FindActionMap("Player").Disable();
        playerDataHub.CurrentplayerInput.actions.FindActionMap("ReadEvent").Disable();
        playerDataHub.CurrentplayerInput.actions.FindActionMap("GamePlayAndMenuCommon").Enable();
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
        playerDataHub.CurrentplayerInput.actions.FindActionMap("Player").Disable();
        playerDataHub.CurrentplayerInput.actions.FindActionMap("MenuOnGamePlay").Disable();
        playerDataHub.CurrentplayerInput.actions.FindActionMap("ReadEvent").Enable();
        playerDataHub.CurrentplayerInput.actions.FindActionMap("GamePlayAndMenuCommon").Disable();
        if (visibleCursor)
        {
            //カーソルは表示
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        EventSystem.current.sendNavigationEvents = false;
    }

}
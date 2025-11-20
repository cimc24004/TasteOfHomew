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
    private InputDeviceDetector inputDeviceDetector;

    // ★ KBM時にカーソルを表示すべきかどうかの内部状態
    // (MenuOnGamePlayMode(true) で true, GameplayMode() で false になる)
    private bool shouldShowCursorForKBM = false; 

    // ★ 最後にチェックしたデバイスタイプ
    private InputDeviceDetector.DeviceType? lastCheckedDeviceType = null;


    void Start()
    {
        // Startで取得を試みる (見つからなくてもUpdateで探し続ける)
        inputDeviceDetector = InputDeviceDetector.Instance;
    }

    // ▼▼▼ Update関数でデバイスを常時監視 ▼▼▼
    void Update()
    {
        // 1. InputDeviceDetector のインスタンスを（まだなら）取得
        if (inputDeviceDetector == null)
        {
            inputDeviceDetector = InputDeviceDetector.Instance;
            if (inputDeviceDetector == null)
            {
                return; // まだ見つからないので何もしない
            }
        }

        // 2. 現在のデバイスタイプを取得
        var currentDeviceType = inputDeviceDetector.CurrentDeviceType;

        // 3. デバイスタイプが前回から変更されたかチェック
        if (lastCheckedDeviceType == null || lastCheckedDeviceType != currentDeviceType)
        {
            // 4. 変更があった場合、現在のゲームステートに基づいてカーソルを更新
            ApplyCursorState(currentDeviceType);
            
            // 5. 最後のデバイスタイプを更新
            lastCheckedDeviceType = currentDeviceType;
        }
    }
    
    // ★ OnDestroy はイベント購読をしないので不要

    public void ActionByModeSelect(GameState game_state, bool cursor_display)
    {
        this.gameState = game_state; 
        
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
        if (playerDataHub == null)
            playerDataHub = PlayerDataHub.instance;
        
        this.gameState = GameState.Gameplay;
        this.shouldShowCursorForKBM = false; // ★リセット
        this.lastCheckedDeviceType = null; // ★リセット

        playerDataHub.CurrentCMFPSController.UnlockView();
        playerDataHub.CurrentplayerInput.actions.FindActionMap("MenuOnGamePlay").Disable();
        playerDataHub.CurrentplayerInput.actions.FindActionMap("ReadEvent").Disable();
        playerDataHub.CurrentplayerInput.actions.FindActionMap("Player").Enable();
        playerDataHub.CurrentplayerInput.actions.FindActionMap("GamePlayAndMenuCommon").Enable();

        // ★ GameplayMode はデバイス問わず常にロック＆非表示
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        Debug.Log("GameplayModeGameplayModeGameplayModeGameplayModeGameplayMode");
        EventSystem.current.sendNavigationEvents = false;
    }

    public void MenuOnGamePlayMode(bool visibleCursor)
    {
        if (playerDataHub == null)
            playerDataHub = PlayerDataHub.instance;

        this.gameState = GameState.MenuOnGamePlay;
        this.shouldShowCursorForKBM = visibleCursor; // ★ KBM時の表示フラグを記憶
        this.lastCheckedDeviceType = null; // ★リセット

        playerDataHub.CurrentCMFPSController?.LockView();
        playerDataHub.CurrentplayerInput.actions.FindActionMap("MenuOnGamePlay").Enable();
        playerDataHub.CurrentplayerInput.actions.FindActionMap("Player").Disable();
        playerDataHub.CurrentplayerInput.actions.FindActionMap("ReadEvent").Disable();
        playerDataHub.CurrentplayerInput.actions.FindActionMap("GamePlayAndMenuCommon").Enable();

        // ★ メニューを開いた瞬間のデバイスタイプで、即時カーソルを更新
        ApplyCursorState(inputDeviceDetector.CurrentDeviceType);
        
        EventSystem.current.sendNavigationEvents = true;
    }

    public void ReadEventMode(bool visibleCursor)
    {
        if (playerDataHub == null)
            playerDataHub = PlayerDataHub.instance;
            
        this.gameState = GameState.MenuOnGamePlay; // ReadEventもメニュー系として扱う
        this.shouldShowCursorForKBM = visibleCursor; // ★ KBM時の表示フラグを記憶
        this.lastCheckedDeviceType = null; // ★リセット

        playerDataHub.CurrentplayerInput.actions.FindActionMap("Player").Disable();
        playerDataHub.CurrentplayerInput.actions.FindActionMap("MenuOnGamePlay").Disable();
        playerDataHub.CurrentplayerInput.actions.FindActionMap("ReadEvent").Enable();
        playerDataHub.CurrentplayerInput.actions.FindActionMap("GamePlayAndMenuCommon").Disable();

        // ★ メニューを開いた瞬間のデバイスタイプで、即時カーソルを更新
        ApplyCursorState(inputDeviceDetector.CurrentDeviceType);

        EventSystem.current.sendNavigationEvents = false;
    }


    /// <summary>
    /// ★ カーソル状態を適用する唯一の関数 ★
    /// </summary>
    private void ApplyCursorState(InputDeviceDetector.DeviceType deviceType)
    {
        // 1. Gameplay モード中は、何もしない (GameplayMode() が管理)
        if (gameState == GameState.Gameplay)
        {
            return;
        }

        // 2. メニュー系モードの場合
        if (deviceType == InputDeviceDetector.DeviceType.Gamepad)
        {
            // Gamepad なら常に非表示
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = false;
        }
        else // KeyboardMouse の場合
        {
            // KBM なら常に表示
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}
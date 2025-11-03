using UnityEngine;
using UnityEngine.InputSystem;
using System;

/// <summary>
/// PlayerInput の "onControlsChanged" (C# Event) をリッスンし、
/// デバイス（Control Scheme）が変更されたらイベントを発行するシングルトン。
/// </summary>
public class InputDeviceDetector : MonoBehaviour
{
    public enum DeviceType { KeyboardMouse, Gamepad }

    public static InputDeviceDetector Instance { get; private set; }

    public DeviceType CurrentDeviceType;

    public event Action<DeviceType> OnDeviceChanged;

    [Header("Control Scheme Names")]
    [SerializeField] public string keyboardMouseSchemeName = "Keyboard&Mouse";
    [SerializeField] public string gamepadSchemeName = "Gamepad";

    private PlayerInput playerInput;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        // オブジェクト破棄時に、必ずイベント購読を解除する
        if (playerInput != null)
        {
            playerInput.onControlsChanged -= OnControlsChanged;
        }
    }

    private void Start()
    {
        // 起動時はとりあえずKBMに設定しておく
        CurrentDeviceType = DeviceType.KeyboardMouse;
        
        // PlayerDataHub が Start() で初期化済みの場合に備えて、一度取得を試みる
        if (PlayerDataHub.instance != null && PlayerDataHub.instance.CurrentplayerInput != null)
        {
            RegisterPlayerInput(PlayerDataHub.instance.CurrentplayerInput);
        }
    }

    /// <summary>
    /// PlayerDataHub が PlayerInput の準備ができたとき、
    /// または PlayerInput が更新された（シーンが切り替わった）ときに呼び出す
    /// </summary>
    public void RegisterPlayerInput(PlayerInput playerInputInstance)
    {
        // 1. もし既に古い PlayerInput が登録されていたら、イベント購読を解除
        // (シーン遷移で破棄された古い PlayerInput を解除)
        if (this.playerInput != null)
        {
            this.playerInput.onControlsChanged -= OnControlsChanged;
        }

        // 2. 新しい PlayerInput を保存
        this.playerInput = playerInputInstance;

        if (this.playerInput == null)
        {
            Debug.LogWarning("[InputDetector] 登録しようとした PlayerInput が null です。");
            return;
        }

        // 3. 新しい PlayerInput のイベントを購読
        // ★重要★: これが動作するには PlayerInput の Behavior が「Invoke C# Events」である必要があります
        this.playerInput.onControlsChanged += OnControlsChanged;

        // 4. 現在の Control Scheme に基づいて、デバイスタイプを即時更新
        Debug.Log("[InputDetector] PlayerInput が正常に登録されました。");
        UpdateDeviceType(this.playerInput.currentControlScheme);
    }


    /// <summary>
    /// PlayerInput の onControlsChanged イベント (C#) から呼び出される
    /// </summary>
    private void OnControlsChanged(PlayerInput input)
    {
        Debug.Log($"[InputDetector] (C# Event) コントロールが変更されました: {input.currentControlScheme}");
        UpdateDeviceType(input.currentControlScheme);
    }

    /// <summary>
    /// Control Scheme名に基づいてデバイスタイプを更新し、変更があればイベントを発行する
    /// </summary>
    private void UpdateDeviceType(string schemeName)
    {
        DeviceType newType;

        if (schemeName == keyboardMouseSchemeName)
        {
            newType = DeviceType.KeyboardMouse;
        }
        else if (schemeName == gamepadSchemeName)
        {
            newType = DeviceType.Gamepad;
        }
        else
        {
            // 不明なSchemeの場合は現在の設定を維持
            // Debug.LogWarning($"[InputDetector] 不明なScheme名: {schemeName}");
            return; 
        }

        // 既に同じタイプなら何もしない
        if (CurrentDeviceType == newType)
        {
            return;
        }

        // タイプを更新
        CurrentDeviceType = newType;
        
        Debug.Log("切り替わりました！！現在のCurrentDviceType: " + CurrentDeviceType);

        // イベント（OnDeviceChanged）を発行し、購読者（InputVisualSwitcher）に通知する
        OnDeviceChanged?.Invoke(newType);
    }
}
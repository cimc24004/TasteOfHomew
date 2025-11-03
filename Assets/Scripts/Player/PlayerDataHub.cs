using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.InputSystem;

// プレイヤー情報やゲーム全体の重要データを一元管理する場所
public class PlayerDataHub : MonoBehaviour
{
    public static PlayerDataHub instance { get; private set; }

    // 他のスクリプトはここから現在のプレイヤー情報を取得する
    public PlayerStatus CurrentPlayerStatus { get; private set; }
    public CMFPSController CurrentCMFPSController { get; private set; }
    [SerializeField, ShowInInspector]public PlayerInput CurrentplayerInput;
    // 他にも必要なコンポーネントがあれば追加

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // PlayerStatus側から呼び出してもらうための登録メソッド
    public void RegisterPlayer(PlayerStatus playerStatus, CMFPSController cMFPSController)
    {
        this.CurrentPlayerStatus = playerStatus;
        this.CurrentCMFPSController = cMFPSController;
    }
}
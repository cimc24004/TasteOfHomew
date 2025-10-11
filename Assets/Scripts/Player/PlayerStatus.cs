using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerStatus : Singleton<PlayerStatus>
{
    public PlayerInput playerInput;
    public Transform playerHead;
    public GameObject CameraRoot;
    public CrossHairController crossHairController;
    private CMFPSController cMFPSController;
    public static Action<PlayerStatus> playerSpawnedEvent;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        playerSpawnedEvent.Invoke(this);
        playerInput = this.gameObject.GetComponent<PlayerInput>();
        cMFPSController = CMFPSController.instance;
        PlayerDataHub.instance.RegisterPlayer(this, cMFPSController);
    }

    // // Update is called once per frame
    // void Update()
    // {
        
    // }
}

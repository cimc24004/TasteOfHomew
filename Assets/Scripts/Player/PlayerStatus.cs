using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerStatus : Singleton<PlayerStatus>
{
    public Transform playerHead;
    public GameObject CameraRoot;
    public CrossHairController crossHairController;
    private CMFPSController cMFPSController;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        cMFPSController = CMFPSController.instance;
        PlayerDataHub.instance.RegisterPlayer(this, cMFPSController);
    }

    // // Update is called once per frame
    // void Update()
    // {
        
    // }
}

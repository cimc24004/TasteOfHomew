using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerStatus : Singleton<PlayerStatus>
{
    public Transform playerHead;
    public GameObject CameraRoot;
    public CrossHairController crossHairController;
    public CMFPSController cMFPSController;
    public FootStepController playerFootStepController;
    void Start()
    {
        cMFPSController = CMFPSController.instance;
        PlayerDataHub.instance.RegisterPlayer(this, cMFPSController);
    }
}

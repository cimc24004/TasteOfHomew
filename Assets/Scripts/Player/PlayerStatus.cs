using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerStatus : Singleton<PlayerStatus>
{
    public PlayerInput playerInput;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        playerInput = this.gameObject.GetComponent<PlayerInput>();
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}

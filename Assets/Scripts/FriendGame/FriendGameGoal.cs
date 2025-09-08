using UnityEngine;

public class FriendGameGoal : MonoBehaviour
{
    private FriendGameController friendGameController;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        friendGameController = FriendGameController.instance;
    }

    // Update is called once per frame
    void Update()
    {

    }

    void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.gameObject.name == "Player2D")
        {
            friendGameController.GameClear();
        }
    }

}

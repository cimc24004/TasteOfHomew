using UnityEngine;

public class FriendGameGoal : MonoBehaviour
{
    private FriendGameController friendGameController;
    void Start()
    {
        friendGameController = FriendGameController.instance;
    }

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

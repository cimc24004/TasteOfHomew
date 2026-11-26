using UnityEngine;
using Sirenix.OdinInspector;
using System.Collections.Generic;
using UnityEngine.Events;
public class DistanceEventer : SerializedMonoBehaviour
{
    public float EventActionInDistance = 5f;
    public float EventActionOutDistance = 5f;
    [SerializeField] private UnityEvent2 eventActionByDistanceIn;
    [SerializeField] private UnityEvent2 eventActionByDistanceOut;
    private PlayerStatus playerStatus;
    private float toPlayerDistance;
    private bool once_1;
    private bool once_2;
    void Start()
    {
        playerStatus = PlayerStatus.instance;
    }

    void Update()
    {
        toPlayerDistance = Vector3.Distance(this.transform.position, playerStatus.transform.position);
        if (toPlayerDistance <= EventActionInDistance)
        {
            if (!once_1)
            {
                eventActionByDistanceIn.Invoke();
                once_1 = true;
            }
        }
        else
        {
            once_1 = false;
        }
        if(toPlayerDistance >= EventActionOutDistance)
        {
            if (!once_2)
            {
                eventActionByDistanceOut.Invoke();
                once_2 = true;
            }
        }
        else
        {
            once_2 = false;
        }
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;
public class Singleton<T> : SerializedMonoBehaviour where T : SerializedMonoBehaviour
{
    public static T instance;
    protected virtual void Awake()
    {
        if (instance == null)
            instance = (T)FindObjectOfType(typeof(T));
        else
            Destroy(this.gameObject);
    }
}

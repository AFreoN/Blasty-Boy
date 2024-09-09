using UnityEngine;
using UnityEngine.Events;
using System;

public class ObjectTrigger : MonoBehaviour
{
    [SerializeField] MyEvent triggeringAction;

    public Action action;

    private void Start()
    {
        action = () => { triggeringAction.Invoke("", null); };
    }
}

[System.Serializable]
public class MyEvent : UnityEvent<string, GameObject> { }

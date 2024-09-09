using UnityEngine;
using System;

public class FunctionTimer
{
    public static FunctionTimer Create(Action action, float time)
    {
        GameObject g = new GameObject("Function Timer", typeof(MonoHook));

        FunctionTimer funcTimer = new FunctionTimer(action, time, g);
        g.GetComponent<MonoHook>().timerupdate = funcTimer.update;

        return funcTimer;
    }

    public static FunctionTimer CreateUnscaled(Action action, float time)
    {
        GameObject g = new GameObject("Function Timer", typeof(MonoHook));

        FunctionTimer funcTimer = new FunctionTimer(action, time, g);
        g.GetComponent<MonoHook>().timerupdate = funcTimer.UpdateUnscaled;

        return funcTimer;
    }

    float localTime;
    Action funcAction;
    GameObject monoObject;

    private FunctionTimer(Action action, float time, GameObject g)
    {
        funcAction = action;
        localTime = time;
        monoObject = g;
    }

    void update()
    {
        localTime -= Time.deltaTime;
        if (localTime <= 0)
        {
            if (funcAction != null)
                funcAction();
            else
                Debug.Log("No function");

            destroySelf();
        }
    }

    void UpdateUnscaled()
    {
        localTime -= Time.unscaledDeltaTime;
        if (localTime <= 0)
        {
            if (funcAction != null)
                funcAction();
            else
                Debug.Log("No function");

            destroySelf();
        }
    }

    void destroySelf()
    {
        UnityEngine.Object.Destroy(monoObject);
    }

    public class MonoHook : MonoBehaviour
    {
        public Action timerupdate;

        private void Update()
        {
            timerupdate();
        }
    }
}

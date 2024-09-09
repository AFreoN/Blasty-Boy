using UnityEngine;
using System;

public class Lerper
{
    #region Lerp Create Static Methods
    public static Lerper Create(Transform transform, Transform target, float lerpSpeed, Action action)
    {
        return Create(transform, target.position, lerpSpeed, action);
    }

    public static Lerper Create(Transform transform, Transform target, float lerpSpeed)
    {
        return Create(transform, target.position, lerpSpeed, null);
    }

    public static Lerper Create(Transform transform, Vector3 targetPosition, float lerpSpeed)

    {
        return Create(transform, targetPosition, lerpSpeed, null);
    }
    public static Lerper Create(Transform transform, Vector3 targetPosition, float lerpSpeed, Action action)
    {
        GameObject g = new GameObject("Lerper", typeof(MonoHook));

        Lerper lerper = new Lerper(transform, targetPosition, lerpSpeed, action, g);
        g.GetComponent<MonoHook>().timerupdate = lerper.Update;

        return lerper;
    }
    #endregion

    #region LinearLerp Create Static Methdos
    public static Lerper CreateLinearLerp(Transform transform,Transform target, float elapsedTime, Action action)
    {
        return CreateLinearLerp(transform, target.position, elapsedTime, action);
    }

    public static Lerper CreateLinearLerp(Transform transform, Transform target, float elapsedTime)
    {
        return CreateLinearLerp(transform, target.position, elapsedTime, null);
    }

    public static Lerper CreateLinearLerp(Transform transform, Vector3 targetPosition, float elapsedTime)
    {
        return CreateLinearLerp(transform, targetPosition, elapsedTime, null);
    }

    public static Lerper CreateLinearLerp(Transform transform, Vector3 targetPosition, float elapsedTime, Action action)
    {
        GameObject g = new GameObject("Linear Lerper", typeof(MonoHook));

        Lerper lerper = new Lerper(transform, targetPosition, elapsedTime, action,g,  true);
        g.GetComponent<MonoHook>().timerupdate = lerper.LinearUpdate;

        return lerper;
    }
    #endregion

    Transform transform;
    Vector3 targetPosition;
    float lerpSpeed;
    Action action;
    GameObject monoObject;
    bool isPaused = false;

    Vector3 startPosition;
    float elapsedTime;
    float temp = 0;

    //Constructor for lerper class
    private Lerper(Transform _transform, Vector3 _targetPosition, float _lerpSpeed, Action _action, GameObject _monoObject, bool linear = false)
    {
        transform = _transform;
        targetPosition = _targetPosition;

        if (linear == false)
            lerpSpeed = _lerpSpeed;
        else
        {
            startPosition = _transform.position;
            elapsedTime = _lerpSpeed;
            temp = 0;
        }

        action = _action;
        monoObject = _monoObject;
    }

    //Non linear lerp update
    void Update()
    {
        if (isPaused)
            return;

        if(Vector3.Distance(transform.position, targetPosition) < .1f)
        {
            transform.position = targetPosition;
            action?.Invoke();

            DestroySelf();
        }
        transform.position = Vector3.Lerp(transform.position, targetPosition, lerpSpeed);
    }

    //Linear lerp update
    void LinearUpdate()
    {
        if (isPaused)
            return;

        Debug.Log(isPaused);

        temp += Time.deltaTime;
        if(Vector3.Distance(transform.position, targetPosition) < .1f && temp >= elapsedTime)
        {
            transform.position = targetPosition;
            action?.Invoke();

            DestroySelf();
        }

        float LSpeed = Mathf.Clamp(temp / elapsedTime, 0, 1);
        transform.position = Vector3.Lerp(startPosition, targetPosition, LSpeed);
    }

    public void PauseLerp() => isPaused = true;
    public void ResumeLerp() => isPaused = false;

    public void DestroySelf()
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

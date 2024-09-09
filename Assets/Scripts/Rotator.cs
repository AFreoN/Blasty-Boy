using UnityEngine;
using CustomExtensions;

public class Rotator : MonoBehaviour
{
    float duration = .5f;
    float rotateAngle = 90;
    float initAngle = 0;

    float temp = 0;
    bool startRotation = false;

    public void Initialize(float _duration, float _startAngle, float _endAngle)
    {
        duration = _duration;
        initAngle = _startAngle;
        rotateAngle = _endAngle;
        temp = 0;

        startRotation = true;
    }

    void Update()
    {
        if (startRotation == false)
            return;

        temp += Time.deltaTime / duration;

        float currentAngle = initAngle + temp * rotateAngle;
        Quaternion finalRotation = Quaternion.Euler(transform.localEulerAngles.ReplaceX(currentAngle));

        transform.rotation = Quaternion.Lerp(transform.rotation, finalRotation, .2f);
        if (temp >= 1)
        {
            transform.rotation = Quaternion.Euler(transform.localEulerAngles.ReplaceX(initAngle + rotateAngle));
            Destroy(this);
        }
    }
}

using UnityEngine;

public class CameraController : MonoBehaviour
{
    public static CameraController instance { get; private set; }
    public static Camera mainCamera = null;

    [SerializeField] Transform target = null;
    [SerializeField][Range(0,1)] float lerpSpeed = .2f;
    Vector3 offset = Vector3.zero;

    bool shouldShake = false;
    float power = 0;
    float shakeDuration = 0;

    bool won = false;

    private void Awake()
    {
        instance = this;
        mainCamera = Camera.main;
    }

    private void Start()
    {
        offset = transform.position - target.position;
    }

    private void LateUpdate()
    {
        if(won)
        {
            Vector3 finalPos = offset + target.position;
            float lerp = lerpSpeed;

            if(shouldShake)
            {
                shakeDuration -= Time.deltaTime;
                if (shakeDuration <= 0)
                    shouldShake = false;
                finalPos += Random.insideUnitSphere * power;
                lerp = .9f;
            }

            transform.position = Vector3.Lerp(transform.position, finalPos, lerp);
            Quaternion finalRotation = Quaternion.LookRotation(((target.position + Vector3.up) - transform.position).normalized);
            transform.rotation = Quaternion.Lerp(transform.rotation, finalRotation, lerpSpeed * .5f);
            return;
        }
        if(shouldShake == false)
            transform.position = Vector3.Lerp(transform.position, offset + target.position, lerpSpeed);
        else
        {
            Vector3 finalPos = offset + target.position + Random.insideUnitSphere * power;
            transform.position = Vector3.Lerp(transform.position, finalPos, .9f);
            shakeDuration -= Time.deltaTime;
            if (shakeDuration <= 0)
                shouldShake = false;
        }
    }

    public void StartShaking(float _power, float _time)
    {
        power = _power;
        shakeDuration = _time;
        shouldShake = true;
    }

    public void GameWon()
    {
        offset = offset * .9f;
        won = true;
    }
}

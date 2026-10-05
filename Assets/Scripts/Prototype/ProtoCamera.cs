using UnityEngine;

// Gameplay camera with trauma-based shake, FOV punches and a spring "kick". Runs on unscaled time so shake
// still reads during hit-stop freezes. Its framing is anchored to the current rooftop and follows the ninja
// along the chase between rooftops.
public class ProtoCamera : MonoBehaviour
{
    public static ProtoCamera instance { get; private set; }

    [SerializeField] float maxShakeOffset = 0.45f;
    [SerializeField] float maxShakeAngle = 3.5f;
    [SerializeField] float traumaDecay = 1.8f;
    [SerializeField] float shakeFrequency = 24f;
    [SerializeField] float aimZoom = 4f;
    [SerializeField] float kickStiffness = 180f;
    [SerializeField] float kickDamping = 16f;

    const float springStep = 1f / 240f;

    [SerializeField] float followSharpness = 5f;
    Transform follow;
    Vector3 anchor, anchorTarget;

    Camera cam;
    Vector3 basePosition;
    Quaternion baseRotation;
    float baseFov;
    float trauma;
    float fovPunch;
    float zoom, zoomTarget;
    Vector3 kick, kickVelocity;
    float seed;

    private void Awake()
    {
        instance = this;
        cam = GetComponent<Camera>();
        basePosition = transform.position;
        baseRotation = transform.rotation;
        baseFov = cam.fieldOfView;
        seed = Random.value * 100f;
    }

    public void Shake(float amount) => trauma = Mathf.Clamp01(trauma + amount);

    // Positive degrees widen the view (impact), negative zooms in.
    public void FovPunch(float degrees)
    {
        if (Mathf.Abs(degrees) > Mathf.Abs(fovPunch))
            fovPunch = degrees;
    }

    public void Kick(Vector3 worldOffset) => kickVelocity += worldOffset * 10f;

    public void SetAimZoom(bool on) => zoomTarget = on ? 1f : 0f;

    // Track a moving target (the ninja during a chase) along the rooftop axis.
    public void Follow(Transform target) => follow = target;

    // Settle on a rooftop: the framing is the scene's original framing, shifted to this zone's origin.
    public void Hold(Vector3 zoneOrigin)
    {
        follow = null;
        anchorTarget = new Vector3(0f, zoneOrigin.y, zoneOrigin.z);
    }

    private void LateUpdate()
    {
        float dt = Time.unscaledDeltaTime;

        if (follow != null)
            anchorTarget = new Vector3(0f, follow.position.y, follow.position.z);
        anchor = Vector3.Lerp(anchor, anchorTarget, 1f - Mathf.Exp(-followSharpness * dt));

        trauma = Mathf.Max(0f, trauma - traumaDecay * dt);
        fovPunch = Mathf.Lerp(fovPunch, 0f, 1f - Mathf.Exp(-9f * dt));
        zoom = Mathf.Lerp(zoom, zoomTarget, 1f - Mathf.Exp(-8f * dt));

        // Fixed small sub-steps keep the stiff spring stable through long frames (editor stalls, device hitches).
        for (float left = Mathf.Min(dt, 0.1f); left > 0f; left -= springStep)
        {
            float h = Mathf.Min(springStep, left);
            kickVelocity += (-kick * kickStiffness - kickVelocity * kickDamping) * h;
            kick += kickVelocity * h;
        }
        kick = Vector3.ClampMagnitude(kick, 1f);

        float shake = trauma * trauma;
        float t = Time.unscaledTime * shakeFrequency;
        Vector3 offset = new Vector3(Noise(t, 0f), Noise(t, 1f), Noise(t, 2f)) * (maxShakeOffset * shake);
        Quaternion tilt = Quaternion.Euler(Noise(t, 3f) * maxShakeAngle * shake, Noise(t, 4f) * maxShakeAngle * shake, Noise(t, 5f) * maxShakeAngle * shake);

        transform.position = basePosition + anchor + offset + kick;
        transform.rotation = baseRotation * tilt;
        cam.fieldOfView = baseFov + fovPunch - zoom * aimZoom;
    }

    float Noise(float t, float channel) => Mathf.PerlinNoise(seed + channel * 17.3f, t) * 2f - 1f;
}

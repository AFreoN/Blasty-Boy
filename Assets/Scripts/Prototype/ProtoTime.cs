using UnityEngine;

// Single owner of Time.timeScale for the prototype: bullet-time while aiming, hit-stop on kills and short slow-mo beats.
// Other prototype scripts ask this class instead of writing Time.timeScale themselves.
[DefaultExecutionOrder(-200)]
public class ProtoTime : MonoBehaviour
{
    public static ProtoTime instance { get; private set; }

    [SerializeField] float blendSpeed = 14f;
    [SerializeField] float hitStopScale = 0.03f;

    float aimTarget = 1f;
    float current = 1f;
    float hitStopUntil = 0f;
    float slowMoUntil = 0f;
    float slowMoScale = 1f;

    private void Awake()
    {
        instance = this;
        Apply(1f);
    }

    private void OnDestroy()
    {
        if (instance == this)
            Apply(1f);
    }

    public void SetAimScale(float scale, bool instant = false)
    {
        aimTarget = Mathf.Clamp(scale, 0.05f, 1f);
        if (instant)
            current = aimTarget;
    }

    public void HitStop(float duration)
    {
        hitStopUntil = Mathf.Max(hitStopUntil, Time.unscaledTime + duration);
    }

    public void SlowMo(float scale, float duration)
    {
        if (Time.unscaledTime > slowMoUntil || scale < slowMoScale)
            slowMoScale = scale;
        slowMoUntil = Mathf.Max(slowMoUntil, Time.unscaledTime + duration);
    }

    public void ResetAll()
    {
        aimTarget = current = 1f;
        hitStopUntil = slowMoUntil = 0f;
        Apply(1f);
    }

    private void Update()
    {
        current = Mathf.Lerp(current, aimTarget, 1f - Mathf.Exp(-blendSpeed * Time.unscaledDeltaTime));

        float scale = current;
        if (Time.unscaledTime < slowMoUntil)
            scale = Mathf.Min(scale, slowMoScale);
        if (Time.unscaledTime < hitStopUntil)
            scale = hitStopScale;

        Apply(scale);
    }

    static void Apply(float scale)
    {
        Time.timeScale = scale;
        Time.fixedDeltaTime = 0.02f * scale;
    }
}

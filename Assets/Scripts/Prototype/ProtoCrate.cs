using UnityEngine;

// Rooftop cover (an HVAC unit): a blade that touches it sticks in and stops. This is what makes straight throws fail
// and the bend matter. Heavy cover only wobbles when hit; an explosion still sends it flying.
public class ProtoCrate : ProtoTarget
{
    [SerializeField] float bladeImpulse = 4f;
    [SerializeField] bool heavy = true;

    Collider box;
    Rigidbody body;

    public override bool IsHittable => transform.position.y > -1f;
    public override float HitRadius => 0.5f;
    public override bool CountsTowardAccuracy => false;
    public override Vector3 HitCenter => box.bounds.center;
    public override Vector3 MarkerPosition => box.bounds.center + Vector3.up * (box.bounds.extents.y + 0.5f);

    private void Awake()
    {
        box = GetComponent<Collider>();
        body = GetComponent<Rigidbody>();
        body.isKinematic = heavy;
        baseScale = transform.localScale;
    }

    Vector3 baseScale;
    float wobble;

    private void Update()
    {
        if (wobble <= 0f)
            return;
        wobble = Mathf.Max(0f, wobble - Time.deltaTime * 3f);
        float w = Mathf.Sin(wobble * 40f) * wobble * 0.06f;
        transform.localScale = new Vector3(baseScale.x * (1f + w), baseScale.y * (1f - w), baseScale.z * (1f + w));
    }

    public override bool Intersects(Vector3 a, Vector3 b, float bladeRadius, out float t, float lookAhead = 0f)
    {
        t = 0f;
        Bounds bounds = box.bounds;
        float y = (a.y + b.y) * 0.5f;
        if (y < bounds.min.y || y > bounds.max.y)
            return false;

        Vector2 min = new Vector2(bounds.min.x - bladeRadius, bounds.min.z - bladeRadius);
        Vector2 max = new Vector2(bounds.max.x + bladeRadius, bounds.max.z + bladeRadius);
        return SegmentBox(new Vector2(a.x, a.z), new Vector2(b.x, b.z), min, max, out t);
    }

    public override PredictState Predict(Vector3 bladeDirection) => PredictState.Blocked;

    public override HitOutcome OnBladeHit(ProtoBlade blade, Vector3 point, Vector3 bladeDirection)
    {
        blade.StickInto(transform, point);
        if (body.isKinematic)
        {
            wobble = 1f;
            ProtoFX.Clang(point);
        }
        else
        {
            body.AddForce(bladeDirection.normalized * bladeImpulse + Vector3.up * 1.5f, ForceMode.VelocityChange);
            body.AddTorque(Random.insideUnitSphere * 8f, ForceMode.VelocityChange);
        }
        ProtoGame.instance.OnCrateHit(point);
        return HitOutcome.Blocked;
    }

    public void Blast(Vector3 center, float impulse)
    {
        body.isKinematic = false;
        transform.localScale = baseScale;
        wobble = 0f;
        Vector3 away = (box.bounds.center - center).normalized;
        body.AddForce(away * impulse + Vector3.up * impulse * 0.6f, ForceMode.VelocityChange);
        body.AddTorque(Random.insideUnitSphere * 12f, ForceMode.VelocityChange);
    }

    // Standing cover blocks goons too; once an explosion sends it flying it's out of the way.
    public override bool BlocksWalking(out Bounds footprint)
    {
        footprint = box.bounds;
        return footprint.min.y < 0.5f && footprint.max.y > 0f;
    }
}

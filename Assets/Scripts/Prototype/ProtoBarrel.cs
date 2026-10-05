using System.Collections.Generic;
using UnityEngine;
using CustomExtensions;

// Explosive barrel: every enemy in the blast is credited to the blade that set it off, so one throw through a
// barrel can become a huge multi-kill. Nearby barrels chain.
public class ProtoBarrel : ProtoTarget
{
    [SerializeField] float hitRadius = 0.7f;
    [SerializeField] float blastRadius = 3.4f;
    [SerializeField] float blastLaunch = 16f;
    [SerializeField] float crateImpulse = 10f;
    [SerializeField] float chainDelay = 0.12f;

    public override bool IsHittable => !exploded;
    public override float HitRadius => hitRadius;
    public override Vector3 MarkerPosition => transform.position + Vector3.up * 2.1f;
    public float BlastRadius => blastRadius;

    bool exploded;
    PredictState predicted;
    Vector3 baseScale;
    Collider shape;

    private void Awake()
    {
        baseScale = transform.localScale;
        shape = GetComponent<Collider>();
    }

    private void Update()
    {
        // Predicted barrels throb so the player notices the opportunity.
        float pulse = predicted == PredictState.Hit ? 1f + 0.08f * Mathf.Sin(Time.unscaledTime * 20f) : 1f;
        transform.localScale = baseScale * pulse;
    }

    public override bool BlocksWalking(out Bounds footprint)
    {
        footprint = shape.bounds;
        return !exploded;
    }

    public override PredictState Predict(Vector3 bladeDirection) => PredictState.Hit;
    public override void SetPredicted(PredictState state) => predicted = state;

    public void CollectVictims(List<ProtoEnemy> into, float lookAhead = 0f)
    {
        foreach (ProtoTarget t in all)
        {
            if (t is ProtoEnemy e && e.IsHittable && InBlast(e.transform.position + e.Velocity * lookAhead))
                into.Add(e);
        }
    }

    public override HitOutcome OnBladeHit(ProtoBlade blade, Vector3 point, Vector3 bladeDirection)
    {
        Explode(blade);
        return HitOutcome.Exploded;
    }

    public void Explode(ProtoBlade credit)
    {
        if (exploded)
            return;
        exploded = true;

        Vector3 center = transform.position + Vector3.up * 0.7f;
        ProtoGame.instance.OnExplosion(center, blastRadius);

        var victims = new List<ProtoEnemy>();
        CollectVictims(victims);
        foreach (ProtoEnemy e in victims)
        {
            Vector3 away = (e.transform.position - center).ReplaceY(0f);
            if (away.sqrMagnitude < 0.01f)
                away = Random.insideUnitCircle;
            e.Kill(away.normalized, blastLaunch);
            if (credit != null)
                credit.RegisterKill(e, e.ChestPosition);
            else
                ProtoGame.instance.OnKill(null, e, 1, e.ChestPosition);
        }

        foreach (ProtoTarget t in all.ToArray())
        {
            if (t == this || !InBlast(t.transform.position))
                continue;
            if (t is ProtoCrate crate)
                crate.Blast(center, crateImpulse);
            else if (t is ProtoBarrel other && !other.exploded)
                FunctionTimer.Create(() => { if (other != null) other.Explode(credit); }, chainDelay);
        }

        Destroy(gameObject);
    }

    bool InBlast(Vector3 position) => (position - transform.position).ReplaceY(0f).sqrMagnitude <= blastRadius * blastRadius;
}

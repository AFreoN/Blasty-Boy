using System.Collections.Generic;
using UnityEngine;

public enum HitOutcome { Ignored, Killed, Blocked, Penalty, Exploded }
public enum PredictState { None, Hit, Blocked, Penalty }

// Anything a prototype blade can hit. Hits are resolved analytically along the blade's curve (not by physics
// triggers), so the aim preview and the real throw always agree.
public abstract class ProtoTarget : MonoBehaviour
{
    public static readonly List<ProtoTarget> all = new List<ProtoTarget>();

    public abstract bool IsHittable { get; }
    public abstract float HitRadius { get; }
    public virtual Vector3 HitCenter => transform.position;
    // Current ground velocity; the aim preview uses it to lead moving targets.
    public virtual Vector3 Velocity => Vector3.zero;
    public virtual Vector3 MarkerPosition => transform.position + Vector3.up * 2.3f;

    public abstract PredictState Predict(Vector3 bladeDirection);
    public abstract HitOutcome OnBladeHit(ProtoBlade blade, Vector3 point, Vector3 bladeDirection);
    public virtual void SetPredicted(PredictState state) { }

    protected virtual void OnEnable() => all.Add(this);
    protected virtual void OnDisable() => all.Remove(this);

    // Segment vs. vertical cylinder, tested in XZ. t is where along a->b the closest approach happens.
    // lookAhead (seconds) tests against where the target will be if it keeps moving.
    public virtual bool Intersects(Vector3 a, Vector3 b, float bladeRadius, out float t, float lookAhead = 0f)
    {
        Vector3 c = HitCenter + Velocity * lookAhead;
        Vector2 A = new Vector2(a.x, a.z), B = new Vector2(b.x, b.z), C = new Vector2(c.x, c.z);
        Vector2 ab = B - A;
        float len2 = ab.sqrMagnitude;
        t = len2 > 1e-6f ? Mathf.Clamp01(Vector2.Dot(C - A, ab) / len2) : 0f;
        float r = HitRadius + bladeRadius;
        return (A + ab * t - C).sqrMagnitude <= r * r;
    }
}

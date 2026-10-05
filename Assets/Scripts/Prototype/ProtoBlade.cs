using System.Collections.Generic;
using UnityEngine;

// A thrown blade. Flies kinematically along ProtoCurve, pierces everything it can kill, and gets bigger and hotter
// with each kill so a multi-kill reads at a glance.
public class ProtoBlade : MonoBehaviour
{
    [SerializeField] Transform mesh = null;
    [SerializeField] TrailRenderer trail = null;
    [SerializeField] float spinSpeed = 1500f;
    [SerializeField] float radius = 0.15f;
    [SerializeField] float growPerKill = 0.14f;
    [SerializeField] float maxStep = 0.35f;

    static readonly Color[] heat =
    {
        new Color(0.75f, 0.95f, 1f),
        new Color(1f, 0.95f, 0.35f),
        new Color(1f, 0.6f, 0.15f),
        new Color(1f, 0.25f, 0.2f),
        new Color(1f, 0.3f, 0.9f),
    };

    public int Kills { get; private set; }
    public int PredictedKills { get; set; }
    // Targets struck that count toward accuracy (kills, shield blocks, barrels...).
    public int Hits { get; private set; }
    public bool IsFeverBlade { get; set; }
    public bool IsStuck { get; private set; }
    public float Radius => radius;

    Vector3 from, to;
    float bend, hook, length, speed, s;
    bool flying, finished;
    ProtoBoss board;
    Vector3 baseScale;
    float baseTrailWidth;
    readonly List<ProtoTarget> touched = new List<ProtoTarget>();
    readonly List<KeyValuePair<float, ProtoTarget>> pending = new List<KeyValuePair<float, ProtoTarget>>();

    public void Launch(Vector3 from, Vector3 to, float bend, float hook, float speed, ProtoBoss board)
    {
        this.from = from;
        this.to = to;
        this.bend = bend;
        this.hook = hook;
        this.speed = speed;
        this.board = board;
        length = ProtoCurve.ApproxLength(from, to, bend, hook);
        s = 0f;
        flying = true;
        baseScale = transform.localScale;
        transform.position = from;
        transform.rotation = Quaternion.LookRotation(ProtoCurve.Tangent(from, to, bend, hook, 0f));
        if (trail != null)
        {
            baseTrailWidth = trail.widthMultiplier;
            trail.Clear();
            ApplyHeat();
        }
    }

    private void Update()
    {
        if (mesh != null && !IsStuck)
            mesh.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.Self);

        if (!flying)
            return;

        // Sub-step so each hit test segment stays close to the curve even on a long frame.
        float advance = speed * Time.deltaTime / length;
        int steps = Mathf.Max(1, Mathf.CeilToInt(advance * length / maxStep));
        // A kill ends the frame's movement so the hit-stop freezes the blade on the body it just went through.
        for (int i = 0; i < steps && flying; i++)
            if (Step(advance / steps))
                break;
    }

    bool Step(float advance)
    {
        bool killed = false;
        float prevS = s;
        s = Mathf.Min(1f, s + advance);
        Vector3 a = transform.position;
        Vector3 b = ProtoCurve.Point(from, to, bend, hook, s);

        pending.Clear();
        foreach (ProtoTarget target in ProtoTarget.all)
        {
            if (!target.IsHittable || touched.Contains(target))
                continue;
            if (target.Intersects(a, b, radius, out float t))
                pending.Add(new KeyValuePair<float, ProtoTarget>(t, target));
        }
        pending.Sort((x, y) => x.Key.CompareTo(y.Key));

        foreach (var hit in pending)
        {
            ProtoTarget target = hit.Value;
            if (target == null || !target.IsHittable)
                continue;   // e.g. already caught in an explosion this frame

            touched.Add(target);
            float hitS = Mathf.Lerp(prevS, s, hit.Key);
            Vector3 point = Vector3.Lerp(a, b, hit.Key);
            Vector3 dir = ProtoCurve.Tangent(from, to, bend, hook, hitS);

            HitOutcome outcome = target.OnBladeHit(this, point, dir);
            if (target.CountsTowardAccuracy)
                Hits++;
            if (outcome == HitOutcome.Killed)
            {
                RegisterKill(target, point);
                killed = true;
            }
            else if (outcome == HitOutcome.Exploded)
            {
                killed = true;
            }
            else if (outcome == HitOutcome.Blocked)
            {
                transform.position = point;
                if (!IsStuck)
                    Deflect(dir);
                return true;
            }
        }

        transform.position = b;
        Vector3 tangent = ProtoCurve.Tangent(from, to, bend, hook, s);
        transform.rotation = Quaternion.LookRotation(tangent);

        if (s >= 1f)
        {
            flying = false;
            if (board != null && !board.Defeated)
                board.Catch(this, tangent);
            else
                Consume();
        }
        return killed;
    }

    public void RegisterKill(ProtoTarget target, Vector3 point)
    {
        Kills++;
        transform.localScale = baseScale * (1f + growPerKill * Mathf.Min(Kills, 6));
        ApplyHeat();
        ProtoGame.instance.OnKill(this, target, Kills, point);
    }

    public void StickInto(Transform parent, Vector3 point)
    {
        flying = false;
        IsStuck = true;
        transform.position = point;
        transform.SetParent(parent, true);
        if (trail != null)
            trail.emitting = false;
        Finish();
    }

    // Reached the boss: the impact is the boss's to show, the blade just disappears.
    public void Consume()
    {
        flying = false;
        Finish();
        Destroy(gameObject);
    }

    void Deflect(Vector3 dir)
    {
        flying = false;
        Rigidbody rb = gameObject.AddComponent<Rigidbody>();
        rb.linearVelocity = -dir * 5f + Vector3.up * 7f + Random.insideUnitSphere * 2f;
        rb.angularVelocity = Random.insideUnitSphere * 30f;
        if (trail != null)
            trail.emitting = false;
        Destroy(gameObject, 1.5f);
        Finish();
    }

    void Finish()
    {
        if (finished)
            return;
        finished = true;
        ProtoGame.instance.OnBladeFinished(this);
    }

    void ApplyHeat()
    {
        if (trail == null)
            return;
        Color c = heat[Mathf.Min(Kills, heat.Length - 1)];
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(c, 0.25f), new GradientColorKey(c, 1f) },
                  new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.4f), new GradientAlphaKey(0f, 1f) });
        trail.colorGradient = g;
        trail.widthMultiplier = baseTrailWidth * (1f + 0.25f * Mathf.Min(Kills, 6));
    }
}

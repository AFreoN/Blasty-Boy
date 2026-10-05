using System.Collections.Generic;
using UnityEngine;
using CustomExtensions;

// Civilian held by nearby guards. Knocking out every guard frees them (bonus); hitting the hostage with a blade costs a
// heart; letting the danger timer run out also costs a heart.
public class ProtoHostage : ProtoTarget
{
    [SerializeField] float hitRadius = 0.55f;
    [SerializeField] float captorRadius = 3.2f;
    [SerializeField] float dangerTime = 16f;
    [SerializeField] float escapeSpeed = 5f;

    static readonly int emissionId = Shader.PropertyToID("_EmissionColor");

    enum State { Captive, Freed, Hurt }

    public override bool IsHittable => state == State.Captive;
    public override float HitRadius => hitRadius;
    public override bool CountsTowardAccuracy => false;
    public override Vector3 MarkerPosition => transform.position + Vector3.up * 2.1f;
    public float Danger01 => state == State.Captive ? Mathf.Clamp01(dangerTimer / dangerTime) : 0f;
    public float DangerSeconds => state == State.Captive ? Mathf.Max(0f, dangerTimer) : 0f;
    public bool IsResolved => state != State.Captive;

    State state = State.Captive;
    readonly List<ProtoEnemy> captors = new List<ProtoEnemy>();
    Animator anim;
    Renderer[] renderers;
    MaterialPropertyBlock block;
    float dangerTimer, flash, escapeTimer;
    PredictState predicted;

    private void Awake()
    {
        anim = GetComponentInChildren<Animator>();
        renderers = GetComponentsInChildren<Renderer>();
        block = new MaterialPropertyBlock();
        dangerTimer = dangerTime;
    }

    private void Start()
    {
        transform.rotation = Quaternion.LookRotation(Vector3.back);
        anim.Play("Captive");
    }

    // Called by ProtoGame once the wave's enemies are spawned.
    public void CollectCaptors(IEnumerable<ProtoEnemy> enemies)
    {
        // Guards are still up in the air for their drop-in at this point, so compare on the ground plane.
        captors.Clear();
        foreach (ProtoEnemy e in enemies)
        {
            if (e.type == ProtoEnemyType.Guard && (e.transform.position - transform.position).ReplaceY(0f).magnitude <= captorRadius)
                captors.Add(e);
        }
    }

    private void Update()
    {
        switch (state)
        {
            case State.Captive:
                if (captors.Count > 0 && captors.TrueForAll(c => c == null || !c.IsAlive))
                {
                    Free();
                    break;
                }
                if (ProtoGame.IsPlaying && captors.Exists(c => c != null && c.IsLanded))
                {
                    dangerTimer -= Time.deltaTime;
                    if (dangerTimer <= 0f)
                        Lose();
                }
                break;

            case State.Freed:
                escapeTimer += Time.deltaTime;
                if (escapeTimer > 1.2f)
                {
                    if (escapeTimer - Time.deltaTime <= 1.2f)
                        anim.CrossFadeInFixedTime("Run", 0.1f);
                    float side = transform.position.x >= 0f ? 1f : -1f;
                    Vector3 dir = new Vector3(side, 0f, -0.3f).normalized;
                    transform.position += dir * escapeSpeed * Time.deltaTime;
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), 1f - Mathf.Exp(-10f * Time.deltaTime));
                }
                if (escapeTimer > 3f)
                {
                    ProtoFX.Poof(transform.position + Vector3.up, 0.8f);
                    Destroy(gameObject);
                }
                break;
        }

        ApplyVisuals();
    }

    void Free()
    {
        state = State.Freed;
        anim.CrossFadeInFixedTime("Cheer", 0.1f);
        flash = 1f;
        ProtoGame.instance.OnHostageFreed(this);
    }

    void Lose()
    {
        state = State.Hurt;
        ProtoGame.instance.OnHostageLost(this);
        Knockdown(Vector3.back, 6f);
    }

    public override PredictState Predict(Vector3 bladeDirection) => PredictState.Penalty;

    public override HitOutcome OnBladeHit(ProtoBlade blade, Vector3 point, Vector3 bladeDirection)
    {
        state = State.Hurt;
        ProtoGame.instance.OnHostageHit(this, point);
        Knockdown(bladeDirection, 7f);
        return HitOutcome.Penalty;
    }

    public override void SetPredicted(PredictState state) => predicted = state;

    void Knockdown(Vector3 direction, float power)
    {
        flash = 1f;
        anim.CrossFadeInFixedTime("Hit", 0.05f);
        float scale = transform.localScale.x;
        var col = gameObject.AddComponent<CapsuleCollider>();
        col.center = new Vector3(0f, 0.9f / scale, 0f);
        col.height = 1.6f / scale;
        col.radius = 0.35f / scale;
        var body = gameObject.AddComponent<Rigidbody>();
        body.linearVelocity = direction.ReplaceY(0f).normalized * power + Vector3.up * power * 0.5f;
        body.angularVelocity = Random.insideUnitSphere * 8f;
        Destroy(gameObject, 3f);
    }

    void ApplyVisuals()
    {
        flash = Mathf.Max(0f, flash - Time.unscaledDeltaTime * 6f);
        Color glow = Color.white * flash;
        if (state == State.Captive && predicted == PredictState.Penalty)
            glow += new Color(1f, 0.1f, 0.1f) * (0.45f + 0.3f * Mathf.Sin(Time.unscaledTime * 22f));

        foreach (Renderer r in renderers)
        {
            r.GetPropertyBlock(block);
            block.SetColor(emissionId, glow);
            r.SetPropertyBlock(block);
        }
    }
}

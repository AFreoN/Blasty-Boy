using System.Collections;
using UnityEngine;
using CustomExtensions;

public enum ProtoEnemyType { Grunt, Shield, Runner, Guard, Armored }

// One of Big Bear's goons: drops in from the sky, walks at the player (around cover and barrels), punches when in range.
// Shield goons block frontal hits (the shield flies off and they stagger); a blade arriving from the side flanks them.
// Armored goons take two hits from any side: the first knocks the helmet off and the blade keeps going.
// Knockouts are cartoon launches: the whole goon becomes a tumbling rigid body.
public class ProtoEnemy : ProtoTarget
{
    [SerializeField] float hitRadius = 0.6f;
    [SerializeField] float attackRange = 1.7f;
    [SerializeField] float attackWindup = 0.6f;
    [SerializeField] float flankAngle = 40f;
    [SerializeField] float dropHeight = 14f;
    [SerializeField] float dropTime = 0.42f;
    [SerializeField] float launchSpeed = 13f;
    [SerializeField] float arenaHalfWidth = 5.6f;
    [Tooltip("How far around props a goon keeps its body, and how far ahead it looks for them")]
    [SerializeField] float bodyRadius = 0.4f;
    [SerializeField] float avoidLookAhead = 2.5f;
    [Tooltip("Shield held by Shield-type goons; attached to the left hand slot")]
    [SerializeField] GameObject shieldPrefab = null;

    static readonly int colorId = Shader.PropertyToID("_Color");
    static readonly int emissionId = Shader.PropertyToID("_EmissionColor");
    static readonly Color deadTint = new Color(0.6f, 0.6f, 0.65f);
    static readonly Color predictedGlow = new Color(1f, 0.8f, 0.1f);
    static readonly Color blockedGlow = new Color(1f, 0.15f, 0.1f);
    static readonly Color armorGlow = new Color(0.45f, 0.7f, 1f);

    enum State { Waiting, Dropping, Idle, Advancing, Attacking, Stunned, Dead }

    public ProtoEnemyType type { get; private set; }
    public bool IsAlive => state != State.Dead;
    public bool IsLanded => state != State.Waiting && state != State.Dropping;
    public bool WasFlanked { get; private set; }
    public Color BodyColor { get; private set; } = new Color(0.55f, 0.25f, 0.75f);
    public override bool IsHittable => state == State.Idle || state == State.Advancing || state == State.Attacking || state == State.Stunned;
    public override float HitRadius => hitRadius;
    public override Vector3 Velocity => state == State.Advancing ? velocity : state == State.Stunned ? stunVelocity : Vector3.zero;
    public override Vector3 MarkerPosition => transform.position + Vector3.up * 2.6f;
    public Vector3 ChestPosition => body != null ? body.worldCenterOfMass : transform.position + Vector3.up * 1.1f;

    State state = State.Waiting;
    float speed, timer;
    Vector3 landPosition, stunVelocity, velocity;
    Animator anim;
    Renderer[] renderers;
    MaterialPropertyBlock block;
    Transform shield;
    readonly System.Collections.Generic.List<SkinnedMeshRenderer> helmet = new System.Collections.Generic.List<SkinnedMeshRenderer>();
    Rigidbody body;
    string currentAnim;
    float flash, deadBlend, squashTime = 10f, baseScale = 1f;
    PredictState predicted;

    private void Awake()
    {
        anim = GetComponentInChildren<Animator>();
        renderers = GetComponentsInChildren<Renderer>();
        block = new MaterialPropertyBlock();
        baseScale = transform.localScale.x;
    }

    public void Init(ProtoEnemyType enemyType, float moveSpeed, float dropDelay)
    {
        type = enemyType;
        speed = moveSpeed;
        timer = dropDelay;

        landPosition = transform.position;
        transform.position = landPosition + Vector3.up * dropHeight;
        transform.rotation = Quaternion.LookRotation(Vector3.back);
        SetVisible(false);

        if (type == ProtoEnemyType.Runner)
            anim.speed = 1.5f;
        if (type == ProtoEnemyType.Shield)
            AttachShield();
        if (type == ProtoEnemyType.Armored)
        {
            foreach (SkinnedMeshRenderer r in GetComponentsInChildren<SkinnedMeshRenderer>())
                if (r.name.Contains("Helmet"))
                    helmet.Add(r);
        }
        ApplyVisuals();
    }

    bool Armored => helmet.Count > 0;

    private void Update()
    {
        float dt = Time.deltaTime;

        switch (state)
        {
            case State.Waiting:
                timer -= dt;
                if (timer <= 0f)
                {
                    state = State.Dropping;
                    timer = 0f;
                    SetVisible(true);
                    Play("Fall");
                }
                break;

            case State.Dropping:
                timer += dt;
                float k = Mathf.Clamp01(timer / dropTime);
                transform.position = landPosition + Vector3.up * dropHeight * (1f - k * k);
                if (k >= 1f)
                    Land();
                break;

            case State.Idle:
                Play(shield != null ? "Guard" : "Idle");
                if (ProtoGame.IsPlaying && type != ProtoEnemyType.Guard)
                    state = State.Advancing;
                break;

            case State.Advancing:
                if (!ProtoGame.IsPlaying)
                {
                    velocity = Vector3.zero;
                    Play("Idle");
                    break;
                }
                Advance(dt);
                break;

            case State.Attacking:
                if (!ProtoGame.IsPlaying)
                    break;
                timer -= dt;
                if (timer <= 0f)
                {
                    ProtoGame.instance.OnEnemyReachedPlayer(this);
                    Vanish();
                }
                break;

            case State.Stunned:
                timer -= dt;
                transform.position = PushOutOfProps(transform.position + stunVelocity * dt);
                stunVelocity = Vector3.Lerp(stunVelocity, Vector3.zero, 1f - Mathf.Exp(-8f * dt));
                if (timer <= 0f)
                    state = type == ProtoEnemyType.Guard ? State.Idle : State.Advancing;
                break;

            case State.Dead:
                if (transform.position.y < -12f)
                    Destroy(gameObject);
                break;
        }

        ApplyVisuals();
    }

    void Land()
    {
        transform.position = landPosition;
        state = State.Idle;
        squashTime = 0f;
        Play("Land", 0.02f);
        ProtoFX.Dust(landPosition, type == ProtoEnemyType.Shield ? 1.3f : 1f);
        ProtoAudio.Play(Sfx.Land, 0.6f, Random.Range(0.9f, 1.1f));
        if (ProtoCamera.instance != null)
            ProtoCamera.instance.Shake(0.06f);
    }

    void Advance(float dt)
    {
        Vector3 target = ProtoThrower.instance.transform.position;
        Vector3 to = (target - transform.position).ReplaceY(0f);
        float distance = to.magnitude;

        if (distance <= attackRange)
        {
            state = State.Attacking;
            timer = attackWindup;
            velocity = Vector3.zero;
            Play("Attack", 0.05f);
            return;
        }

        Vector3 dir = AvoidProps(to / distance);
        Vector3 next = transform.position + (dir + Separation() * 0.8f) * speed * dt;
        next.x = Mathf.Clamp(next.x, -arenaHalfWidth, arenaHalfWidth);
        next = PushOutOfProps(next);
        velocity = dt > 0f ? (next - transform.position) / dt : Vector3.zero;
        transform.position = next;
        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), 1f - Mathf.Exp(-10f * dt));
        Play(type == ProtoEnemyType.Runner ? "Run" : "Walk");
    }

    Vector3 Separation()
    {
        Vector3 push = Vector3.zero;
        foreach (ProtoTarget t in all)
        {
            if (t == this || !(t is ProtoEnemy || t is ProtoHostage) || !t.IsHittable)
                continue;
            Vector3 away = (transform.position - t.HitCenter).ReplaceY(0f);
            float d = away.magnitude;
            if (d > 0.001f && d < 1.1f)
                push += away / d * (1.1f - d);
        }
        return push;
    }

    // Props (cover, barrels) as roof footprints grown by the goon's radius.
    static bool WalkBlocker(ProtoTarget t, float radius, out Bounds box)
    {
        if (!t.BlocksWalking(out box))
            return false;
        box.Expand(new Vector3(radius * 2f, 0f, radius * 2f));
        return true;
    }

    // If a prop sits on the path ahead, veer to whichever side of it the goon is already on (or toward the middle of
    // the roof), so it walks around the prop rather than into it.
    Vector3 AvoidProps(Vector3 dir)
    {
        Vector3 p = transform.position;
        Vector2 a = new Vector2(p.x, p.z);
        Vector2 b = a + new Vector2(dir.x, dir.z) * avoidLookAhead;
        float nearest = float.MaxValue;
        Bounds blocker = default;
        foreach (ProtoTarget t in all)
        {
            if (!WalkBlocker(t, bodyRadius, out Bounds box))
                continue;
            if (SegmentBox(a, b, new Vector2(box.min.x, box.min.z), new Vector2(box.max.x, box.max.z), out float at) && at < nearest)
            {
                nearest = at;
                blocker = box;
            }
        }
        if (nearest == float.MaxValue)
            return dir;

        Vector3 side = new Vector3(-dir.z, 0f, dir.x);
        float s = Vector3.Dot(side, (p - blocker.center).ReplaceY(0f));
        if (Mathf.Abs(s) < 0.05f)
            s = Vector3.Dot(side, Vector3.left * p.x);
        return (dir + side * (s >= 0f ? 1f : -1f) * 2f).normalized;
    }

    // Never end a step inside a prop: slide out through its nearest face.
    Vector3 PushOutOfProps(Vector3 p)
    {
        foreach (ProtoTarget t in all)
        {
            if (!WalkBlocker(t, bodyRadius, out Bounds box))
                continue;
            if (p.x <= box.min.x || p.x >= box.max.x || p.z <= box.min.z || p.z >= box.max.z)
                continue;
            float left = p.x - box.min.x, right = box.max.x - p.x, front = p.z - box.min.z, back = box.max.z - p.z;
            float m = Mathf.Min(Mathf.Min(left, right), Mathf.Min(front, back));
            if (m == left) p.x = box.min.x;
            else if (m == right) p.x = box.max.x;
            else if (m == front) p.z = box.min.z;
            else p.z = box.max.z;
        }
        return p;
    }

    #region Blade interaction
    bool IsFlank(Vector3 bladeDirection)
    {
        Vector3 incoming = -bladeDirection.ReplaceY(0f).normalized;
        return Vector3.Angle(incoming, transform.forward.ReplaceY(0f)) >= flankAngle;
    }

    public override PredictState Predict(Vector3 bladeDirection)
    {
        if (Armored)
            return PredictState.Armor;
        if (shield != null && !IsFlank(bladeDirection))
            return PredictState.Blocked;
        return PredictState.Hit;
    }

    public override HitOutcome OnBladeHit(ProtoBlade blade, Vector3 point, Vector3 bladeDirection)
    {
        if (Armored)
        {
            PopHelmet(bladeDirection);
            ProtoGame.instance.OnArmorPopped(this, point);
            return HitOutcome.Armor;
        }
        if (shield != null && !IsFlank(bladeDirection))
        {
            BreakShield(bladeDirection);
            ProtoGame.instance.OnShieldBlocked(this, point);
            return HitOutcome.Blocked;
        }

        Kill(bladeDirection, launchSpeed, shield != null);
        return HitOutcome.Killed;
    }

    public override void SetPredicted(PredictState state) => predicted = state;

    public void Kill(Vector3 direction, float power, bool flanked = false)
    {
        if (state == State.Dead)
            return;

        state = State.Dead;
        WasFlanked = flanked;
        predicted = PredictState.None;
        flash = 1f;
        SetVisible(true);
        Play("Death", 0.02f);

        if (shield != null)
            DropShield(direction, 1f);

        // Cartoon launch: tumble away from the blade, high and spinning, and bounce off the roof.
        var col = gameObject.AddComponent<CapsuleCollider>();
        col.center = new Vector3(0f, 1f / baseScale, 0f);
        col.height = 1.8f / baseScale;
        col.radius = 0.4f / baseScale;
        body = gameObject.AddComponent<Rigidbody>();
        body.mass = 1f;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        Vector3 flat = direction.ReplaceY(0f).normalized;
        body.linearVelocity = flat * power + Vector3.up * power * 0.6f + Random.insideUnitSphere * 1.5f;
        body.angularVelocity = Vector3.Cross(Vector3.up, flat) * Random.Range(8f, 14f) + Random.insideUnitSphere * 6f;

        StartCoroutine(CleanupAfter(2.6f));
    }

    IEnumerator CleanupAfter(float delay)
    {
        yield return new WaitForSeconds(delay);
        ProtoFX.Poof(ChestPosition, 1.2f);
        ProtoAudio.Play(Sfx.Pop, 0.35f, Random.Range(0.8f, 1.0f));
        Destroy(gameObject);
    }

    // Reached the player: swap the goon for a puff of smoke instead of leaving him standing there.
    void Vanish()
    {
        state = State.Dead;
        ProtoFX.Poof(transform.position + Vector3.up, 1.4f);
        Destroy(gameObject);
    }

    // Boss down: remaining goons bail out.
    public void Scatter()
    {
        if (state == State.Dead)
            return;
        state = State.Dead;
        ProtoFX.Poof(transform.position + Vector3.up, 1.2f);
        Destroy(gameObject);
    }

    void AttachShield()
    {
        Transform hand = FindDeep(transform, "handslot.l");
        if (shieldPrefab != null && hand != null)
        {
            shield = Instantiate(shieldPrefab, hand).transform;
            shield.localPosition = Vector3.zero;
            shield.localRotation = Quaternion.identity;
        }
        else
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            shield = go.transform;
            shield.SetParent(transform, false);
            shield.localPosition = new Vector3(0f, 1.05f, 0.5f) / baseScale;
            shield.localRotation = Quaternion.Euler(90f, 0f, 0f);
            shield.localScale = new Vector3(1.05f, 0.06f, 1.35f) / baseScale;
        }
        foreach (var c in shield.GetComponentsInChildren<Collider>())
            Destroy(c);
    }

    // Swap the skinned helmet for a baked copy that flies off, leaving the bare head; then a short stagger.
    void PopHelmet(Vector3 bladeDirection)
    {
        Vector3 flat = bladeDirection.ReplaceY(0f).normalized;
        foreach (SkinnedMeshRenderer r in helmet)
        {
            var mesh = new Mesh();
            r.BakeMesh(mesh, true);
            var piece = new GameObject("Helmet");
            piece.transform.SetPositionAndRotation(r.transform.position, r.transform.rotation);
            piece.AddComponent<MeshFilter>().sharedMesh = mesh;
            piece.AddComponent<MeshRenderer>().sharedMaterials = r.sharedMaterials;
            piece.AddComponent<BoxCollider>();
            Rigidbody rb = piece.AddComponent<Rigidbody>();
            rb.mass = 0.4f;
            rb.linearVelocity = flat * 4f + Vector3.up * 8f + Random.insideUnitSphere * 1.5f;
            rb.angularVelocity = Random.insideUnitSphere * 20f;
            Destroy(piece, 2.5f);
            Destroy(mesh, 2.6f);
            Destroy(r.gameObject);
        }
        helmet.Clear();
        type = ProtoEnemyType.Grunt;
        state = State.Stunned;
        timer = 0.35f;
        stunVelocity = flat * 2.5f;
        flash = 1f;
        squashTime = 0f;
        Play("Hit", 0.02f);
    }

    void BreakShield(Vector3 bladeDirection)
    {
        DropShield(bladeDirection, 0.7f);
        type = ProtoEnemyType.Grunt;
        state = State.Stunned;
        timer = 0.5f;
        stunVelocity = bladeDirection.ReplaceY(0f).normalized * 4.5f;
        flash = 1f;
        squashTime = 0f;
        Play("Hit", 0.02f);
    }

    void DropShield(Vector3 direction, float strength)
    {
        shield.SetParent(null, true);
        shield.gameObject.AddComponent<BoxCollider>();
        Rigidbody rb = shield.gameObject.AddComponent<Rigidbody>();
        rb.mass = 0.5f;
        rb.linearVelocity = direction.ReplaceY(0f).normalized * 7f * strength + Vector3.up * 8f * strength + Random.insideUnitSphere * 2f;
        rb.angularVelocity = Random.insideUnitSphere * 25f;
        Destroy(shield.gameObject, 2.5f);
        shield = null;
    }
    #endregion

    void Play(string stateName, float fade = 0.12f)
    {
        if (currentAnim == stateName)
            return;
        currentAnim = stateName;
        anim.CrossFadeInFixedTime(stateName, fade);
    }

    void SetVisible(bool visible)
    {
        foreach (Renderer r in renderers)
            if (r != null) r.enabled = visible;
    }

    void ApplyVisuals()
    {
        float udt = Time.unscaledDeltaTime;
        flash = Mathf.Max(0f, flash - udt * 9f);
        if (state == State.Dead)
            deadBlend = Mathf.Min(1f, deadBlend + udt * 4f);

        Color glow = Color.white * flash * 0.9f;
        if (state != State.Dead && predicted != PredictState.None)
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 18f);
            Color c = predicted == PredictState.Hit ? predictedGlow : predicted == PredictState.Armor ? armorGlow : blockedGlow;
            glow += c * (0.35f + 0.35f * pulse);
        }

        Color tint = Color.Lerp(Color.white, deadTint, deadBlend);
        foreach (Renderer r in renderers)
        {
            if (r == null) continue;
            r.GetPropertyBlock(block);
            block.SetColor(colorId, tint);
            block.SetColor(emissionId, glow);
            r.SetPropertyBlock(block);
        }

        // Landing / stagger squash: a damped wobble in height with volume preserved in XZ.
        if (state != State.Dead && squashTime < 1.5f)
        {
            squashTime += udt;
            float a = 0.38f * Mathf.Exp(-6f * squashTime) * Mathf.Cos(18f * squashTime);
            transform.localScale = new Vector3(1f + a * 0.5f, 1f - a, 1f + a * 0.5f) * baseScale;
        }
    }

    public static Transform FindDeep(Transform root, string name)
    {
        if (root.name == name)
            return root;
        foreach (Transform child in root)
        {
            Transform found = FindDeep(child, name);
            if (found != null)
                return found;
        }
        return null;
    }
}

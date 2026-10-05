using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CustomExtensions;

public enum ControlMode { Flick, Stream }

// The ninja. One gesture: hold, slide sideways to bend, release.
//   Normal play is Flick: one blade per gesture, heavy bullet-time while aiming, a small quiver that reloads.
//   Fever ("Shadow Storm") switches to Stream for a few seconds: holding fires a fan of free blades the slide steers.
// While aiming, the full path is simulated and every target it will hit is highlighted in order.
// Between rooftops the ninja runs and leaps after the boss (ChaseTo).
public class ProtoThrower : MonoBehaviour
{
    public static ProtoThrower instance { get; private set; }
    public static ControlMode mode => instance != null && instance.InFever ? ControlMode.Stream : ControlMode.Flick;

    [Header("References")]
    [SerializeField] ProtoBlade bladePrefab = null;
    [SerializeField] ProtoBoss target = null;
    [SerializeField] LineRenderer aimLine = null;

    [Header("Curve")]
    [SerializeField] float maxBend = 6f;
    [SerializeField, Range(0, 1)] float hook = 0.5f;
    [Tooltip("Horizontal drag, as a fraction of screen width, that gives full bend")]
    [SerializeField] float dragForFullBend = 0.3f;
    [SerializeField] float bendExponent = 1.2f;
    [SerializeField] float launchHeight = 1.15f;
    [SerializeField] float launchForward = 0.8f;
    [SerializeField] float bladeSpeed = 32f;
    [SerializeField] int previewSegments = 48;

    [Header("Flick (normal play)")]
    [SerializeField] int flickQuiver = 3;
    [SerializeField] float flickReload = 1.0f;
    [SerializeField] float flickAimTimeScale = 0.25f;
    [Tooltip("Dragging down by this fraction of screen height cancels the throw")]
    [SerializeField] float cancelDrag = 0.1f;

    [Header("Fever (stream)")]
    [SerializeField] float feverFireInterval = 0.09f;
    [Tooltip("The two extra fever blades fan out by this fraction of max bend")]
    [SerializeField] float feverFanSpread = 0.3f;
    [SerializeField] float feverTimeScale = 0.9f;

    [Header("Chase")]
    [SerializeField] float chaseRunSpeed = 14f;
    [SerializeField] float chaseJumpTime = 0.85f;
    [SerializeField] float chaseJumpHeight = 4.5f;

    public int QuiverMax => flickQuiver;
    public float Quiver { get; private set; }
    public bool IsAiming => aiming;
    public bool IsCancelling => cancelling;
    public bool InFever => feverUntil > Time.time;
    public float Fever01 => InFever ? (feverUntil - Time.time) / feverDuration : 0f;
    public int PredictedKills { get; private set; }
    public IReadOnlyList<ProtoTarget> PredictedTargets => predictedTargets;
    public IReadOnlyList<PredictState> PredictedStates => predictedStates;
    public System.Action onThrow;

    Animator anim;
    bool aiming, cancelling, simulated, firedThisAim, aimingInFever;
    Vector2 pressPosition;
    float bend;
    float fireTimer;
    float animResetTimer;
    float lastThrowAnim;
    float feverUntil, feverDuration = 1f;
    string currentAnim;
    Material lineMaterial;
    readonly List<ProtoTarget> predictedTargets = new List<ProtoTarget>();
    readonly List<PredictState> predictedStates = new List<PredictState>();
    readonly List<Vector3> linePoints = new List<Vector3>();
    readonly List<KeyValuePair<float, ProtoTarget>> segmentHits = new List<KeyValuePair<float, ProtoTarget>>();
    readonly List<ProtoEnemy> blastVictims = new List<ProtoEnemy>();

    Vector3 From => new Vector3(transform.position.x, transform.position.y + launchHeight, transform.position.z + launchForward);
    Vector3 To => target.AimPoint;

    private void Awake()
    {
        instance = this;
        anim = GetComponentInChildren<Animator>();
        anim.updateMode = AnimatorUpdateMode.UnscaledTime;
        lineMaterial = aimLine.material;
        lineMaterial.mainTexture = ProtoArt.Dash;
        aimLine.enabled = false;
        Quiver = QuiverMax;
    }

    public void ResetQuiver() => Quiver = QuiverMax;

    public void StartFever(float duration)
    {
        feverDuration = duration;
        feverUntil = Time.time + duration;
    }

    private void Update()
    {
        if (Quiver < QuiverMax)
            Quiver = Mathf.Min(QuiverMax, Quiver + Time.deltaTime / flickReload);

        if (animResetTimer > 0f)
        {
            animResetTimer -= Time.unscaledDeltaTime;
            if (animResetTimer <= 0f)
                anim.speed = 1f;
        }

        if (!ProtoGame.IsPlaying)
        {
            if (aiming)
                EndAim();
            return;
        }

        // Fever starting or ending mid-gesture changes the rules, so the gesture ends; the next touch uses the new ones.
        if (aiming && aimingInFever != InFever)
            EndAim();

        if (!simulated)
        {
            PointerPhase phase = ProtoInput.Poll(out Vector2 position, out bool overUI);
            if (phase == PointerPhase.Began && !overUI)
            {
                BeginAim(position);
            }
            else if (aiming && phase == PointerPhase.Held)
            {
                UpdateAim(position);
            }
            else if (aiming && phase == PointerPhase.Ended)
            {
                UpdateAim(position);
                Release();
            }
            else if (aiming && phase == PointerPhase.None)
            {
                EndAim();
            }
        }

        if (aiming)
        {
            if (aimingInFever)
            {
                fireTimer -= Time.deltaTime;
                if (fireTimer <= 0f)
                {
                    FeverVolley();
                    fireTimer = feverFireInterval;
                }
            }
            RefreshPrediction();
            DrawLine();
            FaceAim();
        }
    }

    #region Aiming
    void BeginAim(Vector2 position)
    {
        aiming = true;
        aimingInFever = InFever;
        firedThisAim = false;
        pressPosition = position;
        bend = 0f;
        cancelling = false;
        fireTimer = 0f;
        aimLine.enabled = true;
        ProtoTime.instance.SetAimScale(aimingInFever ? feverTimeScale : flickAimTimeScale);
        ProtoCamera.instance.SetAimZoom(!aimingInFever);
        ProtoMusic.SetFocus(!aimingInFever);

        if (!aimingInFever)
        {
            // Wind-up: hold an early frame of the throw so the ninja visibly loads the shot.
            Play("Throw", 0.1f, 0.3f);
            anim.speed = 0.06f;
            animResetTimer = 0f;
        }
        ProtoAudio.Play(Sfx.Tick, 0.4f, 0.8f);
        UpdateAim(position);
    }

    void UpdateAim(Vector2 position)
    {
        float dx = (position.x - pressPosition.x) / Screen.width;
        float n = Mathf.Clamp(dx / dragForFullBend, -1f, 1f);
        bend = Mathf.Sign(n) * Mathf.Pow(Mathf.Abs(n), bendExponent) * maxBend;
        cancelling = !aimingInFever && (pressPosition.y - position.y) / Screen.height > cancelDrag;
    }

    void Release()
    {
        if (!aimingInFever && !cancelling)
            TryThrow();
        EndAim();
    }

    void EndAim()
    {
        aiming = false;
        simulated = false;
        aimLine.enabled = false;
        ProtoTime.instance.SetAimScale(1f, true);
        ProtoCamera.instance.SetAimZoom(false);
        ProtoMusic.SetFocus(false);
        ClearPrediction();
        if (animResetTimer <= 0f)
        {
            anim.speed = 1f;
            if (currentAnim == "Throw")
                Play("Idle", 0.15f);
        }
    }

    void FaceAim()
    {
        Vector3 dir = ProtoCurve.Tangent(From, To, bend, hook, 0.08f).ReplaceY(0f);
        if (dir.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime));
    }
    #endregion

    #region Throwing
    bool TryThrow()
    {
        if (Quiver < 1f)
        {
            ProtoAudio.Play(Sfx.Empty, 0.6f);
            ProtoHUD.instance.QuiverEmpty();
            return false;
        }

        Quiver -= 1f;
        ProtoBlade blade = Launch(bend, false);
        blade.PredictedKills = PredictedKills;
        firedThisAim = true;

        ProtoGame.instance.OnBladeThrown(blade, PredictedKills);
        ProtoAudio.Play(Sfx.Whoosh, 0.9f, Random.Range(0.95f, 1.1f));
        ProtoAudio.Play(Sfx.Shing, 0.35f, Random.Range(0.95f, 1.15f));
        ProtoCamera.instance.Kick(Vector3.forward * 0.12f);
        ProtoCamera.instance.Shake(0.05f);
        ProtoHaptics.Pulse(12);
        ThrowAnimation();
        onThrow?.Invoke();
        return true;
    }

    // Fever: three free blades per shot, fanned around the aimed bend.
    void FeverVolley()
    {
        float spread = feverFanSpread * maxBend;
        for (int i = -1; i <= 1; i++)
        {
            ProtoBlade blade = Launch(Mathf.Clamp(bend + i * spread, -maxBend, maxBend), true);
            blade.PredictedKills = i == 0 && !firedThisAim ? PredictedKills : 0;
            ProtoGame.instance.OnBladeThrown(blade, blade.PredictedKills);
        }
        firedThisAim = true;
        ProtoAudio.Play(Sfx.Whoosh, 0.5f, Random.Range(1.1f, 1.3f));
        ProtoCamera.instance.Shake(0.03f);
        if (Time.unscaledTime - lastThrowAnim > 0.3f)
            ThrowAnimation();
    }

    ProtoBlade Launch(float bladeBend, bool fever)
    {
        Vector3 from = From;
        ProtoBlade blade = Instantiate(bladePrefab, from, Quaternion.identity);
        blade.IsFeverBlade = fever;
        blade.Launch(from, To, bladeBend, hook, bladeSpeed, target);
        return blade;
    }

    // Snap into the release frame of the throw clip so the arm whips at the same moment the blade leaves.
    void ThrowAnimation()
    {
        currentAnim = null;
        Play("Throw", 0.02f, 0.5f);
        anim.speed = 1.6f;
        animResetTimer = 0.45f;
        lastThrowAnim = Time.unscaledTime;
    }

    void Play(string state, float fade = 0.12f, float offset = 0f)
    {
        if (currentAnim == state && offset == 0f)
            return;
        currentAnim = state;
        anim.CrossFadeInFixedTime(state, fade, 0, offset);
    }
    #endregion

    #region Chase
    // Run to the roof edge and leap to the next rooftop. ProtoGame pauses gameplay while this runs.
    public IEnumerator ChaseTo(float edgeZ, Vector3 destination)
    {
        EndAim();
        anim.speed = 1f;
        transform.rotation = Quaternion.LookRotation(Vector3.forward);
        Play("Run", 0.1f);

        Vector3 edge = new Vector3(0f, transform.position.y, edgeZ);
        while ((edge - transform.position).sqrMagnitude > 0.04f)
        {
            transform.position = Vector3.MoveTowards(transform.position, edge, chaseRunSpeed * Time.deltaTime);
            yield return null;
        }

        Play("JumpStart", 0.05f);
        ProtoAudio.Play(Sfx.Whoosh, 0.8f, 0.8f);
        Vector3 start = transform.position;
        for (float t = 0f; t < chaseJumpTime; t += Time.deltaTime)
        {
            float k = t / chaseJumpTime;
            transform.position = Vector3.Lerp(start, destination, k) + Vector3.up * chaseJumpHeight * 4f * k * (1f - k);
            yield return null;
        }

        transform.position = destination;
        Play("Land", 0.05f);
        ProtoFX.Dust(destination, 1.4f);
        ProtoAudio.Play(Sfx.Land, 1f);
        ProtoCamera.instance.Shake(0.2f);
        ProtoHaptics.Pulse(30);
    }
    #endregion

    #region Prediction
    void RefreshPrediction()
    {
        int previousKills = PredictedKills;
        ClearPrediction();

        Vector3 from = From, to = To;
        Vector3 prev = from;
        linePoints.Add(prev);
        float radius = bladePrefab.Radius;
        float travelled = 0f;

        for (int i = 1; i <= previewSegments; i++)
        {
            float s0 = (i - 1) / (float)previewSegments;
            float s1 = i / (float)previewSegments;
            Vector3 p = ProtoCurve.Point(from, to, bend, hook, s1);
            float segment = Vector3.Distance(prev, p);
            float eta = (travelled + segment * 0.5f) / bladeSpeed;   // when the blade gets here, to lead moving targets
            travelled += segment;

            segmentHits.Clear();
            foreach (ProtoTarget target in ProtoTarget.all)
            {
                if (target.IsHittable && !predictedTargets.Contains(target) && target.Intersects(prev, p, radius, out float t, eta))
                    segmentHits.Add(new KeyValuePair<float, ProtoTarget>(t, target));
            }
            segmentHits.Sort((x, y) => x.Key.CompareTo(y.Key));

            foreach (var hit in segmentHits)
            {
                if (predictedTargets.Contains(hit.Value))
                    continue;   // already counted as a barrel victim

                Vector3 dir = ProtoCurve.Tangent(from, to, bend, hook, Mathf.Lerp(s0, s1, hit.Key));
                PredictState state = hit.Value.Predict(dir);
                AddPrediction(hit.Value, state);

                if (state == PredictState.Hit && hit.Value is ProtoBarrel barrel)
                {
                    blastVictims.Clear();
                    barrel.CollectVictims(blastVictims, eta);
                    foreach (ProtoEnemy victim in blastVictims)
                        if (!predictedTargets.Contains(victim))
                            AddPrediction(victim, PredictState.Hit);
                }

                if (state == PredictState.Blocked)
                {
                    linePoints.Add(Vector3.Lerp(prev, p, hit.Key));
                    goto done;
                }
            }

            linePoints.Add(p);
            prev = p;
        }
    done:

        if (PredictedKills != previousKills && PredictedKills > 0)
            ProtoAudio.Play(Sfx.Tick, 0.5f, ProtoAudio.Semitones(PredictedKills * 2));
    }

    void AddPrediction(ProtoTarget target, PredictState state)
    {
        predictedTargets.Add(target);
        predictedStates.Add(state);
        target.SetPredicted(state);
        if (state == PredictState.Hit && target is ProtoEnemy)
            PredictedKills++;
    }

    void ClearPrediction()
    {
        foreach (ProtoTarget t in predictedTargets)
            if (t != null)
                t.SetPredicted(PredictState.None);
        predictedTargets.Clear();
        predictedStates.Clear();
        linePoints.Clear();
        PredictedKills = 0;
    }

    void DrawLine()
    {
        aimLine.positionCount = linePoints.Count;
        for (int i = 0; i < linePoints.Count; i++)
            aimLine.SetPosition(i, linePoints[i]);

        bool blocked = predictedStates.Count > 0 && predictedStates[predictedStates.Count - 1] == PredictState.Blocked;
        Color c;
        if (cancelling) c = new Color(1f, 1f, 1f, 0.25f);
        else if (PredictedKills >= 4) c = new Color(1f, 0.35f, 0.95f);
        else if (PredictedKills == 3) c = new Color(1f, 0.55f, 0.1f);
        else if (PredictedKills == 2) c = new Color(1f, 0.92f, 0.2f);
        else c = new Color(1f, 1f, 1f, 0.85f);

        aimLine.startColor = c;
        aimLine.endColor = blocked && !cancelling ? new Color(1f, 0.15f, 0.1f) : c;
        float width = 0.2f + 0.03f * Mathf.Min(PredictedKills, 5);
        aimLine.widthMultiplier = width * (1f + 0.08f * Mathf.Sin(Time.unscaledTime * 16f));
        lineMaterial.mainTextureOffset = new Vector2(-Time.unscaledTime * 2.5f, 0f);
    }
    #endregion

    #region Reactions
    public void Flinch()
    {
        anim.speed = 1f;
        Play("Hit", 0.05f);
    }

    public void Die(Vector3 hitDirection)
    {
        EndAim();
        anim.speed = 1f;
        Play("Death", 0.05f);
        float scale = transform.localScale.x;
        var col = gameObject.AddComponent<CapsuleCollider>();
        col.center = new Vector3(0f, 1f / scale, 0f);
        col.height = 1.8f / scale;
        col.radius = 0.4f / scale;
        var body = gameObject.AddComponent<Rigidbody>();
        body.linearVelocity = hitDirection * 6f + Vector3.up * 6f;
        body.angularVelocity = Vector3.Cross(Vector3.up, hitDirection) * 5f;
    }

    public void Celebrate()
    {
        anim.speed = 1f;
        Play("Cheer", 0.15f);
    }
    #endregion

    #region Test hooks (driven by the Unity CLI to capture screenshots without a finger)
    public void SimulateAim(float bendNormalized)
    {
        if (!aiming)
            BeginAim(Vector2.zero);
        simulated = true;
        float n = Mathf.Clamp(bendNormalized, -1f, 1f);
        bend = Mathf.Sign(n) * Mathf.Pow(Mathf.Abs(n), bendExponent) * maxBend;
        cancelling = false;
    }

    public void SimulateRelease()
    {
        if (aiming)
            Release();
    }

    // Bot helper: scans the bend range and returns the one with the most predicted kills (shield breaks count a
    // little, hostage hits are heavily penalized, smaller bends win ties). Leaves the thrower aiming at the chosen
    // bend. useful is false when no bend achieves anything.
    public float FindBestBend(out int kills) => FindBestBend(out kills, out _);

    public float FindBestBend(out int kills, out bool useful)
    {
        float best = 0f;
        int bestScore = int.MinValue;
        kills = 0;
        useful = false;
        for (int i = -20; i <= 20; i++)
        {
            float n = i / 20f;
            SimulateAim(n);
            RefreshPrediction();
            int penalties = 0, shieldBreaks = 0;
            for (int k = 0; k < predictedStates.Count; k++)
            {
                if (predictedStates[k] == PredictState.Penalty) penalties++;
                if (predictedStates[k] == PredictState.Blocked && predictedTargets[k] is ProtoEnemy) shieldBreaks++;
            }
            int value = PredictedKills * 10 + shieldBreaks * 3 - penalties * 100;
            int score = value - Mathf.Abs(i) / 4;
            if (score > bestScore)
            {
                bestScore = score;
                best = n;
                kills = PredictedKills;
                useful = value > 0;
            }
        }
        SimulateAim(best);
        RefreshPrediction();
        return best;
    }
    #endregion
}

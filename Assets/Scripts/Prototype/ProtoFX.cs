using System.Collections.Generic;
using UnityEngine;
using CustomExtensions;

// Particle effects for the prototype, built in code from procedural textures. Templates live (inactive) under this
// object; each effect call clones a template, plays it once and lets it destroy itself.
public class ProtoFX : MonoBehaviour
{
    public static ProtoFX instance { get; private set; }

    [SerializeField] Material alphaMaterial = null;
    [SerializeField] Material additiveMaterial = null;
    [Tooltip("Optional richer explosion (e.g. from the local VFX pack); layered over the generated one")]
    [SerializeField] GameObject explosionPrefab = null;
    [SerializeField] float explosionPrefabScale = 0.35f;

    readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();
    Transform templates;
    ParticleSystem flash, stars, chunks, ring, poof, sparks, fire, smoke, dust, confetti, chips, sparkle;

    private void Awake()
    {
        instance = this;
        templates = new GameObject("FX Templates").transform;
        templates.SetParent(transform, false);
        BuildTemplates();
    }

    #region Effects
    public static void KillBurst(Vector3 position, Vector3 direction, Color color, int combo)
    {
        if (instance == null) return;
        // Sized for the far gameplay camera: effects need to be much bigger than feels natural up close.
        float big = 1.7f + 0.35f * Mathf.Min(combo - 1, 5);
        instance.Emit(instance.flash, position, big);
        instance.Emit(instance.stars, position, big);
        instance.Emit(instance.chunks, position, 1.5f, color);
        instance.Emit(instance.chunks, position, 1.1f, Color.white);
        instance.Emit(instance.ring, position, big);
        if (combo >= 3)
            instance.Emit(instance.sparkle, position, big);
    }

    public static void Clang(Vector3 position)
    {
        if (instance == null) return;
        instance.Emit(instance.sparks, position, 1f);
        instance.Emit(instance.flash, position, 0.7f, new Color(1f, 0.8f, 0.4f));
        instance.Emit(instance.ring, position, 0.6f, new Color(1f, 0.85f, 0.5f));
    }

    public static void Explosion(Vector3 position, float radius)
    {
        if (instance == null) return;
        float r = radius / 3f;
        instance.Emit(instance.flash, position, 3.2f * r, new Color(1f, 0.9f, 0.6f));
        instance.Emit(instance.fire, position, r);
        instance.Emit(instance.smoke, position, r);
        instance.Emit(instance.sparks, position, 1.4f * r);
        instance.Emit(instance.sparks, position, 1.1f * r);
        instance.Emit(instance.ring, position, 2.2f * r, new Color(1f, 0.7f, 0.3f));
        instance.Emit(instance.dust, position.ReplaceY(0.05f), 2f * r, null, Quaternion.Euler(90f, 0f, 0f));
        if (instance.explosionPrefab != null)
        {
            GameObject boom = Instantiate(instance.explosionPrefab, position, Quaternion.identity);
            boom.transform.localScale *= instance.explosionPrefabScale * r;
            foreach (ParticleSystem ps in boom.GetComponentsInChildren<ParticleSystem>())
            {
                var main = ps.main;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            }
            // Transform scale doesn't shrink lights, which would otherwise tint the whole rooftop.
            foreach (Light l in boom.GetComponentsInChildren<Light>())
            {
                l.range *= instance.explosionPrefabScale * r;
                l.intensity *= 0.6f;
            }
            Destroy(boom, 4f);
        }
    }

    public static void Dust(Vector3 position, float scale = 1f)
    {
        if (instance == null) return;
        instance.Emit(instance.dust, position.ReplaceY(0.05f), scale, null, Quaternion.Euler(90f, 0f, 0f));
    }

    public static void Poof(Vector3 position, float scale = 1f)
    {
        if (instance == null) return;
        instance.Emit(instance.poof, position, scale);
        instance.Emit(instance.stars, position, 0.6f * scale);
    }

    // Purple smoke burst for shadow clones popping in and out.
    public static void ShadowPoof(Vector3 position)
    {
        if (instance == null) return;
        instance.Emit(instance.poof, position, 1.3f, new Color(0.45f, 0.2f, 0.75f, 0.9f));
        instance.Emit(instance.sparkle, position, 1.1f, new Color(0.8f, 0.5f, 1f));
        instance.Emit(instance.ring, position, 0.9f, new Color(0.7f, 0.35f, 1f));
    }

    public static void Confetti(Vector3 position)
    {
        if (instance == null) return;
        instance.Emit(instance.confetti, position, 1f, null, Quaternion.Euler(-90f, 0f, 0f));
    }

    public static void WoodHit(Vector3 position)
    {
        if (instance == null) return;
        instance.Emit(instance.chips, position, 1f);
        instance.Emit(instance.poof, position, 0.4f);
    }

    public static void Sparkle(Vector3 position, float scale = 1f)
    {
        if (instance == null) return;
        instance.Emit(instance.sparkle, position, scale);
    }
    #endregion

    ParticleSystem Emit(ParticleSystem template, Vector3 position, float scale, Color? tint = null, Quaternion? rotation = null)
    {
        ParticleSystem ps = Instantiate(template, position, rotation ?? Quaternion.identity);
        ps.transform.localScale = Vector3.one * scale;
        if (tint.HasValue)
        {
            var main = ps.main;
            main.startColor = tint.Value;
        }
        ps.gameObject.SetActive(true);
        ps.Play(true);
        return ps;
    }

    void BuildTemplates()
    {
        Color white = Color.white;
        flash = Make("Flash", ProtoArt.SoftCircle, true, 1, new Vector2(0.08f, 0.11f), Vector2.zero, new Vector2(2.2f, 2.2f), white, white, 0f, 0.01f, sizeEnd: 1.6f, spin: false);
        stars = Make("Stars", ProtoArt.Star, false, 10, new Vector2(0.35f, 0.6f), new Vector2(5f, 10f), new Vector2(0.25f, 0.5f), new Color(1f, 0.92f, 0.2f), white, 1.2f, 0.1f, sizeEnd: 0f, drag: 2f);
        chunks = Make("Chunks", ProtoArt.Circle, false, 14, new Vector2(0.5f, 0.9f), new Vector2(3f, 8f), new Vector2(0.12f, 0.28f), white, white, 2.5f, 0.15f, sizeEnd: 0.2f);
        ring = Make("Ring", ProtoArt.Ring, true, 1, new Vector2(0.22f, 0.22f), Vector2.zero, new Vector2(0.6f, 0.6f), white, white, 0f, 0.01f, sizeEnd: 5f, spin: false);
        poof = Make("Poof", ProtoArt.SoftCircle, false, 12, new Vector2(0.45f, 0.8f), new Vector2(0.6f, 2.2f), new Vector2(0.5f, 1.1f), new Color(0.97f, 0.97f, 0.97f, 0.9f), new Color(0.8f, 0.8f, 0.85f, 0.9f), -0.1f, 0.3f, sizeEnd: 1.8f, drag: 3f);
        sparks = Make("Sparks", ProtoArt.SoftCircle, true, 24, new Vector2(0.12f, 0.3f), new Vector2(9f, 16f), new Vector2(0.08f, 0.14f), new Color(1f, 0.55f, 0.1f), new Color(1f, 0.95f, 0.5f), 1.5f, 0.1f, sizeEnd: 0.3f, stretch: true);
        fire = Make("Fire", ProtoArt.SoftCircle, true, 26, new Vector2(0.3f, 0.6f), new Vector2(3f, 9f), new Vector2(0.7f, 1.6f), new Color(1f, 0.5f, 0.1f), new Color(1f, 0.9f, 0.3f), -0.3f, 0.4f, sizeEnd: 0.2f, drag: 4f);
        smoke = Make("Smoke", ProtoArt.SoftCircle, false, 14, new Vector2(1f, 1.6f), new Vector2(0.8f, 3f), new Vector2(1.2f, 2.4f), new Color(0.3f, 0.3f, 0.32f, 0.85f), new Color(0.55f, 0.55f, 0.58f, 0.85f), -0.15f, 0.5f, sizeEnd: 1.6f, drag: 2f);
        dust = Make("Dust", ProtoArt.SoftCircle, false, 12, new Vector2(0.35f, 0.5f), new Vector2(2f, 3.5f), new Vector2(0.4f, 0.7f), new Color(0.92f, 0.9f, 0.86f, 0.85f), new Color(0.8f, 0.78f, 0.75f, 0.85f), 0f, 0.3f, sizeEnd: 1.5f, drag: 5f, shape: ParticleSystemShapeType.Circle);
        confetti = Make("Confetti", ProtoArt.Square, false, 80, new Vector2(1.6f, 2.6f), new Vector2(7f, 13f), new Vector2(0.12f, 0.22f), white, white, 1.1f, 0.3f, sizeEnd: 0.8f, drag: 1.2f, shape: ParticleSystemShapeType.Cone, coneAngle: 28f);
        chips = Make("Chips", ProtoArt.Square, false, 10, new Vector2(0.5f, 0.8f), new Vector2(3f, 7f), new Vector2(0.1f, 0.2f), new Color(0.6f, 0.4f, 0.2f), new Color(0.85f, 0.65f, 0.38f), 2.5f, 0.15f, sizeEnd: 0.4f);
        sparkle = Make("Sparkle", ProtoArt.Star, true, 16, new Vector2(0.5f, 0.9f), new Vector2(1.5f, 4f), new Vector2(0.2f, 0.4f), new Color(0.5f, 1f, 0.4f), new Color(1f, 1f, 0.5f), -0.5f, 0.3f, sizeEnd: 0f, drag: 1.5f);

        var main = confetti.main;
        main.startColor = new ParticleSystem.MinMaxGradient(RainbowGradient()) { mode = ParticleSystemGradientMode.RandomColor };
    }

    ParticleSystem Make(string name, Texture2D texture, bool additive, int count, Vector2 lifetime, Vector2 speed, Vector2 size,
        Color colorA, Color colorB, float gravity, float shapeRadius, float sizeEnd = 0f, bool stretch = false, float drag = 0f,
        bool spin = true, ParticleSystemShapeType shape = ParticleSystemShapeType.Sphere, float coneAngle = 25f)
    {
        var go = new GameObject(name);
        go.SetActive(false);
        go.transform.SetParent(templates, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.playOnAwake = false;
        main.loop = false;
        main.duration = lifetime.y;
        main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime.x, lifetime.y);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed.x, speed.y);
        main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
        main.startColor = new ParticleSystem.MinMaxGradient(colorA, colorB);
        main.startRotation = spin ? new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f) : new ParticleSystem.MinMaxCurve(0f);
        main.gravityModifier = gravity;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.stopAction = ParticleSystemStopAction.Destroy;
        main.maxParticles = count * 2;

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

        var sh = ps.shape;
        sh.enabled = true;
        sh.shapeType = shape;
        sh.radius = shapeRadius;
        if (shape == ParticleSystemShapeType.Cone)
            sh.angle = coneAngle;

        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, sizeEnd));

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var fade = new Gradient();
        fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                     new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.55f), new GradientAlphaKey(0f, 1f) });
        col.color = fade;

        if (spin)
        {
            var rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-8f, 8f);
        }

        if (drag > 0f)
        {
            var limit = ps.limitVelocityOverLifetime;
            limit.enabled = true;
            limit.drag = drag;
        }

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = MaterialFor(texture, additive);
        renderer.maxParticleSize = 4f;
        if (stretch)
        {
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.05f;
            renderer.lengthScale = 1.5f;
        }
        return ps;
    }

    Material MaterialFor(Texture2D texture, bool additive)
    {
        string key = texture.name + (additive ? "+" : "");
        if (materials.TryGetValue(key, out Material m))
            return m;

        Material source = additive ? additiveMaterial : alphaMaterial;
        m = source != null ? new Material(source) : new Material(Shader.Find(additive ? "Legacy Shaders/Particles/Additive" : "Sprites/Default"));
        m.mainTexture = texture;
        materials[key] = m;
        return m;
    }

    static Gradient RainbowGradient()
    {
        var g = new Gradient();
        g.SetKeys(new[]
        {
            new GradientColorKey(new Color(1f, 0.25f, 0.3f), 0f),
            new GradientColorKey(new Color(1f, 0.8f, 0.1f), 0.25f),
            new GradientColorKey(new Color(0.3f, 0.9f, 0.35f), 0.5f),
            new GradientColorKey(new Color(0.2f, 0.7f, 1f), 0.75f),
            new GradientColorKey(new Color(0.85f, 0.35f, 1f), 1f),
        }, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
        return g;
    }
}

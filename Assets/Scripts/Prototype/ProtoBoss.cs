using System.Collections;
using UnityEngine;
using CustomExtensions;

// Big Bear, the gang boss. Every throw ends at him, which is what gives the curve a target: a blade that reaches him
// hurts him, and every goon it cut through on the way adds damage. He taunts, flinches, flees to the next rooftop
// when a zone is cleared (escaping for good at the end of most levels), and gets launched off the roof when his
// health runs out in a showdown.
public class ProtoBoss : MonoBehaviour
{
    public static ProtoBoss instance { get; private set; }

    [SerializeField] string displayName = "BIG BEAR";
    [SerializeField] int maxHealth = 80;
    [Tooltip("Before the showdown he shrugs off damage below this fraction of his health")]
    [SerializeField, Range(0, 1)] float healthFloorBeforeShowdown = 0.35f;
    [SerializeField] float aimHeight = 1.5f;
    [SerializeField] int damagePerKill = 2;
    [SerializeField] Vector2 tauntInterval = new Vector2(6f, 10f);
    [SerializeField] float runSpeed = 9f;
    [SerializeField] float jumpTime = 1.3f;
    [SerializeField] float jumpHeight = 8f;

    static readonly int emissionId = Shader.PropertyToID("_EmissionColor");
    static readonly string[] taunts = { "GET HIM, BOYS!", "TOO SLOW, NINJA!", "IS THAT ALL?", "HA HA HA!", "YOU'LL NEVER CATCH ME!" };

    public string DisplayName => displayName;
    public int Health { get; private set; }
    public int MaxHealth => maxHealth;
    public bool Defeated { get; private set; }
    public bool IsBusy { get; private set; }
    public Vector3 AimPoint => transform.position + Vector3.up * aimHeight;
    // Set by ProtoGame on the showdown rooftop; until then he can be worn down but not knocked out.
    public bool CanBeKnockedOut { get; set; }

    Animator anim;
    Renderer[] renderers;
    MaterialPropertyBlock block;
    Vector3 baseScale;
    float flash, punch, tauntTimer;

    private void Awake()
    {
        instance = this;
        anim = GetComponentInChildren<Animator>();
        renderers = GetComponentsInChildren<Renderer>();
        block = new MaterialPropertyBlock();
        baseScale = transform.localScale;
        Health = maxHealth;
        tauntTimer = Random.Range(tauntInterval.x, tauntInterval.y);
    }

    public void Place(Vector3 position)
    {
        transform.SetPositionAndRotation(position, Quaternion.LookRotation(Vector3.back));
        anim.Play("Idle");
    }

    private void Update()
    {
        if (ProtoGame.IsPlaying && !Defeated && !IsBusy)
        {
            tauntTimer -= Time.deltaTime;
            if (tauntTimer <= 0f)
            {
                tauntTimer = Random.Range(tauntInterval.x, tauntInterval.y);
                Taunt();
            }
        }

        float udt = Time.unscaledDeltaTime;
        flash = Mathf.Max(0f, flash - udt * 7f);
        punch = Mathf.Max(0f, punch - udt * 5f);
        if (!Defeated)
            transform.localScale = new Vector3(1f + 0.12f * punch, 1f - 0.18f * punch, 1f + 0.12f * punch).MultiplyBy(baseScale);
        foreach (Renderer r in renderers)
        {
            r.GetPropertyBlock(block);
            block.SetColor(emissionId, new Color(1f, 0.9f, 0.8f) * flash * 1.2f);
            r.SetPropertyBlock(block);
        }
    }

    public void Taunt(string line = null)
    {
        anim.CrossFadeInFixedTime("Taunt", 0.15f);
        ProtoHUD.instance.Taunt(line ?? taunts[Random.Range(0, taunts.Length)]);
    }

    // Per-level health, set before the level starts.
    public void SetMaxHealth(int health)
    {
        maxHealth = Mathf.Max(1, health);
        Health = maxHealth;
    }

    // Called when a blade completes its curve. More goons cut on the way = more damage.
    public void Catch(ProtoBlade blade, Vector3 direction)
    {
        int damage = blade.IsFeverBlade ? 1 : 1 + damagePerKill * blade.Kills;
        Vector3 point = AimPoint - direction.normalized * 0.4f;
        blade.Consume();
        TakeHit(damage, point, direction);
    }

    void TakeHit(int damage, Vector3 point, Vector3 direction)
    {
        if (Defeated)
            return;

        int floor = CanBeKnockedOut ? 0 : Mathf.CeilToInt(maxHealth * healthFloorBeforeShowdown);
        if (Health - damage < floor)
        {
            damage = Mathf.Max(0, Health - floor);
            if (damage == 0)
            {
                punch = 0.6f;
                ProtoHUD.instance.Taunt("GRR! NOT YET!");
                ProtoAudio.Play(Sfx.Thunk, 0.6f, 0.7f);
                return;
            }
        }
        Health = Mathf.Max(0, Health - damage);
        flash = 1f;
        punch = 1f;
        if (!IsBusy)
            anim.CrossFadeInFixedTime("Hit", 0.05f, 0, 0f);
        ProtoGame.instance.OnBossHit(this, damage, point);

        if (Health == 0)
        {
            Defeated = true;
            ProtoGame.instance.OnBossDefeated(this, direction);
        }
    }

    // Runs to the far edge of his rooftop and leaps to the next one.
    public IEnumerator FleeTo(float edgeZ, Vector3 destination)
    {
        IsBusy = true;
        transform.rotation = Quaternion.LookRotation(Vector3.forward);
        anim.CrossFadeInFixedTime("Run", 0.1f);
        ProtoHUD.instance.Taunt("CATCH ME IF YOU CAN!");

        Vector3 edge = new Vector3(transform.position.x, transform.position.y, Mathf.Max(edgeZ, transform.position.z));
        while ((edge - transform.position).ReplaceY(0f).sqrMagnitude > 0.04f)
        {
            transform.position = Vector3.MoveTowards(transform.position, edge, runSpeed * Time.deltaTime);
            yield return null;
        }

        anim.CrossFadeInFixedTime("JumpAir", 0.08f);
        Vector3 start = transform.position;
        for (float t = 0f; t < jumpTime; t += Time.deltaTime)
        {
            float k = t / jumpTime;
            transform.position = Vector3.Lerp(start, destination, k) + Vector3.up * jumpHeight * 4f * k * (1f - k);
            yield return null;
        }
        transform.SetPositionAndRotation(destination, Quaternion.LookRotation(Vector3.back));
        anim.CrossFadeInFixedTime("Land", 0.05f);
        ProtoFX.Dust(destination, 2f);
        ProtoAudio.Play(Sfx.Land, 1f, 0.7f);
        punch = 1f;
        IsBusy = false;
    }

    // Final blow: launched off the roof, spinning, and gone with a twinkle.
    public void Knockout(Vector3 direction)
    {
        anim.CrossFadeInFixedTime("KO", 0.05f);
        transform.localScale = baseScale;
        var body = gameObject.AddComponent<Rigidbody>();
        body.mass = 3f;
        Vector3 flat = direction.ReplaceY(0f).sqrMagnitude > 0.01f ? direction.ReplaceY(0f).normalized : Vector3.forward;
        body.linearVelocity = flat * 9f + Vector3.up * 16f;
        body.angularVelocity = Vector3.Cross(Vector3.up, flat) * 6f + Random.insideUnitSphere * 4f;
        StartCoroutine(Twinkle());
    }

    IEnumerator Twinkle()
    {
        yield return new WaitForSeconds(1.6f);
        ProtoFX.Sparkle(transform.position, 3f);
        ProtoAudio.Play(Sfx.Tick, 1f, 2f);
        gameObject.SetActive(false);
    }
}

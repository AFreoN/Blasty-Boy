using System.Collections;
using UnityEngine;

// Shadow Storm visuals: two shadow clones of the ninja appear at his sides for the duration of the storm, mirror his
// throws, and supply the launch points for the outer blades of each fever volley.
[RequireComponent(typeof(ProtoThrower))]
public class ProtoShadowClones : MonoBehaviour
{
    [SerializeField] GameObject clonePrefab = null;
    [SerializeField] Material cloneMaterial = null;
    [SerializeField] float sideOffset = 1.8f;
    [SerializeField] float backOffset = 0.5f;
    [SerializeField] float followSharpness = 12f;
    [SerializeField] float bobHeight = 0.12f;
    [SerializeField] float throwRipple = 0.06f;

    static readonly int fadeId = Shader.PropertyToID("_Fade");

    class Clone
    {
        public Transform root;
        public Animator anim;
        public Renderer[] renderers;
        public float side;
        public float fade;
    }

    ProtoThrower thrower;
    readonly Clone[] clones = new Clone[2];
    MaterialPropertyBlock block;
    bool shown;

    public bool Active => shown;

    private void Awake()
    {
        thrower = GetComponent<ProtoThrower>();
        block = new MaterialPropertyBlock();
        if (clonePrefab == null)
            return;

        for (int i = 0; i < 2; i++)
        {
            float side = i == 0 ? -1f : 1f;
            GameObject go = Instantiate(clonePrefab, transform.position, transform.rotation);
            go.name = "ShadowClone" + (i == 0 ? "L" : "R");
            var clone = new Clone { root = go.transform, anim = go.GetComponentInChildren<Animator>(), renderers = go.GetComponentsInChildren<Renderer>(), side = side };
            clone.anim.updateMode = AnimatorUpdateMode.UnscaledTime;
            foreach (Renderer r in clone.renderers)
            {
                if (cloneMaterial != null)
                    r.sharedMaterials = new Material[r.sharedMaterials.Length].Populate(cloneMaterial);
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
            go.SetActive(false);
            clones[i] = clone;
        }
    }

    // Where the outer blade of a volley leaves from: -1 = left clone, +1 = right clone.
    public Vector3 LaunchOffset(int side)
    {
        Vector3 right = transform.right * sideOffset * side;
        return right - transform.forward * backOffset;
    }

    // Each clone throws a beat after the ninja, so a volley reads as three throws, not one.
    public void Throw()
    {
        if (!shown)
            return;
        for (int i = 0; i < clones.Length; i++)
            if (clones[i] != null)
                StartCoroutine(ThrowAfter(clones[i], throwRipple * (i + 1)));
    }

    IEnumerator ThrowAfter(Clone clone, float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        clone.anim.speed = 1.6f;
        clone.anim.CrossFadeInFixedTime("Throw", 0.02f, 0, 0.5f);
    }

    private void Update()
    {
        if (clones[0] == null)
            return;

        bool want = thrower.InFever;
        if (want != shown)
        {
            shown = want;
            foreach (Clone c in clones)
            {
                if (shown)
                {
                    c.root.position = transform.position;
                    c.root.gameObject.SetActive(true);
                    c.anim.Play("Idle");
                }
                // Appear at the flank they're about to occupy; vanish from wherever they are.
                Vector3 at = shown ? transform.position + LaunchOffset((int)c.side) : c.root.position;
                ProtoFX.ShadowPoof(at + Vector3.up);
            }
            ProtoAudio.Play(Sfx.Whoosh, 0.8f, shown ? 0.6f : 0.8f);
        }

        float dt = Time.unscaledDeltaTime;
        float bob = Mathf.Sin(Time.unscaledTime * 3f);
        foreach (Clone c in clones)
        {
            c.fade = Mathf.MoveTowards(c.fade, shown ? 1f : 0f, dt * (shown ? 4f : 3f));
            if (c.fade <= 0f && !shown)
            {
                c.root.gameObject.SetActive(false);
                continue;
            }

            // Slide out from the ninja to the flank, hover with a little bob, copy his facing.
            Vector3 target = transform.position + LaunchOffset((int)c.side) + Vector3.up * (bobHeight * (1f + bob * c.side) + 0.05f);
            c.root.position = Vector3.Lerp(c.root.position, target, 1f - Mathf.Exp(-followSharpness * dt));
            c.root.rotation = Quaternion.Slerp(c.root.rotation, transform.rotation, 1f - Mathf.Exp(-followSharpness * dt));
            float flicker = 0.9f + 0.1f * Mathf.Sin(Time.unscaledTime * 23f + c.side * 2f);
            c.root.localScale = Vector3.one * transform.localScale.x * Mathf.Lerp(0.6f, 1f, c.fade);

            foreach (Renderer r in c.renderers)
            {
                r.GetPropertyBlock(block);
                block.SetFloat(fadeId, c.fade * flicker);
                r.SetPropertyBlock(block);
            }
        }
    }

    private void OnDestroy()
    {
        foreach (Clone c in clones)
            if (c != null && c.root != null)
                Destroy(c.root.gameObject);
    }
}

static class ProtoArrayExtensions
{
    public static T[] Populate<T>(this T[] array, T value)
    {
        for (int i = 0; i < array.Length; i++)
            array[i] = value;
        return array;
    }
}

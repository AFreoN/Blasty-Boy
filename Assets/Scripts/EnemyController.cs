using UnityEngine;
using CustomExtensions;
using System.Collections.Generic;

public class EnemyController : MonoBehaviour
{
    Rigidbody rb = null;
    Animator anim = null;

    public bool IsAlive { get; private set; }
    Emote currentEmote = null;

    const string bodyMeshName = "HumanBase_LowPoly";

    public ENEMYTYPE enemyType = ENEMYTYPE.Standing;
    bool animationInitialized = false;

    const string Nwalking = "walk";
    const string Nrunning = "run";
    const string Nattacking = "attack";     //Trigger

    Transform player = null;
    GameObject currentWeapon = null;

    public ForWalk walking;
    [System.Serializable]
    public struct ForWalk
    {
        [SerializeField] private float WalkingSpeed;
        [SerializeField] private Transform[] Path;
        [HideInInspector] public int currentPoint;

        public float walkingSpeed => WalkingSpeed;
        public Transform[] path => Path;
    }

    public ForChaser chasing;
    [System.Serializable]
    public struct ForChaser
    {
        [SerializeField] Transform WeaponHolder;
        [SerializeField] float ChaseSpeed;
        [SerializeField] float ChaseDistance;
        [SerializeField] float ChaseAttackDistance;

        public Transform weaponHolder => WeaponHolder;
        public float chaseSpeed => ChaseSpeed;
        public float chaseDistance => ChaseDistance;
        public float chaseAttackDistance => ChaseAttackDistance;
    }

    public ForBully bullying;
    [System.Serializable]
    public struct ForBully
    {
        [SerializeField] Transform Target;
        [SerializeField] Transform WeaponHolder;
        [SerializeField] float ChaseSpeed;
        [SerializeField] float ChaseDistance;
        [SerializeField] float ChaseAttackDistance;
        [SerializeField] float MinAttackForce;
        [SerializeField] float MaxAttackForce;
        [HideInInspector] public bool emoteInitialized;

        public Transform prisoner => Target;
        public Transform weaponHolder => WeaponHolder;
        public float chaseSpeed => ChaseSpeed;
        public float chaseDistance => ChaseDistance;
        public float chaseAttackDistance => ChaseAttackDistance;
        public float minAttackForce => MinAttackForce;
        public float maxAttackForce => MaxAttackForce;
    }

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        anim = GetComponent<Animator>();

        IsAlive = true;
        InitializeCharacter();
        player = PlayerController.instance.transform;

        walking.currentPoint = 0;

    }

    private void Update()
    {
        if(GameManager.gameState == GAMESTATE.Game && animationInitialized == false)
        {
            InitializeAnimations();
            animationInitialized = true;
        }
    }

    void InitializeCharacter()
    {
        switch(enemyType)
        {
            case ENEMYTYPE.Standing:
                anim.runtimeAnimatorController = PrefabManager.standWalkEnemy;
                break;

            case ENEMYTYPE.PathWalker:
                anim.runtimeAnimatorController = PrefabManager.standWalkEnemy;
                break;

            case ENEMYTYPE.Chaser:
                anim.runtimeAnimatorController = PrefabManager.chasingEnemy;
                currentWeapon = SpawnWeaponForChasing().gameObject;
                break;

            case ENEMYTYPE.Bully:
                bullying.emoteInitialized = false;
                anim.runtimeAnimatorController = PrefabManager.chasingEnemy;
                currentWeapon = SpawnWeaponForBully().gameObject;
                break;
        }
    }

    void InitializeAnimations()
    {
        switch(enemyType)
        {
            case ENEMYTYPE.Standing:
                anim.SetBool(Nwalking, false);
                break;

            case ENEMYTYPE.PathWalker:
                anim.SetBool(Nwalking, true);
                break;

            case ENEMYTYPE.Chaser:
                anim.SetBool(Nwalking, true);
                break;

            case ENEMYTYPE.Bully:
                anim.SetBool(Nwalking, true);
                anim.SetBool(Nattacking, false);
                if(bullying.emoteInitialized == false)
                {
                    bullying.emoteInitialized = true;
                    currentEmote = Emote.Create(EMOTETYPE.Angry, transform, Vector3.up * 2.5f, Random.Range(1.5f,2.3f));
                }
                break;
        }
    }

    public void OnGameOver()
    {
        switch(enemyType)
        {
            case ENEMYTYPE.Standing:
                anim.SetBool(Nwalking, false);
                break;
            case ENEMYTYPE.PathWalker:
                anim.SetBool(Nwalking, false);
                break;
            case ENEMYTYPE.Chaser:
                anim.SetBool(Nwalking, false);
                anim.SetBool(Nattacking, false);
                break;
            case ENEMYTYPE.Bully:
                anim.SetBool(Nwalking, false);
                anim.SetBool(Nattacking, false);
                break;
        }
    }

    Transform SpawnWeaponForChasing()
    {
        Transform t = Instantiate(PrefabManager.baseBallBatPrefab);
        t.SetParent(chasing.weaponHolder);
        t.localScale = Vector3.one;
        t.localPosition = Vector3.zero;
        t.localRotation = Quaternion.identity;
        return t;
    }

    Transform SpawnWeaponForBully()
    {
        Transform t = Instantiate(PrefabManager.baseBallBatPrefab);
        t.SetParent(chasing.weaponHolder);
        t.localScale = Vector3.one;
        t.localPosition = Vector3.zero;
        t.localRotation = Quaternion.identity;
        return t;
    }

    private void FixedUpdate()
    {
        if (IsAlive == false || GameManager.gameState != GAMESTATE.Game)
            return;

        switch(enemyType)
        {
            case ENEMYTYPE.PathWalker:
                Vector3 walkDirection = (walking.path[walking.currentPoint].position.ReplaceY(transform.position.y) - transform.position).normalized;
                transform.forward = walkDirection;
                rb.velocity = walkDirection * walking.walkingSpeed * Time.fixedDeltaTime * 50;

                if(Vector3.Distance(transform.position, walking.path[walking.currentPoint].position.ReplaceY(transform.position.y)) < .1f)
                {
                    walking.currentPoint++;
                    if (walking.currentPoint >= walking.path.Length)
                        walking.currentPoint = 0;
                }
                break;

            case ENEMYTYPE.Chaser:

                if(Vector3.Distance(transform.position, player.position) <= chasing.chaseDistance)
                {
                    anim.SetBool(Nwalking, true);
                }
                else
                {
                    anim.SetBool(Nwalking, false);
                    rb.velocity = Vector3.zero;
                    return;
                }
                walkDirection = (player.position.ReplaceY(transform.position.y) - transform.position).normalized;
                transform.forward = walkDirection;
                rb.velocity = walkDirection * chasing.chaseSpeed * Time.fixedDeltaTime * 50;

                if(Vector3.Distance(transform.position, player.position) <= chasing.chaseAttackDistance)
                {
                    rb.velocity = Vector3.zero;
                    anim.SetBool(Nattacking,true);
                    anim.SetBool(Nwalking, false);
                }
                break;

            case ENEMYTYPE.Bully:

                Transform target = bullying.prisoner;
                if (Vector3.Distance(transform.position, player.position) <= bullying.chaseDistance)
                {
                    anim.SetBool(Nwalking, true);
                }
                else
                {
                    anim.SetBool(Nwalking, false);
                    rb.velocity = Vector3.zero;
                    return;
                }
                walkDirection = (target.position.ReplaceY(transform.position.y) - transform.position).normalized;
                transform.forward = walkDirection;
                rb.velocity = walkDirection * bullying.chaseSpeed * Time.fixedDeltaTime * 50;

                if (Vector3.Distance(transform.position, target.position) <= bullying.chaseAttackDistance)
                {
                    rb.velocity = Vector3.zero;
                    anim.SetBool(Nattacking, true);
                    anim.SetBool(Nwalking, false);

                    FunctionTimer.Create(() => anim.SetBool(Nattacking, false), .8f);
                }
                break;
        }
    }

    public void Die(bool deathEffect = true)
    {
        if (!IsAlive)
            return;

        LevelManager.instance.DecreaseEnemies(deathEffect);
        Vibration.Vibrate(30);

        if (currentEmote != null)
            currentEmote.Destroy();
        if(currentWeapon != null)
        {
            currentWeapon.transform.SetParent(null);
            currentWeapon.AddComponent<BoxCollider>();
            currentWeapon.AddComponent<Rigidbody>();
        }

        IsAlive = false;
        anim.enabled = false;
        GetComponent<RagdollManager>().ActivateRagdoll();
        rb.isKinematic = true;
        rb.detectCollisions = false;

        Color c = Color.grey;
        transform.Find(bodyMeshName).GetComponent<SkinnedMeshRenderer>().material.SetColor("_Color", new Color(c.r, c.g, c.b, 1));
    }

    //called from the animation event while attacking
    public void OnAttack()
    {
        Vector3 center = transform.position + Vector3.up + transform.forward * bullying.chaseAttackDistance;
        Collider[] col = Physics.OverlapBox(center, Vector3.one, transform.rotation, TagsLayers.bodyLayer);

        List<Transform> prisoners = new List<Transform>();
        Transform player = null;

        foreach(Collider c in col)
        {
            Transform t = c.transform.GetRootParent();
            if (t.CompareTag(TagsLayers.prisonerTag) && prisoners.Contains(t) == false)
                prisoners.Add(t);

            if (player == null && t.CompareTag(TagsLayers.playerTag))
                player = t;
        }

        foreach(Transform t in prisoners)
        {
            Prisoner p = t.GetComponent<Prisoner>();
            p.Die();
        }
        if (player != null)
            PlayerController.instance.Die();

        foreach(Collider c in col)
        {
            float applyForce = Random.Range(bullying.minAttackForce, bullying.maxAttackForce);
            Vector3 forceDir = (c.bounds.center - bullying.weaponHolder.position).normalized;
            c.HasComponent((Rigidbody r) => r.AddForce(forceDir * applyForce, ForceMode.Impulse));
        }
    }

    public enum ENEMYTYPE
    {
        Standing,
        PathWalker,
        Chaser,
        Runner,
        Bully
    }
}

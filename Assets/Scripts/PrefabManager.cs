using UnityEngine;
using CustomExtensions;

public class PrefabManager : MonoBehaviour
{
    [SerializeField] Transform ThrowingBlade = null;
    [SerializeField] Transform DummyBlade = null;

    public static Transform throwingBladePrefab { get; private set; }
    public static Transform dummyBladePrefab { get; private set; }

    [Header("Particles")]
    [SerializeField] Transform[] Confettis = null;
    [SerializeField] Transform Blood = null;

    static Transform[] confettis = null;
    public static Transform confettiParticle => confettis[Random.Range(0, confettis.Length)];
    public static Transform bloodParticle { get; private set; }

    #region Enemy Items
    [Header("Enemy Items")]
    [SerializeField] RuntimeAnimatorController StandWalkEnemy = null;
    [SerializeField] RuntimeAnimatorController ChasingEnemy = null;

    [SerializeField] Transform BaseBallbat = null;

    public static RuntimeAnimatorController standWalkEnemy { get; private set; }
    public static RuntimeAnimatorController chasingEnemy { get; private set; }

    public static Transform baseBallBatPrefab { get; private set; }
    #endregion

    [SerializeField] EmotePrefabs Emotes;
    public static EmotePrefabs emotes { get; private set; }
    [System.Serializable]
    public struct EmotePrefabs
    {
        [SerializeField] GameObject emoteCanvas;
        [SerializeField] Sprite smiling;
        [SerializeField] Sprite smilingEyeClosed;
        [SerializeField] Sprite rofl;
        [SerializeField] Sprite love;
        [SerializeField] Sprite sad;
        [SerializeField] Sprite kissing;
        [SerializeField] Sprite angry;
        [SerializeField] Sprite yummy;
        [SerializeField] Sprite cryOneEye;
        [SerializeField] Sprite heaven;
        [SerializeField] Sprite thugLife;

        public GameObject EmoteCanvas => emoteCanvas;
        public Sprite Smiling => smiling;
        public Sprite SmilingEyeClosed => smilingEyeClosed;
        public Sprite Rofl => rofl;
        public Sprite Love => love;
        public Sprite Sad => sad;
        public Sprite Kissing => kissing;
        public Sprite Angry => angry;
        public Sprite Yummy => yummy;
        public Sprite CryOneEye => cryOneEye;
        public Sprite Heaven => heaven;
        public Sprite ThugLife => thugLife;
    }

    private void Awake()
    {
        emotes = Emotes;
        throwingBladePrefab = ThrowingBlade;
        dummyBladePrefab = DummyBlade;

        confettis = new Transform[Confettis.Length];
        for(int i = 0; i < confettis.Length; i++)
        {
            confettis[i] = Confettis[i];
        }
        bloodParticle = Blood;

        #region Enemy Item Setting
        standWalkEnemy = StandWalkEnemy;
        chasingEnemy = ChasingEnemy;

        baseBallBatPrefab = BaseBallbat;
        #endregion
    }

    public static Transform SpawnPrefab(Transform prefab, Vector3 position, Quaternion rotation)
    {
        return Instantiate(prefab, position, rotation);
    }

    public static Transform SpawnPrefab(Transform prefab, Vector3 position)
    {
        return Instantiate(prefab, position, prefab.rotation);
    }

    public static Transform SpawnPrefab(Transform prefab, Quaternion rotation)
    {
        return Instantiate(prefab, Vector3.zero, rotation);
    }

    public static Transform SpawnPrefab(Transform prefab)
    {
        return Instantiate(prefab, Vector3.zero, prefab.rotation);
    }

    public static Transform SpawnPrefab(Transform prefab, Transform parent, Vector3 localPosition, Vector3 localScale)
    {
        Transform t = Instantiate(prefab);
        t.SetParent(parent);
        t.localPosition = localPosition;
        t.localScale = localScale;
        return t;
    }
}

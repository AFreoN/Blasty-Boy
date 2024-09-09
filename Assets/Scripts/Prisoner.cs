using UnityEngine;

public class Prisoner : MonoBehaviour
{
    Animator anim = null;
    Rigidbody rb = null;
    [SerializeField] float lookDistance = 20f;
    [SerializeField] EnemyController[] targetEnemies = null;
    bool isAlive = true;

    Transform player = null;
    Emote currentEmote = null;
    const float emoteDistance = 2.2f;
    const float minEmoteTime = 2, maxEmoteTime = 4;

    bool emoteInitialized = false;
    bool Escaped = false;
    const string escapeName = "escaped";
    const string bodyMeshName = "HumanBase_LowPoly";

    private void Start()
    {
        anim = GetComponent<Animator>();
        rb = GetComponent<Rigidbody>();
        isAlive = true;

        player = PlayerController.instance.transform;
        anim.SetBool(escapeName, false);

        //currentEmote = Emote.Create(EMOTETYPE.Sad, transform,Vector3.up * emoteDistance, 5);
    }

    private void Update()
    {
        if(emoteInitialized == false && GameManager.gameState == GAMESTATE.Game)
        {
            if(Vector3.Distance(transform.position,player.position) <= lookDistance)
            {
                emoteInitialized = true;

                float delayTime = Random.Range(.1f, .5f);
                if (Variance.Roll(1))
                {
                    FunctionTimer.Create(() => 
                    currentEmote = Emote.Create(EMOTETYPE.Sad, transform, Vector3.up * emoteDistance, Random.Range(minEmoteTime, maxEmoteTime))
                    , delayTime);
                }
            }
        }

        if(Escaped == false && isEnemiesKilled())
        {
            Escaped = true;
            anim.SetBool(escapeName, true);

            float delay = .5f;
            if (Variance.Roll(1))
            {
                if (currentEmote != null)
                    currentEmote.Destroy();

                FunctionTimer.CreateUnscaled(() =>
                Emote.Create(EMOTETYPE.Smiling, transform, Vector3.up * 2.5f, Random.Range(1, 1.8f)), delay);
            }
        }
    }

    public void Die()
    {
        if (!isAlive)
            return;

        FunctionTimer.Create(GameManager.instance.gameLose, .5f);
        Vibration.Vibrate(40);

        anim.enabled = false;
        rb.isKinematic = true;
        rb.detectCollisions = false;

        GetComponent<RagdollManager>().ActivateRagdoll();
        if (currentEmote != null)
            currentEmote.Destroy();

        Color c = Color.grey;
        transform.Find(bodyMeshName).GetComponent<SkinnedMeshRenderer>().material.SetColor("_Color", new Color(c.r, c.g, c.b, 1));
    }

    bool isEnemiesKilled()
    {
        foreach(EnemyController ec in targetEnemies)
        {
            if (ec.IsAlive)
                return false;
        }
        return true;
    }
}

using UnityEngine;
using CustomExtensions;

public class Blade : MonoBehaviour
{
    [SerializeField] float minForce = 80;
    [SerializeField] float maxForce = 160;

    private void OnCollisionEnter(Collision collision)
    {
        Collider other = collision.gameObject.GetComponent<Collider>();
        if (other.CompareTag(TagsLayers.bodyPartsTag))
        {
            EnemyController ec = other.transform.GetRootParent().GetComponent<EnemyController>();
            if (ec != null && ec.IsAlive)
            {
                ec.Die();

                Vector3 forceDir = (other.transform.GetComponent<Collider>().bounds.center - transform.position).normalized;
                forceDir = other.ClosestPoint(transform.position).normalized;
                other.GetComponent<Rigidbody>().AddForce(forceDir * Random.Range(minForce, maxForce), ForceMode.Impulse);
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if(other.CompareTag(TagsLayers.bodyPartsTag))
        {
            Transform root = other.transform.GetRootParent();

            if(root.CompareTag(TagsLayers.enemyTag))
            {
                CollidedEnemy(other, root);
            }
            else if(root.CompareTag(TagsLayers.prisonerTag))
            {
                CollidedPrisoner(other, root);
            }
        }

        if(other.CompareTag(TagsLayers.targetBoardTag))
        {
            Destroy(gameObject);
        }

        if(other.CompareTag(TagsLayers.objectTag))
        {
            InteractibleObject io = other.GetComponent<InteractibleObject>();
            if (io != null)
                io.ExecuteAction(transform);
            else
                Destroy(gameObject);
        }
    }

    void CollidedEnemy(Collider other, Transform parent)
    {
        EnemyController ec = parent.GetComponent<EnemyController>();
        if (ec != null && ec.IsAlive)
        {
            ec.Die();

            Vector3 forceDir = (other.transform.GetComponent<Collider>().bounds.center - transform.position).normalized;
            forceDir = other.ClosestPoint(transform.position).normalized;
            other.GetComponent<Rigidbody>().AddForce(forceDir * Random.Range(minForce, maxForce), ForceMode.Impulse);

            SpawnBlood(other);
        }
    }

    void CollidedPrisoner(Collider other, Transform parent)
    {
        parent.HasComponent((Prisoner p) => p.Die());

        Vector3 forceDir = (other.transform.GetComponent<Collider>().bounds.center - transform.position).normalized;
        forceDir = other.ClosestPoint(transform.position).normalized;
        other.GetComponent<Rigidbody>().AddForce(forceDir * Random.Range(minForce, maxForce) * .2f, ForceMode.Impulse);

        SpawnBlood(other);
    }

    void SpawnBlood(Collider other)
    {
        Transform blood = PrefabManager.SpawnPrefab(PrefabManager.bloodParticle, other.ClosestPoint(transform.position));
        Vector3 otherClosePoint = other.ClosestPoint(transform.position).normalized;
        blood.forward = -1 * otherClosePoint;
        blood.SetParent(other.transform);

    }

    private void OnBecameInvisible()
    {
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        LevelManager.instance.BladeDestroyed();
    }
}

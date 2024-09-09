using UnityEngine;
using CustomExtensions;

public class PushWall : MonoBehaviour
{
    [SerializeField] float minPushForce = 10f;
    [SerializeField] float maxPushForce = 20f;

    private void OnCollisionEnter(Collision collision)
    {
        if(collision.CompareTag(TagsLayers.bodyPartsTag))
        {
            Transform t = collision.transform.GetRootParent();
            if(t.CompareTag(TagsLayers.enemyTag))
            {
                PushEnemy(collision, t.GetComponent<EnemyController>());
            }
        }
    }

    void PushEnemy(Collision collision, EnemyController ec)
    {
        ec.Die();

        Vector3 forceDir = (collision.transform.GetComponent<Collider>().bounds.center - collision.GetContact(0).point).normalized;
        forceDir = collision.GetContact(0).normal.normalized;
        collision.transform.HasComponent((Rigidbody r) => r.AddForce(forceDir * Random.Range(minPushForce, maxPushForce), ForceMode.Impulse));
    }
}

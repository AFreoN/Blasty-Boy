using UnityEngine;
using System.Collections.Generic;
using CustomExtensions;

public class InteractibleObject : MonoBehaviour
{
    Collider thisCollider = null;
    [SerializeField] ObjectType Objecttype = ObjectType.None;
    public ObjectType objectType => Objecttype;

    [SerializeField] Barrel barrel;
    [System.Serializable]
    public struct Barrel
    {
        [SerializeField] float ExplosiveRadius;
        [SerializeField] float ExplosiveForce;

        public float explosiveRadius => ExplosiveRadius;
        public float explosiveForce => ExplosiveForce;
    }

    private void Start()
    {
        thisCollider = GetComponent<Collider>();
    }
    public void ExecuteAction(Transform blade)
    {
        switch(Objecttype)
        {
            case ObjectType.None:

                Vector3 forceDir = thisCollider.ClosestPoint(blade.position).normalized;
                thisCollider.HasComponent((Rigidbody r) => { r.AddForce(forceDir * Random.Range(8, 15), ForceMode.Impulse); });

                blade.HasComponent((Rigidbody r) => Destroy(r));
                blade.GetChild(0).HasComponent((Weapon w) => Destroy(w));
                blade.SetParent(transform);
                Destroy(blade.GetComponent<Blade>());
                break;

            case ObjectType.Rigid:

                blade.HasComponent((Blade b) => Destroy(b));
                blade.HasComponent((Rigidbody r) => Destroy(r));
                blade.GetChild(0).HasComponent((Weapon w) => Destroy(w));
                blade.HasComponent((Collider c) => Destroy(c));
                blade.SetParent(transform);
                break;

            case ObjectType.Barrel:

                Vector3 center = GetComponent<Collider>().bounds.center;
                Collider[] col = Physics.OverlapSphere(center, barrel.explosiveRadius);

                List<Transform> enemies = new List<Transform>();
                foreach(Collider c in col)
                {
                    Transform t = c.transform.GetRootParent();
                    if (t.CompareTag(TagsLayers.enemyTag) && enemies.Contains(t) == false)
                        enemies.Add(t);
                }

                foreach (Transform tr in enemies)
                    tr.GetComponent<EnemyController>().Die();

                foreach(Collider c in col)
                    c.HasComponent((Rigidbody r) => r.AddExplosionForce(barrel.explosiveForce * r.mass, center, barrel.explosiveRadius, 2));

                Destroy(gameObject);
                Destroy(blade.gameObject);

                break;

            case ObjectType.Puncher:
                GetComponent<ObjectTrigger>().action?.Invoke();

                FunctionTimer.Create(() =>
                {
                    blade.HasComponent((Blade b) => Destroy(b));
                }, 1);
                blade.HasComponent((Rigidbody r) => Destroy(r));
                blade.GetChild(0).HasComponent((Weapon w) => Destroy(w));
                blade.HasComponent((Collider c) => Destroy(c));
                blade.SetParent(transform);
                break;
        }
    }
}

public enum ObjectType
{
    None,
    Rigid,
    Barrel,
    Puncher
}

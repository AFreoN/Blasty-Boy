using UnityEngine;
using CustomExtensions;

public class TargetBoard : MonoBehaviour
{
    public float maxTravelDistance = 10f;
    const float tolerance = 1;

    public float throwTime = 1f;
    public float jumpTime = 1.5f;

    const string groundName = "TargetBoardGround";
    Transform targetGround = null;

    private void Start()
    {
        targetGround = transform.Find(groundName);

        targetGround.localScale = targetGround.localScale.ReplaceX(maxTravelDistance + tolerance);
        targetGround.SetParent(null);
    }

    public void DisableBoard()
    {
        Transform t = null;
        if(Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, Mathf.Infinity, TagsLayers.groundLayer))
        {
            t = new GameObject("Pivot").transform;
            t.position = hit.point;
            transform.SetParent(t);
            t.gameObject.AddComponent<Rotator>().Initialize(.5f, 0, 90);
        }
    }
}

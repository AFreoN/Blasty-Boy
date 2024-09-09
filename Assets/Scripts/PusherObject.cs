using UnityEngine;

public class PusherObject : MonoBehaviour
{
    [SerializeField] Transform rigidWall = null;
    [SerializeField] Transform piston = null;
    [SerializeField] Transform pushWall = null;

    [SerializeField] float maxMoveDistance = 10f;
    [SerializeField] float pushSpeed = 10;
    [SerializeField] float retractSpeed = 5;

    [SerializeField] PUSHDIRECTION pushDirection = PUSHDIRECTION.Left;
    Vector3 moveDirection = Vector3.zero;
    Vector3 startPosition = Vector3.zero;
    bool move = false, pushed = false;

    private void Start()
    {
        startPosition = pushWall.position;
        moveDirection = pushDirection == PUSHDIRECTION.Right ? Vector3.right : Vector3.left;

        move = false;
        pushed = false;
    }

    public void StartPush()
    {
        move = true;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
            StartPush();
    }

    private void FixedUpdate()
    {
        if (!move)
            return;

        if(pushed == false)
        {
            if(pushDirection == PUSHDIRECTION.Left)
            {
                pushWall.position = pushWall.position + Vector3.left * pushSpeed * Time.fixedDeltaTime;
                if(pushWall.position.x <= startPosition.x - maxMoveDistance)
                {
                    pushed = true;
                }
            }
            else
            {
                pushWall.position = startPosition + Vector3.right * pushSpeed * Time.fixedDeltaTime;
                if (pushWall.position.x >= startPosition.x + maxMoveDistance)
                    pushed = true;
            }
        }
        else
        {
            if(pushDirection == PUSHDIRECTION.Left)
            {
                pushWall.position = pushWall.position + Vector3.right * retractSpeed * Time.fixedDeltaTime;
                if(pushWall.position.x >= startPosition.x)
                {
                    move = false;
                    pushed = false;
                }
            }
            else
            {
                pushWall.position = pushWall.position + Vector3.left * retractSpeed * Time.fixedDeltaTime;
                if(pushWall.position.x <= startPosition.x)
                {
                    move = false;
                    pushed = false;
                }
            }
        }

        float pistonScale = Mathf.Abs(pushWall.position.x - rigidWall.position.x) * .5f;
        piston.localScale = new Vector3(pistonScale, 1, 1);
    }
}

public enum PUSHDIRECTION
{
    Right,
    Left
}

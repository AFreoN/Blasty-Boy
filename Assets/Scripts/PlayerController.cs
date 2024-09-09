using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using CustomExtensions;
using System.Collections.Generic;

public class PlayerController : MonoBehaviour
{
    public static PlayerController instance { get; private set; }

    Rigidbody rb = null;
    Animator anim = null;
    [SerializeField] LineRenderer projectionLine = null;
    [SerializeField] float runSpeed = 10f;

    [Header("Parts")]
    [SerializeField] Transform hand = null;
    public Transform bladeContainer;
    Transform endPoint = null;      //Current target board transform
    TargetBoard currenTargetBoard = null;   //Current target board
    public static int currentBoardsIndex { get; private set; }

    [HideInInspector] public Rigidbody currentBlade = null;  //Current throwed blade(or weapon)
    bool isRunning = false;
    bool isJumping = false;
    Vector3 runDirection = Vector3.zero;

    float throwTime = 1;
    float jumpTime = 1f;
    const float XGravity = 9.81f;
    float gravityModifier = 1;

    //Input
    [Header("UI")][SerializeField] Image cancelBtn = null;
    bool ready = true;
    bool haveInput = false;
    float startPos = 0, endPos = 0;
    bool throwNeeded = false;
    static bool IsTouchInput = false;
    const string cancelName = "Cancel_Img";

    //For EndPoint Controlling
    [SerializeField] Slider eSlider = null;
    Vector3 endInitPosition = Vector3.zero;
    float maxTravel = 4;

    private void Awake()
    {
        instance = this;
        isRunning = false;
        isJumping = false;
        IsTouchInput = Input.touchSupported;
    }

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        anim = GetComponent<Animator>();

        currentBoardsIndex = 0;

        projectionLine.enabled = false;
        cancelBtn.color = Color.red.ChangeAlpha(0);
    }

    public void ChangeEndpoint()
    {
        transform.position = LevelManager.startPoints[currentBoardsIndex].position;

        if (currentBoardsIndex >= LevelManager.targetBoards.Length)
            return;

        endPoint = LevelManager.targetBoards[currentBoardsIndex];

        endInitPosition = endPoint.position;
        currenTargetBoard = endPoint.GetComponent<TargetBoard>();
        maxTravel = currenTargetBoard.maxTravelDistance;
        throwTime = currenTargetBoard.throwTime;
        jumpTime = currenTargetBoard.jumpTime;
    }

    private void Update()
    {
        if(GameManager.gameState == GAMESTATE.Game && isRunning == false && isJumping == false)
        {
            if (IsTouchInput)
            {
                if (Input.touchCount > 0)
                    getTouchInput();
            }
            else
                getMouseInput();
        }
    }

    private void FixedUpdate()
    {
        if (currentBlade != null)
        {
            currentBlade.AddForce(Vector3.left * XGravity * gravityModifier);
        }

        if(isRunning)
        {
            rb.velocity = runDirection * runSpeed * Time.fixedDeltaTime * 50;
            if(Vector3.Distance(transform.position, LevelManager.endPoints[currentBoardsIndex].position) < .5f)
            {
                isRunning = false;
                isJumping = true;

                rb.velocity = Extension.CalculateVelocity(transform, LevelManager.startPoints[currentBoardsIndex + 1], jumpTime);
                anim.SetTrigger("jump");
            }
        }
    }

    void getMouseInput()
    {
        if (Input.GetMouseButtonDown(0) && ready && EventSystem.current.IsPointerOverGameObject() == false)
        {
            ready = false;
            haveInput = true;
            startPos = Input.mousePosition.x;
            gravityModifier = 0;
            projectionLine.transform.position = hand.position;
            projectionLine.positionCount = 0;
            projectionLine.enabled = true;
            cancelBtn.color = Color.red.ChangeAlpha(1);
        }
        else if (Input.GetMouseButton(0) && haveInput)
        {
            endPos = Input.mousePosition.x;
            float dif = (endPos - startPos) / Screen.width * 10;

            gravityModifier += dif;
            ShowTrajectory(CalculateVelocity(endPoint.position, hand.position, throwTime));

            startPos = Input.mousePosition.x;

            if (isThrowRequired(Input.mousePosition))
            {
                cancelBtn.transform.localScale = Vector3.one;
                cancelBtn.color = Color.red;
            }
            else
            {
                cancelBtn.transform.localScale = Vector3.one * 1.2f;
                cancelBtn.color = Color.green;
            }
        }
        else if (Input.GetMouseButtonUp(0) && haveInput)
        {
            haveInput = false;
            startPos = 0; endPos = 0;
            projectionLine.enabled = false;

            if (isThrowRequired(Input.mousePosition))
                anim.SetTrigger("throw");
            else
            {
                currentBlade = null;
                ready = true;
            }

            cancelBtn.color = Color.red.ChangeAlpha(0);
            cancelBtn.transform.localScale = Vector3.one;
        }
    }

    void getTouchInput()
    {
        if (Input.touchCount == 0)
            return;
        Touch touch = Input.GetTouch(0);

        if(touch.phase == TouchPhase.Began && ready && EventSystem.current.IsPointerOverGameObject(touch.fingerId) == false)
        {
            ready = false;
            haveInput = true;
            startPos = touch.position.x;
            gravityModifier = 0;
            projectionLine.transform.position = hand.position;
            projectionLine.positionCount = 0;
            projectionLine.enabled = true;
            cancelBtn.color = Color.red.ChangeAlpha(1);
        }
        else if(touch.phase == TouchPhase.Moved && haveInput || touch.phase == TouchPhase.Stationary && haveInput)
        {
            endPos = touch.position.x;
            float dif = (endPos - startPos) / Screen.width * 10;

            gravityModifier += dif;
            ShowTrajectory(CalculateVelocity(endPoint.position, hand.position, throwTime));

            startPos = touch.position.x;

            if (isThrowRequired(touch))
            {
                throwNeeded = true;
                cancelBtn.transform.localScale = Vector3.one;
                cancelBtn.color = Color.red;
            }
            else
            {
                throwNeeded = false;
                cancelBtn.transform.localScale = Vector3.one * 1.2f;
                cancelBtn.color = Color.green;
            }
        }
        else if(touch.phase == TouchPhase.Ended && haveInput || touch.phase == TouchPhase.Canceled && haveInput)
        {
            haveInput = false;
            startPos = 0; endPos = 0;
            projectionLine.enabled = false;

            if (throwNeeded)
                anim.SetTrigger("throw");
            else
            {
                currentBlade = null;
                ready = true;
            }
            throwNeeded = false;

            cancelBtn.color = Color.red.ChangeAlpha(0);
            cancelBtn.transform.localScale = Vector3.one;
        }
    }

    bool isThrowRequired(Vector2 inputPosition)
    {
        bool result = false;
        if (EventSystem.current.IsPointerOverGameObject() == false)
            result = true;
        else if (EventSystem.current.IsPointerOverGameObject() == true)
        {
            result = true;

            PointerEventData ped = new PointerEventData(EventSystem.current);
            ped.position = inputPosition;
            List<RaycastResult> hitList = new List<RaycastResult>();
            EventSystem.current.RaycastAll(ped, hitList);
            string s = hitList[0].gameObject.name;

            foreach(RaycastResult g in hitList)
            {
                if(g.gameObject.name == cancelName)
                {
                    result = false;
                    break;
                }
            }
        }
        return result;
    }
    bool isThrowRequired(Touch touch)
    {
        bool result = false;

        if (EventSystem.current.IsPointerOverGameObject(touch.fingerId) == false)
            result = true;
        else if (EventSystem.current.IsPointerOverGameObject(touch.fingerId) == true)
        {
            result = true;

            PointerEventData ped = new PointerEventData(EventSystem.current);
            ped.position = touch.position;
            List<RaycastResult> hitList = new List<RaycastResult>();
            EventSystem.current.RaycastAll(ped, hitList);
            string s = hitList[0].gameObject.name;

            foreach (RaycastResult g in hitList)
            {
                if (g.gameObject.name == cancelName)
                {
                    result = false;
                    break;
                }
            }
        }
        return result;
    }
    //Function to call to throw the weapon //used from animation event for proper syncing
    public void OnThrow()
    {
        Rigidbody r = PrefabManager.throwingBladePrefab.Instantiate(hand.position).GetComponent<Rigidbody>();
        r.velocity = CalculateVelocity(endPoint.position, hand.position, throwTime);
        currentBlade = r;
        FunctionTimer.Create(() => { ready = true; }, .2f);

        LevelManager.instance.BladeThrowed();
    }

    public void Die()
    {
        GameManager.instance.gameLose();
        GetComponent<RagdollManager>().ActivateRagdoll();
    }

    Vector3 CalculateVelocity(Vector3 EndPos, Vector3 StartPos, float time)
    {
        Vector3 distance = EndPos - StartPos;
        Vector3 distanceYZ = distance;
        distanceYZ.x = 0;

        float Sx = distance.x;
        float Syz = distanceYZ.magnitude;

        float Vyz = Syz / time;
        float Vx = Sx / time + 0.5f * XGravity * gravityModifier * time;

        Vector3 result = distanceYZ.normalized;
        result *= Vyz;
        result.x = Vx;
        return result;
    }

    void ShowTrajectory(Vector3 finalVelocity)
    {
        int maxIterations = Mathf.RoundToInt(throwTime / Time.fixedDeltaTime);
        projectionLine.positionCount = maxIterations;
        Vector3 pos = hand.position;
        Vector3 vel = finalVelocity;

        float elapsedTime = 0.0f;

        for (int i = 0; i < maxIterations; i++)
        {
            vel = vel + (new Vector3(-XGravity * gravityModifier, 0, 0) * Time.fixedDeltaTime);
            pos += vel * Time.fixedDeltaTime;
            elapsedTime += Time.fixedDeltaTime;
            projectionLine.SetPosition(i, pos.ReplaceY(endPoint.position.y));
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if(collision.gameObject.CompareTag(TagsLayers.groundTag) && isJumping)
        {
            Vibration.Vibrate(40);
            currentBoardsIndex++;
            ChangeEndpoint();

            rb.velocity = Vector3.zero;
            anim.SetBool("run", false);
            anim.SetTrigger("land");
            isJumping = false;

            FunctionTimer.Create(() => SetTimeScale(1), .5f);
            CameraController.instance.StartShaking(Random.Range(.1f,.2f), Random.Range(.2f, .4f));
        }

        if(collision.gameObject.CompareTag(TagsLayers.finishLineTag) && GameManager.gameState != GAMESTATE.Win)
        {
            rb.velocity = Vector3.zero;
            anim.SetBool("run", false);
            anim.SetTrigger("land");
            isJumping = false;

            FunctionTimer.Create(() => { anim.SetBool("dance", true); }, .05f);
            FunctionTimer.Create(() => SetTimeScale(1), .5f);
            CameraController.instance.GameWon();
            CameraController.instance.StartShaking(Random.Range(.1f, .15f), Random.Range(.25f, .4f));
            GameManager.instance.gameWon();
            Vibration.Vibrate(50);

            //For spawning confettis
            float radius = 5.18f, yPos = 0.0291f;

            Transform finishLineHolder = collision.transform.parent;
            PrefabManager.SpawnPrefab(PrefabManager.confettiParticle, finishLineHolder, new Vector3(radius, yPos,0), Vector3.one * .5f);
            PrefabManager.SpawnPrefab(PrefabManager.confettiParticle, finishLineHolder, new Vector3(-radius, yPos,0), Vector3.one * .5f);
            PrefabManager.SpawnPrefab(PrefabManager.confettiParticle, finishLineHolder, new Vector3(0, yPos,radius), Vector3.one * .5f);
            PrefabManager.SpawnPrefab(PrefabManager.confettiParticle, finishLineHolder, new Vector3(0, yPos,-radius), Vector3.one * .5f);
        }
    }

    public void SetEndPoint()
    {
        if (isRunning || isJumping)
            return;

        float v = eSlider.value - .5f;
        v = v.Clamp(-0.5f, 0.5f);
        Vector3 finalPos = endInitPosition + Vector3.right * v * maxTravel;
        endPoint.position = finalPos;
    }

    public void StartRunning()
    {
        if (currenTargetBoard != null)
            currenTargetBoard.DisableBoard();

        runDirection = (LevelManager.endPoints[currentBoardsIndex].position - transform.position).ReplaceY(0).normalized;
        isRunning = true;
        anim.SetBool("run", true);

        SetTimeScale(1);
    }

    void SetTimeScale(float scale)
    {
        if (scale <= Mathf.Epsilon)
            scale = 1;

        Time.timeScale = scale;
        Time.fixedDeltaTime = scale * .02f;
    }

    public void DisableInput()
    {
        rb.velocity = Vector3.zero;
        haveInput = false;
        projectionLine.enabled = false;
        ready = false;

        isRunning = false;
        anim.SetBool("run", false);
        SetTimeScale(1);

        if (GameManager.gameState == GAMESTATE.Lose)
            anim.SetInteger("fail", Random.Range(1, 3));
    }
}

using UnityEngine;
using UnityEngine.SceneManagement;
using CustomExtensions;
using System.Collections.Generic;

public class LevelManager : MonoBehaviour
{
    public static LevelManager instance { get; private set; }
    #region Pointers
    const string pointersName = "Pointers";
    const string startersName = "Starts";
    const string endsName = "Ends";
    const string targetboardsName = "TargetBoards";

    public static Transform[] startPoints { get; private set; }
    public static Transform[] endPoints { get; private set; }
    public static Transform[] targetBoards { get; private set; }
    #endregion

    int currentLevel = 0;
    public IntArray[] enemyCounts = null;
    public static int currentEnemies { get; private set; }
    int zoneIndex = 0;
    int totalZone = 0;

    [Header("For Blade Count")]
    [SerializeField] int[] BladeCounts = null;
    static int[] bladeCountArray = null;
    public static int BladeCount { get; private set; }

    Transform[] dummyBlades = null;

    public static EnemyController[] allEnemies { get; private set; }
    public static Prisoner[] allPrioners { get; private set; }

    private void Awake()
    {
        instance = this;
        bladeCountArray = BladeCounts;

        SceneManager.sceneLoaded += OnSceneLoad;
        //GetPointerValues();
    }

    void OnSceneLoad(Scene scene, LoadSceneMode loadMode)
    {
        if(scene.buildIndex == currentLevel)
        {
            FunctionTimer.Create(GetPointerValues, .1f);
            FunctionTimer.Create(PlayerController.instance.ChangeEndpoint, .12f);
        }
    }

    private void Start()
    {
        currentLevel = DataManager.currentLevel-1;
        BladeCount = bladeCountArray[currentLevel];
        SpawnPlayerDummyBlades();

        zoneIndex = 0;
        currentEnemies = enemyCounts[currentLevel].count[zoneIndex];
        totalZone = enemyCounts[currentLevel].count.Length;
    }

    void SpawnPlayerDummyBlades()
    {
        Transform parent = PlayerController.instance.bladeContainer;
        float spawnDistance = 0.08f;

        dummyBlades = new Transform[5];
        for(int i = 0; i < BladeCount; i++)
        {
            Transform t = PrefabManager.SpawnPrefab(PrefabManager.dummyBladePrefab);
            t.SetParent(parent);
            t.localPosition = Vector3.up * i * -spawnDistance;
            t.localRotation = Quaternion.identity;
            t.localScale = Vector3.one;

            dummyBlades[i] = t;
        }
    }

    public void DecreaseEnemies(bool deathEffect = true)
    {
        currentEnemies--;
        if(currentEnemies <= 0)
        {
            zoneIndex++;
            currentEnemies = enemyCounts[currentLevel].count[zoneIndex];

            if (BladeCount <= 0 && !IsInLastZone())
                GameManager.instance.gameLose();
            else
                FunctionTimer.Create(() => PlayerController.instance.StartRunning(), .2f);
        }

        if (!deathEffect)
            return;

        Time.timeScale = .2f;
        Time.fixedDeltaTime = Time.timeScale * 0.02f;
        FunctionTimer.CreateUnscaled(() => { Time.timeScale = 1; Time.fixedDeltaTime = .02f; }, .3f);
    }

    void GetPointerValues()
    {
        Transform pointer = GameObject.FindGameObjectWithTag(TagsLayers.pointerTag).transform;
        if (pointer == null)
        {
            Debug.LogError("No Pointers found");
            return;
        }

        Transform s = pointer.Find(startersName);
        Transform e = pointer.Find(endsName);
        Transform tb = pointer.Find(targetboardsName);

        startPoints = s.GetChildsAsArray();
        endPoints = e.GetChildsAsArray();
        targetBoards = tb.GetChildsAsArray();

        GameObject[] enems = GameObject.FindGameObjectsWithTag(TagsLayers.enemyTag);
        allEnemies = enems.ToOtherComponentArray<EnemyController>();

        GameObject[] prisons = GameObject.FindGameObjectsWithTag(TagsLayers.prisonerTag);
        allPrioners = prisons.ToOtherComponentArray<Prisoner>();
    }

    public void BladeThrowed()
    {
        BladeCount--;
        UIManager.instance.UpdateInGameUI();
        releaseDummy();

        //if (BladeCount <= 0 && IsEnemyAlive())
        //    GameManager.instance.gameLose();
    }

    public void BladeDestroyed()
    {
        if (BladeCount <= 0 && IsEnemyAlive())
            GameManager.instance.gameLose();
    }

    public void releaseDummy()
    {
        Destroy(dummyBlades[BladeCount].gameObject);
    }

    bool IsEnemyAlive()
    {
        //Next zone index equals finish line zone, then return false
        //Debug.Log("Zone Index = " + zoneIndex + " || Total Zone = " + totalZone);
        if (zoneIndex >= totalZone - 1)
            return false;
        else
            return true;
    }

    bool IsInLastZone()
    {
        if (zoneIndex >= totalZone - 1)
            return true;
        else
            return false;
    }
}

[System.Serializable]
public class IntArray
{
    [SerializeField] string levelName = "Level ";
    [Tooltip("Number of enemies for each zones")]
    public int[] count = null;

    string NegateError() => levelName;
}

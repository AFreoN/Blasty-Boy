using UnityEngine;
using UnityEngine.SceneManagement;
using CustomExtensions;

public class DataManager : MonoBehaviour
{
    public static int currentLevel { get; private set; }

    public static int vibration { get; private set; }

    const string levelKey = "Level";
    static int MaxLevels = 3;

    [SerializeField] bool SetLevel = false;
    [SerializeField] int Level = 1;

    private void Awake()
    {
        MaxLevels = SceneManager.sceneCountInBuildSettings - 1;

        if(PlayerPrefs.HasKey(levelKey))
        {
            LoadLevel();
        }
        else
        {
            PlayerPrefs.SetInt(levelKey, 1);
            currentLevel = 1;
        }

        if(SetLevel)
        {
            currentLevel = Level;
            PlayerPrefs.SetInt(levelKey, currentLevel);
        }

        vibration = 1;
        SceneManager.LoadScene(currentLevel, LoadSceneMode.Additive);
    }

    public static void SaveLevel()
    {
        //currentLevel = (currentLevel + 1) % (totalLevel + 1);
        //currentLevel = currentLevel != 0 ? currentLevel : 1;
        currentLevel++;
        currentLevel = currentLevel.ClampToMin(1, MaxLevels);
        PlayerPrefs.SetInt(levelKey, currentLevel);
    }

    public static int LoadLevel()
    {
        currentLevel = PlayerPrefs.GetInt(levelKey,1);
        return currentLevel;
    }

    [ContextMenu("Set Level")]
    void SetLevelFromEditor()
    {
        PlayerPrefs.SetInt(levelKey, Level);
    }
}

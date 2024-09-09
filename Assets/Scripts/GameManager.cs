using UnityEngine;
using UnityEngine.SceneManagement;
using CustomExtensions;

public class GameManager : MonoBehaviour
{
    public static GameManager instance { get; private set; }

    public static GAMESTATE gameState { get; private set; }

    private void Awake()
    {
        Application.targetFrameRate = 60;

        instance = this;
        gameState = GAMESTATE.Menu;
    }

    void Update()
    {
#if UNITY_EDITOR
        if (Input.GetMouseButtonUp(1))
            ReloadScene();
#endif
        if(Input.GetKeyDown(KeyCode.Escape))
        {
            if (gameState == GAMESTATE.Menu)
                Application.Quit();
            else
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }

    public void startGame()
    {
        gameState = GAMESTATE.Game;
        UIManager.instance.GameStarted();
    }

    public void gameWon()
    {
        if (gameState != GAMESTATE.Game)
            return;

        gameState = GAMESTATE.Win;
        UIManager.instance.GameWon();
        DataManager.SaveLevel();

        if (PlayerController.instance != null)
            PlayerController.instance.DisableInput();
    }

    public void gameLose()
    {
        if (gameState != GAMESTATE.Game)
            return;

        gameState = GAMESTATE.Lose;
        UIManager.instance.GameLose();

        if (PlayerController.instance != null)
            PlayerController.instance.DisableInput();

        foreach (EnemyController e in LevelManager.allEnemies)
            e.OnGameOver();
    }

    public void ReloadScene()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}

public enum GAMESTATE
{
    Menu,
    Game,
    Win,
    Lose
}

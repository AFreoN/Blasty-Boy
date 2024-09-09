using UnityEngine;
using UnityEngine.UI;
using CustomExtensions;

public class UIManager : MonoBehaviour
{
    public static UIManager instance { get; private set; }

    [Header("Panels")]
    [SerializeField] GameObject mainMenuPanel = null;
    [SerializeField] GameObject inGamePanel = null;
    [SerializeField] GameObject gameWonPanel = null;
    [SerializeField] GameObject gameLosePanel = null;

    [Header("Texts")]
    [SerializeField] Text bladeCountText = null;
    [SerializeField] Text levelMainMenuText = null;
    [SerializeField] Text levelGameWonText = null;

    [Header("Images")]
    [SerializeField] Image wonEmoteImg = null;
    [SerializeField] Image loseEmoteImg = null;

    const string bladeCountPrefix = "<color=#ffffff>x</color>  ";

    private void Awake()
    {
        instance = this;
    }

    private void Start()
    {
        startSetter();
    }

    void startSetter()
    {
        mainMenuPanel.SetActive(true);
        inGamePanel.SetActive(false);
        gameWonPanel.SetActive(false);
        gameLosePanel.SetActive(false);

        bladeCountText.text = bladeCountPrefix + LevelManager.BladeCount.ToString();
        levelMainMenuText.text = "Level  " + DataManager.currentLevel.ToString().ToHtmlColorString(ColorsEnum.Yellow);
        levelGameWonText.text = DataManager.currentLevel.ToString() + "  >>  " + (DataManager.currentLevel + 1).ToString().ToHtmlColorString("FDFF00");
    }

    public void GameStarted()
    {
        mainMenuPanel.SetActive(false);
        inGamePanel.SetActive(true);
    }

    public void GameWon()
    {
        inGamePanel.SetActive(false);
        gameWonPanel.SetActive(true);

        EMOTETYPE et = Variance.Roll2 ? EMOTETYPE.Smiling : EMOTETYPE.SmileEyeClosed;
        wonEmoteImg.sprite = Emote.GetEmoteSprite(et);
    }

    public void GameLose()
    {
        inGamePanel.SetActive(false);
        gameLosePanel.SetActive(true);

        EMOTETYPE et = Variance.Roll2 ? EMOTETYPE.CryOneEye : EMOTETYPE.Sad;
        loseEmoteImg.sprite = Emote.GetEmoteSprite(et);
    }

    public void UpdateInGameUI()
    {
        bladeCountText.text = bladeCountPrefix + LevelManager.BladeCount.ToString();
    }
}

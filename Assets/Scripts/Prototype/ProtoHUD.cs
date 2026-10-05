using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public struct ProtoRunSummary
{
    public bool won, knockout, lastLevel, nextIsBoss;
    public int level, score, rooftops, totalRooftops, bossDamage, bossMaxHealth, bestMulti, accuracy, storms, seconds, stars;
}

public enum CalloutPriority { Low = 0, Medium = 1, High = 2, Top = 3 }

// Runtime-built HUD. Layout rules that keep a busy moment readable:
//   - one callout slot (kill graphics, KABOOM, SHADOW STORM...): a new callout of equal or higher priority replaces the
//     current one instead of stacking on it;
//   - one merged score popup per blade (+100 -> +300 -> +600) instead of one popup per kill;
//   - banners, callouts, world tags and the boss's speech bubble each own a separate screen zone;
//   - world tags are capped in count and size.
// Art comes from ProtoUISkin (Asset Store GUI kit, local only); every element falls back to generated sprites.
public class ProtoHUD : MonoBehaviour
{
    public static ProtoHUD instance { get; private set; }

    [SerializeField] ProtoUISkin skin = null;
    [SerializeField] Font legacyFont = null;

    static readonly Color gold = new Color(1f, 0.84f, 0.15f);
    static readonly Color red = new Color(1f, 0.25f, 0.28f);
    static readonly Color purple = new Color(0.72f, 0.38f, 1f);
    static readonly Color[] comboColors =
    {
        Color.white, Color.white, new Color(1f, 0.92f, 0.2f), new Color(1f, 0.6f, 0.15f),
        new Color(1f, 0.3f, 0.4f), new Color(1f, 0.35f, 0.95f), new Color(0.4f, 1f, 1f),
    };
    static readonly string[] comboWords = { "", "", "DOUBLE KILL!", "TRIPLE KILL!", "QUADRA KILL!", "PENTA KILL!", "UNSTOPPABLE!" };

    const int maxWorldTags = 4;

    RectTransform root;
    Camera cam;
    TMP_FontAsset font;

    // top bar
    Image[] hearts;
    TextMeshProUGUI scoreText;
    RectTransform scorePill, heartPill;
    Image[] stageNodes;
    TextMeshProUGUI stageLabel;
    int displayedScore, targetScore;
    float scorePunch, heartShake;

    // boss bar
    RectTransform bossRoot;
    Image bossFill, bossTrail;
    TextMeshProUGUI bossName, bossHp;
    float bossPunch;

    // bottom
    RectTransform quiverRoot;
    readonly List<Image> pipFills = new List<Image>();
    readonly List<float> pipPunch = new List<float>();
    int pipCount = -1, lastFull;
    float quiverShake;
    RectTransform stormRoot;
    Image stormFill, stormIcon, stormGlow;
    TextMeshProUGUI stormLabel;
    bool stormUsesOrb;

    // aim
    TextMeshProUGUI badge;
    int badgeValue;
    float badgePunch;
    readonly List<RectTransform> markers = new List<RectTransform>();
    readonly List<TextMeshProUGUI> markerTexts = new List<TextMeshProUGUI>();
    readonly List<Image> markerImages = new List<Image>();
    readonly List<ProtoTarget> markerTargets = new List<ProtoTarget>();
    readonly List<float> markerAge = new List<float>();
    readonly List<RectTransform> dangerTags = new List<RectTransform>();

    // feedback zones
    RectTransform calloutRoot, tagLayer;
    Image calloutImage;
    TextMeshProUGUI calloutText;
    Coroutine calloutRoutine;
    int calloutPriority = -1;
    float calloutUntil;
    RectTransform bannerRoot;
    Image bannerRibbon;
    TextMeshProUGUI bannerTitle, bannerSub;
    Image[] bannerStars;
    Coroutine bannerRoutine;
    RectTransform hintRoot;
    TextMeshProUGUI hintText;
    Image hintHand;
    bool hintVisible;
    RectTransform bubbleRoot;
    TextMeshProUGUI bubbleText;
    float bubbleUntil;
    Image flash, vignette;
    Color flashColor;
    float vignetteAlpha;

    // world popups
    readonly List<WorldTag> tags = new List<WorldTag>();
    readonly Dictionary<ProtoBlade, WorldTag> bladeTags = new Dictionary<ProtoBlade, WorldTag>();

    // screens
    GameObject menuPanel, endPanel, pausePanel;
    TextMeshProUGUI endTitle, endSub, endButtonLabel, menuLevel;
    Button endButton;
    Image menuPlay;

    // boss intro
    RectTransform bossIntroRoot, bossIntroBand, bossIntroTitle, bossIntroIcon;
    RectTransform[] bossIntroTickers;
    TextMeshProUGUI bossIntroName;
    Image bossIntroDim;
    Image endRibbon;
    Image[] endStars;
    TextMeshProUGUI[] endTileValues;
    RectTransform pauseButton;

    class WorldTag
    {
        public RectTransform rt;
        public TextMeshProUGUI text;
        public Vector3 world;
        public float age, life, scale;
        public Color color;
        public Vector2 spread;
        public bool active;
    }

    private void Awake()
    {
        instance = this;
        cam = Camera.main;
        font = skin != null && skin.font != null ? skin.font : TMP_Settings.defaultFontAsset;
        // Dynamic atlases add glyphs on first use, which can flash as blocks for a frame (e.g. when the boss intro
        // slams in), so add the usual set up front.
        if (font != null && font.atlasPopulationMode == AtlasPopulationMode.Dynamic)
            font.TryAddCharacters("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 !?.,:;'\"-+/%()x");
        Build();
    }

    #region Public API
    public void ShowMenu(int level, string title, bool bossFight)
    {
        if (bossFight)
            title = "<color=#FF4D4D>BOSS FIGHT!</color>";
        else if (!string.IsNullOrEmpty(title))
            title = "<color=#FFD54A>" + title + "</color>";
        menuLevel.text = "LEVEL " + level + (string.IsNullOrEmpty(title) ? "" : "  " + title);
        SetButtonStyle(menuPlay, bossFight ? skin?.buttonRed : skin?.buttonYellow, bossFight ? red : gold);
        menuPanel.SetActive(true);
        endPanel.SetActive(false);
        SetGameplayVisible(false);
    }

    public void HideMenu()
    {
        menuPanel.SetActive(false);
        SetGameplayVisible(true);
    }

    public void SetHearts(int count, bool animateLoss)
    {
        for (int i = 0; i < hearts.Length; i++)
            hearts[i].color = i < count ? Color.white : new Color(0.2f, 0.2f, 0.28f, 0.6f);
        if (animateLoss && count >= 0 && count < hearts.Length)
        {
            StartCoroutine(Punch(hearts[count].rectTransform, 1.4f, 0.45f));
            heartShake = 1f;
        }
    }

    // One node per rooftop in this level; a showdown level puts the boss crown on the last one.
    public void SetWave(int wave, int total, bool showdownLast)
    {
        for (int i = 0; i < stageNodes.Length; i++)
        {
            Image node = stageNodes[i];
            node.gameObject.SetActive(i < total);
            if (i >= total)
                continue;
            bool crown = showdownLast && i == total - 1;
            node.sprite = crown ? skinOr(skin?.iconCrown, ProtoArt.Star) : skinOr(skin?.stageNode, ProtoArt.Circle);
            node.rectTransform.sizeDelta = crown ? new Vector2(52f, 52f) : new Vector2(28f, 28f);
            node.rectTransform.anchoredPosition = new Vector2(total > 1 ? -100f + i * 200f / (total - 1) : 0f, 10f);
            bool done = i < wave - 1, current = i == wave - 1;
            node.color = current ? gold : done ? new Color(0.45f, 0.9f, 0.45f) : new Color(1f, 1f, 1f, 0.35f);
            node.rectTransform.localScale = Vector3.one * (current ? 1.3f : 1f);
        }
        stageLabel.text = showdownLast && wave >= total ? "SHOWDOWN" : "ROOFTOP " + wave + "/" + total;
    }

    public void SetScore(int score, bool punch)
    {
        targetScore = score;
        if (!punch)
            displayedScore = score;
        else
            scorePunch = 1f;
    }

    public void ShowHint(string text)
    {
        hintText.text = text;
        hintVisible = true;
    }

    public void HideHint() => hintVisible = false;

    public void QuiverEmpty() => quiverShake = 1f;

    public void Flash(Color color) => flashColor = color;

    public void DamageFlash()
    {
        vignetteAlpha = 0.9f;
        flashColor = new Color(1f, 0.1f, 0.1f, 0.3f);
    }

    public void BossHit(bool big) => bossPunch = big ? 1f : 0.5f;

    // Multi-kill callout: the kit's DOUBLE/TRIPLE/QUADRA/PENTA KILL art when available, styled text otherwise.
    public void Combo(int n)
    {
        Sprite art = skin != null ? skin.Kill(n) : null;
        string word = n < comboWords.Length ? comboWords[n] : "x" + n + " UNSTOPPABLE!";
        ShowCallout(art, word, comboColors[Mathf.Min(n, comboColors.Length - 1)], CalloutPriority.Medium + Mathf.Min(n - 2, 1), 0.75f);
    }

    public void Callout(string text, Color color, CalloutPriority priority, float hold = 0.8f)
    {
        ShowCallout(null, text, color, priority, hold);
    }

    public void Banner(string title, string subtitle, float hold, bool gold = false)
    {
        StartBanner(title, subtitle, hold, gold ? skinOr(skin?.ribbonYellow) : skinOr(skin?.ribbonOrange), gold ? new Color(1f, 0.75f, 0.1f) : new Color(1f, 0.5f, 0.15f), -1);
    }

    // Opens a boss level: the screen dims, a red warning band with scrolling tickers sweeps in, and BOSS FIGHT! slams
    // down over the boss's name. Takes about 2.2 s; ProtoGame times its sounds and camera to the slam at 0.3 s.
    public void BossIntro(string bossName, string subtitle)
    {
        if (bannerRoutine != null)
            StopCoroutine(bannerRoutine);
        bannerRoot.gameObject.SetActive(false);
        HideHint();
        bossIntroName.text = bossName + (string.IsNullOrEmpty(subtitle) ? "" : "\n<size=55%><color=#FFD54A>" + subtitle + "</color></size>");
        StartCoroutine(BossIntroRoutine());
    }

    // "ROOFTOP CLEAR!" with stars for hearts kept, each landing on a beat with its stinger.
    public void RooftopClear(int stars, string subtitle, float hold)
    {
        StartBanner("ROOFTOP CLEAR!", subtitle, hold, skinOr(skin?.ribbonGreen), new Color(0.3f, 0.8f, 0.3f), Mathf.Clamp(stars, 0, 3));
    }

    // Boss lines go in a speech bubble over his head, one at a time.
    public void Taunt(string line)
    {
        bubbleText.text = line;
        bubbleUntil = Time.unscaledTime + 1.8f;
        bubbleRoot.gameObject.SetActive(true);
        StartCoroutine(Punch(bubbleRoot, 0.35f, 0.3f));
    }

    // Small world-anchored tag (CLANG, OUCH, +500...). Capped in count; the oldest makes way.
    public void Popup(Vector3 world, string text, Color color, float scale = 1f)
    {
        WorldTag tag = TakeTag();
        tag.world = world;
        tag.text.text = text;
        tag.color = color;
        tag.scale = Mathf.Clamp(scale, 0.8f, 1.25f);
        tag.life = 0.9f;
        tag.age = 0f;
        tag.spread = new Vector2(Random.Range(-50f, 50f), 0f);
        tag.text.fontSize = 56f;
    }

    // One running score tag per blade: each extra kill on the same blade updates and re-punches it.
    public void BladeScore(ProtoBlade blade, Vector3 world, int total, int kills)
    {
        if (blade == null || !bladeTags.TryGetValue(blade, out WorldTag tag) || !tag.active)
        {
            tag = TakeTag();
            if (blade != null)
                bladeTags[blade] = tag;
            tag.spread = Vector2.zero;
        }
        tag.world = world;
        tag.text.text = "+" + total;
        tag.color = kills >= 3 ? new Color(1f, 0.65f, 0.2f) : kills == 2 ? new Color(1f, 0.92f, 0.3f) : Color.white;
        tag.scale = 1f + 0.08f * Mathf.Min(kills - 1, 3);
        tag.text.fontSize = 64f;
        tag.life = 1.1f;
        tag.age = 0f;
    }

    public void ShowEnd(ProtoRunSummary s)
    {
        SetGameplayVisible(false);
        endPanel.SetActive(true);
        endTitle.text = !s.won ? "KNOCKED OUT!" : s.knockout ? "VICTORY!" : "LEVEL " + s.level + " CLEAR!";
        endRibbon.sprite = s.won ? skinOr(skin?.ribbonGreen) : skinOr(skin?.ribbonOrange);
        endRibbon.color = endRibbon.sprite == ProtoArt.Sprite(ProtoArt.Square) ? (s.won ? new Color(0.3f, 0.8f, 0.3f) : new Color(0.9f, 0.35f, 0.2f)) : Color.white;
        string headline = !s.won ? "BIG BEAR GOT AWAY..." : s.knockout ? "BIG BEAR IS DOWN!" : "HE GOT AWAY! AFTER HIM!";
        if (s.won && s.lastLevel)
            headline += "\n<size=70%>ALL LEVELS CLEARED. MORE COMING SOON!</size>";
        endSub.text = headline + "\n<size=70%>ROOFTOPS " + s.rooftops + "/" + s.totalRooftops + "   STORMS " + s.storms + "   TIME " + s.seconds + "s</size>";
        endButtonLabel.text = !s.won ? "TRY AGAIN" : s.lastLevel ? "PLAY AGAIN" : s.nextIsBoss ? "NEXT: BOSS FIGHT!" : "NEXT LEVEL";
        endButtonLabel.fontSize = endButtonLabel.text.Length > 12 ? 56f : 72f;
        bool bossNext = s.won && s.nextIsBoss;
        SetButtonStyle(endButton.image, bossNext ? skin?.buttonRed : skin?.buttonGreen, bossNext ? red : new Color(0.3f, 0.85f, 0.35f));
        endButton.onClick.RemoveAllListeners();
        endButton.onClick.AddListener(() => ProtoAudio.Play(Sfx.Pop, 0.7f, 1.3f));
        if (s.won)
            endButton.onClick.AddListener(() => ProtoGame.instance.NextLevel());
        else
            endButton.onClick.AddListener(() => ProtoGame.instance.Retry());
        endTileValues[0].text = s.score.ToString("N0");
        endTileValues[1].text = s.bossDamage + "/" + s.bossMaxHealth;
        endTileValues[2].text = "x" + s.bestMulti;
        endTileValues[3].text = s.accuracy + "%";
        StartCoroutine(EndSequence(s.stars));
    }
    #endregion

    #region Per-frame updates
    private void LateUpdate()
    {
        float dt = Time.unscaledDeltaTime;
        float now = Time.unscaledTime;

        if (displayedScore != targetScore)
            displayedScore = Mathf.CeilToInt(Mathf.Lerp(displayedScore, targetScore, 1f - Mathf.Exp(-12f * dt)));
        if (Mathf.Abs(displayedScore - targetScore) < 2)
            displayedScore = targetScore;
        scoreText.text = displayedScore.ToString("N0");
        scorePunch = Mathf.Max(0f, scorePunch - dt * 5f);
        scorePill.localScale = Vector3.one * (1f + 0.15f * scorePunch * scorePunch);

        heartShake = Mathf.Max(0f, heartShake - dt * 3f);
        heartPill.anchoredPosition = new Vector2(30f + Mathf.Sin(now * 70f) * 16f * heartShake, -60f);

        flashColor.a = Mathf.Max(0f, flashColor.a - dt * 3.5f);
        flash.color = flashColor;
        vignetteAlpha = Mathf.Max(0f, vignetteAlpha - dt * 1.4f);
        vignette.color = new Color(1f, 0.05f, 0.05f, vignetteAlpha);

        UpdateHint(dt);
        UpdateBubble();
        UpdateQuiver(dt);
        UpdateBossAndStorm(dt);
        UpdateAimMarkers(dt);
        UpdateDanger();
        UpdateTags(dt);
    }

    void UpdateHint(float dt)
    {
        float a = Mathf.MoveTowards(hintText.alpha, hintVisible ? 1f : 0f, dt * 4f);
        hintText.alpha = a;
        hintHand.color = new Color(1f, 1f, 1f, a);
        hintRoot.gameObject.SetActive(a > 0.01f);
        // The hand demonstrates the gesture: press, slide sideways, lift.
        float t = Mathf.Repeat(Time.unscaledTime, 1.6f) / 1.6f;
        float x = Mathf.Sin(t * Mathf.PI * 2f) * 150f;
        hintHand.rectTransform.anchoredPosition = new Vector2(x, 40f - (t < 0.1f || t > 0.9f ? 18f : 0f));
    }

    void UpdateBubble()
    {
        bool show = Time.unscaledTime < bubbleUntil && ProtoBoss.instance != null && ProtoBoss.instance.gameObject.activeInHierarchy;
        bubbleRoot.gameObject.SetActive(show);
        if (show)
            bubbleRoot.anchoredPosition = WorldToCanvas(ProtoBoss.instance.transform.position + Vector3.up * 3.4f) + new Vector2(90f, 40f);
    }

    void UpdateQuiver(float dt)
    {
        ProtoThrower thrower = ProtoThrower.instance;
        if (thrower == null)
            return;
        if (pipCount != thrower.QuiverMax)
            BuildPips(thrower.QuiverMax);

        int full = Mathf.FloorToInt(thrower.Quiver);
        float partial = thrower.Quiver - full;
        for (int i = 0; i < pipFills.Count; i++)
        {
            pipFills[i].fillAmount = i < full ? 1f : i == full ? partial : 0f;
            if (i < full && i >= lastFull)
                pipPunch[i] = 1f;
            pipPunch[i] = Mathf.Max(0f, pipPunch[i] - dt * 5f);
            pipFills[i].rectTransform.localScale = Vector3.one * (1f + 0.35f * pipPunch[i]);
            pipFills[i].color = i < full ? Color.white : new Color(1f, 1f, 1f, 0.5f);
        }
        lastFull = full;
        quiverShake = Mathf.Max(0f, quiverShake - dt * 4f);
        quiverRoot.anchoredPosition = new Vector2(Mathf.Sin(Time.unscaledTime * 80f) * 20f * quiverShake, 150f);
    }

    void UpdateBossAndStorm(float dt)
    {
        ProtoGame game = ProtoGame.instance;
        if (game == null || game.Boss == null)
            return;

        ProtoBoss boss = game.Boss;
        float hp = boss.MaxHealth > 0 ? (float)boss.Health / boss.MaxHealth : 0f;
        bossFill.fillAmount = hp;
        bossTrail.fillAmount = Mathf.Max(hp, Mathf.MoveTowards(bossTrail.fillAmount, hp, dt * 0.35f));
        bossPunch = Mathf.Max(0f, bossPunch - dt * 4f);
        bossRoot.localScale = Vector3.one * (1f + 0.08f * bossPunch);
        bossRoot.anchoredPosition = new Vector2(Mathf.Sin(Time.unscaledTime * 60f) * 12f * bossPunch, -190f);
        bossName.text = boss.DisplayName;
        bossHp.text = boss.Health + "/" + boss.MaxHealth;

        ProtoThrower thrower = ProtoThrower.instance;
        bool storming = thrower != null && thrower.InFever;
        float charge = storming ? thrower.Fever01 : game.FeverCharge;
        bool ready = !storming && charge >= 1f;
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 12f);
        if (stormUsesOrb)
        {
            // Cooldown-style shade that recedes as the meter charges (and creeps back while the storm drains).
            stormFill.fillAmount = 1f - charge;
        }
        else
        {
            stormFill.fillAmount = charge;
            stormFill.color = storming ? Color.Lerp(purple, Color.white, pulse * 0.5f) : purple;
        }
        stormGlow.color = new Color(purple.r, purple.g, purple.b, storming ? 0.5f + 0.4f * pulse : charge * 0.35f);
        stormLabel.text = storming ? "HOLD!" : charge >= 0.999f ? "READY!" : "STORM";
        stormRoot.localScale = Vector3.one * (storming ? 1.1f + 0.06f * pulse : 1f);
        if (storming && vignetteAlpha < 0.3f)
            vignette.color = new Color(0.55f, 0.2f, 1f, 0.22f + 0.1f * pulse);
    }

    void UpdateAimMarkers(float dt)
    {
        ProtoThrower thrower = ProtoThrower.instance;
        bool aiming = thrower != null && thrower.IsAiming && !thrower.IsCancelling;
        int used = 0;

        if (aiming)
        {
            int order = 0;
            for (int i = 0; i < thrower.PredictedTargets.Count; i++)
            {
                ProtoTarget target = thrower.PredictedTargets[i];
                PredictState state = thrower.PredictedStates[i];
                if (target == null)
                    continue;

                string label;
                Color color;
                if (state == PredictState.Hit && target is ProtoEnemy) { order++; label = order.ToString(); color = comboColors[Mathf.Clamp(order, 2, comboColors.Length - 1)]; }
                else if (state == PredictState.Hit) { label = "!"; color = new Color(1f, 0.55f, 0.1f); }
                else if (state == PredictState.Penalty) { label = "!"; color = red; }
                else { label = "X"; color = red; }

                if (used >= markers.Count)
                    AddMarker();
                RectTransform m = markers[used];
                if (markerTargets[used] != target)
                {
                    markerTargets[used] = target;
                    markerAge[used] = 0f;
                }
                markerAge[used] += dt;
                float pop = Mathf.Clamp01(markerAge[used] / 0.18f);
                m.localScale = Vector3.one * OutBack(pop);
                m.anchoredPosition = WorldToCanvas(target.MarkerPosition);
                m.gameObject.SetActive(true);
                markerTexts[used].text = label;
                markerImages[used].color = color;
                used++;
            }
        }
        for (int i = used; i < markers.Count; i++)
        {
            markers[i].gameObject.SetActive(false);
            markerTargets[i] = null;
        }

        int kills = aiming ? thrower.PredictedKills : 0;
        if (kills != badgeValue)
        {
            badgeValue = kills;
            badgePunch = 1f;
        }
        badgePunch = Mathf.Max(0f, badgePunch - dt * 5f);
        badge.gameObject.SetActive(kills > 0);
        if (kills > 0)
        {
            badge.text = "x" + kills;
            badge.color = comboColors[Mathf.Clamp(kills, 1, comboColors.Length - 1)];
            badge.rectTransform.localScale = Vector3.one * (1f + 0.5f * badgePunch * badgePunch);
            ProtoBoss boss = ProtoBoss.instance;
            Vector2 at = boss != null ? WorldToCanvas(boss.AimPoint + Vector3.up * 2.2f) : new Vector2(0f, 400f);
            badge.rectTransform.anchoredPosition = at + new Vector2(-150f, 0f);
        }
    }

    void UpdateDanger()
    {
        int used = 0;
        foreach (ProtoTarget t in ProtoTarget.all)
        {
            if (!(t is ProtoHostage h) || h.IsResolved || !ProtoGame.IsPlaying)
                continue;
            if (used >= dangerTags.Count)
                dangerTags.Add(BuildDangerTag());
            RectTransform d = dangerTags[used++];
            bool urgent = h.Danger01 < 0.35f;
            d.gameObject.SetActive(true);
            d.GetComponentInChildren<TextMeshProUGUI>().text = "HELP! " + Mathf.CeilToInt(h.DangerSeconds);
            d.GetComponent<Image>().color = urgent ? red : new Color(0.1f, 0.12f, 0.2f, 0.85f);
            d.anchoredPosition = WorldToCanvas(h.MarkerPosition + Vector3.up * 0.4f);
            d.localScale = Vector3.one * (urgent ? 1.05f + 0.1f * Mathf.Sin(Time.unscaledTime * 18f) : 1f);
        }
        for (int i = used; i < dangerTags.Count; i++)
            dangerTags[i].gameObject.SetActive(false);
    }

    void UpdateTags(float dt)
    {
        foreach (WorldTag tag in tags)
        {
            if (!tag.active)
                continue;
            tag.age += dt;
            float k = tag.age / tag.life;
            if (k >= 1f)
            {
                tag.active = false;
                tag.rt.gameObject.SetActive(false);
                continue;
            }
            float pop = OutBack(Mathf.Clamp01(tag.age / 0.14f));
            tag.rt.anchoredPosition = WorldToCanvas(tag.world) + tag.spread + new Vector2(0f, 110f * (1f - (1f - k) * (1f - k)));
            tag.rt.localScale = Vector3.one * tag.scale * Mathf.LerpUnclamped(1.5f, 1f, pop);
            tag.text.color = new Color(tag.color.r, tag.color.g, tag.color.b, Mathf.Clamp01((1f - k) / 0.3f));
        }
        var stale = new List<ProtoBlade>();
        foreach (var kv in bladeTags)
            if (kv.Key == null || !kv.Value.active)
                stale.Add(kv.Key);
        foreach (var b in stale)
            bladeTags.Remove(b);
    }

    WorldTag TakeTag()
    {
        WorldTag free = tags.Find(t => !t.active);
        int activeCount = tags.FindAll(t => t.active).Count;
        if (free == null && tags.Count < maxWorldTags + 2)
        {
            free = new WorldTag();
            free.text = Label("Tag", tagLayer, "", 56f, Color.white, Center, Vector2.zero, new Vector2(500f, 100f));
            free.rt = free.text.rectTransform;
            tags.Add(free);
        }
        if (free == null || activeCount >= maxWorldTags)
        {
            // Retire the oldest so the screen never holds more than a handful of tags.
            WorldTag oldest = null;
            foreach (WorldTag t in tags)
                if (t.active && (oldest == null || t.age > oldest.age))
                    oldest = t;
            free = oldest ?? free;
        }
        free.active = true;
        free.rt.gameObject.SetActive(true);
        free.rt.SetAsLastSibling();
        return free;
    }

    Vector2 WorldToCanvas(Vector3 world)
    {
        Vector3 screen = cam.WorldToScreenPoint(world);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen, null, out Vector2 local);
        return local;
    }
    #endregion

    #region Callouts, banners, end sequence
    void ShowCallout(Sprite art, string text, Color color, CalloutPriority priority, float hold)
    {
        int p = (int)priority;
        if (Time.unscaledTime < calloutUntil && p < calloutPriority)
            return;   // something more important is on screen; this one isn't worth covering it
        calloutPriority = p;
        calloutUntil = Time.unscaledTime + hold + 0.2f;
        if (calloutRoutine != null)
            StopCoroutine(calloutRoutine);
        calloutRoutine = StartCoroutine(CalloutRoutine(art, text, color, hold));
    }

    IEnumerator CalloutRoutine(Sprite art, string text, Color color, float hold)
    {
        calloutRoot.gameObject.SetActive(true);
        calloutImage.gameObject.SetActive(art != null);
        calloutText.gameObject.SetActive(text != null);
        if (art != null)
        {
            calloutImage.sprite = art;
            calloutImage.SetNativeSize();
            float w = calloutImage.rectTransform.sizeDelta.x;
            calloutImage.rectTransform.localScale = Vector3.one * (w > 1f ? 900f / w : 1f);
        }
        if (text != null)
        {
            calloutText.text = text;
            calloutText.color = color;
        }
        calloutRoot.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-5f, 5f));

        CanvasGroup group = calloutRoot.GetComponent<CanvasGroup>();
        for (float t = 0f; t < 0.16f; t += Time.unscaledDeltaTime)
        {
            calloutRoot.localScale = Vector3.one * Mathf.LerpUnclamped(2.2f, 1f, OutBack(t / 0.16f));
            group.alpha = Mathf.Clamp01(t / 0.05f);
            yield return null;
        }
        calloutRoot.localScale = Vector3.one;
        group.alpha = 1f;
        yield return new WaitForSecondsRealtime(hold);
        for (float t = 0f; t < 0.2f; t += Time.unscaledDeltaTime)
        {
            group.alpha = 1f - t / 0.2f;
            calloutRoot.localScale = Vector3.one * (1f + 0.15f * t / 0.2f);
            yield return null;
        }
        calloutRoot.gameObject.SetActive(false);
        calloutPriority = -1;
    }

    void StartBanner(string title, string subtitle, float hold, Sprite ribbon, Color fallbackColor, int stars)
    {
        // A banner owns the middle of the screen: clear any callout so they never overlap.
        if (calloutRoutine != null)
            StopCoroutine(calloutRoutine);
        calloutRoot.gameObject.SetActive(false);
        calloutPriority = -1;

        if (bannerRoutine != null)
            StopCoroutine(bannerRoutine);
        bannerRibbon.sprite = ribbon;
        bannerRibbon.color = ribbon == ProtoArt.Sprite(ProtoArt.Square) ? fallbackColor : Color.white;
        bannerTitle.text = title;
        bannerSub.text = subtitle ?? "";
        bannerRoutine = StartCoroutine(BannerRoutine(hold, stars));
    }

    IEnumerator BannerRoutine(float hold, int stars)
    {
        bannerRoot.gameObject.SetActive(true);
        CanvasGroup group = bannerRoot.GetComponent<CanvasGroup>();
        foreach (Image s in bannerStars)
            s.gameObject.SetActive(false);

        for (float t = 0f; t < 0.22f; t += Time.unscaledDeltaTime)
        {
            float k = OutBack(t / 0.22f);
            bannerRoot.localScale = new Vector3(Mathf.LerpUnclamped(0.3f, 1f, k), Mathf.LerpUnclamped(1.4f, 1f, k), 1f);
            group.alpha = Mathf.Clamp01(t / 0.08f);
            yield return null;
        }
        bannerRoot.localScale = Vector3.one;
        group.alpha = 1f;

        // stars < 0: a plain banner; 0..3: show three star slots, earned ones landing on a beat.
        if (stars >= 0)
        {
            for (int i = 0; i < bannerStars.Length; i++)
            {
                Image s = bannerStars[i];
                s.gameObject.SetActive(true);
                bool earned = i < stars;
                s.color = earned ? Color.white : new Color(0.2f, 0.2f, 0.3f, 0.7f);
                StartCoroutine(Punch(s.rectTransform, earned ? 0.6f : 0.2f, 0.3f));
                if (earned)
                {
                    ProtoMusic.Play(i == 0 ? MusicCue.Star1 : i == 1 ? MusicCue.Star2 : MusicCue.Star3);
                    ProtoAudio.Play(Sfx.Tick, 0.7f, ProtoAudio.Semitones(i * 4));
                }
                yield return new WaitForSecondsRealtime(0.22f);
            }
        }

        yield return new WaitForSecondsRealtime(hold);
        for (float t = 0f; t < 0.2f; t += Time.unscaledDeltaTime)
        {
            group.alpha = 1f - t / 0.2f;
            bannerRoot.localScale = Vector3.one * (1f + 0.1f * t / 0.2f);
            yield return null;
        }
        bannerRoot.gameObject.SetActive(false);
    }

    IEnumerator BossIntroRoutine()
    {
        const float hold = 1.6f;
        bossIntroRoot.gameObject.SetActive(true);
        CanvasGroup group = bossIntroRoot.GetComponent<CanvasGroup>();
        group.alpha = 1f;
        float total = 0.35f + hold + 0.25f;
        for (float t = 0f; t < total; t += Time.unscaledDeltaTime)
        {
            float bandIn = OutBack(Mathf.Clamp01(t / 0.18f));
            float titleIn = Mathf.Clamp01((t - 0.12f) / 0.2f);
            float exit = Mathf.Clamp01((t - 0.35f - hold) / 0.25f);

            bossIntroDim.color = new Color(0f, 0f, 0f, 0.5f * Mathf.Clamp01(t / 0.15f) * (1f - exit));
            bossIntroBand.localScale = new Vector3(1f, Mathf.LerpUnclamped(0f, 1f, bandIn) * (1f - exit), 1f);
            bossIntroTitle.localScale = Vector3.one * (titleIn <= 0f ? 0f : Mathf.LerpUnclamped(2.6f, 1f, OutBack(titleIn)));
            bossIntroIcon.localScale = Vector3.one * (1f + 0.1f * Mathf.Sin(t * 9f) * (1f - exit));
            // The two tickers scroll in opposite directions and wrap every 700 px (one repeat of their text).
            for (int i = 0; i < bossIntroTickers.Length; i++)
                bossIntroTickers[i].anchoredPosition = new Vector2((i == 0 ? -1f : 1f) * Mathf.Repeat(t * 260f, 700f), 0f);
            group.alpha = 1f - exit;
            yield return null;
        }
        bossIntroRoot.gameObject.SetActive(false);
    }

    IEnumerator EndSequence(int stars)
    {
        RectTransform title = endRibbon.rectTransform;
        for (float t = 0f; t < 0.25f; t += Time.unscaledDeltaTime)
        {
            title.localScale = Vector3.one * Mathf.LerpUnclamped(2f, 1f, OutBack(t / 0.25f));
            yield return null;
        }
        title.localScale = Vector3.one;
        for (int i = 0; i < endStars.Length; i++)
        {
            endStars[i].color = new Color(0.2f, 0.2f, 0.3f, 0.7f);
            endStars[i].rectTransform.localScale = Vector3.one;
        }
        yield return new WaitForSecondsRealtime(0.2f);
        for (int i = 0; i < stars && i < endStars.Length; i++)
        {
            endStars[i].color = Color.white;
            StartCoroutine(Punch(endStars[i].rectTransform, 0.8f, 0.35f));
            ProtoMusic.Play(i == 0 ? MusicCue.Star1 : i == 1 ? MusicCue.Star2 : MusicCue.Star3);
            yield return new WaitForSecondsRealtime(0.3f);
        }
    }
    #endregion

    #region Construction
    static readonly TextAlignmentOptions Center = TextAlignmentOptions.Center;

    void Build()
    {
        var go = new GameObject("HUD Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        go.transform.SetParent(transform, false);
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.matchWidthOrHeight = 0f;
        root = (RectTransform)go.transform;

        vignette = Img("Vignette", root, ProtoArt.Sprite(ProtoArt.Vignette), Color.clear, Mid, Vector2.zero, Vector2.zero);
        Stretch(vignette.rectTransform);
        flash = Img("Flash", root, null, Color.clear, Mid, Vector2.zero, Vector2.zero);
        Stretch(flash.rectTransform);

        BuildMarkersLayer();
        BuildTopBar();
        BuildBossBar();
        BuildBottom();
        BuildFeedback();
        BuildMenu();
        BuildEnd();
        BuildPause();
    }

    void BuildMarkersLayer()
    {
        badge = Label("Badge", root, "", 120f, gold, Center, Vector2.zero, new Vector2(400f, 160f));
    }

    void BuildTopBar()
    {
        // Hearts (top-left)
        heartPill = Img("Hearts", root, skin?.pill, new Color(0.08f, 0.1f, 0.2f, 0.75f), new Vector2(0f, 1f), new Vector2(30f, -60f), new Vector2(285f, 96f)).rectTransform;
        heartPill.pivot = new Vector2(0f, 0.5f);
        hearts = new Image[ProtoGame.instance != null ? ProtoGame.instance.MaxHearts : 3];
        for (int i = 0; i < hearts.Length; i++)
            hearts[i] = Img("Heart", heartPill, skinOr(skin?.iconHeart, ProtoArt.Heart), skin?.iconHeart != null ? Color.white : red, new Vector2(0f, 0.5f), new Vector2(55f + i * 88f, 0f), new Vector2(78f, 78f));

        // Score (top-right)
        scorePill = Img("Score", root, skin?.pill, new Color(0.08f, 0.1f, 0.2f, 0.75f), new Vector2(1f, 1f), new Vector2(-30f, -60f), new Vector2(300f, 96f)).rectTransform;
        scorePill.pivot = new Vector2(1f, 0.5f);
        Img("Trophy", scorePill, skinOr(skin?.iconTrophy, ProtoArt.Star), gold, new Vector2(0f, 0.5f), new Vector2(50f, 2f), new Vector2(76f, 76f));
        scoreText = Label("Value", scorePill, "0", 54f, Color.white, TextAlignmentOptions.MidlineRight, new Vector2(1f, 0.5f), new Vector2(-28f, 2f), new Vector2(200f, 90f));
        scoreText.rectTransform.pivot = new Vector2(1f, 0.5f);

        // Rooftop progress (top-centre): one node per rooftop, laid out by SetWave.
        var stage = Rect("Stage", root, new Vector2(0.5f, 1f), new Vector2(-8f, -55f), new Vector2(250f, 100f));
        Img("Path", stage, null, new Color(1f, 1f, 1f, 0.25f), Mid, new Vector2(0f, 10f), new Vector2(200f, 6f));
        stageNodes = new Image[5];
        for (int i = 0; i < stageNodes.Length; i++)
        {
            bool last = i == stageNodes.Length - 1;
            Sprite s = last ? skinOr(skin?.iconCrown, ProtoArt.Star) : skinOr(skin?.stageNode, ProtoArt.Circle);
            stageNodes[i] = Img("Node", stage, s, Color.white, Mid, new Vector2(-100f + i * 50f, 10f), last ? new Vector2(52f, 52f) : new Vector2(28f, 28f));
        }
        stageLabel = Label("Label", stage, "ROOFTOP 1/5", 30f, Color.white, Center, Mid, new Vector2(0f, -32f), new Vector2(250f, 40f));

        pauseButton = Btn("Pause", root, skin?.buttonPause, null, new Color(0.2f, 0.45f, 0.9f), new Vector2(1f, 1f), new Vector2(-95f, -175f), new Vector2(96f, 96f), () => ProtoGame.instance.TogglePause()).rectTransform;
        if (skin?.buttonPause == null)
            Label("II", pauseButton, "II", 48f, Color.white, Center, Mid, Vector2.zero, new Vector2(96f, 96f));
    }

    void BuildBossBar()
    {
        bossRoot = Rect("Boss", root, new Vector2(0.5f, 1f), new Vector2(0f, -190f), new Vector2(720f, 90f));
        Img("Icon", bossRoot, skinOr(skin?.iconBoss, ProtoArt.Star), skin?.iconBoss != null ? Color.white : gold, new Vector2(0f, 0.5f), new Vector2(-10f, -6f), new Vector2(96f, 96f));
        Image back = Img("Back", bossRoot, skin?.bossBarBack, new Color(0.05f, 0.05f, 0.1f, 0.8f), Mid, new Vector2(30f, -14f), new Vector2(620f, 44f));
        bossTrail = Img("Trail", bossRoot, skinOr(skin?.bossBarFill, ProtoArt.Square), new Color(1f, 0.95f, 0.85f), Mid, new Vector2(30f, -14f), new Vector2(604f, 30f));
        bossFill = Img("Fill", bossRoot, skinOr(skin?.bossBarFill, ProtoArt.Square), skin?.bossBarFill != null ? Color.white : new Color(0.9f, 0.2f, 0.25f), Mid, new Vector2(30f, -14f), new Vector2(604f, 30f));
        foreach (Image bar in new[] { bossTrail, bossFill })
        {
            bar.type = Image.Type.Filled;
            bar.fillMethod = Image.FillMethod.Horizontal;
        }
        bossTrail.color = new Color(1f, 0.95f, 0.85f, 0.9f);
        bossName = Label("Name", bossRoot, "BIG BEAR", 40f, gold, TextAlignmentOptions.MidlineLeft, new Vector2(0f, 0.5f), new Vector2(60f, 30f), new Vector2(400f, 50f));
        bossName.rectTransform.pivot = new Vector2(0f, 0.5f);
        bossHp = Label("Hp", bossRoot, "", 30f, Color.white, Center, Mid, new Vector2(30f, -14f), new Vector2(300f, 40f));
    }

    void BuildBottom()
    {
        quiverRoot = Rect("Quiver", root, new Vector2(0.5f, 0f), new Vector2(0f, 150f), new Vector2(10f, 10f));

        stormRoot = Rect("Storm", root, new Vector2(1f, 0f), new Vector2(-130f, 160f), new Vector2(180f, 180f));
        stormGlow = Img("Glow", stormRoot, skinOr(skin?.glow, ProtoArt.SoftCircle), Color.clear, Mid, Vector2.zero, new Vector2(300f, 300f));
        bool orb = skin != null && skin.skillFrame != null;
        Img("Frame", stormRoot, skinOr(skin?.skillFrame, ProtoArt.Circle), orb ? Color.white : new Color(0.1f, 0.08f, 0.2f, 0.85f), Mid, Vector2.zero, new Vector2(170f, 170f));
        stormFill = Img("Fill", stormRoot, orb ? skinOr(skin.skillCooldown, ProtoArt.Circle) : ProtoArt.Sprite(ProtoArt.Circle), orb ? new Color(0.03f, 0.02f, 0.1f, 0.72f) : purple, Mid, Vector2.zero, orb ? new Vector2(158f, 158f) : new Vector2(150f, 150f));
        stormFill.type = Image.Type.Filled;
        stormFill.fillMethod = Image.FillMethod.Radial360;
        stormFill.fillOrigin = (int)Image.Origin360.Top;
        stormFill.fillClockwise = !orb;
        stormIcon = Img("Bolt", stormRoot, skinOr(skin?.iconBolt, ProtoArt.Star), Color.white, Mid, new Vector2(0f, 6f), new Vector2(96f, 96f));
        stormIcon.gameObject.SetActive(!orb);
        stormUsesOrb = orb;
        stormLabel = Label("Label", stormRoot, "STORM", 36f, Color.white, Center, Mid, new Vector2(0f, -100f), new Vector2(240f, 50f));
    }

    void BuildFeedback()
    {
        // World tags live in their own layer under the banners, callouts and menus.
        tagLayer = Rect("Tags", root, Mid, Vector2.zero, Vector2.zero);
        Stretch(tagLayer);

        hintRoot = Rect("Hint", root, new Vector2(0.5f, 0f), new Vector2(0f, 330f), new Vector2(900f, 200f));
        hintHand = Img("Hand", hintRoot, skinOr(skin?.tutorialHand, ProtoArt.Circle), Color.white, Mid, new Vector2(0f, 40f), new Vector2(150f, 150f));
        hintText = Label("Text", hintRoot, "", 46f, Color.white, Center, Mid, new Vector2(0f, -70f), new Vector2(900f, 70f));

        bannerRoot = Rect("Banner", root, Mid, new Vector2(0f, 260f), new Vector2(900f, 260f));
        bannerRoot.gameObject.AddComponent<CanvasGroup>();
        bannerRibbon = Img("Ribbon", bannerRoot, skinOr(skin?.ribbonOrange, ProtoArt.Square), Color.white, Mid, new Vector2(0f, 30f), new Vector2(860f, 170f));
        if (bannerRibbon.sprite != null && bannerRibbon.sprite.border != Vector4.zero)
            bannerRibbon.type = Image.Type.Sliced;
        bannerTitle = Label("Title", bannerRibbon.rectTransform, "", 84f, Color.white, Center, Mid, new Vector2(0f, 8f), new Vector2(820f, 120f));
        bannerSub = Label("Sub", bannerRoot, "", 46f, Color.white, Center, Mid, new Vector2(0f, -100f), new Vector2(900f, 70f));
        bannerStars = new Image[3];
        for (int i = 0; i < 3; i++)
            bannerStars[i] = Img("Star", bannerRoot, skinOr(skin?.starLarge, ProtoArt.Star), gold, Mid, new Vector2((i - 1) * 150f, i == 1 ? 175f : 150f), i == 1 ? new Vector2(150f, 150f) : new Vector2(120f, 120f));
        bannerRoot.gameObject.SetActive(false);

        calloutRoot = Rect("Callout", root, Mid, new Vector2(0f, 40f), new Vector2(900f, 240f));
        calloutRoot.gameObject.AddComponent<CanvasGroup>();
        calloutImage = Img("Art", calloutRoot, null, Color.white, Mid, Vector2.zero, new Vector2(680f, 200f));
        calloutText = Label("Text", calloutRoot, "", 110f, Color.white, Center, Mid, Vector2.zero, new Vector2(1000f, 200f));
        calloutRoot.gameObject.SetActive(false);

        bubbleRoot = Img("Bubble", root, skinOr(skin?.speechBubble, ProtoArt.Square), skin?.speechBubble != null ? Color.white : new Color(1f, 1f, 1f, 0.95f), Mid, Vector2.zero, new Vector2(420f, 110f)).rectTransform;
        if (skin?.speechBubble != null && skin.speechBubble.border != Vector4.zero)
            bubbleRoot.GetComponent<Image>().type = Image.Type.Sliced;
        bubbleText = Label("Line", bubbleRoot, "", 38f, new Color(0.15f, 0.12f, 0.2f), Center, Mid, Vector2.zero, new Vector2(400f, 100f), false);
        bubbleRoot.gameObject.SetActive(false);

        BuildBossIntro();
    }

    void BuildBossIntro()
    {
        bossIntroRoot = Rect("BossIntro", root, Mid, Vector2.zero, Vector2.zero);
        Stretch(bossIntroRoot);
        bossIntroRoot.gameObject.AddComponent<CanvasGroup>().blocksRaycasts = false;
        bossIntroDim = Img("Dim", bossIntroRoot, null, Color.clear, Mid, Vector2.zero, Vector2.zero);
        Stretch(bossIntroDim.rectTransform);

        bossIntroBand = Img("Band", bossIntroRoot, null, new Color(0.32f, 0.02f, 0.06f, 0.94f), Mid, new Vector2(0f, 180f), new Vector2(1400f, 560f)).rectTransform;
        bossIntroTickers = new RectTransform[2];
        for (int i = 0; i < 2; i++)
        {
            float y = i == 0 ? 245f : -245f;
            Image bar = Img("Ticker", bossIntroBand, null, new Color(0.95f, 0.12f, 0.15f), Mid, new Vector2(0f, y), new Vector2(1400f, 70f));
            bar.gameObject.AddComponent<RectMask2D>();
            // Wide enough to cover the screen while it scrolls by one 700 px repeat.
            string line = string.Concat(System.Linq.Enumerable.Repeat("BOSS FIGHT  ///  ", 8));
            bossIntroTickers[i] = Label("Text", bar.transform, line, 44f, Color.white, Center, Mid, Vector2.zero, new Vector2(2800f, 70f)).rectTransform;
        }
        bossIntroIcon = Img("Icon", bossIntroBand, skinOr(skin?.iconBoss, ProtoArt.Star), skin?.iconBoss != null ? Color.white : gold, Mid, new Vector2(0f, 120f), new Vector2(190f, 190f)).rectTransform;
        bossIntroTitle = Label("Title", bossIntroBand, "BOSS FIGHT!", 150f, new Color(1f, 0.3f, 0.25f), Center, Mid, new Vector2(0f, -30f), new Vector2(1000f, 170f)).rectTransform;
        bossIntroName = Label("Name", bossIntroBand, "", 72f, Color.white, Center, Mid, new Vector2(0f, -150f), new Vector2(1000f, 120f));
        bossIntroRoot.gameObject.SetActive(false);
    }

    void BuildPips(int count)
    {
        foreach (Transform child in quiverRoot)
            Destroy(child.gameObject);
        pipFills.Clear();
        pipPunch.Clear();
        pipCount = count;
        lastFull = count;
        const float size = 112f, spacing = 136f;
        float start = -(count - 1) * spacing * 0.5f;
        Sprite shuriken = ProtoArt.Sprite(ProtoArt.Shuriken);
        for (int i = 0; i < count; i++)
        {
            Vector2 pos = new Vector2(start + i * spacing, 0f);
            Img("Slot", quiverRoot, ProtoArt.Sprite(ProtoArt.Circle), new Color(0.06f, 0.08f, 0.16f, 0.75f), Mid, pos, Vector2.one * (size + 18f));
            Img("Back", quiverRoot, shuriken, new Color(1f, 1f, 1f, 0.15f), Mid, pos, Vector2.one * size);
            Image fill = Img("Pip", quiverRoot, shuriken, Color.white, Mid, pos, Vector2.one * size);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Radial360;
            fill.fillOrigin = (int)Image.Origin360.Top;
            pipFills.Add(fill);
            pipPunch.Add(0f);
        }
    }

    void AddMarker()
    {
        Image ring = Img("Marker", root, skinOr(skin?.targetReticle, ProtoArt.Circle), Color.white, Mid, Vector2.zero, new Vector2(92f, 92f));
        ring.transform.SetSiblingIndex(3);
        TextMeshProUGUI t = Label("Label", ring.transform, "1", 50f, new Color(0.1f, 0.1f, 0.15f), Center, Mid, Vector2.zero, new Vector2(92f, 92f), false);
        if (skin?.targetReticle != null)
            t.color = Color.white;
        markers.Add(ring.rectTransform);
        markerImages.Add(ring);
        markerTexts.Add(t);
        markerTargets.Add(null);
        markerAge.Add(0f);
    }

    RectTransform BuildDangerTag()
    {
        Image bg = Img("Danger", root, skinOr(skin?.toast, ProtoArt.Square), Color.white, Mid, Vector2.zero, new Vector2(220f, 64f));
        if (bg.sprite != null && bg.sprite.border != Vector4.zero)
            bg.type = Image.Type.Sliced;
        Label("Text", bg.transform, "", 38f, Color.white, Center, Mid, Vector2.zero, new Vector2(220f, 64f));
        return bg.rectTransform;
    }

    void BuildMenu()
    {
        Image panel = Img("Menu", root, skinOr(skin?.dim, ProtoArt.Square), new Color(0.04f, 0.04f, 0.12f, 0.55f), Mid, Vector2.zero, Vector2.zero);
        Stretch(panel.rectTransform);
        panel.raycastTarget = true;
        menuPanel = panel.gameObject;

        Img("Glow", panel.transform, skinOr(skin?.glow, ProtoArt.SoftCircle), new Color(1f, 0.85f, 0.4f, 0.5f), Mid, new Vector2(0f, 560f), new Vector2(1100f, 700f));
        Label("Title", panel.transform, "BLASTY BOY", 165f, gold, Center, Mid, new Vector2(0f, 580f), new Vector2(1000f, 220f));
        Image ribbon = Img("Ribbon", panel.transform, skinOr(skin?.ribbonOrange, ProtoArt.Square), skin?.ribbonOrange != null ? Color.white : new Color(1f, 0.5f, 0.15f), Mid, new Vector2(0f, 410f), new Vector2(640f, 120f));
        Label("Sub", ribbon.transform, "ROOFTOP CHASE", 64f, Color.white, Center, Mid, new Vector2(0f, 6f), new Vector2(620f, 100f));

        Image story = Img("Story", panel.transform, skinOr(skin?.panel, ProtoArt.Square), skin?.panel != null ? Color.white : new Color(0.1f, 0.12f, 0.25f, 0.9f), Mid, new Vector2(0f, 90f), new Vector2(820f, 320f));
        if (story.sprite != null && story.sprite.border != Vector4.zero)
            story.type = Image.Type.Sliced;
        Label("Text", story.transform, "BIG BEAR AND HIS GANG\nARE TAKING OVER THE CITY.\n<color=#FFD54A>CHASE HIM DOWN!</color>", 52f, Color.white, Center, Mid, new Vector2(0f, 10f), new Vector2(800f, 300f));

        Label("HowTo", panel.transform, "HOLD to aim  -  SLIDE to bend  -  RELEASE to throw", 36f, Color.white, Center, Mid, new Vector2(0f, -150f), new Vector2(1000f, 60f));
        Label("HowTo2", panel.transform, "Line up multi-kills to charge the <color=#C77DFF>SHADOW STORM</color>", 36f, Color.white, Center, Mid, new Vector2(0f, -205f), new Vector2(1000f, 60f));
        menuLevel = Label("Level", panel.transform, "LEVEL 1", 56f, Color.white, Center, Mid, new Vector2(0f, -300f), new Vector2(1000f, 80f));

        menuPlay = Btn("Play", panel.transform, skin?.buttonYellow, "PLAY", gold, Mid, new Vector2(0f, -470f), new Vector2(560f, 200f), () => ProtoGame.instance.StartRun());
        StartCoroutine(Breathe(menuPlay.rectTransform));
    }

    void BuildEnd()
    {
        Image panel = Img("End", root, skinOr(skin?.dim, ProtoArt.Square), new Color(0.04f, 0.04f, 0.12f, 0.75f), Mid, Vector2.zero, Vector2.zero);
        Stretch(panel.rectTransform);
        panel.raycastTarget = true;
        endPanel = panel.gameObject;

        Img("Glow", panel.transform, skinOr(skin?.glow, ProtoArt.SoftCircle), new Color(1f, 0.85f, 0.4f, 0.45f), Mid, new Vector2(0f, 560f), new Vector2(1100f, 800f));
        endStars = new Image[3];
        for (int i = 0; i < 3; i++)
            endStars[i] = Img("Star", panel.transform, skinOr(skin?.starLarge, ProtoArt.Star), gold, Mid, new Vector2((i - 1) * 210f, i == 1 ? 700f : 660f), i == 1 ? new Vector2(220f, 220f) : new Vector2(170f, 170f));
        endRibbon = Img("Ribbon", panel.transform, skinOr(skin?.ribbonGreen, ProtoArt.Square), Color.white, Mid, new Vector2(0f, 480f), new Vector2(880f, 170f));
        endTitle = Label("Title", endRibbon.transform, "", 92f, Color.white, Center, Mid, new Vector2(0f, 8f), new Vector2(840f, 140f));
        endSub = Label("Sub", panel.transform, "", 48f, Color.white, Center, Mid, new Vector2(0f, 320f), new Vector2(1000f, 140f));

        string[] titles = { "SCORE", "BOSS DAMAGE", "BEST COMBO", "ACCURACY" };
        Sprite[] icons = { skinOr(skin?.iconTrophy, ProtoArt.Star), skinOr(skin?.iconCrown, ProtoArt.Star), skinOr(skin?.iconSword, ProtoArt.Star), skinOr(skin?.iconTarget, ProtoArt.Circle) };
        endTileValues = new TextMeshProUGUI[4];
        for (int i = 0; i < 4; i++)
        {
            Vector2 pos = new Vector2(i % 2 == 0 ? -215f : 215f, i < 2 ? 70f : -200f);
            Image tile = Img("Tile", panel.transform, skinOr(skin?.tile, ProtoArt.Square), skin?.tile != null ? Color.white : new Color(0.12f, 0.16f, 0.35f, 0.95f), Mid, pos, new Vector2(400f, 240f));
            if (tile.sprite != null && tile.sprite.border != Vector4.zero)
                tile.type = Image.Type.Sliced;
            Img("Icon", tile.transform, icons[i], icons[i] == ProtoArt.Sprite(ProtoArt.Star) ? gold : Color.white, Mid, new Vector2(0f, 45f), new Vector2(110f, 110f));
            endTileValues[i] = Label("Value", tile.transform, "", 58f, Color.white, Center, Mid, new Vector2(0f, -38f), new Vector2(380f, 70f));
            Label("Name", tile.transform, titles[i], 30f, gold, Center, Mid, new Vector2(0f, -88f), new Vector2(380f, 40f));
        }

        // Its label and action are set per result in ShowEnd.
        Image again = Btn("Again", panel.transform, skin?.buttonGreen, "NEXT LEVEL", new Color(0.3f, 0.85f, 0.35f), Mid, new Vector2(0f, -520f), new Vector2(600f, 190f), () => { });
        endButton = again.GetComponent<Button>();
        endButtonLabel = again.GetComponentInChildren<TextMeshProUGUI>();
        endPanel.SetActive(false);
    }

    void BuildPause()
    {
        Image panel = Img("Pause", root, skinOr(skin?.dim, ProtoArt.Square), new Color(0.04f, 0.04f, 0.12f, 0.7f), Mid, Vector2.zero, Vector2.zero);
        Stretch(panel.rectTransform);
        panel.raycastTarget = true;
        pausePanel = panel.gameObject;
        Label("Title", panel.transform, "PAUSED", 140f, Color.white, Center, Mid, new Vector2(0f, 300f), new Vector2(900f, 200f));
        Btn("Resume", panel.transform, skin?.buttonGreen, "RESUME", new Color(0.3f, 0.85f, 0.35f), Mid, new Vector2(0f, 20f), new Vector2(560f, 180f), () => ProtoGame.instance.TogglePause());
        Btn("Restart", panel.transform, skin?.buttonBlue, "RESTART", new Color(0.25f, 0.55f, 0.95f), Mid, new Vector2(0f, -200f), new Vector2(560f, 160f), () => ProtoGame.instance.Retry());
        pausePanel.SetActive(false);
    }

    public void SetPaused(bool paused) => pausePanel.SetActive(paused);

    void SetGameplayVisible(bool visible)
    {
        heartPill.gameObject.SetActive(visible);
        scorePill.gameObject.SetActive(visible);
        stageLabel.transform.parent.gameObject.SetActive(visible);
        bossRoot.gameObject.SetActive(visible);
        quiverRoot.gameObject.SetActive(visible);
        stormRoot.gameObject.SetActive(visible);
        pauseButton.gameObject.SetActive(visible);
    }
    #endregion

    #region Helpers
    static readonly Vector2 Mid = new Vector2(0.5f, 0.5f);

    Sprite skinOr(Sprite s) => s != null ? s : ProtoArt.Sprite(ProtoArt.Square);

    // Swap a button's art; without the kit, tint the plain fallback sprite instead.
    static void SetButtonStyle(Image button, Sprite sprite, Color fallback)
    {
        button.sprite = sprite != null ? sprite : ProtoArt.Sprite(ProtoArt.Square);
        button.color = sprite != null ? Color.white : fallback;
    }
    Sprite skinOr(Sprite s, Texture2D fallback) => s != null ? s : ProtoArt.Sprite(fallback);

    IEnumerator Punch(RectTransform rt, float amount, float duration)
    {
        for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
        {
            float k = t / duration;
            rt.localScale = Vector3.one * (1f + amount * Mathf.Sin(k * Mathf.PI * 3f) * (1f - k));
            yield return null;
        }
        rt.localScale = Vector3.one;
    }

    IEnumerator Breathe(RectTransform rt)
    {
        while (true)
        {
            rt.localScale = Vector3.one * (1f + 0.05f * Mathf.Sin(Time.unscaledTime * 5f));
            yield return null;
        }
    }

    static float OutBack(float t)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        float u = t - 1f;
        return 1f + c3 * u * u * u + c1 * u * u;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;
    }

    static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = position;
        rt.sizeDelta = size;
        return rt;
    }

    TextMeshProUGUI Label(string name, Transform parent, string text, float size, Color color, TextAlignmentOptions align, Vector2 anchor, Vector2 position, Vector2 box, bool outline = true)
    {
        RectTransform rt = Rect(name, parent, anchor, position, box);
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        t.font = font;
        if (outline && skin != null && skin.fontOutlineMaterial != null)
            t.fontSharedMaterial = skin.fontOutlineMaterial;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.text = text;
        t.raycastTarget = false;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Overflow;
        t.richText = true;
        if (outline && (skin == null || skin.fontOutlineMaterial == null))
        {
            t.outlineWidth = 0.22f;
            t.outlineColor = new Color32(20, 14, 40, 255);
        }
        return t;
    }

    // Overload with the anchor before the alignment (kept for readability at call sites using Mid).
    TextMeshProUGUI Label(string name, Transform parent, string text, float size, Color color, TextAlignmentOptions align, Vector2 position, Vector2 box)
        => Label(name, parent, text, size, color, align, Mid, position, box);

    static Image Img(string name, Transform parent, Sprite sprite, Color color, Vector2 anchor, Vector2 position, Vector2 size)
    {
        RectTransform rt = Rect(name, parent, anchor, position, size);
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        if (sprite != null && sprite.border != Vector4.zero)
            img.type = Image.Type.Sliced;
        img.preserveAspect = sprite != null && sprite.border == Vector4.zero && size.x > 0f && size.y > 0f && Mathf.Abs(size.x - size.y) < 1f;
        return img;
    }

    Image Btn(string name, Transform parent, Sprite sprite, string label, Color fallback, Vector2 anchor, Vector2 position, Vector2 size, UnityAction onClick)
    {
        Image img = Img(name, parent, sprite != null ? sprite : ProtoArt.Sprite(ProtoArt.Square), sprite != null ? Color.white : fallback, anchor, position, size);
        img.raycastTarget = true;
        img.preserveAspect = false;
        if (sprite != null && sprite.border != Vector4.zero)
            img.type = Image.Type.Sliced;
        var button = img.gameObject.AddComponent<Button>();
        button.targetGraphic = img;
        button.onClick.AddListener(() => ProtoAudio.Play(Sfx.Pop, 0.7f, 1.3f));
        button.onClick.AddListener(onClick);
        if (label != null)
            Label("Label", img.transform, label, size.y * 0.38f, Color.white, Center, Mid, new Vector2(0f, 6f), size);
        return img;
    }
    #endregion
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// Runtime-built UI for the prototype (no hand-authored canvas): HUD, aim markers, combo text, banners, popups,
// screen flashes and the menu / end panels. Everything animates on unscaled time so it stays snappy in slow-mo.
public class ProtoHUD : MonoBehaviour
{
    public static ProtoHUD instance { get; private set; }

    [SerializeField] Font font = null;

    static readonly Color yellow = new Color(1f, 0.85f, 0.1f);
    static readonly Color red = new Color(1f, 0.2f, 0.25f);
    static readonly Color[] comboColors =
    {
        Color.white, Color.white, new Color(1f, 0.92f, 0.2f), new Color(1f, 0.55f, 0.1f),
        new Color(1f, 0.25f, 0.35f), new Color(1f, 0.3f, 0.95f), new Color(0.4f, 1f, 1f),
    };
    static readonly string[] comboWords = { "", "", "DOUBLE!", "TRIPLE!", "QUAD!", "PENTA!", "UNSTOPPABLE!" };

    RectTransform root;
    Camera cam;

    Image[] hearts;
    Text waveText, scoreText;
    RectTransform quiverRoot;
    readonly List<Image> pipFills = new List<Image>();
    readonly List<float> pipPunch = new List<float>();
    int pipCount = -1, lastFull;
    float quiverShake;

    Text badge;
    int badgeValue;
    float badgePunch;
    readonly List<RectTransform> markers = new List<RectTransform>();
    readonly List<Text> markerTexts = new List<Text>();
    readonly List<Image> markerImages = new List<Image>();
    readonly List<ProtoTarget> markerTargets = new List<ProtoTarget>();
    readonly List<float> markerAge = new List<float>();
    readonly List<Text> dangerTexts = new List<Text>();

    Text comboText, bannerText, bannerSub, hintText;
    Coroutine comboRoutine, bannerRoutine;
    bool hintVisible;

    Image flash, vignette;
    Color flashColor;
    float vignetteAlpha;

    int displayedScore, targetScore;
    float scorePunch, heartShake;

    GameObject menuPanel, endPanel;
    Text endTitle, endStats;

    RectTransform bossRoot, feverRoot;
    Image bossFill, bossTrail, feverFill;
    Text bossName, feverLabel;
    float bossPunch;

    readonly List<Text> popupPool = new List<Text>();

    private void Awake()
    {
        instance = this;
        cam = Camera.main;
        if (font == null)
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        Build();
    }

    #region Public API
    public void ShowMenu()
    {
        menuPanel.SetActive(true);
        endPanel.SetActive(false);
    }

    public void HideMenu() => menuPanel.SetActive(false);

    public void SetHearts(int count, bool animateLoss)
    {
        for (int i = 0; i < hearts.Length; i++)
            hearts[i].color = i < count ? red : new Color(0.15f, 0.15f, 0.2f, 0.45f);
        if (animateLoss && count >= 0 && count < hearts.Length)
        {
            StartCoroutine(Punch(hearts[count].rectTransform, 1.2f, 0.4f));
            heartShake = 1f;
        }
    }

    public void SetWave(int wave, int total) => waveText.text = "ROOFTOP " + wave + "/" + total;

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

    public void Combo(int n)
    {
        string word = n < comboWords.Length ? comboWords[n] : "x" + n + " UNSTOPPABLE!";
        Color color = comboColors[Mathf.Min(n, comboColors.Length - 1)];
        if (comboRoutine != null)
            StopCoroutine(comboRoutine);
        comboText.transform.SetAsLastSibling();
        comboRoutine = StartCoroutine(Slam(comboText, word, color, 2.6f, 0.6f, Random.Range(-8f, 8f)));
    }

    public void Banner(string title, string subtitle, float hold)
    {
        if (bannerRoutine != null)
            StopCoroutine(bannerRoutine);
        bannerRoutine = StartCoroutine(BannerRoutine(title, subtitle, hold));
    }

    public void Popup(Vector3 world, string text, Color color, float scale = 1f)
    {
        Text t = popupPool.Find(p => !p.gameObject.activeSelf);
        if (t == null)
        {
            t = NewText("Popup", root, "", 84, Color.white, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(600, 120));
            popupPool.Add(t);
        }
        t.text = text;
        t.color = color;
        t.transform.SetSiblingIndex(Mathf.Max(0, comboText.transform.GetSiblingIndex()));
        t.gameObject.SetActive(true);
        // Spread simultaneous popups (multi-kills, explosions) so they don't print on top of each other.
        int live = popupPool.FindAll(p => p.gameObject.activeSelf).Count;
        Vector2 spread = new Vector2(Random.Range(-90f, 90f), (live - 1) * 55f);
        StartCoroutine(PopupRoutine(t, world, scale, spread));
    }

    public void ShowEnd(bool won, string summary)
    {
        endPanel.SetActive(true);
        endTitle.text = won ? "VICTORY!" : "KNOCKED OUT!";
        endTitle.color = won ? yellow : red;
        endStats.text = summary;
        StartCoroutine(Slam(endTitle, endTitle.text, endTitle.color, 2.2f, -1f, 0f));
    }
    #endregion

    private void LateUpdate()
    {
        float dt = Time.unscaledDeltaTime;
        float now = Time.unscaledTime;

        // Score count-up and punch.
        if (displayedScore != targetScore)
            displayedScore = Mathf.CeilToInt(Mathf.Lerp(displayedScore, targetScore, 1f - Mathf.Exp(-12f * dt)));
        if (Mathf.Abs(displayedScore - targetScore) < 2)
            displayedScore = targetScore;
        scoreText.text = displayedScore.ToString();
        scorePunch = Mathf.Max(0f, scorePunch - dt * 5f);
        scoreText.rectTransform.localScale = Vector3.one * (1f + 0.35f * scorePunch * scorePunch);

        heartShake = Mathf.Max(0f, heartShake - dt * 3f);
        ((RectTransform)hearts[0].rectTransform.parent).anchoredPosition = new Vector2(Mathf.Sin(now * 70f) * 18f * heartShake, 0f);

        // Screen flashes.
        flashColor.a = Mathf.Max(0f, flashColor.a - dt * 3.5f);
        flash.color = flashColor;
        vignetteAlpha = Mathf.Max(0f, vignetteAlpha - dt * 1.4f);
        vignette.color = new Color(1f, 0.05f, 0.05f, vignetteAlpha);

        hintText.color = new Color(1f, 1f, 1f, hintVisible ? 0.65f + 0.35f * Mathf.Sin(now * 6f) : Mathf.Max(0f, hintText.color.a - dt * 4f));

        UpdateQuiver(dt);
        UpdateMeters(dt);
        UpdateAimMarkers(dt);
        UpdateDanger();
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
            pipFills[i].rectTransform.localScale = Vector3.one * (1f + 0.4f * pipPunch[i]);
            pipFills[i].color = i < full ? Color.white : new Color(1f, 1f, 1f, 0.55f);
        }
        lastFull = full;

        quiverShake = Mathf.Max(0f, quiverShake - dt * 4f);
        quiverRoot.anchoredPosition = new Vector2(Mathf.Sin(Time.unscaledTime * 80f) * 22f * quiverShake, 170f);
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
                m.localScale = Vector3.one * OutBack(pop) * (1f + 0.06f * Mathf.Sin(Time.unscaledTime * 14f));
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

        // Predicted kill count, shown over the board.
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
            badge.rectTransform.localScale = Vector3.one * (1f + 0.6f * badgePunch * badgePunch) * (1f + 0.08f * Mathf.Min(kills, 5));
        }
    }

    void UpdateDanger()
    {
        int used = 0;
        foreach (ProtoTarget t in ProtoTarget.all)
        {
            if (!(t is ProtoHostage h) || h.IsResolved || !ProtoGame.IsPlaying)
                continue;
            if (used >= dangerTexts.Count)
                dangerTexts.Add(NewText("Danger", root, "", 52, red, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(400, 100)));
            Text d = dangerTexts[used++];
            bool urgent = h.Danger01 < 0.35f;
            d.gameObject.SetActive(true);
            d.text = "HELP! " + Mathf.CeilToInt(h.DangerSeconds);
            d.color = urgent ? red : new Color(1f, 0.95f, 0.8f);
            d.rectTransform.anchoredPosition = WorldToCanvas(h.MarkerPosition + Vector3.up * 0.4f);
            d.rectTransform.localScale = Vector3.one * (urgent ? 1.1f + 0.15f * Mathf.Sin(Time.unscaledTime * 18f) : 1f);
        }
        for (int i = used; i < dangerTexts.Count; i++)
            dangerTexts[i].gameObject.SetActive(false);

        // The predicted-kill badge floats over the boss, where every throw ends.
        ProtoBoss boss = ProtoBoss.instance;
        Vector2 bossPos = boss != null ? WorldToCanvas(boss.AimPoint + Vector3.up * 1.6f) : new Vector2(0f, 600f);
        badge.rectTransform.anchoredPosition = bossPos + new Vector2(0f, 120f);
    }

    Vector2 WorldToCanvas(Vector3 world)
    {
        Vector3 screen = cam.WorldToScreenPoint(world);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen, null, out Vector2 local);
        return local;
    }

    #region Animations
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

    // Big text that slams in from a large scale with overshoot, holds, then fades (hold < 0 keeps it on screen).
    IEnumerator Slam(Text text, string value, Color color, float fromScale, float hold, float angle)
    {
        RectTransform rt = text.rectTransform;
        text.text = value;
        text.gameObject.SetActive(true);
        rt.localRotation = Quaternion.Euler(0f, 0f, angle);

        const float inTime = 0.16f;
        for (float t = 0f; t < inTime; t += Time.unscaledDeltaTime)
        {
            float k = OutBack(t / inTime);
            rt.localScale = Vector3.one * Mathf.LerpUnclamped(fromScale, 1f, k);
            text.color = new Color(color.r, color.g, color.b, Mathf.Clamp01(t / 0.06f));
            yield return null;
        }
        rt.localScale = Vector3.one;
        text.color = color;
        if (hold < 0f)
            yield break;

        yield return new WaitForSecondsRealtime(hold);
        for (float t = 0f; t < 0.25f; t += Time.unscaledDeltaTime)
        {
            float k = t / 0.25f;
            text.color = new Color(color.r, color.g, color.b, 1f - k);
            rt.localScale = Vector3.one * (1f + 0.3f * k);
            yield return null;
        }
        text.gameObject.SetActive(false);
    }

    IEnumerator BannerRoutine(string title, string subtitle, float hold)
    {
        bannerSub.text = subtitle;
        bannerSub.gameObject.SetActive(true);
        StartCoroutine(Slam(bannerText, title, yellow, 3f, hold, Random.Range(-4f, 4f)));
        for (float t = 0f; t < hold + 0.25f; t += Time.unscaledDeltaTime)
        {
            float a = Mathf.Clamp01((t - 0.1f) / 0.15f) * Mathf.Clamp01((hold + 0.25f - t) / 0.25f);
            bannerSub.color = new Color(1f, 1f, 1f, a);
            yield return null;
        }
        bannerSub.gameObject.SetActive(false);
    }

    IEnumerator PopupRoutine(Text text, Vector3 world, float scale, Vector2 spread)
    {
        RectTransform rt = text.rectTransform;
        Color c = text.color;
        rt.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-10f, 10f));
        const float life = 0.85f;
        for (float t = 0f; t < life; t += Time.unscaledDeltaTime)
        {
            float k = t / life;
            rt.anchoredPosition = WorldToCanvas(world) + spread + new Vector2(0f, 160f * (1f - (1f - k) * (1f - k)));
            float pop = OutBack(Mathf.Clamp01(t / 0.14f));
            rt.localScale = Vector3.one * scale * Mathf.LerpUnclamped(1.8f, 1f, pop);
            text.color = new Color(c.r, c.g, c.b, Mathf.Clamp01((1f - k) / 0.35f));
            yield return null;
        }
        text.gameObject.SetActive(false);
    }

    static float OutBack(float t)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        float u = t - 1f;
        return 1f + c3 * u * u * u + c1 * u * u;
    }
    #endregion

    #region Construction
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
        scaler.matchWidthOrHeight = 0.5f;
        root = (RectTransform)go.transform;

        vignette = NewImage("Vignette", root, ProtoArt.Sprite(ProtoArt.Vignette), Color.clear, Vector2.one * 0.5f, Vector2.zero, Vector2.zero);
        Stretch(vignette.rectTransform);
        flash = NewImage("Flash", root, null, Color.clear, Vector2.one * 0.5f, Vector2.zero, Vector2.zero);
        Stretch(flash.rectTransform);

        // Top bar
        RectTransform heartRow = NewRect("Hearts", root, new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(10f, 10f));
        hearts = new Image[ProtoGameMaxHearts()];
        for (int i = 0; i < hearts.Length; i++)
            hearts[i] = NewImage("Heart", heartRow, ProtoArt.Sprite(ProtoArt.Heart), red, new Vector2(0.5f, 0.5f), new Vector2(100f + i * 115f, -115f), new Vector2(100f, 100f));
        waveText = NewText("Wave", root, "", 58, Color.white, new Vector2(0.5f, 1f), new Vector2(0f, -115f), new Vector2(500f, 100f));
        scoreText = NewText("Score", root, "0", 84, yellow, new Vector2(1f, 1f), new Vector2(-230f, -115f), new Vector2(400f, 120f), TextAnchor.MiddleRight);
        scoreText.rectTransform.pivot = new Vector2(1f, 0.5f);
        scoreText.rectTransform.anchoredPosition = new Vector2(-50f, -115f);

        quiverRoot = NewRect("Quiver", root, new Vector2(0.5f, 0f), new Vector2(0f, 170f), new Vector2(10f, 10f));
        hintText = NewText("Hint", root, "", 54, Color.white, new Vector2(0.5f, 0f), new Vector2(0f, 340f), new Vector2(1000f, 100f));
        badge = NewText("Badge", root, "", 110, yellow, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(400f, 160f));
        // Combo word sits in the empty middle of the arena, away from the kills and their score popups.
        comboText = NewText("Combo", root, "", 150, Color.white, new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(1100f, 220f));
        comboText.gameObject.SetActive(false);
        bannerText = NewText("Banner", root, "", 150, yellow, new Vector2(0.5f, 0.5f), new Vector2(0f, 120f), new Vector2(1100f, 220f));
        bannerText.gameObject.SetActive(false);
        bannerSub = NewText("BannerSub", root, "", 60, Color.white, new Vector2(0.5f, 0.5f), new Vector2(0f, -10f), new Vector2(1000f, 100f));
        bannerSub.gameObject.SetActive(false);

        BuildMeters();
        BuildMenu();
        BuildEnd();
    }

    int ProtoGameMaxHearts() => ProtoGame.instance != null ? ProtoGame.instance.MaxHearts : 3;

    void BuildPips(int count)
    {
        foreach (Transform child in quiverRoot)
            Destroy(child.gameObject);
        pipFills.Clear();
        pipPunch.Clear();
        pipCount = count;
        lastFull = count;

        float size = count <= 4 ? 120f : 70f;
        float spacing = size + (count <= 4 ? 24f : 8f);
        float start = -(count - 1) * spacing * 0.5f;
        Sprite shuriken = ProtoArt.Sprite(ProtoArt.Shuriken);
        for (int i = 0; i < count; i++)
        {
            Vector2 pos = new Vector2(start + i * spacing, 0f);
            NewImage("PipBack", quiverRoot, shuriken, new Color(0f, 0f, 0f, 0.35f), new Vector2(0.5f, 0.5f), pos, Vector2.one * size);
            Image fill = NewImage("Pip", quiverRoot, shuriken, Color.white, new Vector2(0.5f, 0.5f), pos, Vector2.one * size);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Radial360;
            fill.fillOrigin = (int)Image.Origin360.Top;
            pipFills.Add(fill);
            pipPunch.Add(0f);
        }
    }

    void AddMarker()
    {
        Image img = NewImage("Marker", root, ProtoArt.Sprite(ProtoArt.Circle), Color.white, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(86f, 86f));
        img.transform.SetSiblingIndex(2);
        Text t = NewText("Label", img.transform, "1", 56, new Color(0.1f, 0.1f, 0.15f), new Vector2(0.5f, 0.5f), new Vector2(0f, -2f), new Vector2(86f, 86f));
        Destroy(t.GetComponent<Outline>());
        Destroy(t.GetComponent<Shadow>());
        markers.Add(img.rectTransform);
        markerImages.Add(img);
        markerTexts.Add(t);
        markerTargets.Add(null);
        markerAge.Add(0f);
    }

    void BuildMenu()
    {
        Image panel = NewImage("Menu", root, null, new Color(0.05f, 0.05f, 0.12f, 0.6f), Vector2.one * 0.5f, Vector2.zero, Vector2.zero);
        Stretch(panel.rectTransform);
        panel.raycastTarget = true;
        menuPanel = panel.gameObject;

        NewText("Title", panel.transform, "BLASTY BOY", 170, yellow, Vector2.one * 0.5f, new Vector2(0f, 560f), new Vector2(1100f, 220f));
        NewText("Sub", panel.transform, "ROOFTOP CHASE", 60, Color.white, Vector2.one * 0.5f, new Vector2(0f, 420f), new Vector2(1000f, 80f));
        NewText("Story", panel.transform, "BIG BEAR AND HIS GANG\nARE TAKING THE CITY.\nCHASE HIM DOWN!", 52, new Color(1f, 0.85f, 0.4f), Vector2.one * 0.5f, new Vector2(0f, 170f), new Vector2(1000f, 240f));
        NewText("HowTo", panel.transform, "HOLD to aim  -  SLIDE to bend  -  RELEASE to throw\nMulti-kills charge the SHADOW STORM", 38, Color.white, Vector2.one * 0.5f, new Vector2(0f, -60f), new Vector2(1000f, 140f));

        Image play = NewButton("Play", panel.transform, "PLAY", Vector2.one * 0.5f, new Vector2(0f, -430f), new Vector2(560f, 190f), () => ProtoGame.instance.StartRun());
        play.color = new Color(0.25f, 0.85f, 0.35f);
        play.GetComponentInChildren<Text>().fontSize = 96;
        StartCoroutine(Breathe(play.rectTransform));
    }

    void BuildEnd()
    {
        Image panel = NewImage("End", root, null, new Color(0.05f, 0.05f, 0.12f, 0.7f), Vector2.one * 0.5f, Vector2.zero, Vector2.zero);
        Stretch(panel.rectTransform);
        panel.raycastTarget = true;
        endPanel = panel.gameObject;

        endTitle = NewText("Title", panel.transform, "", 150, yellow, Vector2.one * 0.5f, new Vector2(0f, 600f), new Vector2(1100f, 220f));
        endStats = NewText("Stats", panel.transform, "", 54, Color.white, Vector2.one * 0.5f, new Vector2(0f, 150f), new Vector2(950f, 650f));
        endStats.lineSpacing = 1.15f;

        Image again = NewButton("Again", panel.transform, "PLAY AGAIN", Vector2.one * 0.5f, new Vector2(0f, -430f), new Vector2(620f, 170f), () => ProtoGame.instance.Restart());
        again.color = new Color(0.25f, 0.85f, 0.35f);
        endPanel.SetActive(false);
    }

    // Boss health along the top and the Shadow Storm meter above the quiver.
    void BuildMeters()
    {
        bossRoot = NewRect("Boss", root, new Vector2(0.5f, 1f), new Vector2(0f, -215f), new Vector2(760f, 90f));
        bossName = NewText("Name", bossRoot, "BIG BEAR", 44, new Color(1f, 0.85f, 0.4f), new Vector2(0.5f, 0.5f), new Vector2(0f, 38f), new Vector2(760f, 60f));
        NewImage("Back", bossRoot, null, new Color(0f, 0f, 0f, 0.55f), new Vector2(0.5f, 0.5f), new Vector2(0f, -12f), new Vector2(720f, 40f));
        bossTrail = NewImage("Trail", bossRoot, null, new Color(1f, 0.95f, 0.8f), new Vector2(0.5f, 0.5f), new Vector2(0f, -12f), new Vector2(710f, 30f));
        bossFill = NewImage("Fill", bossRoot, null, new Color(0.9f, 0.2f, 0.25f), new Vector2(0.5f, 0.5f), new Vector2(0f, -12f), new Vector2(710f, 30f));
        foreach (Image bar in new[] { bossTrail, bossFill })
        {
            bar.sprite = ProtoArt.Sprite(ProtoArt.Square);
            bar.type = Image.Type.Filled;
            bar.fillMethod = Image.FillMethod.Horizontal;
        }

        feverRoot = NewRect("Fever", root, new Vector2(0.5f, 0f), new Vector2(0f, 290f), new Vector2(640f, 80f));
        NewImage("Back", feverRoot, null, new Color(0f, 0f, 0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -10f), new Vector2(600f, 26f));
        feverFill = NewImage("Fill", feverRoot, ProtoArt.Sprite(ProtoArt.Square), new Color(0.65f, 0.3f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -10f), new Vector2(592f, 18f));
        feverFill.type = Image.Type.Filled;
        feverFill.fillMethod = Image.FillMethod.Horizontal;
        feverLabel = NewText("Label", feverRoot, "SHADOW STORM", 34, new Color(0.85f, 0.7f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, 26f), new Vector2(640f, 50f));
    }

    public void BossHit(bool big) => bossPunch = big ? 1f : 0.5f;

    void UpdateMeters(float dt)
    {
        ProtoGame game = ProtoGame.instance;
        if (game == null || game.Boss == null)
            return;

        ProtoBoss boss = game.Boss;
        bool show = game.state != ProtoState.Menu;
        bossRoot.gameObject.SetActive(show);
        feverRoot.gameObject.SetActive(show);
        if (!show)
            return;

        // Health bar with a lagging "damage trail" and a punch on every hit.
        float hp = boss.MaxHealth > 0 ? (float)boss.Health / boss.MaxHealth : 0f;
        bossFill.fillAmount = hp;
        bossTrail.fillAmount = Mathf.Max(hp, Mathf.MoveTowards(bossTrail.fillAmount, hp, dt * 0.35f));
        bossPunch = Mathf.Max(0f, bossPunch - dt * 4f);
        bossRoot.localScale = Vector3.one * (1f + 0.12f * bossPunch);
        bossRoot.anchoredPosition = new Vector2(Mathf.Sin(Time.unscaledTime * 60f) * 14f * bossPunch, -215f);
        bossName.text = boss.DisplayName + "  " + boss.Health + "/" + boss.MaxHealth;

        ProtoThrower thrower = ProtoThrower.instance;
        bool storming = thrower != null && thrower.InFever;
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 14f);
        feverFill.fillAmount = storming ? thrower.Fever01 : game.FeverCharge;
        feverFill.color = storming ? Color.Lerp(new Color(0.75f, 0.35f, 1f), Color.white, pulse * 0.6f) : new Color(0.65f, 0.3f, 1f);
        feverLabel.text = storming ? "SHADOW STORM!  HOLD!" : "SHADOW STORM";
        feverRoot.localScale = Vector3.one * (storming ? 1.08f + 0.05f * pulse : 1f);
        vignette.color = storming && vignetteAlpha < 0.3f ? new Color(0.55f, 0.2f, 1f, 0.25f + 0.1f * pulse) : vignette.color;
    }

    IEnumerator Breathe(RectTransform rt)
    {
        while (true)
        {
            rt.localScale = Vector3.one * (1f + 0.05f * Mathf.Sin(Time.unscaledTime * 5f));
            yield return null;
        }
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;
    }

    static RectTransform NewRect(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size)
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

    Text NewText(string name, Transform parent, string text, int size, Color color, Vector2 anchor, Vector2 position, Vector2 box, TextAnchor align = TextAnchor.MiddleCenter)
    {
        RectTransform rt = NewRect(name, parent, anchor, position, box);
        var t = rt.gameObject.AddComponent<Text>();
        t.font = font;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.text = text;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        var outline = rt.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
        outline.effectDistance = new Vector2(4f, -4f);
        var shadow = rt.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.5f);
        shadow.effectDistance = new Vector2(0f, -9f);
        return t;
    }

    static Image NewImage(string name, Transform parent, Sprite sprite, Color color, Vector2 anchor, Vector2 position, Vector2 size)
    {
        RectTransform rt = NewRect(name, parent, anchor, position, size);
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    Image NewButton(string name, Transform parent, string label, Vector2 anchor, Vector2 position, Vector2 size, UnityAction onClick)
    {
        Image img = NewImage(name, parent, null, Color.white, anchor, position, size);
        img.raycastTarget = true;
        var outline = img.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.8f);
        outline.effectDistance = new Vector2(6f, -6f);
        var button = img.gameObject.AddComponent<Button>();
        button.targetGraphic = img;
        button.onClick.AddListener(() => ProtoAudio.Play(Sfx.Pop, 0.7f, 1.3f));
        button.onClick.AddListener(onClick);
        NewText("Label", img.transform, label, 64, Color.white, Vector2.one * 0.5f, Vector2.zero, size);
        return img;
    }
    #endregion
}

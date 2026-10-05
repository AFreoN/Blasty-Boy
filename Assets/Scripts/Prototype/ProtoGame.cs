using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using CustomExtensions;

public enum ProtoState { Menu, WaveIntro, Playing, WaveClear, Chase, Won, Lost }
public enum ProtoSpawnKind { Grunt, Shield, Runner, Guard, Hostage, Barrel, Crate }

[System.Serializable]
public class ProtoSpawn
{
    public ProtoSpawnKind kind;
    [Tooltip("x = sideways (-5.5..5.5), y = distance from the start of the rooftop (5..18)")]
    public Vector2 position;
    [Tooltip("Seconds after the wave starts before this one drops in")]
    public float delay;

    public ProtoSpawn(ProtoSpawnKind kind, float x, float z, float delay = 0f)
    {
        this.kind = kind;
        position = new Vector2(x, z);
        this.delay = delay;
    }
}

[System.Serializable]
public class ProtoWave
{
    public string title;
    public string hint;
    public float speedMultiplier = 1f;
    [Tooltip("The showdown rooftop: goons keep coming until the boss is down")]
    public bool finale;
    public List<ProtoSpawn> spawns = new List<ProtoSpawn>();
}

// Runs the rooftop chase: one wave of goons per rooftop, Big Bear at the far end of each, a run-and-leap chase between
// rooftops, and a showdown on the last one. Also owns the feedback ladder for kills (OnKill) and the Fever meter.
public class ProtoGame : MonoBehaviour
{
    public static ProtoGame instance { get; private set; }
    public static bool IsPlaying => instance != null && instance.state == ProtoState.Playing;

    [Header("Prefabs")]
    [SerializeField] ProtoEnemy gruntPrefab = null;
    [SerializeField] ProtoEnemy shieldPrefab = null;
    [SerializeField] ProtoEnemy runnerPrefab = null;
    [SerializeField] ProtoHostage hostagePrefab = null;
    [SerializeField] ProtoBarrel barrelPrefab = null;
    [SerializeField] ProtoCrate cratePrefab = null;

    [Header("World")]
    [SerializeField] ProtoBoss boss = null;
    [Tooltip("Distance between rooftop origins along +Z")]
    [SerializeField] float zoneSpacing = 34f;
    [Tooltip("Where the boss stands, relative to the rooftop origin")]
    [SerializeField] Vector3 bossOffset = new Vector3(0f, 0f, 19.4f);
    [Tooltip("Z (relative to the rooftop origin) where the chase leap starts")]
    [SerializeField] float roofEdge = 21f;

    [Header("Rules")]
    [SerializeField] int maxHearts = 3;
    [SerializeField] float gruntSpeed = 0.55f;
    [SerializeField] float shieldSpeed = 0.4f;
    [SerializeField] float runnerSpeed = 1.5f;
    [SerializeField] int killScore = 100;
    [SerializeField] int flankBonus = 150;
    [SerializeField] int rescueBonus = 500;
    [SerializeField] int heartBonus = 250;
    [SerializeField] int bossHitScore = 50;

    [Header("Fever")]
    [Tooltip("Meter gained per extra kill on one blade (a triple adds 2x this)")]
    [SerializeField] float feverPerExtraKill = 0.14f;
    [SerializeField] float feverDuration = 6f;

    [Header("Waves (one per rooftop)")]
    [SerializeField] List<ProtoWave> waves = new List<ProtoWave>();

    public ProtoState state { get; private set; } = ProtoState.Menu;
    public int WaveIndex => waveIndex;
    public int WaveCount => waves.Count;
    public int Hearts => hearts;
    public int MaxHearts => maxHearts;
    public int Score => score;
    public int PredictedTotal { get; private set; }
    public int ActualTotal { get; private set; }
    public ProtoBoss Boss => boss;
    public float FeverCharge => feverCharge;

    int waveIndex;
    int hearts;
    int score;
    float feverCharge;
    bool feverPending;
    float runStart, waveStart;
    readonly List<ProtoEnemy> enemies = new List<ProtoEnemy>();
    readonly List<GameObject> props = new List<GameObject>();

    // Playtest stats
    int throws, throwsWithKill, kills, bestMulti, damageTaken, rescues, bossDamage, fevers;

    public Vector3 ZoneOrigin(int index) => Vector3.forward * zoneSpacing * index;

    private void Awake()
    {
        instance = this;
        if (waves.Count == 0)
            waves = DefaultWaves();
    }

    private void Start()
    {
        state = ProtoState.Menu;
        boss.Place(ZoneOrigin(0) + bossOffset);
        ProtoCamera.instance.Hold(ZoneOrigin(0));
        ProtoHUD.instance.ShowMenu();
    }

    private void Update()
    {
#if UNITY_EDITOR
        if (Input.GetKeyDown(KeyCode.R))
            Restart();
#endif
        if (state == ProtoState.Playing && WaveDone())
            StartCoroutine(ClearWave());
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused && (state == ProtoState.Playing || state == ProtoState.WaveIntro || state == ProtoState.Chase))
            ProtoTelemetry.Log("paused_mid_run", waveIndex + 1, Time.time - runStart, score);
    }

    #region Flow
    public void StartRun()
    {
        hearts = maxHearts;
        score = 0;
        feverCharge = 0f;
        throws = throwsWithKill = kills = bestMulti = damageTaken = rescues = bossDamage = fevers = 0;
        runStart = Time.time;
        ProtoTelemetry.BeginSession("rooftops");
        ProtoHUD.instance.HideMenu();
        ProtoHUD.instance.SetHearts(hearts, false);
        ProtoHUD.instance.SetScore(score, false);
        ProtoThrower.instance.ResetQuiver();
        StartCoroutine(BeginWave(0));
    }

    public void Restart()
    {
        ProtoTime.instance.ResetAll();
        Scene scene = SceneManager.GetActiveScene();
#if UNITY_EDITOR
        // The prototype scene isn't in Build Settings (that would shift the old game's level indices).
        if (scene.buildIndex < 0)
        {
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(scene.path, new LoadSceneParameters(LoadSceneMode.Single));
            return;
        }
#endif
        SceneManager.LoadScene(scene.buildIndex);
    }

    IEnumerator BeginWave(int index)
    {
        waveIndex = index;
        state = ProtoState.WaveIntro;
        ClearProps();

        ProtoWave wave = waves[index];
        boss.CanBeKnockedOut = wave.finale;
        ProtoHUD.instance.SetWave(index + 1, waves.Count);
        ProtoHUD.instance.Banner(wave.finale ? "SHOWDOWN!" : "ROOFTOP " + (index + 1), wave.title, 1.4f);
        if (!string.IsNullOrEmpty(wave.hint))
            ProtoHUD.instance.ShowHint(wave.hint);
        Spawn(wave, ZoneOrigin(index));
        ProtoTelemetry.Log("wave_start", index + 1);

        yield return new WaitForSecondsRealtime(1.5f);
        waveStart = Time.time;
        state = ProtoState.Playing;
    }

    bool WaveDone()
    {
        enemies.RemoveAll(e => e == null);
        foreach (ProtoEnemy e in enemies)
            if (e.IsAlive)
                return false;
        return true;
    }

    IEnumerator ClearWave()
    {
        ProtoWave wave = waves[waveIndex];
        ProtoHUD.instance.HideHint();

        // Showdown: Big Bear keeps calling in goons until he goes down.
        if (wave.finale)
        {
            state = ProtoState.WaveIntro;
            boss.Taunt();
            ProtoHUD.instance.Banner("MORE GOONS!", "TAKE DOWN BIG BEAR", 1f);
            yield return new WaitForSecondsRealtime(1f);
            if (state != ProtoState.WaveIntro)
                yield break;
            Spawn(wave, ZoneOrigin(waveIndex));
            yield return new WaitForSecondsRealtime(1f);
            if (state == ProtoState.WaveIntro)
                state = ProtoState.Playing;
            yield break;
        }

        state = ProtoState.WaveClear;
        ProtoTelemetry.Log("wave_clear", waveIndex + 1, Time.time - waveStart, hearts);
        Vector3 origin = ZoneOrigin(waveIndex);
        ProtoTime.instance.SlowMo(0.35f, 0.6f);
        ProtoAudio.Play(Sfx.Fanfare, 0.8f);
        ProtoFX.Confetti(origin + new Vector3(-3f, 0f, 9f));
        ProtoFX.Confetti(origin + new Vector3(3f, 0f, 9f));
        ProtoHUD.instance.Banner("ROOFTOP CLEAR!", "+" + (hearts * heartBonus) + " HEART BONUS", 1.3f);
        AddScore(hearts * heartBonus);
        ProtoHaptics.Pulse(40);
        yield return new WaitForSecondsRealtime(1.5f);

        yield return Chase(waveIndex + 1);
    }

    IEnumerator Chase(int next)
    {
        state = ProtoState.Chase;
        ClearProps();
        ProtoHUD.instance.Banner("AFTER HIM!", null, 1.2f);
        ProtoCamera.instance.Follow(ProtoThrower.instance.transform);

        float edgeZ = ZoneOrigin(waveIndex).z + roofEdge;
        StartCoroutine(boss.FleeTo(edgeZ, ZoneOrigin(next) + bossOffset));
        yield return new WaitForSeconds(0.35f);
        yield return ProtoThrower.instance.ChaseTo(edgeZ, ZoneOrigin(next));
        while (boss.IsBusy)
            yield return null;

        ProtoCamera.instance.Hold(ZoneOrigin(next));
        yield return BeginWave(next);
    }

    void Win()
    {
        state = ProtoState.Won;
        ProtoThrower.instance.Celebrate();
        ProtoFX.Confetti(ProtoThrower.instance.transform.position + Vector3.forward);
        ProtoTelemetry.Log("run_end", waveIndex + 1, "win", Time.time - runStart, Summary());
        ProtoHUD.instance.ShowEnd(true, Summary());
    }

    void Lose(Vector3 hitDirection)
    {
        state = ProtoState.Lost;
        ProtoTime.instance.SlowMo(0.25f, 1.2f);
        ProtoThrower.instance.Die(hitDirection);
        ProtoAudio.Play(Sfx.Hurt, 1f, 0.7f);
        ProtoTelemetry.Log("run_end", waveIndex + 1, "lose", Time.time - runStart, Summary());
        StartCoroutine(ShowEndLater(false, 1.4f));
    }

    IEnumerator ShowEndLater(bool won, float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        ProtoHUD.instance.ShowEnd(won, Summary());
    }

    string Summary()
    {
        int accuracy = throws > 0 ? Mathf.RoundToInt(100f * throwsWithKill / throws) : 0;
        return "SCORE  " + score
            + "\nROOFTOPS  " + (state == ProtoState.Won ? waves.Count : waveIndex) + " / " + waves.Count
            + "\nBOSS DAMAGE  " + bossDamage + " / " + boss.MaxHealth
            + "\nBEST MULTI-KILL  x" + bestMulti
            + "\nACCURACY  " + accuracy + "%"
            + "\nSHADOW STORMS  " + fevers
            + "\nTIME  " + Mathf.RoundToInt(Time.time - runStart) + "s";
    }
    #endregion

    #region Spawning
    void Spawn(ProtoWave wave, Vector3 origin)
    {
        var hostages = new List<ProtoHostage>();
        foreach (ProtoSpawn sp in wave.spawns)
        {
            Vector3 pos = origin + new Vector3(sp.position.x, 0f, sp.position.y);
            switch (sp.kind)
            {
                case ProtoSpawnKind.Grunt: SpawnEnemy(gruntPrefab, ProtoEnemyType.Grunt, pos, gruntSpeed * wave.speedMultiplier, sp.delay); break;
                case ProtoSpawnKind.Guard: SpawnEnemy(gruntPrefab, ProtoEnemyType.Guard, pos, 0f, sp.delay); break;
                case ProtoSpawnKind.Shield: SpawnEnemy(shieldPrefab, ProtoEnemyType.Shield, pos, shieldSpeed * wave.speedMultiplier, sp.delay); break;
                case ProtoSpawnKind.Runner: SpawnEnemy(runnerPrefab, ProtoEnemyType.Runner, pos, runnerSpeed * wave.speedMultiplier, sp.delay); break;
                case ProtoSpawnKind.Hostage:
                    ProtoHostage h = Instantiate(hostagePrefab, pos, Quaternion.LookRotation(Vector3.back));
                    hostages.Add(h);
                    props.Add(h.gameObject);
                    break;
                case ProtoSpawnKind.Barrel:
                    props.Add(Instantiate(barrelPrefab, pos, barrelPrefab.transform.rotation).gameObject);
                    ProtoFX.Dust(pos, 0.8f);
                    break;
                case ProtoSpawnKind.Crate:
                    // Crates come as a two-high stack so they cover blade height.
                    props.Add(Instantiate(cratePrefab, pos + Vector3.up * 0.5f, Quaternion.identity).gameObject);
                    props.Add(Instantiate(cratePrefab, pos + Vector3.up * 1.5f, Quaternion.identity).gameObject);
                    break;
            }
        }
        foreach (ProtoHostage h in hostages)
            h.CollectCaptors(enemies);
    }

    void SpawnEnemy(ProtoEnemy prefab, ProtoEnemyType type, Vector3 position, float speed, float delay)
    {
        ProtoEnemy e = Instantiate(prefab, position, Quaternion.LookRotation(Vector3.back));
        e.Init(type, speed, delay + Random.Range(0f, 0.25f));
        enemies.Add(e);
    }

    void ClearProps()
    {
        foreach (GameObject go in props)
        {
            if (go == null)
                continue;
            ProtoFX.Poof(go.transform.position, 0.8f);
            Destroy(go);
        }
        props.Clear();
    }
    #endregion

    #region Feedback events
    public void OnBladeThrown(ProtoBlade blade, int predictedKills)
    {
        throws++;
        ProtoHUD.instance.HideHint();
        if (!blade.IsFeverBlade)
            ProtoTelemetry.Log("throw", waveIndex + 1, predictedKills, ProtoThrower.instance.Quiver);
    }

    public void OnBladeFinished(ProtoBlade blade)
    {
        if (blade.IsFeverBlade)
            return;
        ProtoTelemetry.Log("blade", waveIndex + 1, blade.PredictedKills, blade.Kills);
        PredictedTotal += blade.PredictedKills;
        ActualTotal += blade.Kills;
        if (blade.Kills > 0)
            throwsWithKill++;
        bestMulti = Mathf.Max(bestMulti, blade.Kills);
        if (blade.Kills >= 2)
            ProtoTelemetry.Log("multi_kill", waveIndex + 1, blade.Kills);
    }

    // The juice ladder: every extra kill on the same blade escalates freeze, shake, zoom, pitch and text.
    // Fever blades get a lighter version so a storm of them doesn't stutter the game with hit-stops.
    public void OnKill(ProtoBlade blade, ProtoTarget target, int nth, Vector3 point)
    {
        kills++;
        int n = Mathf.Max(1, nth);
        bool fever = blade != null && blade.IsFeverBlade;
        ProtoEnemy enemy = target as ProtoEnemy;
        Color color = enemy != null ? enemy.BodyColor : Color.red;
        Vector3 dir = blade != null ? blade.transform.forward : Vector3.forward;

        int points = killScore * n;
        bool flank = enemy != null && enemy.WasFlanked;
        if (flank)
            points += flankBonus;
        AddScore(points);

        if (fever)
        {
            ProtoCamera.instance.Shake(0.12f);
            ProtoFX.KillBurst(point, dir, color, 1);
            ProtoAudio.Play(Sfx.Hit, 0.6f, ProtoAudio.Semitones(Random.Range(0, 5) * 2));
            ProtoHUD.instance.Popup(point + Vector3.up * 0.6f, "+" + points, new Color(0.85f, 0.6f, 1f), 0.9f);
            return;
        }

        ProtoTime.instance.HitStop(0.045f + 0.02f * Mathf.Min(n, 6));
        ProtoCamera.instance.Shake(0.22f + 0.07f * n);
        ProtoCamera.instance.FovPunch(1.5f + 1.2f * Mathf.Min(n, 6));
        ProtoCamera.instance.Kick(dir * 0.08f * n);
        ProtoFX.KillBurst(point, dir, color, n);
        ProtoAudio.Play(Sfx.Hit, 1f, ProtoAudio.Semitones((n - 1) * 2));
        ProtoAudio.Play(Sfx.Pop, 0.5f, ProtoAudio.Semitones((n - 1) * 2));
        ProtoHaptics.Pulse(15 + 8 * Mathf.Min(n, 6));

        ProtoHUD.instance.Popup(point + Vector3.up * 0.6f, "+" + points, n >= 3 ? new Color(1f, 0.6f, 0.15f) : Color.white, 1f + 0.12f * n);
        if (flank)
            ProtoHUD.instance.Popup(point + Vector3.up * 1.4f, "FLANKED!", new Color(0.4f, 1f, 1f), 1.2f);

        if (n >= 2)
        {
            ProtoHUD.instance.Combo(n);
            ProtoAudio.Play(Sfx.Combo, 0.6f, ProtoAudio.Semitones((n - 2) * 3));
            ChargeFever(feverPerExtraKill);
        }
        if (n == 3)
            ProtoTime.instance.SlowMo(0.3f, 0.45f);
        if (n >= 4)
        {
            ProtoTime.instance.SlowMo(0.2f, 0.6f);
            ProtoHUD.instance.Flash(new Color(1f, 1f, 1f, 0.45f));
        }
    }

    void ChargeFever(float amount)
    {
        if (ProtoThrower.instance.InFever || feverPending || state != ProtoState.Playing)
            return;
        feverCharge = Mathf.Min(1f, feverCharge + amount);
        if (feverCharge >= 1f)
            StartCoroutine(StartFeverSoon());
    }

    // Let the combo word that triggered it land first, so the Shadow Storm banner doesn't print on top of it.
    IEnumerator StartFeverSoon()
    {
        feverPending = true;
        yield return new WaitForSecondsRealtime(0.75f);
        feverPending = false;
        feverCharge = 0f;
        if (state == ProtoState.Playing)
            StartFever();
    }

    void StartFever()
    {
        fevers++;
        ProtoThrower.instance.StartFever(feverDuration);
        ProtoTime.instance.SlowMo(0.3f, 0.45f);
        ProtoCamera.instance.Shake(0.4f);
        ProtoCamera.instance.FovPunch(8f);
        ProtoAudio.Play(Sfx.Fanfare, 1f, 1.25f);
        ProtoHUD.instance.Flash(new Color(0.7f, 0.3f, 1f, 0.55f));
        ProtoHUD.instance.Banner("SHADOW STORM!", "HOLD TO UNLEASH", 1.1f);
        ProtoHaptics.Pulse(60);
        ProtoTelemetry.Log("fever", waveIndex + 1);
    }

    public void OnBossHit(ProtoBoss hitBoss, int damage, Vector3 point)
    {
        bossDamage += damage;
        AddScore(damage * bossHitScore);
        bool big = damage >= 3;
        if (big)
        {
            ProtoTime.instance.HitStop(0.06f + 0.01f * Mathf.Min(damage, 10));
            ProtoCamera.instance.FovPunch(2f + 0.5f * damage);
        }
        ProtoCamera.instance.Shake(big ? 0.35f : 0.1f);
        ProtoFX.KillBurst(point, Vector3.forward, new Color(1f, 0.85f, 0.4f), Mathf.Clamp(damage / 2, 1, 6));
        ProtoAudio.Play(Sfx.Thunk, 0.9f, big ? 0.75f : 1f);
        ProtoAudio.Play(Sfx.Hit, big ? 1f : 0.5f, 0.7f);
        ProtoHUD.instance.Popup(point + Vector3.up * 0.8f, "-" + damage, big ? new Color(1f, 0.3f, 0.2f) : new Color(1f, 0.85f, 0.4f), big ? 1.6f : 1f);
        ProtoHUD.instance.BossHit(big);
        ProtoTelemetry.Log("boss_hit", waveIndex + 1, damage, hitBoss.Health);
    }

    public void OnBossDefeated(ProtoBoss defeated, Vector3 direction)
    {
        StartCoroutine(BossKnockout(defeated, direction));
    }

    IEnumerator BossKnockout(ProtoBoss defeated, Vector3 direction)
    {
        state = ProtoState.Won;
        ProtoHUD.instance.HideHint();
        ProtoTime.instance.HitStop(0.25f);
        ProtoTime.instance.SlowMo(0.2f, 1.6f);
        ProtoCamera.instance.Shake(1f);
        ProtoCamera.instance.FovPunch(12f);
        ProtoHUD.instance.Flash(new Color(1f, 1f, 1f, 0.8f));
        ProtoAudio.Play(Sfx.Boom, 1f, 0.8f);
        ProtoHaptics.Pulse(150);
        defeated.Knockout(direction);
        foreach (ProtoEnemy e in enemies)
            if (e != null)
                e.Scatter();
        ProtoHUD.instance.Banner("K.O.!", "BIG BEAR IS DOWN!", 2f);
        ProtoTelemetry.Log("boss_ko", waveIndex + 1, Time.time - runStart);
        yield return new WaitForSecondsRealtime(2.4f);
        Win();
    }

    public void OnShieldBlocked(ProtoEnemy enemy, Vector3 point)
    {
        ProtoTime.instance.HitStop(0.06f);
        ProtoCamera.instance.Shake(0.3f);
        ProtoFX.Clang(point);
        ProtoAudio.Play(Sfx.Clang, 0.9f, Random.Range(0.95f, 1.05f));
        ProtoHUD.instance.Popup(point + Vector3.up * 0.8f, "CLANG!", new Color(0.75f, 0.85f, 1f), 1.2f);
        ProtoHaptics.Pulse(30);
    }

    public void OnCrateHit(Vector3 point)
    {
        ProtoCamera.instance.Shake(0.12f);
        ProtoFX.WoodHit(point);
        ProtoAudio.Play(Sfx.Wood, 0.9f, Random.Range(0.9f, 1.1f));
        ProtoHUD.instance.Popup(point + Vector3.up * 0.8f, "THUNK", new Color(0.95f, 0.8f, 0.55f), 0.9f);
    }

    public void OnExplosion(Vector3 center, float radius)
    {
        ProtoTime.instance.HitStop(0.12f);
        ProtoTime.instance.SlowMo(0.3f, 0.5f);
        ProtoCamera.instance.Shake(0.85f);
        ProtoCamera.instance.FovPunch(9f);
        ProtoFX.Explosion(center, radius);
        ProtoAudio.Play(Sfx.Boom, 1f, Random.Range(0.9f, 1.05f));
        ProtoHUD.instance.Flash(new Color(1f, 0.85f, 0.5f, 0.6f));
        ProtoHUD.instance.Popup(center + Vector3.up * 1.5f, "KABOOM!", new Color(1f, 0.55f, 0.1f), 1.8f);
        ProtoHaptics.Pulse(80);
    }

    public void OnEnemyReachedPlayer(ProtoEnemy enemy)
    {
        Vector3 dir = (ProtoThrower.instance.transform.position - enemy.transform.position).ReplaceY(0f).normalized;
        Damage(dir, "OUCH!");
    }

    public void OnHostageHit(ProtoHostage hostage, Vector3 point)
    {
        ProtoHUD.instance.Popup(point + Vector3.up, "OOPS!", new Color(1f, 0.3f, 0.3f), 1.6f);
        Damage(Vector3.back, null);
    }

    public void OnHostageLost(ProtoHostage hostage)
    {
        ProtoHUD.instance.Popup(hostage.transform.position + Vector3.up * 2f, "TOO SLOW!", new Color(1f, 0.3f, 0.3f), 1.5f);
        Damage(Vector3.back, null);
    }

    public void OnHostageFreed(ProtoHostage hostage)
    {
        rescues++;
        AddScore(rescueBonus);
        ProtoFX.Sparkle(hostage.transform.position + Vector3.up, 1.5f);
        ProtoFX.Confetti(hostage.transform.position);
        ProtoAudio.Play(Sfx.Rescue, 1f);
        ProtoHUD.instance.Popup(hostage.transform.position + Vector3.up * 2.2f, "RESCUED! +" + rescueBonus, new Color(0.45f, 1f, 0.45f), 1.6f);
        ProtoTelemetry.Log("rescue", waveIndex + 1);
    }

    void Damage(Vector3 direction, string text)
    {
        if (state != ProtoState.Playing)
            return;

        hearts--;
        damageTaken++;
        ProtoTelemetry.Log("damage", waveIndex + 1, hearts);
        ProtoTime.instance.HitStop(0.1f);
        ProtoCamera.instance.Shake(0.7f);
        ProtoCamera.instance.Kick(-direction * 0.4f);
        ProtoAudio.Play(Sfx.Hurt, 1f);
        ProtoHUD.instance.SetHearts(hearts, true);
        ProtoHUD.instance.DamageFlash();
        ProtoHaptics.Pulse(120);
        if (text != null)
            ProtoHUD.instance.Popup(ProtoThrower.instance.transform.position + Vector3.up * 2.4f, text, new Color(1f, 0.3f, 0.3f), 1.5f);

        if (hearts <= 0)
            Lose(direction);
        else
            ProtoThrower.instance.Flinch();
    }

    void AddScore(int amount)
    {
        score += amount;
        ProtoHUD.instance.SetScore(score, true);
    }
    #endregion

    #region Default content
    // Five rooftops, each introducing one idea, ending in the showdown. Positions are relative to the rooftop origin
    // and tuned for the default curve (hook 0.5, boss at z = 19.4, ninja at the rooftop origin).
    public static List<ProtoWave> DefaultWaves()
    {
        return new List<ProtoWave>
        {
            new ProtoWave
            {
                title = "LINE 'EM UP",
                hint = "HOLD, SLIDE TO BEND, RELEASE!",
                speedMultiplier = 0.6f,
                spawns =
                {
                    new ProtoSpawn(ProtoSpawnKind.Grunt, 3.0f, 9.2f),
                    new ProtoSpawn(ProtoSpawnKind.Grunt, 3.3f, 13.0f, 0.15f),
                    new ProtoSpawn(ProtoSpawnKind.Grunt, 2.2f, 16.7f, 0.3f),
                    new ProtoSpawn(ProtoSpawnKind.Grunt, -2.8f, 10.1f, 0.45f),
                    new ProtoSpawn(ProtoSpawnKind.Grunt, -2.3f, 15.8f, 0.6f),
                }
            },
            new ProtoWave
            {
                title = "RIOT SHIELDS",
                hint = "SHIELDS BLOCK. BEND AROUND THEM!",
                speedMultiplier = 0.8f,
                spawns =
                {
                    new ProtoSpawn(ProtoSpawnKind.Shield, -1.5f, 8.5f),
                    new ProtoSpawn(ProtoSpawnKind.Shield, 0f, 8.5f, 0.15f),
                    new ProtoSpawn(ProtoSpawnKind.Shield, 1.5f, 8.5f, 0.3f),
                    new ProtoSpawn(ProtoSpawnKind.Grunt, -3.2f, 14f, 0.5f),
                    new ProtoSpawn(ProtoSpawnKind.Grunt, 3.4f, 13.5f, 0.6f),
                    new ProtoSpawn(ProtoSpawnKind.Grunt, 0f, 16.5f, 0.7f),
                    new ProtoSpawn(ProtoSpawnKind.Runner, -4.5f, 17.5f, 6f),
                }
            },
            new ProtoWave
            {
                title = "KABOOM",
                hint = "HIT THE BARREL!",
                speedMultiplier = 0.9f,
                spawns =
                {
                    new ProtoSpawn(ProtoSpawnKind.Barrel, 0f, 12.5f),
                    new ProtoSpawn(ProtoSpawnKind.Crate, -2.6f, 7.5f),
                    new ProtoSpawn(ProtoSpawnKind.Crate, 2.6f, 7.5f),
                    new ProtoSpawn(ProtoSpawnKind.Grunt, -1.6f, 12.6f, 0.2f),
                    new ProtoSpawn(ProtoSpawnKind.Grunt, 1.6f, 12.6f, 0.3f),
                    new ProtoSpawn(ProtoSpawnKind.Grunt, 0f, 14.4f, 0.4f),
                    new ProtoSpawn(ProtoSpawnKind.Grunt, -0.9f, 10.8f, 0.5f),
                    new ProtoSpawn(ProtoSpawnKind.Grunt, 1.0f, 10.9f, 0.6f),
                    new ProtoSpawn(ProtoSpawnKind.Runner, -4.5f, 17f, 3f),
                    new ProtoSpawn(ProtoSpawnKind.Runner, 4.5f, 17f, 3.5f),
                }
            },
            new ProtoWave
            {
                title = "HOSTAGE",
                hint = "DON'T HIT THE HOSTAGE!",
                speedMultiplier = 1f,
                spawns =
                {
                    new ProtoSpawn(ProtoSpawnKind.Hostage, 0f, 13f),
                    new ProtoSpawn(ProtoSpawnKind.Guard, -1.4f, 13.6f, 0.1f),
                    new ProtoSpawn(ProtoSpawnKind.Guard, 1.4f, 13.6f, 0.2f),
                    new ProtoSpawn(ProtoSpawnKind.Guard, 0f, 15.3f, 0.3f),
                    new ProtoSpawn(ProtoSpawnKind.Shield, -3f, 9.5f, 0.5f),
                    new ProtoSpawn(ProtoSpawnKind.Grunt, 3f, 10f, 0.6f),
                    new ProtoSpawn(ProtoSpawnKind.Runner, 4f, 17.5f, 5f),
                }
            },
            new ProtoWave
            {
                title = "TAKE DOWN BIG BEAR",
                hint = null,
                speedMultiplier = 1.15f,
                finale = true,
                spawns =
                {
                    new ProtoSpawn(ProtoSpawnKind.Barrel, 0f, 15.5f),
                    new ProtoSpawn(ProtoSpawnKind.Grunt, -3f, 16f),
                    new ProtoSpawn(ProtoSpawnKind.Grunt, 3f, 16f, 0.2f),
                    new ProtoSpawn(ProtoSpawnKind.Shield, -1.2f, 10f, 0.3f),
                    new ProtoSpawn(ProtoSpawnKind.Shield, 1.2f, 10f, 0.4f),
                    new ProtoSpawn(ProtoSpawnKind.Runner, -4f, 12f, 1f),
                    new ProtoSpawn(ProtoSpawnKind.Runner, 4f, 12f, 1.5f),
                    new ProtoSpawn(ProtoSpawnKind.Grunt, -2f, 17.5f, 3f),
                    new ProtoSpawn(ProtoSpawnKind.Grunt, 2f, 17.5f, 3.5f),
                }
            },
        };
    }

#if UNITY_EDITOR
    public void EditorAssignDefaultWaves() => waves = DefaultWaves();
#endif
    #endregion
}

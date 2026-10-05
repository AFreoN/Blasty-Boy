using System.Collections;
using UnityEngine;

public enum MusicCue { Menu, GetReady, Chase, Showdown, RooftopClear, Victory, Defeat, Star1, Star2, Star3, Fever }

// Music and stingers. Loops crossfade between two sources; stingers play over a ducked loop. While the player aims in
// bullet-time the music is low-passed so the focus moment reads in the audio too.
// Clips come from the locally installed Asset Store music pack; with none assigned the game simply has no music.
public class ProtoMusic : MonoBehaviour
{
    public static ProtoMusic instance { get; private set; }

    [Header("Loops")]
    [SerializeField] AudioClip menu = null;
    [SerializeField] AudioClip chase = null;
    [SerializeField] AudioClip showdown = null;
    [Header("Stingers")]
    [SerializeField] AudioClip getReady = null;
    [SerializeField] AudioClip rooftopClear = null;
    [SerializeField] AudioClip victory = null;
    [SerializeField] AudioClip defeat = null;
    [SerializeField] AudioClip star1 = null;
    [SerializeField] AudioClip star2 = null;
    [SerializeField] AudioClip star3 = null;
    [SerializeField] AudioClip fever = null;
    [Header("Mix")]
    [SerializeField, Range(0, 1)] float musicVolume = 0.55f;
    [SerializeField, Range(0, 1)] float stingerVolume = 0.85f;
    [SerializeField] float crossfade = 0.8f;
    [SerializeField] float focusCutoff = 900f;

    AudioSource[] loops;
    AudioSource stinger;
    AudioLowPassFilter lowPass;
    int active;
    float duck = 1f, duckTarget = 1f, focus;
    Coroutine fade;

    private void Awake()
    {
        instance = this;
        loops = new[] { NewSource(true), NewSource(true) };
        stinger = NewSource(false);
        lowPass = gameObject.AddComponent<AudioLowPassFilter>();
        lowPass.cutoffFrequency = 22000f;
    }

    AudioSource NewSource(bool loop)
    {
        var src = gameObject.AddComponent<AudioSource>();
        src.loop = loop;
        src.playOnAwake = false;
        src.spatialBlend = 0f;
        src.ignoreListenerPause = true;
        return src;
    }

    public static void Play(MusicCue cue)
    {
        if (instance != null)
            instance.PlayCue(cue);
    }

    // 0..1: how far into the "aiming in bullet-time" muffle the music should be.
    public static void SetFocus(bool on)
    {
        if (instance != null)
            instance.focus = on ? 1f : 0f;
    }

    void PlayCue(MusicCue cue)
    {
        switch (cue)
        {
            case MusicCue.Menu: Loop(menu); break;
            case MusicCue.Chase: Loop(chase); break;
            case MusicCue.Showdown: Loop(showdown); break;
            case MusicCue.GetReady: Sting(getReady, 0.35f); break;
            case MusicCue.RooftopClear: Sting(rooftopClear, 0.25f); break;
            case MusicCue.Fever: Sting(fever, 0.5f); break;
            case MusicCue.Star1: Sting(star1, 0.6f); break;
            case MusicCue.Star2: Sting(star2, 0.6f); break;
            case MusicCue.Star3: Sting(star3, 0.6f); break;
            case MusicCue.Victory: Loop(null); Sting(victory, 1f); break;
            case MusicCue.Defeat: Loop(null); Sting(defeat, 1f); break;
        }
    }

    void Loop(AudioClip clip)
    {
        AudioSource current = loops[active];
        if (clip != null && current.clip == clip && current.isPlaying)
            return;
        active = 1 - active;
        AudioSource next = loops[active];
        next.clip = clip;
        next.volume = 0f;
        if (clip != null)
            next.Play();
        if (fade != null)
            StopCoroutine(fade);
        fade = StartCoroutine(Crossfade(current, next));
    }

    IEnumerator Crossfade(AudioSource from, AudioSource to)
    {
        float fromStart = from.volume;
        for (float t = 0f; t < crossfade; t += Time.unscaledDeltaTime)
        {
            float k = t / crossfade;
            from.volume = fromStart * (1f - k);
            to.volume = musicVolume * k;
            yield return null;
        }
        from.Stop();
        to.volume = musicVolume;
        fade = null;
    }

    void Sting(AudioClip clip, float duckTo)
    {
        if (clip == null)
            return;
        stinger.PlayOneShot(clip, stingerVolume);
        duckTarget = duckTo;
        StopCoroutine(nameof(Unduck));
        StartCoroutine(nameof(Unduck), clip.length);
    }

    IEnumerator Unduck(float after)
    {
        yield return new WaitForSecondsRealtime(after * 0.85f);
        duckTarget = 1f;
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        duck = Mathf.MoveTowards(duck, duckTarget, dt * 2.5f);
        if (fade == null)
            loops[active].volume = musicVolume * duck;
        float target = Mathf.Lerp(22000f, focusCutoff, focus);
        lowPass.cutoffFrequency = Mathf.Lerp(lowPass.cutoffFrequency, target, 1f - Mathf.Exp(-10f * dt));
    }
}

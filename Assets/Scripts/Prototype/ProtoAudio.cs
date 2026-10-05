using System;
using System.Collections.Generic;
using UnityEngine;

public enum Sfx { Whoosh, Shing, Hit, Pop, Clang, Boom, Thunk, Tick, Hurt, Fanfare, Rescue, Land, Combo, Empty, Wood }

// Synthesizes every prototype sound effect at startup (the project has no audio assets yet).
// Pitch is used as a juice lever: consecutive pierces in one throw climb the scale.
public class ProtoAudio : MonoBehaviour
{
    public static ProtoAudio instance { get; private set; }

    [SerializeField] int voices = 14;
    [SerializeField, Range(0, 1)] float masterVolume = 0.8f;

    const int sampleRate = 44100;
    readonly Dictionary<Sfx, AudioClip> clips = new Dictionary<Sfx, AudioClip>();
    AudioSource[] sources;
    int nextSource;
    static System.Random rng = new System.Random(7);

    private void Awake()
    {
        instance = this;
        sources = new AudioSource[voices];
        for (int i = 0; i < voices; i++)
        {
            sources[i] = gameObject.AddComponent<AudioSource>();
            sources[i].playOnAwake = false;
            sources[i].spatialBlend = 0f;
        }
        Build();
    }

    public static void Play(Sfx sfx, float volume = 1f, float pitch = 1f)
    {
        if (instance == null || !instance.clips.TryGetValue(sfx, out AudioClip clip))
            return;

        AudioSource src = instance.sources[instance.nextSource];
        instance.nextSource = (instance.nextSource + 1) % instance.sources.Length;
        src.pitch = pitch;
        src.PlayOneShot(clip, volume * instance.masterVolume);
    }

    // Semitone helper so callers can think musically: Play(Sfx.Hit, 1, Semitones(n * 2)).
    public static float Semitones(float st) => Mathf.Pow(2f, st / 12f);

    void Build()
    {
        clips[Sfx.Whoosh] = Make("whoosh", 0.3f, LowpassNoise(t => Mathf.Lerp(0.04f, 0.35f, t / 0.3f), t => Mathf.Pow(Mathf.Sin(Mathf.PI * t / 0.3f), 2f) * 0.9f));
        clips[Sfx.Shing] = Make("shing", 0.35f, t => (Sine(2600f, t) * 0.5f + Sine(3720f, t) * 0.35f + Sine(5150f, t) * 0.2f) * Mathf.Exp(-t * 14f) * 0.5f);
        clips[Sfx.Hit] = Make("hit", 0.22f, Sum(
            Sweep(200f, 50f, 18f, t => Mathf.Exp(-t * 20f)),
            LowpassNoise(t => 0.5f, t => Mathf.Exp(-t * 55f) * 0.9f),
            t => Sine(900f, t) * Mathf.Exp(-t * 60f) * 0.3f));
        clips[Sfx.Pop] = Make("pop", 0.16f, t => Sine(220f + 900f * Mathf.Exp(-t * 22f), t) * Mathf.Exp(-t * 16f) * 0.8f);
        clips[Sfx.Clang] = Make("clang", 0.7f, Sum(
            t => (Sine(523f, t) * 0.45f * Mathf.Exp(-t * 5f) + Sine(1187f, t) * 0.35f * Mathf.Exp(-t * 7f) + Sine(1693f, t) * 0.3f * Mathf.Exp(-t * 9f) + Sine(2475f, t) * 0.25f * Mathf.Exp(-t * 12f) + Sine(3301f, t) * 0.2f * Mathf.Exp(-t * 16f)),
            LowpassNoise(t => 0.7f, t => Mathf.Exp(-t * 80f) * 0.6f)));
        clips[Sfx.Boom] = Make("boom", 1.2f, Saturate(Sum(
            LowpassNoise(t => Mathf.Lerp(0.15f, 0.03f, t), t => Mathf.Exp(-t * 3.5f) * 1.4f),
            Sweep(90f, 35f, 4f, t => Mathf.Exp(-t * 4f) * 1.2f))));
        clips[Sfx.Thunk] = Make("thunk", 0.18f, Sum(
            Sweep(170f, 85f, 30f, t => Mathf.Exp(-t * 26f)),
            LowpassNoise(t => 0.3f, t => Mathf.Exp(-t * 70f) * 0.7f)));
        clips[Sfx.Wood] = Make("wood", 0.2f, Sum(
            Sweep(320f, 180f, 25f, t => Mathf.Exp(-t * 30f) * 0.7f),
            LowpassNoise(t => 0.4f, t => Mathf.Exp(-t * 45f) * 0.8f)));
        clips[Sfx.Tick] = Make("tick", 0.06f, t => Sine(1760f, t) * Mathf.Exp(-t * 70f) * 0.6f);
        clips[Sfx.Hurt] = Make("hurt", 0.45f, Saturate(t => (Saw(110f, t) + Saw(116f, t)) * 0.35f * Mathf.Exp(-t * 6f)));
        clips[Sfx.Land] = Make("land", 0.16f, Sum(
            Sweep(110f, 45f, 25f, t => Mathf.Exp(-t * 25f)),
            LowpassNoise(t => 0.12f, t => Mathf.Exp(-t * 30f) * 0.8f)));
        clips[Sfx.Empty] = Make("empty", 0.12f, t => Square(220f, t) * Mathf.Exp(-t * 30f) * 0.25f);
        clips[Sfx.Combo] = Make("combo", 0.3f, Arpeggio(new[] { 0, 4, 7, 12 }, 0.06f, 659f));
        clips[Sfx.Fanfare] = Make("fanfare", 0.9f, Arpeggio(new[] { 0, 4, 7, 12, 16, 19, 24 }, 0.075f, 523f));
        clips[Sfx.Rescue] = Make("rescue", 0.45f, Arpeggio(new[] { 7, 12, 19 }, 0.11f, 523f));
    }

    static AudioClip Make(string name, float duration, Func<float, float> fn)
    {
        int n = Mathf.CeilToInt(duration * sampleRate);
        var data = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)sampleRate;
            float fade = Mathf.Clamp01((duration - t) / 0.01f);   // de-click tail
            data[i] = Mathf.Clamp(fn(t) * fade, -1f, 1f);
        }
        AudioClip clip = AudioClip.Create("Proto_" + name, n, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    static float Sine(float f, float t) => Mathf.Sin(2f * Mathf.PI * f * t);
    static float Saw(float f, float t) => 2f * Mathf.Repeat(f * t, 1f) - 1f;
    static float Square(float f, float t) => Mathf.Repeat(f * t, 1f) < 0.5f ? 1f : -1f;
    static float Noise() => (float)(rng.NextDouble() * 2.0 - 1.0);

    static Func<float, float> Sum(params Func<float, float>[] parts) => t =>
    {
        float v = 0f;
        foreach (var p in parts) v += p(t);
        return v;
    };

    static Func<float, float> Saturate(Func<float, float> fn) => t => (float)Math.Tanh(fn(t) * 1.5f);

    // Pitch sweep with exponential glide; phase is integrated so the sweep stays click-free.
    static Func<float, float> Sweep(float fStart, float fEnd, float glide, Func<float, float> env)
    {
        float phase = 0f, lastT = 0f;
        return t =>
        {
            float f = fEnd + (fStart - fEnd) * Mathf.Exp(-t * glide);
            phase += 2f * Mathf.PI * f * (t - lastT);
            lastT = t;
            return Mathf.Sin(phase) * env(t);
        };
    }

    // One-pole low-passed noise; cutoff(t) is the filter coefficient in 0..1 (higher = brighter).
    static Func<float, float> LowpassNoise(Func<float, float> cutoff, Func<float, float> env)
    {
        float state = 0f;
        return t =>
        {
            state += (Noise() - state) * Mathf.Clamp01(cutoff(t));
            return state * env(t) * 2f;
        };
    }

    static Func<float, float> Arpeggio(int[] semitones, float step, float root)
    {
        return t =>
        {
            int i = Mathf.Min((int)(t / step), semitones.Length - 1);
            float local = t - i * step;
            float f = root * Mathf.Pow(2f, semitones[i] / 12f);
            float tone = Sine(f, t) * 0.6f + Sine(f * 2f, t) * 0.2f + Square(f, t) * 0.08f;
            float env = Mathf.Exp(-local * (i == semitones.Length - 1 ? 5f : 18f));
            return tone * env * 0.55f;
        };
    }
}

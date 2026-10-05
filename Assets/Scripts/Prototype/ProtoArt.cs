using System;
using System.Collections.Generic;
using UnityEngine;

// Procedurally generated textures and sprites for the prototype's UI and particles, so the prototype
// doesn't depend on art that doesn't exist yet.
public static class ProtoArt
{
    static readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();
    static readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();

    public static Texture2D SoftCircle => Get("soft", 64, (x, y) => { float d = Mathf.Sqrt(x * x + y * y); return Mathf.Clamp01(1f - d) * Mathf.Clamp01(1f - d); });
    public static Texture2D Circle => Get("circle", 64, (x, y) => Edge(1f - Mathf.Sqrt(x * x + y * y), 64));
    public static Texture2D Ring => Get("ring", 128, (x, y) => { float d = Mathf.Sqrt(x * x + y * y); return Edge(Mathf.Min(1f - d, d - 0.78f), 128); });
    public static Texture2D Star => Get("star", 64, (x, y) => Edge(StarDistance(x, y, 5, 0.45f), 64));
    public static Texture2D Square => Get("square", 16, (x, y) => 1f);
    public static Texture2D Shuriken => Get("shuriken", 128, (x, y) =>
    {
        float d = Mathf.Sqrt(x * x + y * y);
        return Mathf.Min(Edge(StarDistance(x, y, 4, 0.32f), 128), Edge(d - 0.16f, 128));
    });
    public static Texture2D Heart => Get("heart", 128, (x, y) =>
    {
        float hx = x * 1.3f, hy = y * 1.3f + 0.15f;
        float a = hx * hx + hy * hy - 1f;
        float f = a * a * a - hx * hx * hy * hy * hy;
        return Edge(-f * 0.6f, 128);
    });
    public static Texture2D Vignette => Get("vignette", 128, (x, y) =>
    {
        float d = Mathf.Sqrt(x * x + y * y) / 1.41f;
        return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 1f, d));
    });
    public static Texture2D Dash => GetDash();

    public static Sprite Sprite(Texture2D texture)
    {
        if (sprites.TryGetValue(texture.name, out Sprite s) && s != null)
            return s;
        s = UnityEngine.Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
        s.name = texture.name;
        sprites[texture.name] = s;
        return s;
    }

    // Signed distance-ish value for an n-pointed star (positive inside).
    static float StarDistance(float x, float y, int points, float inner)
    {
        float d = Mathf.Sqrt(x * x + y * y);
        float angle = Mathf.Atan2(y, x) - Mathf.PI * 0.5f;
        float sector = Mathf.Repeat(angle * points / (2f * Mathf.PI), 1f);
        float k = Mathf.Abs(sector - 0.5f) * 2f;
        float radius = Mathf.Lerp(inner, 1f, k * k);
        return radius - d;
    }

    // Converts a distance (in normalized units, positive inside) into a ~1px anti-aliased alpha.
    static float Edge(float distance, int size) => Mathf.Clamp01(distance * size * 0.5f + 0.5f);

    static Texture2D Get(string name, int size, Func<float, float, float> alpha)
    {
        if (textures.TryGetValue(name, out Texture2D tex) && tex != null)
            return tex;

        tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "Proto_" + name, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float nx = (x + 0.5f) / size * 2f - 1f;
                float ny = (y + 0.5f) / size * 2f - 1f;
                byte a = (byte)(Mathf.Clamp01(alpha(nx, ny)) * 255f);
                pixels[y * size + x] = new Color32(255, 255, 255, a);
            }
        }
        tex.SetPixels32(pixels);
        tex.Apply(false, true);
        textures[name] = tex;
        return tex;
    }

    static Texture2D GetDash()
    {
        if (textures.TryGetValue("dash", out Texture2D tex) && tex != null)
            return tex;

        const int w = 64, h = 16;
        tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = "Proto_dash", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
        var pixels = new Color32[w * h];
        for (int y = 0; y < h; y++)
        {
            float vy = 1f - Mathf.Abs((y + 0.5f) / h * 2f - 1f);
            for (int x = 0; x < w; x++)
            {
                float vx = Mathf.Clamp01(Mathf.Min(x - 4f, 44f - x) / 3f);
                byte a = (byte)(Mathf.Clamp01(vx) * Mathf.Clamp01(vy * 3f) * 255f);
                pixels[y * w + x] = new Color32(255, 255, 255, a);
            }
        }
        tex.SetPixels32(pixels);
        tex.Apply(false, true);
        textures["dash"] = tex;
        return tex;
    }
}

using UnityEngine;

// Path family for a thrown blade. Every throw starts at the player and ends at the board; "bend" picks how far
// the blade swings sideways on the way. One finger axis -> one number -> a curve that covers the whole arena.
//   hook = 0 : symmetric arc (bulges evenly)
//   hook = 1 : launches straight, swings out late and hooks hard back into the board
public static class ProtoCurve
{
    public static float Shape(float s, float hook)
    {
        float symmetric = 4f * s * (1f - s);
        float hooked = 6.75f * s * s * (1f - s);
        return Mathf.Lerp(symmetric, hooked, hook);
    }

    public static float ShapeSlope(float s, float hook)
    {
        float symmetric = 4f - 8f * s;
        float hooked = 6.75f * (2f * s - 3f * s * s);
        return Mathf.Lerp(symmetric, hooked, hook);
    }

    public static Vector3 Point(Vector3 from, Vector3 to, float bend, float hook, float s)
    {
        Vector3 d = to - from;
        return from + d * s + Right(d) * (bend * Shape(s, hook));
    }

    public static Vector3 Tangent(Vector3 from, Vector3 to, float bend, float hook, float s)
    {
        Vector3 d = to - from;
        return (d + Right(d) * (bend * ShapeSlope(s, hook))).normalized;
    }

    public static float ApproxLength(Vector3 from, Vector3 to, float bend, float hook, int steps = 24)
    {
        float length = 0f;
        Vector3 prev = from;
        for (int i = 1; i <= steps; i++)
        {
            Vector3 p = Point(from, to, bend, hook, i / (float)steps);
            length += Vector3.Distance(prev, p);
            prev = p;
        }
        return Mathf.Max(length, 0.01f);
    }

    static Vector3 Right(Vector3 d)
    {
        d.y = 0f;
        return Vector3.Cross(Vector3.up, d.normalized);
    }
}

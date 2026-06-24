using System.Collections;
using UnityEngine;

public static class Ease
{
    /// <summary>Slow start, faster toward the end.</summary>
    public static float InQuad(float t) => t * t;

    /// <summary>Fast start, slower toward the end — "slowly then all at once".</summary>
    public static float OutQuad(float t)
    {
        float inv = 1f - t;
        return 1f - inv * inv;
    }

    public static float Lerp(float from, float to, float t) => Mathf.Lerp(from, to, InQuad(Mathf.Clamp01(t)));

    public static float LerpOut(float from, float to, float t) => Mathf.Lerp(from, to, OutQuad(Mathf.Clamp01(t)));
}

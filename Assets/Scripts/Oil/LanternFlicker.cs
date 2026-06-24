using UnityEngine;

// Shared Perlin flicker used by world oil lights and the player lantern.
public static class LanternFlicker
{
    private const float SlowFlickerSpeed = 3.5f;
    private const float FastFlickerSpeed = 13f;
    private const float IntensityWobble = 0.35f;
    private const float ColorWobble = 0.1f;

    public static float SampleIntensity(float seed, float baseIntensity)
    {
        float flicker = SampleFlicker(seed);
        float intensityScale = 1f + (flicker - 0.5f) * IntensityWobble;
        return baseIntensity * intensityScale;
    }

    public static Color SampleColor(float seed, Color baseColor)
    {
        float flicker = SampleFlicker(seed);
        float colorShift = (flicker - 0.5f) * ColorWobble;
        return new Color(
            Mathf.Clamp01(baseColor.r + colorShift),
            Mathf.Clamp01(baseColor.g + colorShift * 0.5f),
            Mathf.Clamp01(baseColor.b - colorShift * 0.25f),
            baseColor.a);
    }

    private static float SampleFlicker(float seed)
    {
        float slow = Mathf.PerlinNoise(seed, Time.time * SlowFlickerSpeed);
        float fast = Mathf.PerlinNoise(seed + 37f, Time.time * FastFlickerSpeed);
        return slow * 0.55f + fast * 0.45f;
    }
}

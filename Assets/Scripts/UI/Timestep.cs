using UnityEngine;

// Frame-rate-independent timing helpers. Pass an explicit deltaTime (usually
// Time.deltaTime or Time.unscaledDeltaTime) so animations run at the same speed
// whether the game renders at 60 or 240 FPS.
public static class Timestep
{
    public static float Delta => Time.deltaTime;
    public static float UnscaledDelta => Time.unscaledDeltaTime;

    public static float ExpDecay(float speed, float deltaTime)
    {
        return 1f - Mathf.Exp(-speed * deltaTime);
    }

    public static float ExpLerp(float current, float target, float speed, float deltaTime)
    {
        return Mathf.Lerp(current, target, ExpDecay(speed, deltaTime));
    }

    public static Vector3 ExpLerp(Vector3 current, Vector3 target, float speed, float deltaTime)
    {
        return Vector3.Lerp(current, target, ExpDecay(speed, deltaTime));
    }

    public static float MoveTowards(float current, float target, float maxDeltaPerSecond, float deltaTime)
    {
        return Mathf.MoveTowards(current, target, maxDeltaPerSecond * deltaTime);
    }

    public static float AdvanceTimer(float elapsed, float duration, float deltaTime)
    {
        return Mathf.Min(elapsed + deltaTime, duration);
    }

    public static float NormalizeTimer(float elapsed, float duration)
    {
        return duration > 0f ? Mathf.Clamp01(elapsed / duration) : 1f;
    }
}

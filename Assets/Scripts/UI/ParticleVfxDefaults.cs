using UnityEngine;

public static class ParticleVfxDefaults
{
    public static void ApplyTimedSimulation(ParticleSystem.MainModule main)
    {
        main.useUnscaledTime = false;
        main.simulationSpeed = 1f;
    }
}

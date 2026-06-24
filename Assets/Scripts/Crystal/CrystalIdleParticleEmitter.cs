using UnityEngine;

// Soft upward sparkles for idle crystal glow.
public class CrystalIdleParticleEmitter : MonoBehaviour
{
    private ParticleSystem particles;
    private bool roomFill;

    public void Configure(Color color, float emissionRate, bool fillRoom = false)
    {
        roomFill = fillRoom;
        EnsureParticleSystem();
        ApplyColor(color);

        var emission = particles.emission;
        emission.rateOverTime = emissionRate;
        particles.Play();
    }

    private void EnsureParticleSystem()
    {
        if (particles != null)
        {
            return;
        }

        particles = gameObject.AddComponent<ParticleSystem>();

        var main = particles.main;
        ParticleVfxDefaults.ApplyTimedSimulation(main);
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = roomFill ? 240 : 64;
        main.startLifetime = roomFill
            ? new ParticleSystem.MinMaxCurve(2.8f, 5.2f)
            : new ParticleSystem.MinMaxCurve(0.9f, 1.6f);
        main.startSpeed = roomFill
            ? new ParticleSystem.MinMaxCurve(0.05f, 0.28f)
            : new ParticleSystem.MinMaxCurve(0.08f, 0.35f);
        main.startSize = roomFill
            ? new ParticleSystem.MinMaxCurve(0.035f, 0.12f)
            : new ParticleSystem.MinMaxCurve(0.03f, 0.08f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.gravityModifier = roomFill ? -0.015f : -0.04f;

        var emission = particles.emission;
        emission.enabled = true;
        emission.rateOverTime = 14f;

        var shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = roomFill ? 4.75f : 0.18f;
        shape.radiusThickness = roomFill ? 1f : 0.6f;

        var velocityOverLifetime = particles.velocityOverLifetime;
        velocityOverLifetime.enabled = true;
        velocityOverLifetime.space = ParticleSystemSimulationSpace.World;
        velocityOverLifetime.z = new ParticleSystem.MinMaxCurve(0f, 0f);
        velocityOverLifetime.x = roomFill
            ? new ParticleSystem.MinMaxCurve(-0.18f, 0.18f)
            : new ParticleSystem.MinMaxCurve(0f, 0f);
        velocityOverLifetime.y = roomFill
            ? new ParticleSystem.MinMaxCurve(0.02f, 0.18f)
            : new ParticleSystem.MinMaxCurve(0.15f, 0.55f);

        var colorOverLifetime = particles.colorOverLifetime;
        colorOverLifetime.enabled = true;

        var sizeOverLifetime = particles.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.4f),
            new Keyframe(0.25f, 1f),
            new Keyframe(1f, 0.05f)));

        var renderer = particles.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        var material = new Material(FlameVfxSprites.AlphaMaterial) { mainTexture = FlameVfxSprites.Ember.texture };
        renderer.material = material;
        renderer.sortingOrder = 6;
    }

    private void ApplyColor(Color color)
    {
        if (particles == null)
        {
            return;
        }

        var main = particles.main;
        main.startColor = new ParticleSystem.MinMaxGradient(color);

        var gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(color, 0f),
                new GradientColorKey(color * 0.55f, 1f),
            },
            new[]
            {
                new GradientAlphaKey(roomFill ? 0.45f : 0.85f, 0f),
                new GradientAlphaKey(0f, 1f),
            });
        var colorOverLifetime = particles.colorOverLifetime;
        colorOverLifetime.color = gradient;
    }
}

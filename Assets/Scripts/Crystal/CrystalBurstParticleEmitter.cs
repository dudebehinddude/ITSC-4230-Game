using UnityEngine;
using UnityEngine.Rendering.Universal;

// One-shot radial burst used when a crystal activates or cutscene tiles swap.
public class CrystalBurstParticleEmitter : MonoBehaviour
{
    [SerializeField] private float lifetime = 1.35f;

    private ParticleSystem particles;
    private Light2D burstLight;
    private float elapsed;

    public void Play(Color primaryColor, Color secondaryColor, int particleCount)
    {
        EnsureParticleSystem(particleCount);
        ApplyColors(primaryColor, secondaryColor);
        ConfigureBurstLight(primaryColor);
        particles.Emit(Mathf.Max(1, particleCount));
        elapsed = 0f;
    }

    private void Update()
    {
        elapsed = Timestep.AdvanceTimer(elapsed, lifetime, Timestep.Delta);
        if (burstLight != null)
        {
            float t = Timestep.NormalizeTimer(elapsed, lifetime);
            float fade = 1f - Ease.OutQuad(t);
            burstLight.intensity = 3.4f * fade;
            burstLight.pointLightOuterRadius = Mathf.Lerp(7.5f, 3.25f, t);
        }

        if (elapsed >= lifetime)
        {
            Destroy(gameObject);
        }
    }

    private void EnsureParticleSystem(int particleCount)
    {
        if (particles != null)
        {
            return;
        }

        particles = gameObject.AddComponent<ParticleSystem>();

        var main = particles.main;
        ParticleVfxDefaults.ApplyTimedSimulation(main);
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = Mathf.Max(180, particleCount + 32);
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.75f, 1.55f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.4f, 4.4f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.2f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.gravityModifier = 0.04f;

        var emission = particles.emission;
        emission.enabled = false;

        var shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.28f;

        var velocityOverLifetime = particles.velocityOverLifetime;
        velocityOverLifetime.enabled = true;
        velocityOverLifetime.space = ParticleSystemSimulationSpace.World;
        velocityOverLifetime.z = new ParticleSystem.MinMaxCurve(0f, 0f);
        velocityOverLifetime.x = new ParticleSystem.MinMaxCurve(0f, 0f);
        velocityOverLifetime.y = new ParticleSystem.MinMaxCurve(0f, 0f);
        velocityOverLifetime.radial = new ParticleSystem.MinMaxCurve(2.2f, 5.6f);

        var colorOverLifetime = particles.colorOverLifetime;
        colorOverLifetime.enabled = true;

        var sizeOverLifetime = particles.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 1.1f),
            new Keyframe(0.35f, 0.85f),
            new Keyframe(1f, 0.05f)));

        var renderer = particles.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        var material = new Material(FlameVfxSprites.AdditiveMaterial);
        material.mainTexture = FlameVfxSprites.Core.texture;
        renderer.material = material;
        renderer.sortingOrder = 7;
    }

    private void ConfigureBurstLight(Color color)
    {
        burstLight = gameObject.AddComponent<Light2D>();
        burstLight.lightType = Light2D.LightType.Point;
        burstLight.falloffIntensity = 0.25f;
        burstLight.shadowsEnabled = false;
        burstLight.color = color;
        burstLight.intensity = 3.4f;
        burstLight.pointLightInnerRadius = 0.4f;
        burstLight.pointLightOuterRadius = 7.5f;
    }

    private void ApplyColors(Color primaryColor, Color secondaryColor)
    {
        var main = particles.main;
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Gradient
            {
                colorKeys = new[]
                {
                    new GradientColorKey(primaryColor, 0f),
                    new GradientColorKey(secondaryColor, 1f),
                },
                alphaKeys = new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0.85f, 1f),
                },
            });

        var gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(primaryColor, 0f),
                new GradientColorKey(secondaryColor, 0.55f),
                new GradientColorKey(primaryColor * 0.35f, 1f),
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0f, 1f),
            });
        var colorOverLifetime = particles.colorOverLifetime;
        colorOverLifetime.color = gradient;
    }
}

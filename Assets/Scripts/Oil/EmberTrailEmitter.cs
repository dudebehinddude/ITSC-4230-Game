using UnityEngine;

// Ember trail driven by a real ParticleSystem emitter; soft lights spawn from live particles.
public class EmberTrailEmitter : MonoBehaviour
{
    [SerializeField] private float emberLifetime = 0.65f;
    [SerializeField] private float emissionRate = 30f;
    [SerializeField] private float lightSampleInterval = 0.14f;
    [SerializeField] private int maxEmberLights = 4;

    private ParticleSystem particles;
    private readonly ParticleSystem.Particle[] particleBuffer = new ParticleSystem.Particle[128];
    private Color emberColor = Color.white;
    private float lightSampleTimer;
    private int activeEmberLights;
    private Vector2 lastLightSpawnPosition = new Vector2(float.MaxValue, float.MaxValue);

    public void Configure(Color color)
    {
        emberColor = color;
        EnsureParticleSystem();
        ApplyColor(color);
    }

    public void Begin()
    {
        EnsureParticleSystem();
        lightSampleTimer = 0f;
        activeEmberLights = 0;
        lastLightSpawnPosition = new Vector2(float.MaxValue, float.MaxValue);
        particles.Clear();
        particles.Play();
    }

    public void Stop()
    {
        if (particles != null)
        {
            particles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    private void Update()
    {
        if (particles == null || !particles.isPlaying)
        {
            return;
        }

        lightSampleTimer -= Timestep.Delta;
        if (lightSampleTimer > 0f || activeEmberLights >= maxEmberLights)
        {
            return;
        }

        int alive = particles.GetParticles(particleBuffer);
        if (alive == 0)
        {
            return;
        }

        for (int attempt = 0; attempt < 4; attempt++)
        {
            int index = Random.Range(0, alive);
            ParticleSystem.Particle particle = particleBuffer[index];
            float age = emberLifetime - particle.remainingLifetime;
            if (age < 0.12f)
            {
                continue;
            }

            Vector2 position = particle.position;
            if ((position - lastLightSpawnPosition).sqrMagnitude < 0.35f * 0.35f)
            {
                continue;
            }

            lightSampleTimer = lightSampleInterval;
            lastLightSpawnPosition = position;
            Vector2 drift = new Vector2(Random.Range(-0.12f, 0.12f), Random.Range(-0.2f, -0.05f));
            SpawnEmberLight(position + Random.insideUnitCircle * 0.08f, drift);
            return;
        }
    }

    private void SpawnEmberLight(Vector2 position, Vector2 drift)
    {
        activeEmberLights++;
        EmberLight.Spawn(position, emberColor, emberLifetime, OnEmberLightFinished, drift);
    }

    private void OnEmberLightFinished()
    {
        activeEmberLights = Mathf.Max(0, activeEmberLights - 1);
    }

    private void EnsureParticleSystem()
    {
        if (particles != null)
        {
            return;
        }

        var emberObject = new GameObject("EmberParticles");
        emberObject.transform.SetParent(transform, false);
        particles = emberObject.AddComponent<ParticleSystem>();

        var main = particles.main;
        ParticleVfxDefaults.ApplyTimedSimulation(main);
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 140;
        main.startLifetime = new ParticleSystem.MinMaxCurve(emberLifetime * 0.85f, emberLifetime * 1.1f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.55f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.11f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.gravityModifier = new ParticleSystem.MinMaxCurve(0.08f, 0.18f);
        main.startColor = new ParticleSystem.MinMaxGradient(emberColor);

        var emission = particles.emission;
        emission.enabled = true;
        emission.rateOverTime = emissionRate;

        var shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.22f;
        shape.radiusThickness = 1f;
        shape.arc = 360f;
        shape.randomDirectionAmount = 0.75f;
        shape.sphericalDirectionAmount = 0.35f;

        ConfigureLinearVelocityOverLifetime(
            particles.velocityOverLifetime,
            -0.35f, 0.35f,
            -0.55f, 0.05f);

        var rotationOverLifetime = particles.rotationOverLifetime;
        rotationOverLifetime.enabled = true;
        rotationOverLifetime.x = new ParticleSystem.MinMaxCurve(0f, 0f);
        rotationOverLifetime.y = new ParticleSystem.MinMaxCurve(0f, 0f);
        rotationOverLifetime.z = new ParticleSystem.MinMaxCurve(-90f * Mathf.Deg2Rad, 90f * Mathf.Deg2Rad);

        var colorOverLifetime = particles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        ApplyColorGradient(colorOverLifetime, emberColor);

        var sizeOverLifetime = particles.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.85f),
            new Keyframe(0.2f, 1f),
            new Keyframe(1f, 0.1f)));

        var noise = particles.noise;
        noise.enabled = true;
        noise.strength = 0.32f;
        noise.frequency = 0.9f;
        noise.scrollSpeed = 0.35f;
        noise.damping = true;

        var renderer = particles.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        var particleMaterial = new Material(FlameVfxSprites.AlphaMaterial);
        particleMaterial.mainTexture = FlameVfxSprites.Ember.texture;
        renderer.material = particleMaterial;
        renderer.sortingOrder = 5;
    }

    private void ApplyColor(Color color)
    {
        if (particles == null)
        {
            return;
        }

        var main = particles.main;
        main.startColor = new ParticleSystem.MinMaxGradient(color);
        ApplyColorGradient(particles.colorOverLifetime, color);
    }

    // Unity requires X/Y/Z velocity axes to share the same MinMaxCurve mode.
    private static void ConfigureLinearVelocityOverLifetime(
        ParticleSystem.VelocityOverLifetimeModule velocityOverLifetime,
        float xMin, float xMax,
        float yMin, float yMax)
    {
        velocityOverLifetime.enabled = true;
        velocityOverLifetime.space = ParticleSystemSimulationSpace.World;
        velocityOverLifetime.z = new ParticleSystem.MinMaxCurve(0f, 0f);
        velocityOverLifetime.x = new ParticleSystem.MinMaxCurve(xMin, xMax);
        velocityOverLifetime.y = new ParticleSystem.MinMaxCurve(yMin, yMax);
    }

    private static void ApplyColorGradient(ParticleSystem.ColorOverLifetimeModule module, Color color)
    {
        var gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(color, 0f),
                new GradientColorKey(color * 0.7f, 1f),
            },
            new[]
            {
                new GradientAlphaKey(0.75f, 0f),
                new GradientAlphaKey(0f, 1f),
            });
        module.color = gradient;
    }
}

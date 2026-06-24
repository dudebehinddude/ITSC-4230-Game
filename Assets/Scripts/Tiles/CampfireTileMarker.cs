using UnityEngine;
using UnityEngine.Rendering.Universal;

// Spawned per painted campfire tile via Tile.m_InstancedGameObject.
public class CampfireTileMarker : MonoBehaviour
{
    [SerializeField] private Color flameColor = new Color(1f, 0.42f, 0.08f, 1f);
    [SerializeField] private Color emberColor = new Color(1f, 0.82f, 0.28f, 1f);
    [SerializeField] private float emissionRate = 72f;
    [SerializeField] private float lightIntensity = 1.35f;
    [SerializeField] private float lightInnerRadius = 0.45f;
    [SerializeField] private float lightOuterRadius = 5.6f;

    private ParticleSystem flames;
    private Light2D light2D;
    private float baseIntensity;
    private Color baseLightColor;
    private float seed;

    private void Awake()
    {
        seed = Mathf.Abs(transform.position.x * 29.37f + transform.position.y * 67.91f) % 100f;
        baseIntensity = lightIntensity;
        baseLightColor = emberColor;

        EnsureFlameParticles();
        EnsureLight();
    }

    private void Update()
    {
        if (light2D == null)
        {
            return;
        }

        light2D.intensity = LanternFlicker.SampleIntensity(seed, baseIntensity);
        light2D.color = LanternFlicker.SampleColor(seed, baseLightColor);
    }

    private void EnsureFlameParticles()
    {
        flames = gameObject.AddComponent<ParticleSystem>();

        var main = flames.main;
        ParticleVfxDefaults.ApplyTimedSimulation(main);
        main.loop = true;
        main.playOnAwake = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 160;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.55f, 1.15f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.28f, 0.9f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.22f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.gravityModifier = -0.08f;

        var emission = flames.emission;
        emission.enabled = true;
        emission.rateOverTime = emissionRate;

        var shape = flames.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.13f;
        shape.radiusThickness = 0.65f;

        var velocityOverLifetime = flames.velocityOverLifetime;
        velocityOverLifetime.enabled = true;
        velocityOverLifetime.space = ParticleSystemSimulationSpace.World;
        velocityOverLifetime.z = new ParticleSystem.MinMaxCurve(0f, 0f);
        velocityOverLifetime.x = new ParticleSystem.MinMaxCurve(-0.12f, 0.12f);
        velocityOverLifetime.y = new ParticleSystem.MinMaxCurve(0.65f, 1.35f);

        var colorOverLifetime = flames.colorOverLifetime;
        colorOverLifetime.enabled = true;
        colorOverLifetime.color = BuildFlameGradient();

        var sizeOverLifetime = flames.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.45f),
            new Keyframe(0.2f, 1f),
            new Keyframe(1f, 0.05f)));

        var renderer = flames.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        var material = new Material(FlameVfxSprites.AdditiveMaterial) { mainTexture = FlameVfxSprites.Core.texture };
        renderer.material = material;
        renderer.sortingOrder = 7;

        flames.Play();
    }

    private Gradient BuildFlameGradient()
    {
        var gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(emberColor, 0f),
                new GradientColorKey(flameColor, 0.45f),
                new GradientColorKey(flameColor * 0.45f, 1f),
            },
            new[]
            {
                new GradientAlphaKey(0.95f, 0f),
                new GradientAlphaKey(0.7f, 0.45f),
                new GradientAlphaKey(0f, 1f),
            });
        return gradient;
    }

    private void EnsureLight()
    {
        light2D = gameObject.AddComponent<Light2D>();
        light2D.lightType = Light2D.LightType.Point;
        light2D.falloffIntensity = 0.55f;
        light2D.shadowsEnabled = false;
        light2D.color = baseLightColor;
        light2D.intensity = baseIntensity;
        light2D.pointLightInnerRadius = lightInnerRadius;
        light2D.pointLightOuterRadius = lightOuterRadius;
    }
}

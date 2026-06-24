using System;
using UnityEngine;
using UnityEngine.Rendering.Universal;

[RequireComponent(typeof(Light2D))]
public class PlayerLantern : MonoBehaviour
{
    [SerializeField] private bool startFilled = true;
    [SerializeField] private FuelKind startingFuel = FuelKind.Orange;

    private const float MinIntensity = 0f;
    // Covers the player at minimum; shrinks a little as fuel drains.
    private const float CoreRadius = 0.85f;
    private const float MinCoreRadius = 0.55f;
    private const float RadiusEasePower = 2.5f;
    private const float VisualSmoothing = 10f;

    private Light2D light2D;
    private FuelType currentFuel;
    private float remaining;
    private float capacity;
    private float flickerSeed;
    private float smoothedIntensity;
    private float smoothedOuterRadius;
    private float smoothedInnerRadius;
    private Color smoothedColor;

    public bool IsLit => remaining > 0f && currentFuel != null;
    public float Normalized => capacity > 0f ? Mathf.Clamp01(remaining / capacity) : 0f;
    public FuelType CurrentFuel => currentFuel;

    /// <summary>Fires with true when the lantern lights up, false when it dies.</summary>
    public event Action<bool> OnLitChanged;

    private bool wasLit;

    private void Awake()
    {
        light2D = GetComponent<Light2D>();
        flickerSeed = UnityEngine.Random.Range(0f, 100f);

        light2D.lightType = Light2D.LightType.Point;
        light2D.falloffIntensity = 0.55f;
        light2D.shadowsEnabled = false;
        light2D.intensity = MinIntensity;
        light2D.pointLightInnerRadius = CoreRadius;
        light2D.pointLightOuterRadius = CoreRadius;

        smoothedIntensity = MinIntensity;
        smoothedOuterRadius = CoreRadius;
        smoothedInnerRadius = CoreRadius;
        smoothedColor = Color.white;

        if (startFilled)
        {
            Refill(startingFuel);
        }

        wasLit = IsLit;
    }

    private void Update()
    {
        if (GameSettings.AssistMode && currentFuel != null)
        {
            remaining = capacity;
        }
        else if (remaining > 0f)
        {
            remaining = Mathf.Max(0f, remaining - Timestep.Delta);
        }

        UpdateVisuals(Timestep.Delta);

        bool lit = IsLit;
        if (lit != wasLit)
        {
            wasLit = lit;
            OnLitChanged?.Invoke(lit);
        }
    }

    public void Refill(FuelKind kind) => Refill(FuelTypes.Get(kind));

    /// <summary>Sets the active fuel and fills the lantern to full for that type.</summary>
    public void Refill(FuelType fuel)
    {
        if (fuel == null)
        {
            return;
        }

        currentFuel = fuel;
        capacity = Mathf.Max(0.0001f, fuel.burnDuration);
        remaining = capacity;
    }

    private void UpdateVisuals(float dt)
    {
        if (currentFuel == null)
        {
            return;
        }

        float intensityT = Normalized;
        float radiusT = 1f - Mathf.Pow(1f - Normalized, RadiusEasePower);

        float targetIntensity = Mathf.Lerp(MinIntensity, currentFuel.maxIntensity, intensityT);
        float targetOuterRadius = Mathf.Lerp(CoreRadius, currentFuel.maxRadius, radiusT);
        float targetInnerRadius = Mathf.Lerp(MinCoreRadius, CoreRadius, intensityT);
        Color targetColor = currentFuel.lightColor;

        float k = Timestep.ExpDecay(VisualSmoothing, dt);
        smoothedIntensity = Mathf.Lerp(smoothedIntensity, targetIntensity, k);
        smoothedOuterRadius = Mathf.Lerp(smoothedOuterRadius, targetOuterRadius, k);
        smoothedInnerRadius = Mathf.Lerp(smoothedInnerRadius, targetInnerRadius, k);
        smoothedColor = Color.Lerp(smoothedColor, targetColor, k);

        light2D.pointLightInnerRadius = smoothedInnerRadius;
        light2D.pointLightOuterRadius = smoothedOuterRadius;
        light2D.intensity = LanternFlicker.SampleIntensity(flickerSeed, smoothedIntensity);
        light2D.color = LanternFlicker.SampleColor(flickerSeed, smoothedColor);
    }
}

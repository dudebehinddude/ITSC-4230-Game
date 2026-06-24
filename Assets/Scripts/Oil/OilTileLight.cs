using UnityEngine;
using UnityEngine.Rendering.Universal;

// Flickering world light spawned by each painted OilTile.
[RequireComponent(typeof(Light2D))]
public class OilTileLight : MonoBehaviour
{
    [SerializeField] private FuelKind fuelKind = FuelKind.Orange;

    private Light2D light2D;
    private float baseIntensity;
    private Color baseColor;
    private float seed;

    private void Awake()
    {
        light2D = GetComponent<Light2D>();
        seed = Mathf.Abs(transform.position.x * 19.193f + transform.position.y * 73.817f) % 100f;

        FuelType fuel = FuelTypes.Get(fuelKind);
        baseIntensity = fuel.worldLightIntensity * Mathf.Lerp(0.96f, 1.04f, Mathf.PerlinNoise(seed, seed * 0.37f));
        baseColor = fuel.lightColor;

        light2D.lightType = Light2D.LightType.Point;
        light2D.falloffIntensity = 0.5f;
        light2D.shadowsEnabled = false;
        light2D.color = fuel.lightColor;
        light2D.intensity = baseIntensity;
        light2D.pointLightInnerRadius = fuel.worldLightInnerRadius;
        light2D.pointLightOuterRadius = fuel.worldLightRadius;
    }

    private void Update()
    {
        if (light2D == null)
        {
            return;
        }

        light2D.intensity = baseIntensity;
        light2D.color = LanternFlicker.SampleColor(seed, baseColor);
    }
}

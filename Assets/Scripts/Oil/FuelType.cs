using System;
using UnityEngine;

public enum FuelKind
{
    Orange,
    Blue,
}

[Serializable]
public class FuelType
{
    public readonly float burnDuration;
    public readonly Color lightColor;
    public readonly float maxIntensity;
    public readonly float maxRadius;
    public readonly float worldLightIntensity;
    public readonly float worldLightInnerRadius;
    public readonly float worldLightRadius;

    public FuelType(
        float burnDuration,
        Color lightColor,
        float maxIntensity,
        float maxRadius,
        float worldLightIntensity,
        float worldLightInnerRadius,
        float worldLightRadius)
    {
        this.burnDuration = burnDuration;
        this.lightColor = lightColor;
        this.maxIntensity = maxIntensity;
        this.maxRadius = maxRadius;
        this.worldLightIntensity = worldLightIntensity;
        this.worldLightInnerRadius = worldLightInnerRadius;
        this.worldLightRadius = worldLightRadius;
    }
}

public static class FuelTypes
{
    // Player lantern is only slightly brighter than painted oil lights.
    public static readonly FuelType Orange = new FuelType(
        burnDuration: 5f,
        lightColor: new Color(1f, 0.82f, 0.45f),
        maxIntensity: 0.58f,
        maxRadius: 5.6f,
        worldLightIntensity: 0.45f,
        worldLightInnerRadius: 0.15f,
        worldLightRadius: 2.5f);

    public static readonly FuelType Blue = new FuelType(
        burnDuration: 10f,
        lightColor: new Color(0.45f, 0.65f, 1f),
        maxIntensity: 0.52f,
        maxRadius: 5.1f,
        worldLightIntensity: 0.4f,
        worldLightInnerRadius: 0.15f,
        worldLightRadius: 2.2f);

    public static FuelType Get(FuelKind kind) =>
        kind == FuelKind.Blue ? Blue : Orange;
}

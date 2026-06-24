using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;

// Runtime glow + idle particles spawned for each painted crystal cell.
public class CrystalTileMarker : MonoBehaviour
{
    [SerializeField] private float inactiveIntensity = 1.75f;
    [SerializeField] private float activatedIntensity = 2.25f;
    [SerializeField] private float inactiveOuterRadius = 5.75f;
    [SerializeField] private float activatedOuterRadius = 6.75f;
    [SerializeField] private float inactiveInnerRadius = 0.55f;
    [SerializeField] private float idleEmissionRate = 18f;
    [SerializeField] private float activatedEmissionRate = 26f;
    [SerializeField] private float roomEmissionRate = 34f;
    [SerializeField] private float activatedRoomEmissionRate = 46f;

    private Light2D light2D;
    private float seed;
    private Color baseColor;
    private float baseIntensity;
    private bool isActivated;

    private void Awake()
    {
        NormalizeSettings();
        seed = Mathf.Abs(transform.position.x * 23.71f + transform.position.y * 61.13f) % 100f;
        ConfigureFromTile();
    }

    private void NormalizeSettings()
    {
        inactiveIntensity = Mathf.Max(inactiveIntensity, 1.75f);
        activatedIntensity = Mathf.Max(activatedIntensity, 2.25f);
        inactiveOuterRadius = Mathf.Max(inactiveOuterRadius, 5.75f);
        activatedOuterRadius = Mathf.Max(activatedOuterRadius, 6.75f);
        inactiveInnerRadius = Mathf.Max(inactiveInnerRadius, 0.55f);
        idleEmissionRate = Mathf.Max(idleEmissionRate, 18f);
        activatedEmissionRate = Mathf.Max(activatedEmissionRate, 26f);
        roomEmissionRate = Mathf.Max(roomEmissionRate, 34f);
        activatedRoomEmissionRate = Mathf.Max(activatedRoomEmissionRate, 46f);
    }

    private void ConfigureFromTile()
    {
        Tilemap tilemap = GetComponentInParent<Tilemap>();
        if (tilemap == null)
        {
            return;
        }

        Vector3Int cell = tilemap.WorldToCell(transform.position);
        TileBase tile = tilemap.GetTile(cell);
        Color glowColor;
        Color particleColor;
        float intensity;
        float outerRadius;
        float emissionRate;

        if (tile is ActivatedCrystalTile activatedTile)
        {
            isActivated = true;
            glowColor = activatedTile.GlowColor;
            particleColor = activatedTile.ParticleColor;
            intensity = activatedIntensity;
            outerRadius = activatedOuterRadius;
            emissionRate = activatedEmissionRate;
        }
        else if (tile is CrystalTile crystalTile)
        {
            isActivated = false;
            glowColor = crystalTile.GlowColor;
            particleColor = crystalTile.ParticleColor;
            intensity = inactiveIntensity;
            outerRadius = inactiveOuterRadius;
            emissionRate = idleEmissionRate;
        }
        else
        {
            return;
        }

        baseColor = glowColor;
        baseIntensity = intensity;
        EnsureLight(outerRadius);
        CrystalParticleVfx.SpawnIdleEmitter(transform, particleColor, emissionRate);
        CrystalParticleVfx.SpawnIdleEmitter(
            transform,
            particleColor,
            isActivated ? activatedRoomEmissionRate : roomEmissionRate,
            roomFill: true);
    }

    private void EnsureLight(float outerRadius)
    {
        light2D = GetComponent<Light2D>() ?? gameObject.AddComponent<Light2D>();
        
        light2D.lightType = Light2D.LightType.Point;
        light2D.falloffIntensity = 0.3f;
        light2D.shadowsEnabled = false;
        light2D.color = baseColor;
        light2D.intensity = baseIntensity;
        light2D.pointLightInnerRadius = inactiveInnerRadius;
        light2D.pointLightOuterRadius = outerRadius;
    }

    private void Update()
    {
        if (light2D == null)
        {
            return;
        }

        float pulse = isActivated ? 0.08f : 0.05f;
        float flicker = 1f + pulse * Mathf.Sin((Time.time + seed) * (isActivated ? 3.2f : 2.1f));
        light2D.intensity = baseIntensity * flicker;
        Color tintedColor = baseColor.linear;
        tintedColor.a = 1f;
        light2D.color = Color.Lerp(baseColor, tintedColor, 0.35f + 0.1f * Mathf.Sin((Time.time + seed * 0.37f) * 1.6f));
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// Senses whether the player is currently in a lit area.
// Lit = the lantern is burning, OR the player is inside the outer radius of any
// other point Light2D in the scene. World lights are picked up automatically; no
// per-light component needed. Hook OnLightStateChanged for the death mechanic.
public class LightSensor : MonoBehaviour
{
    [SerializeField] private PlayerLantern lantern;

    private const float MinIntensityToCount = 0.05f; // ignore lights dimmer than this
    private const float RefreshInterval = 1f;         // re-scan the scene for lights this often

    public bool IsInLight { get; private set; }

    /// <summary>True when inside a world oil lamp / point light, ignoring the player lantern.</summary>
    public bool IsInWorldLight => IsInWorldLightAt(transform.position);

    /// <summary>Fires with the new state whenever the player crosses the light/dark boundary.</summary>
    public event Action<bool> OnLightStateChanged;

    private const float DefaultBuffer = 0.5f;

    private readonly List<Light2D> pointLights = new List<Light2D>();
    private Light2D lanternLight;
    private float bufferRadius = DefaultBuffer;
    private float refreshTimer;
    private bool initialized;
    private bool lastLanternLit;

    private void Awake()
    {
        if (lantern == null)
        {
            lantern = GetComponentInChildren<PlayerLantern>();
        }

        if (lantern != null)
        {
            lanternLight = lantern.GetComponent<Light2D>();
        }

        // Buffer so "any part of the player inside the radius" counts as lit,
        // instead of only the player's exact center point.
        var col = GetComponent<Collider2D>();
        if (col != null)
        {
            bufferRadius = col.bounds.extents.magnitude;
        }

        RefreshLights();
    }

    private void Update()
    {
        refreshTimer -= Timestep.Delta;
        if (refreshTimer <= 0f)
        {
            RefreshLights();
        }

        bool lit = ComputeInLight();
        if (!initialized || lit != IsInLight)
        {
            if (initialized)
            {
                LogTransition(lit);
            }

            initialized = true;
            IsInLight = lit;
            OnLightStateChanged?.Invoke(lit);
        }

        lastLanternLit = lantern != null && lantern.IsLit;
    }

    /// <summary>Re-scans the scene for point lights. Call after spawning lights at runtime.</summary>
    public void RefreshLights()
    {
        refreshTimer = RefreshInterval;
        pointLights.Clear();

        var all = FindObjectsByType<Light2D>();
        foreach (var light in all)
        {
            if (light == null || light == lanternLight)
            {
                continue;
            }

            if (light.lightType != Light2D.LightType.Point)
            {
                continue;
            }

            pointLights.Add(light);
        }
    }

    private void LogTransition(bool lit)
    {
        if (lit)
        {
            Debug.Log("[LightSensor] Back in the light.", this);
            return;
        }

        // We can only fall into darkness while the lantern is out. If it was lit
        // last frame, the lantern just died; otherwise the player left a light.
        bool lanternJustDied = lastLanternLit && (lantern == null || !lantern.IsLit);
        string reason = lanternJustDied
            ? "lantern ran out of fuel"
            : "walked out of the light";
        Debug.Log($"[LightSensor] Entered darkness — {reason}.", this);
    }

    private bool ComputeInLight() => IsLitAt(transform.position);

    /// <summary>
    /// Whether the given world position would be lit. The lantern lights any position
    /// (it travels with the player); otherwise the position must be within an external
    /// point light's radius, expanded by the player buffer.
    /// </summary>
    public bool IsLitAt(Vector2 position)
    {
        if (lantern != null && lantern.IsLit)
        {
            return true;
        }

        for (int i = 0; i < pointLights.Count; i++)
        {
            Light2D light = pointLights[i];
            if (light == null || light.intensity < MinIntensityToCount)
            {
                continue;
            }

            float radius = light.pointLightOuterRadius + bufferRadius;
            if (radius <= 0f)
            {
                continue;
            }

            if (((Vector2)light.transform.position - position).sqrMagnitude <= radius * radius)
            {
                return true;
            }
        }

        return false;
    }

    public bool IsInWorldLightAt(Vector2 position)
    {
        for (int i = 0; i < pointLights.Count; i++)
        {
            Light2D light = pointLights[i];
            if (light == null || light.intensity < MinIntensityToCount)
            {
                continue;
            }

            float radius = light.pointLightOuterRadius + bufferRadius;
            if (radius <= 0f)
            {
                continue;
            }

            if (((Vector2)light.transform.position - position).sqrMagnitude <= radius * radius)
            {
                return true;
            }
        }

        return false;
    }
}

using System;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// Soft ground glow left by a drifting ember particle. Fades in/out — no flicker.
public class EmberLight : MonoBehaviour
{
    [SerializeField] private float lifetime = 0.65f;
    [SerializeField] private float fallSpeed = 0.18f;
    [SerializeField] private float maxIntensity = 0.09f;
    [SerializeField] private float outerRadius = 1.15f;
    [SerializeField] private float innerRadius = 0.04f;
    [SerializeField] private float fadeInDuration = 0.1f;

    private Light2D light2D;
    private SpriteRenderer spriteRenderer;
    private float elapsed;
    private Color baseColor;
    private Action onFinished;
    private Vector2 driftVelocity;

    public static EmberLight Spawn(
        Vector2 position,
        Color color,
        float lifetimeOverride = -1f,
        Action onFinished = null,
        Vector2? drift = null)
    {
        var go = new GameObject("EmberLight");
        go.transform.position = new Vector3(position.x, position.y, 0f);
        var ember = go.AddComponent<EmberLight>();
        ember.Initialize(color, lifetimeOverride, onFinished, drift ?? Vector2.zero);
        return ember;
    }

    private void Initialize(Color color, float lifetimeOverride, Action finishedCallback, Vector2 drift)
    {
        if (lifetimeOverride > 0f)
        {
            lifetime = lifetimeOverride;
        }

        onFinished = finishedCallback;
        baseColor = color;
        driftVelocity = drift;

        spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = FlameVfxSprites.Ember;
        spriteRenderer.color = new Color(color.r, color.g, color.b, 0f);
        spriteRenderer.sortingOrder = 4;

        light2D = gameObject.AddComponent<Light2D>();
        light2D.lightType = Light2D.LightType.Point;
        light2D.falloffIntensity = 0.55f;
        light2D.shadowsEnabled = false;
        light2D.color = color;
        light2D.intensity = 0f;
        light2D.pointLightInnerRadius = innerRadius;
        light2D.pointLightOuterRadius = outerRadius;
    }

    private void Update()
    {
        elapsed = Timestep.AdvanceTimer(elapsed, lifetime, Timestep.Delta);
        float t = Timestep.NormalizeTimer(elapsed, lifetime);
        float fadeIn = fadeInDuration > 0f ? Mathf.Clamp01(elapsed / fadeInDuration) : 1f;
        float fadeOut = 1f - Ease.OutQuad(t);
        float fade = fadeIn * fadeOut;

        transform.position += (Vector3)(driftVelocity * Timestep.Delta) + Vector3.down * (fallSpeed * Timestep.Delta);
        light2D.intensity = maxIntensity * fade;
        light2D.pointLightOuterRadius = outerRadius * (0.65f + 0.35f * fadeOut);

        if (spriteRenderer != null)
        {
            Color spriteColor = baseColor;
            spriteColor.a = fade * 0.85f;
            spriteRenderer.color = spriteColor;
            transform.localScale = Vector3.one * Mathf.Lerp(0.1f, 0.05f, t);
        }

        if (elapsed >= lifetime)
        {
            onFinished?.Invoke();
            Destroy(gameObject);
        }
    }
}

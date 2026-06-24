using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(CircleCollider2D))]
public class FlameProjectile : MonoBehaviour
{
    [Header("Flight")]
    [SerializeField] private float maxLifetime = 3.0625f;
    [SerializeField] private float impactFadeDuration = 0.22f;
    [SerializeField] private float gravityScale = 0.7f;
    [SerializeField] private float colliderRadius = 0.12f;
    [SerializeField] private float postDarknessTravelTiles = 6.5f;

    [Header("Visual")]
    [SerializeField] private float bodyScale = 0.95f;
    [SerializeField] private float bodyPulseAmount = 0.06f;
    [SerializeField] private float bodyMinScaleFactor = 0.32f;

    [Header("Light")]
    [SerializeField] private float maxIntensity = 0.25f;
    [SerializeField] private float outerRadius = 6f;
    [SerializeField] private float innerRadius = 0f;
    [SerializeField] private float falloffIntensity = 0.75f;
    [SerializeField] private float flickerAmount = 0.1f;
    [SerializeField] private float darknessIntensityScale = 0.28f;
    [SerializeField] private float darknessRadiusScale = 0.45f;
    [Tooltip("Fraction of lifetime before light begins to dim.")]
    [SerializeField] private float lightHoldFraction = 0.45f;

    private Rigidbody2D rb;
    private CircleCollider2D col;
    private Light2D light2D;
    private SpriteRenderer bodyRenderer;
    private Transform bodyTransform;
    private EmberTrailEmitter emberTrail;
    private float elapsed;
    private bool landed;
    private bool dying;
    private float baseIntensity;
    private float baseOuterRadius;
    private Color baseColor;
    private float flickerSeed;
    private Collider2D ownerCollider;
    private Vector2 lastDarknessCheckPosition;
    private readonly HashSet<Vector3Int> darknessCellsTouched = new HashSet<Vector3Int>();

    public bool IsAlive => !dying && elapsed < maxLifetime;

    public void Extinguish()
    {
        if (dying)
        {
            return;
        }

        BeginDieOut(impactFadeDuration);
    }

    public void PassThroughDarkness(Tilemap tilemap, Vector3Int cell)
    {
        if (dying || !darknessCellsTouched.Add(cell))
        {
            return;
        }

        if (darknessCellsTouched.Count > 1)
        {
            Extinguish();
            return;
        }

        float tileDistance = GetTileTravelDistance(tilemap);
        float speed = rb != null ? rb.linearVelocity.magnitude : 0f;
        if (speed > 0.01f)
        {
            maxLifetime = elapsed + (postDarknessTravelTiles * tileDistance / speed);
        }

        baseIntensity *= darknessIntensityScale;
        baseOuterRadius *= darknessRadiusScale;
    }

    public void Launch(Vector2 velocity, FuelType fuel, Collider2D ignoreCollider)
    {
        ownerCollider = ignoreCollider;
        baseColor = fuel != null ? fuel.lightColor : Color.white;
        SetupComponents();
        ConfigureVisuals();
        ConfigureLight();

        rb.gravityScale = gravityScale;
        rb.linearVelocity = velocity;
        lastDarknessCheckPosition = transform.position;
        darknessCellsTouched.Clear();

        if (ownerCollider != null)
        {
            Physics2D.IgnoreCollision(col, ownerCollider, true);
        }

        emberTrail.Configure(baseColor);
        emberTrail.Begin();
    }

    private void Awake()
    {
        SetupComponents();
        lastDarknessCheckPosition = transform.position;
    }

    private void SetupComponents()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;

        col = GetComponent<CircleCollider2D>();
        col.radius = colliderRadius;
        col.isTrigger = false;

        if (light2D == null)
        {
            light2D = gameObject.AddComponent<Light2D>();
        }

        if (bodyRenderer == null)
        {
            var bodyObject = new GameObject("FlameBody");
            bodyObject.transform.SetParent(transform, false);
            bodyTransform = bodyObject.transform;
            bodyRenderer = bodyObject.AddComponent<SpriteRenderer>();
        }

        if (emberTrail == null)
        {
            emberTrail = gameObject.AddComponent<EmberTrailEmitter>();
        }
    }

    private void ConfigureVisuals()
    {
        bodyRenderer.sprite = FlameVfxSprites.Core;
        bodyRenderer.sortingOrder = 7;
        var bodyMaterial = new Material(FlameVfxSprites.AdditiveMaterial);
        bodyMaterial.mainTexture = FlameVfxSprites.Core.texture;
        bodyRenderer.material = bodyMaterial;
        bodyRenderer.color = baseColor;
        bodyTransform.localScale = Vector3.one * bodyScale;
    }

    private void ConfigureLight()
    {
        flickerSeed = Random.Range(0f, 100f);
        baseIntensity = maxIntensity;
        baseOuterRadius = outerRadius;

        light2D.lightType = Light2D.LightType.Point;
        light2D.falloffIntensity = falloffIntensity;
        light2D.shadowsEnabled = false;
        light2D.color = baseColor;
        light2D.intensity = baseIntensity;
        light2D.pointLightInnerRadius = innerRadius;
        light2D.pointLightOuterRadius = outerRadius;
    }

    private void Update()
    {
        if (dying)
        {
            return;
        }

        elapsed = Timestep.AdvanceTimer(elapsed, maxLifetime, Timestep.Delta);
        float lifeT = Timestep.NormalizeTimer(elapsed, maxLifetime);
        float bodyFade = 1f - Ease.OutQuad(lifeT);
        float lightFade = EvaluateLightFade(lifeT);

        light2D.intensity = SampleProjectileIntensity(baseIntensity * lightFade);
        light2D.pointLightOuterRadius = baseOuterRadius * (0.9f + 0.1f * lightFade);
        light2D.color = baseColor;

        if (bodyRenderer != null)
        {
            float pulse = 1f + Mathf.Sin(Time.time * 18f + flickerSeed) * bodyPulseAmount;
            float sizeT = Mathf.Lerp(bodyMinScaleFactor, 1f, bodyFade);
            bodyTransform.localScale = Vector3.one * (bodyScale * pulse * sizeT);
            Color bodyColor = baseColor;
            bodyColor.a = bodyFade;
            bodyRenderer.color = bodyColor;
        }

        Vector2 currentPosition = transform.position;
        FlameTileHitResolver.TryExtinguishOnDarkness(lastDarknessCheckPosition, currentPosition, this);
        if (dying)
        {
            return;
        }

        lastDarknessCheckPosition = currentPosition;

        if (elapsed >= maxLifetime)
        {
            BeginDieOut(impactFadeDuration);
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (dying || landed)
        {
            return;
        }

        if (ownerCollider != null && collision.collider == ownerCollider)
        {
            return;
        }

        landed = true;
        rb.linearVelocity = Vector2.zero;
        rb.bodyType = RigidbodyType2D.Kinematic;
        emberTrail.Stop();

        if (collision.collider.TryGetComponent(out IFlameProjectileTarget target))
        {
            target.OnFlameProjectileHit(this, collision);
        }

        Vector2 hitPoint = collision.contactCount > 0
            ? collision.GetContact(0).point
            : (Vector2)transform.position;
        FlameTileHitResolver.TryHitTiles(hitPoint, this);
        BeginDieOut(impactFadeDuration);
    }

    private void BeginDieOut(float duration)
    {
        if (dying)
        {
            return;
        }

        dying = true;
        emberTrail.Stop();
        StartCoroutine(DieOutRoutine(duration));
    }

    private IEnumerator DieOutRoutine(float duration)
    {
        float startIntensity = light2D.intensity;
        float startRadius = light2D.pointLightOuterRadius;
        float timer = 0f;

        while (timer < duration)
        {
            timer = Timestep.AdvanceTimer(timer, duration, Timestep.Delta);
            float t = Timestep.NormalizeTimer(timer, duration);
            float fade = 1f - Ease.OutQuad(t);
            light2D.intensity = startIntensity * fade;
            light2D.pointLightOuterRadius = startRadius * fade;

            if (bodyRenderer != null)
            {
                Color bodyColor = baseColor;
                bodyColor.a = fade;
                bodyRenderer.color = bodyColor;
            }

            yield return null;
        }

        Destroy(gameObject);
    }

    private float SampleProjectileIntensity(float baseValue)
    {
        float slow = Mathf.PerlinNoise(flickerSeed, Time.time * 3.5f);
        float scale = 1f + (slow - 0.5f) * flickerAmount;
        return baseValue * scale;
    }

    // Holds full brightness early, then eases down gently instead of dropping off a cliff.
    private float EvaluateLightFade(float lifeT)
    {
        if (lifeT <= lightHoldFraction)
        {
            return 1f;
        }

        float t = (lifeT - lightHoldFraction) / (1f - lightHoldFraction);
        return 1f - Mathf.SmoothStep(0f, 1f, t) * 0.7f;
    }

    private static float GetTileTravelDistance(Tilemap tilemap)
    {
        if (tilemap == null)
        {
            return 1f;
        }

        Vector3 cellSize = tilemap.cellSize;
        return Mathf.Max(0.01f, Mathf.Max(Mathf.Abs(cellSize.x), Mathf.Abs(cellSize.y)));
    }
}

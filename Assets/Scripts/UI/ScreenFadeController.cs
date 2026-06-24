using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Player-centered darkness mask. This is an inverse point light in screen space:
// the area around the player stays clearer while darkness closes in outside it.
public class ScreenFadeController : MonoBehaviour
{
    public static ScreenFadeController Instance { get; private set; }

    private static readonly int CenterId = Shader.PropertyToID("_Center");
    private static readonly int AspectId = Shader.PropertyToID("_Aspect");
    private static readonly int RadiusId = Shader.PropertyToID("_Radius");
    private static readonly int SoftnessId = Shader.PropertyToID("_Softness");
    private static readonly int DarknessAlphaId = Shader.PropertyToID("_DarknessAlpha");
    private static readonly int CenterAlphaId = Shader.PropertyToID("_CenterAlpha");

    private const float MaxClearRadius = 0.62f;
    private const float MinClearRadius = 0.18f;
    private const float MaxSoftness = 0.55f;
    private const float MinSoftness = 0.42f;
    private const float MaxDarknessAlpha = 0.96f;

    private RawImage overlay;
    private Material darknessMaterial;
    private Transform darknessTarget;
    private Camera targetCamera;
    private Coroutine fadeRoutine;
    private float radius = MaxClearRadius;
    private float softness = MaxSoftness;
    private float darknessAlpha;
    private float centerAlpha;

    public float Alpha => Mathf.Max(darknessAlpha, centerAlpha);
    public float DrainAmount { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null)
        {
            return;
        }

        var go = new GameObject(nameof(ScreenFadeController));
        go.AddComponent<ScreenFadeController>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        BuildOverlay();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void LateUpdate()
    {
        UpdateCenter();
        ApplyMaterialValues();
    }

    public void SetVignetteTarget(Transform target, Camera camera)
    {
        darknessTarget = target;
        targetCamera = camera;
    }

    public void SetAlpha(float alpha)
    {
        float clamped = Mathf.Clamp01(alpha);
        DrainAmount = clamped;
        darknessAlpha = clamped;
        centerAlpha = clamped;
        radius = Mathf.Lerp(MaxClearRadius, MinClearRadius, clamped);
        softness = Mathf.Lerp(MaxSoftness, MinSoftness, clamped);
        ApplyMaterialValues();
    }

    public void ApplyPlayerDrain(float drainAmount)
    {
        float drain = Mathf.Clamp01(drainAmount);
        DrainAmount = drain;

        darknessAlpha = Mathf.Clamp01(Mathf.SmoothStep(0f, 1f, drain) * 1.12f) * MaxDarknessAlpha;
        // The whole screen dims immediately; the player-centered mask only makes
        // the edges darker, instead of leaving the center fully untouched.
        centerAlpha = Mathf.Pow(drain, 1.35f) * MaxDarknessAlpha;
        radius = Mathf.Lerp(MaxClearRadius, MinClearRadius, Ease.InQuad(drain));
        softness = Mathf.Lerp(MaxSoftness, MinSoftness, Ease.InQuad(drain));
        ApplyMaterialValues();
    }

    public IEnumerator FadeToDrainCoroutine(float targetDrain, float duration)
    {
        if (fadeRoutine != null)
        {
            StopCoroutine(fadeRoutine);
        }

        yield return FadeDrainRoutine(targetDrain, duration);
        fadeRoutine = null;
    }

    public IEnumerator FadeToClearCoroutine(float duration)
    {
        if (fadeRoutine != null)
        {
            StopCoroutine(fadeRoutine);
        }

        yield return FadeDrainRoutine(0f, duration);
        fadeRoutine = null;
    }

    public IEnumerator FadeToVignetteCoroutine(float targetAlpha, float duration, bool closing)
    {
        if (fadeRoutine != null)
        {
            StopCoroutine(fadeRoutine);
        }

        yield return FadeFlatRoutine(targetAlpha, duration);
        fadeRoutine = null;
    }

    public void StopFading()
    {
        if (fadeRoutine != null)
        {
            StopCoroutine(fadeRoutine);
            fadeRoutine = null;
        }
    }

    private IEnumerator FadeDrainRoutine(float targetDrain, float duration)
    {
        float startDrain = DrainAmount;
        float elapsed = 0f;

        if (duration <= 0f)
        {
            ApplyPlayerDrain(targetDrain);
            yield break;
        }

        while (elapsed < duration)
        {
            elapsed = Timestep.AdvanceTimer(elapsed, duration, Timestep.Delta);
            float t = Ease.InQuad(Timestep.NormalizeTimer(elapsed, duration));
            ApplyPlayerDrain(Mathf.Lerp(startDrain, targetDrain, t));
            yield return null;
        }

        ApplyPlayerDrain(targetDrain);
    }

    private IEnumerator FadeFlatRoutine(float targetAlpha, float duration)
    {
        float start = Alpha;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed = Timestep.AdvanceTimer(elapsed, duration, Timestep.Delta);
            float t = Timestep.NormalizeTimer(elapsed, duration);
            SetAlpha(Mathf.Lerp(start, targetAlpha, t));
            yield return null;
        }

        SetAlpha(targetAlpha);
    }

    private void UpdateCenter()
    {
        if (darknessMaterial == null)
        {
            return;
        }

        Camera cam = targetCamera != null ? targetCamera : Camera.main;
        Vector2 center = new Vector2(0.5f, 0.5f);
        if (cam != null && darknessTarget != null)
        {
            Vector3 screen = cam.WorldToScreenPoint(darknessTarget.position);
            center = new Vector2(screen.x / Screen.width, screen.y / Screen.height);
        }

        float aspect = Screen.height > 0 ? (float)Screen.width / Screen.height : 1.78f;
        darknessMaterial.SetVector(CenterId, new Vector4(center.x, center.y, 0f, 0f));
        darknessMaterial.SetFloat(AspectId, aspect);
    }

    private void ApplyMaterialValues()
    {
        if (darknessMaterial != null)
        {
            darknessMaterial.SetFloat(RadiusId, radius);
            darknessMaterial.SetFloat(SoftnessId, softness);
            darknessMaterial.SetFloat(DarknessAlphaId, darknessAlpha);
            darknessMaterial.SetFloat(CenterAlphaId, centerAlpha);
            return;
        }

        if (overlay != null)
        {
            overlay.color = new Color(0f, 0f, 0f, Alpha);
        }
    }

    private void BuildOverlay()
    {
        var canvasGo = new GameObject("ScreenFadeCanvas");
        canvasGo.transform.SetParent(transform, false);

        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;

        canvasGo.AddComponent<CanvasScaler>();
        canvasGo.AddComponent<GraphicRaycaster>();

        var overlayGo = new GameObject("PlayerDarknessMask");
        overlayGo.transform.SetParent(canvasGo.transform, false);

        overlay = overlayGo.AddComponent<RawImage>();
        overlay.color = Color.black;
        overlay.raycastTarget = false;

        var rect = overlay.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Shader shader = Shader.Find("UI/PlayerDarknessMask");
        if (shader != null)
        {
            darknessMaterial = new Material(shader);
            overlay.material = darknessMaterial;
        }
        else
        {
            Debug.LogWarning("UI/PlayerDarknessMask shader not found; screen fade will be flat.", this);
        }

        ApplyPlayerDrain(0f);
        UpdateCenter();
        ApplyMaterialValues();
    }
}

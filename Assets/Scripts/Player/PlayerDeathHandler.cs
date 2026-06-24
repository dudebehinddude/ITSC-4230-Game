using System.Collections;
using UnityEngine;

// Screen darkness tracks lantern fuel when away from world lights. Death follows
// shortly after the lantern goes out; re-entering a world light clears the vignette.
[RequireComponent(typeof(Player))]
[RequireComponent(typeof(LightSensor))]
[RequireComponent(typeof(SpriteRenderer))]
public class PlayerDeathHandler : MonoBehaviour
{
    [SerializeField] private PlayerLantern lantern;
    [SerializeField] private readonly FuelKind respawnFuel = FuelKind.Orange;

    private const float LightExitCatchUpDuration = 0.35f;
    private const float EmptyLanternGraceDuration = 1.3f;
    private const float MaxCatchUpSpeed = 2.85f;
    private const float DeathAfterLanternOutDelay = 0.15f;
    private const float UndimDuration = 0.45f;
    private const float DeathToRespawnDelay = 0.2f;

    private Player player;
    private LightSensor lightSensor;
    private SpriteRenderer spriteRenderer;
    private Rigidbody2D rb;
    private bool isDead;
    private bool isUndimming;
    private Coroutine undimRoutine;
    private Coroutine deathDelayRoutine;
    private Coroutine deathRoutine;

    private Vector3 spriteBaseScale;
    private Color spriteBaseColor;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AttachToPlayer()
    {
        Player player = FindAnyObjectByType<Player>();
        if (player != null && player.GetComponent<PlayerDeathHandler>() == null)
        {
            player.gameObject.AddComponent<PlayerDeathHandler>();
        }
    }

    private void Awake()
    {
        player = GetComponent<Player>();
        lightSensor = GetComponent<LightSensor>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        rb = GetComponent<Rigidbody2D>();

        if (lantern == null)
        {
            lantern = GetComponent<PlayerLantern>();
        }

        spriteBaseScale = transform.localScale;
        spriteBaseColor = spriteRenderer.color;
    }

    private void Start()
    {
        ScreenFadeController fade = ScreenFadeController.Instance;
        if (fade == null)
        {
            return;
        }

        Camera cam = Camera.main;
        if (CameraController.Instance != null)
        {
            cam = CameraController.Instance.GetComponent<Camera>();
        }

        fade.SetVignetteTarget(transform, cam);
    }

    private void OnEnable()
    {
        lightSensor.OnLightStateChanged += HandleLightStateChanged;
    }

    private void OnDisable()
    {
        lightSensor.OnLightStateChanged -= HandleLightStateChanged;
    }

    private void Update()
    {
        ScreenFadeController fade = ScreenFadeController.Instance;
        if (isDead || lantern == null || fade == null)
        {
            return;
        }

        if (lightSensor.IsInWorldLight)
        {
            StopDeathDelay();

            if (!isUndimming && fade.Alpha > 0.001f)
            {
                BeginUndim();
            }

            return;
        }

        if (isUndimming)
        {
            CancelUndim();
        }

        if (lantern.IsLit)
        {
            StopDeathDelay();
            float targetDrain = 1f - lantern.Normalized;
            float catchUpSpeed = CalculateCatchUpSpeed(fade.DrainAmount, targetDrain);
            fade.ApplyPlayerDrain(Timestep.MoveTowards(
                fade.DrainAmount,
                targetDrain,
                catchUpSpeed,
                Timestep.Delta));
            return;
        }

        fade.ApplyPlayerDrain(Timestep.MoveTowards(
            fade.DrainAmount,
            1f,
            1f / EmptyLanternGraceDuration,
            Timestep.Delta));

        if (fade.DrainAmount >= 0.999f)
        {
            StartDeathDelay();
        }
    }

    private void HandleLightStateChanged(bool inLight)
    {
        if (isDead)
        {
            return;
        }

        if (inLight)
        {
            StopDeathDelay();
        }
    }

    private float CalculateCatchUpSpeed(float currentDrain, float targetDrain)
    {
        if (targetDrain <= currentDrain)
        {
            return 1f / UndimDuration;
        }

        float catchUpSpeed = (targetDrain - currentDrain) / LightExitCatchUpDuration;
        float normalDrainSpeed = lantern.CurrentFuel != null
            ? 1f / Mathf.Max(0.0001f, lantern.CurrentFuel.burnDuration)
            : 1f;

        return Mathf.Min(Mathf.Max(catchUpSpeed, normalDrainSpeed), MaxCatchUpSpeed);
    }

    private void BeginUndim()
    {
        if (isUndimming || ScreenFadeController.Instance == null)
        {
            return;
        }

        isUndimming = true;

        if (undimRoutine != null)
        {
            StopCoroutine(undimRoutine);
        }

        undimRoutine = StartCoroutine(UndimRoutine());
    }

    private void CancelUndim()
    {
        if (!isUndimming)
        {
            return;
        }

        isUndimming = false;

        if (undimRoutine != null)
        {
            StopCoroutine(undimRoutine);
            undimRoutine = null;
        }

        ScreenFadeController.Instance?.StopFading();
    }

    private IEnumerator UndimRoutine()
    {
        ScreenFadeController fade = ScreenFadeController.Instance;
        yield return fade.FadeToClearCoroutine(UndimDuration);
        isUndimming = false;
        undimRoutine = null;
    }

    private void StartDeathDelay()
    {
        if (deathDelayRoutine != null)
        {
            return;
        }

        deathDelayRoutine = StartCoroutine(DeathAfterLanternOut());
    }

    private void StopDeathDelay()
    {
        if (deathDelayRoutine != null)
        {
            StopCoroutine(deathDelayRoutine);
            deathDelayRoutine = null;
        }
    }

    private IEnumerator DeathAfterLanternOut()
    {
        yield return new WaitForSeconds(DeathAfterLanternOutDelay);

        deathDelayRoutine = null;

        if (isDead || lightSensor.IsInWorldLight || (lantern != null && lantern.IsLit))
        {
            yield break;
        }

        Kill();
    }

    public void Kill()
    {
        if (isDead)
        {
            return;
        }

        isDead = true;
        RoomManager.Instance?.BeginRespawn();
        CancelUndim();
        StopDeathDelay();
        player.enabled = false;
        rb.linearVelocity = Vector2.zero;
        rb.simulated = false;

        if (deathRoutine != null)
        {
            StopCoroutine(deathRoutine);
        }

        deathRoutine = StartCoroutine(DeathSequence());
    }

    private IEnumerator DeathSequence()
    {
        ScreenFadeController fade = ScreenFadeController.Instance;
        fade?.SetAlpha(1f);

        yield return new WaitForSeconds(DeathToRespawnDelay);

        Respawn();

        player.enabled = true;
        rb.simulated = true;

        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();

        RoomManager.Instance?.EndRespawn();

        if (fade != null)
        {
            isUndimming = true;
            yield return fade.FadeToClearCoroutine(UndimDuration);
            isUndimming = false;
        }

        isDead = false;
        deathRoutine = null;
    }

    private void Respawn()
    {
        transform.localScale = spriteBaseScale;
        spriteRenderer.color = spriteBaseColor;
        RoomManager.Instance?.RespawnPlayer();
        lantern?.Refill(respawnFuel);
    }
}

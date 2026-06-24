using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using UnityEngine.Tilemaps;

// Refills the player's lantern near painted OilTiles and throws flame while in range.
[DefaultExecutionOrder(50)]
public class OilTileRefill : MonoBehaviour
{
    [Header("Oil Refill")]
    [FormerlySerializedAs("fuelTilemap")]
    [SerializeField] private Tilemap oilTilemap;

    [FormerlySerializedAs("lantern")]
    [SerializeField] private PlayerLantern playerLantern;
    [SerializeField] private Collider2D playerCollider;
    [SerializeField] private readonly string tilemapObjectName = "Background Tiles";
    [SerializeField] private readonly float refillPadding = 0.5f;

    [Header("Flame Throw")]
    [SerializeField] private readonly float launchSpeed = 17f;
    [SerializeField] private readonly float launchUpwardBias = 3f;
    [SerializeField] private readonly float minAimDistance = 0.35f;
    [SerializeField] private readonly float aimLineLength = 2.5f;
    [SerializeField] private readonly float gamepadAimDeadzone = 0.2f;

    public bool IsInRefillRange { get; private set; }

    private InputAction throwFlameAction;
    private InputAction aimAction;
    private InputAction aimPointAction;
    private Camera aimCamera;
    private LineRenderer aimLine;
    private bool aiming;
    private Vector2 aimDirection = Vector2.right;

    private void Awake()
    {
        if (playerLantern == null)
        {
            playerLantern = GetComponentInChildren<PlayerLantern>();
        }

        if (playerCollider == null)
        {
            playerCollider = GetComponent<Collider2D>();
        }

        if (oilTilemap == null)
        {
            foreach (var tilemap in FindObjectsByType<Tilemap>())
            {
                if (tilemap.gameObject.name == tilemapObjectName)
                {
                    oilTilemap = tilemap;
                    break;
                }
            }
        }

        aimCamera = Camera.main;
        var playerMap = InputSystem.actions?.FindActionMap("Player");
        throwFlameAction = playerMap?.FindAction("ThrowFlame") ?? InputSystem.actions?.FindAction("ThrowFlame");
        aimAction = playerMap?.FindAction("Aim") ?? InputSystem.actions?.FindAction("Aim");
        aimPointAction = playerMap?.FindAction("AimPoint") ?? InputSystem.actions?.FindAction("AimPoint");
        EnsureAimLine();
    }

    private void OnEnable()
    {
        throwFlameAction?.Enable();
        aimAction?.Enable();
        aimPointAction?.Enable();
    }

    private void OnDisable()
    {
        throwFlameAction?.Disable();
        aimAction?.Disable();
        aimPointAction?.Disable();
        SetAimVisual(false);
    }

    private void Update()
    {
        UpdateRefillRange();
        UpdateFlameThrow();
    }

    private void UpdateRefillRange()
    {
        IsInRefillRange = false;

        if (playerLantern == null || oilTilemap == null)
        {
            return;
        }

        Bounds playerBounds = playerCollider != null
            ? playerCollider.bounds
            : new Bounds(transform.position, Vector3.zero);

        Vector3Int minCell = oilTilemap.WorldToCell(new Vector3(
            playerBounds.min.x - refillPadding,
            playerBounds.min.y - refillPadding,
            oilTilemap.transform.position.z));
        Vector3Int maxCell = oilTilemap.WorldToCell(new Vector3(
            playerBounds.max.x + refillPadding,
            playerBounds.max.y + refillPadding,
            oilTilemap.transform.position.z));
        int tileZ = oilTilemap.cellBounds.zMin;

        for (int x = minCell.x; x <= maxCell.x; x++)
        {
            for (int y = minCell.y; y <= maxCell.y; y++)
            {
                Vector3Int cell = new(x, y, tileZ);
                if (oilTilemap.GetTile(cell) is OilTile oilTile && PlayerTouchesRefillArea(cell, playerBounds))
                {
                    IsInRefillRange = true;
                    playerLantern.Refill(oilTile.FuelKind);
                    return;
                }
            }
        }
    }

    private void UpdateFlameThrow()
    {
        if (!CanThrow())
        {
            if (aiming)
            {
                aiming = false;
                SetAimVisual(false);
            }

            return;
        }

        bool pressed = throwFlameAction != null && throwFlameAction.WasPressedThisFrame();
        bool held = throwFlameAction != null && throwFlameAction.IsPressed();
        bool released = throwFlameAction != null && throwFlameAction.WasReleasedThisFrame();

        if (pressed)
        {
            aiming = true;
            aimDirection = ReadAimDirection(GetThrowOrigin());
            SetAimVisual(true);
            UpdateAimVisual();
        }

        if (aiming && held)
        {
            aimDirection = ReadAimDirection(GetThrowOrigin());
            UpdateAimVisual();
        }

        if (aiming && released)
        {
            ThrowFlame();
            aiming = false;
            SetAimVisual(false);
        }
    }

    private bool CanThrow()
    {
        return IsInRefillRange
            && playerLantern != null
            && playerLantern.IsLit;
    }

    private Vector2 GetThrowOrigin()
    {
        return playerCollider != null ? playerCollider.bounds.center : (Vector2)transform.position;
    }

    private Vector2 ReadAimDirection(Vector2 origin)
    {
        Vector2 stick = aimAction != null ? aimAction.ReadValue<Vector2>() : Vector2.zero;
        if (stick.sqrMagnitude >= gamepadAimDeadzone * gamepadAimDeadzone)
        {
            return stick.normalized;
        }

        if (aimCamera == null)
        {
            aimCamera = Camera.main;
        }

        if (aimPointAction == null || aimCamera == null)
        {
            return aimDirection.sqrMagnitude > 0.01f ? aimDirection : Vector2.right;
        }

        Vector3 screen = aimPointAction.ReadValue<Vector2>();
        screen.z = Mathf.Abs(aimCamera.transform.position.z);
        Vector2 world = aimCamera.ScreenToWorldPoint(screen);
        Vector2 dir = world - origin;
        if (dir.sqrMagnitude < minAimDistance * minAimDistance)
        {
            return aimDirection.sqrMagnitude > 0.01f ? aimDirection : Vector2.right;
        }

        return dir.normalized;
    }

    private void ThrowFlame()
    {
        Vector2 origin = GetThrowOrigin();
        Vector2 direction = ReadAimDirection(origin);
        Vector2 velocity = direction * launchSpeed + Vector2.up * launchUpwardBias;

        var projectileObject = new GameObject("FlameProjectile");
        projectileObject.transform.position = origin;
        FlameProjectile projectile = projectileObject.AddComponent<FlameProjectile>();

        FuelType fuel = playerLantern.CurrentFuel ?? FuelTypes.Get(FuelKind.Orange);
        projectile.Launch(velocity, fuel, playerCollider);
    }

    private void EnsureAimLine()
    {
        if (aimLine != null)
        {
            return;
        }

        var lineObject = new GameObject("FlameAimLine");
        lineObject.transform.SetParent(transform, false);
        aimLine = lineObject.AddComponent<LineRenderer>();
        aimLine.useWorldSpace = true;
        aimLine.positionCount = 2;
        aimLine.startWidth = 0.05f;
        aimLine.endWidth = 0.02f;
        aimLine.material = new Material(Shader.Find("Sprites/Default"));
        aimLine.startColor = new Color(1f, 0.75f, 0.35f, 0.85f);
        aimLine.endColor = new Color(1f, 0.55f, 0.2f, 0.15f);
        aimLine.sortingOrder = 20;
        aimLine.enabled = false;
    }

    private void UpdateAimVisual()
    {
        if (aimLine == null)
        {
            return;
        }

        Vector2 origin = GetThrowOrigin();
        aimLine.SetPosition(0, origin);
        aimLine.SetPosition(1, origin + aimDirection * aimLineLength);
    }

    private void SetAimVisual(bool visible)
    {
        if (aimLine != null)
        {
            aimLine.enabled = visible;
        }
    }

    private bool PlayerTouchesRefillArea(Vector3Int cell, Bounds playerBounds)
    {
        Vector3 cellSize = oilTilemap.layoutGrid.cellSize;
        Vector3 tileCenter = oilTilemap.GetCellCenterWorld(cell);
        float halfWidth = cellSize.x * 0.5f + refillPadding;
        float halfHeight = cellSize.y * 0.5f + refillPadding;

        return playerBounds.max.x >= tileCenter.x - halfWidth
            && playerBounds.min.x <= tileCenter.x + halfWidth
            && playerBounds.max.y >= tileCenter.y - halfHeight
            && playerBounds.min.y <= tileCenter.y + halfHeight;
    }
}

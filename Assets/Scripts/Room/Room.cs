using UnityEngine;
using UnityEngine.Tilemaps;

[RequireComponent(typeof(BoxCollider2D))]
public class Room : MonoBehaviour
{
    private BoxCollider2D boundsCollider;
    private RoomSpawn[] spawnPoints;

    [Header("Camera")]
    [Tooltip("Use a fixed orthographic size in this room instead of auto-fitting to bounds.")]
    [SerializeField] private bool useCustomOrthoSize = false;
    [SerializeField, Min(0.1f)] private float customOrthoSize = 10f;

    [Header("Cutscene")]
    [SerializeField] private bool hasCutscene = false;
    [SerializeField] private string cutsceneName = "";

    [Header("Game Stage")]
    [Tooltip("Optional stage to activate the first time the player enters this room.")]
    [SerializeField] private string stageOnEnter = "";
    [Tooltip("Optional stage to activate when this room's crystal is activated.")]
    [SerializeField] private string stageOnCrystalActivation = "";

    public bool HasCutscene => hasCutscene;
    public string CutsceneName => cutsceneName;
    public string StageOnEnter => stageOnEnter;
    public string StageOnCrystalActivation => stageOnCrystalActivation;
    public bool HasStageOnEnter => !string.IsNullOrWhiteSpace(stageOnEnter);
    public bool HasStageOnCrystalActivation => !string.IsNullOrWhiteSpace(stageOnCrystalActivation);

    public bool TryFindCrystal(out Tilemap tilemap, out Vector3Int cell) =>
        CrystalRoomUtility.TryFindCrystalInRoom(this, out tilemap, out cell);

    public Bounds CameraBounds
    {
        get
        {
            EnsureCollider();
            return boundsCollider.bounds;
        }
    }

    private void Awake()
    {
        EnsureCollider();
        boundsCollider.isTrigger = true;
        spawnPoints = GetComponentsInChildren<RoomSpawn>();
    }

    public bool ContainsPoint(Vector2 worldPoint)
    {
        EnsureCollider();
        return boundsCollider.bounds.Contains(worldPoint);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.TryGetComponent<Player>(out Player player) || RoomManager.Instance == null)
        {
            return;
        }

        RoomManager.Instance.OnPlayerEnteredRoom(this, player.transform.position);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.TryGetComponent<Player>(out Player player) || RoomManager.Instance == null)
        {
            return;
        }

        RoomManager.Instance.OnPlayerExitedRoom(this, player.transform.position);
    }

    public float InsideDepth(Vector2 worldPoint)
    {
        Bounds bounds = CameraBounds;
        float depthX = Mathf.Min(worldPoint.x - bounds.min.x, bounds.max.x - worldPoint.x);
        float depthY = Mathf.Min(worldPoint.y - bounds.min.y, bounds.max.y - worldPoint.y);
        return Mathf.Min(depthX, depthY);
    }

    public bool TryGetOrthoSizeOverride(out float orthoSize)
    {
        if (useCustomOrthoSize)
        {
            orthoSize = customOrthoSize;
            return true;
        }

        orthoSize = 0f;
        return false;
    }

    private void EnsureCollider()
    {
        if (boundsCollider == null)
        {
            boundsCollider = GetComponent<BoxCollider2D>();
        }
    }

    public RoomSpawn GetNearestSpawn(Vector2 position)
    {
        EnsureSpawnPoints();

        RoomSpawn nearest = null;
        float nearestSqr = float.MaxValue;

        for (int i = 0; i < spawnPoints.Length; i++)
        {
            RoomSpawn spawn = spawnPoints[i];
            if (spawn == null)
            {
                continue;
            }

            float sqr = ((Vector2)spawn.transform.position - position).sqrMagnitude;
            if (sqr < nearestSqr)
            {
                nearestSqr = sqr;
                nearest = spawn;
            }
        }

        return nearest;
    }

    public RoomSpawn DefaultSpawn
    {
        get
        {
            EnsureSpawnPoints();
            return spawnPoints.Length > 0 ? spawnPoints[0] : null;
        }
    }

    private void EnsureSpawnPoints()
    {
        spawnPoints ??= GetComponentsInChildren<RoomSpawn>();
    }

    private void OnDrawGizmosSelected()
    {
        BoxCollider2D box = GetComponent<BoxCollider2D>();
        if (box == null)
        {
            return;
        }

        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.35f);
        Gizmos.DrawCube(box.bounds.center, box.bounds.size);
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 1f);
        Gizmos.DrawWireCube(box.bounds.center, box.bounds.size);

        if (!hasCutscene)
        {
            return;
        }

        if (TryFindCrystal(out Tilemap tilemap, out Vector3Int cell))
        {
            Gizmos.color = new Color(0.45f, 0.9f, 1f, 1f);
            Gizmos.DrawWireSphere(tilemap.GetCellCenterWorld(cell), 0.25f);
        }
    }
}

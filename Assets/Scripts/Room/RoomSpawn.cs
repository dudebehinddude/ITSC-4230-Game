using UnityEngine;

public class RoomSpawn : MonoBehaviour
{
    private static readonly Vector2 DefaultCheckpointSize = new Vector2(2f, 1.5f);

    [Header("Checkpoint")]
    [Tooltip("When enabled, the BoxCollider2D on this object acts as a respawn checkpoint trigger.")]
    [SerializeField] private bool isCheckpoint = false;

    public Room Room => GetComponentInParent<Room>();
    public Vector2 Position => transform.position;
    public bool IsCheckpoint => isCheckpoint;

    private void Awake()
    {
        if (!isCheckpoint)
        {
            return;
        }

        BoxCollider2D col = GetComponent<BoxCollider2D>();
        if (col != null)
        {
            col.isTrigger = true;
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!isCheckpoint || GetComponent<BoxCollider2D>() != null)
        {
            return;
        }

        BoxCollider2D col = gameObject.AddComponent<BoxCollider2D>();
        col.isTrigger = true;
        col.size = DefaultCheckpointSize;
    }
#endif

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!isCheckpoint || !other.TryGetComponent<Player>(out _))
        {
            return;
        }

        if (RoomManager.Instance == null)
        {
            return;
        }

        RoomManager.Instance.SetActiveSpawn(this);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, 0.25f);

        if (!isCheckpoint)
        {
            return;
        }

        BoxCollider2D col = GetComponent<BoxCollider2D>();
        if (col == null)
        {
            return;
        }

        Gizmos.color = new Color(0.2f, 1f, 0.3f, 0.35f);
        Bounds bounds = col.bounds;
        Gizmos.DrawCube(bounds.center, bounds.size);
        Gizmos.color = new Color(0.2f, 1f, 0.3f, 1f);
        Gizmos.DrawWireCube(bounds.center, bounds.size);
    }
}

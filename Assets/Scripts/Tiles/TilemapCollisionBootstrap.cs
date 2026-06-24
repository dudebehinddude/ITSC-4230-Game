using System.Collections;
using UnityEngine;
using UnityEngine.Tilemaps;

public class TilemapCollisionBootstrap : MonoBehaviour
{
    private const int ColliderRebuildPasses = 20;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureExists()
    {
        var bootstrapObject = new GameObject(nameof(TilemapCollisionBootstrap));
        DontDestroyOnLoad(bootstrapObject);
        bootstrapObject.AddComponent<TilemapCollisionBootstrap>();
    }

    private IEnumerator Start()
    {
        // Let scene Awake/Start calls finish before forcing tile and collider rebuilds.
        yield return null;
        RebuildTilemapCollisions();
    }

    public static void RebuildTilemapCollisions()
    {
        Tilemap[] tilemaps = FindObjectsByType<Tilemap>();
        for (int i = 0; i < tilemaps.Length; i++)
        {
            RebuildCollider(tilemaps[i]);
        }
    }

    private static void RebuildCollider(Tilemap tilemap)
    {
        if (tilemap == null)
        {
            return;
        }

        tilemap.RefreshAllTiles();

        TilemapCollider2D collider = tilemap.GetComponent<TilemapCollider2D>();
        if (collider == null && tilemap.gameObject.name == TilemapUtility.GetObjectName(TilemapLayer.Foreground))
        {
            collider = tilemap.gameObject.AddComponent<TilemapCollider2D>();
        }

        if (collider == null)
        {
            return;
        }

        collider.enabled = false;
        collider.enabled = true;

        // Large tilemaps can exceed the default per-pass change budget in player builds.
        for (int pass = 0; pass < ColliderRebuildPasses; pass++)
        {
            collider.ProcessTilemapChanges();
        }
    }
}

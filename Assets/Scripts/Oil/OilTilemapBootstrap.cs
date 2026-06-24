using UnityEngine;
using UnityEngine.Tilemaps;

// Refreshes the tilemap so painted OilTiles spawn their linked OilTileLight prefabs.
[RequireComponent(typeof(Tilemap))]
public class OilTilemapBootstrap : MonoBehaviour
{
    [SerializeField] private Tilemap tilemap;

    private void Awake()
    {
        if (tilemap == null)
        {
            tilemap = GetComponent<Tilemap>();
        }

        tilemap.RefreshAllTiles();
    }
}

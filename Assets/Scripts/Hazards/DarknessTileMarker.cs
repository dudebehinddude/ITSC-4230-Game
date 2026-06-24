using UnityEngine;
using UnityEngine.Tilemaps;

// Spawned per painted DarknessTile (via m_InstancedGameObject). Registers its cell with the hazard tilemap.
public class DarknessTileMarker : MonoBehaviour
{
    private void Awake()
    {
        Register();
    }

    private void Register()
    {
        Tilemap tilemap = GetComponentInParent<Tilemap>();
        if (tilemap == null)
        {
            return;
        }

        DarknessTilemapHazard hazard = tilemap.GetComponent<DarknessTilemapHazard>();
        if (hazard == null)
        {
            hazard = tilemap.gameObject.AddComponent<DarknessTilemapHazard>();
        }

        hazard.RegisterDarknessCell(transform.position);
    }
}

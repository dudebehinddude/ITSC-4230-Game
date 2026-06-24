using UnityEngine;
using UnityEngine.Tilemaps;

// Painted oil-lamp tile. Links a prefab (OilTileLight) spawned per cell by the Tilemap.
[CreateAssetMenu(fileName = "OilTile", menuName = "Tiles/Oil Tile")]
public class OilTile : Tile
{
    [SerializeField] private FuelKind fuelKind = FuelKind.Orange;

    public FuelKind FuelKind => fuelKind;

    // Flame position on 16x16 sprites (bottom-left pixel is 0,0).
    public static Vector3 FlameOffsetFromTileCenter()
    {
        const float pixelsPerUnit = 16f;
        const float tileSizePixels = 16f;
        const float flamePixelX = 8f;
        const float flamePixelY = 12f;

        float tileCenterPx = tileSizePixels * 0.5f;
        float flameCenterX = flamePixelX + 0.5f;
        float flameCenterY = flamePixelY + 0.5f;

        return new Vector3(
            (flameCenterX - tileCenterPx) / pixelsPerUnit,
            (flameCenterY - tileCenterPx) / pixelsPerUnit,
            0f);
    }
}

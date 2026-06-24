using UnityEngine;
using UnityEngine.Tilemaps;

[CreateAssetMenu(fileName = "DarknessTile", menuName = "Tiles/Darkness Tile")]
public class DarknessTile : Tile, IFlameReactiveTile
{
    public void OnFlameHit(Tilemap tilemap, Vector3Int cell, FlameProjectile projectile)
    {
        projectile?.PassThroughDarkness(tilemap, cell);
    }
}

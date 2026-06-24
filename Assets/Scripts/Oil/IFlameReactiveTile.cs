using UnityEngine;
using UnityEngine.Tilemaps;

// Implement on Tile assets that should react when flame lands on their cell.
public interface IFlameReactiveTile
{
    void OnFlameHit(Tilemap tilemap, Vector3Int cell, FlameProjectile projectile);
}

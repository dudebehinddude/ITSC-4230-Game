using UnityEngine;
using UnityEngine.Tilemaps;

// Resolves tile-based flame reactions at a world position.
public static class FlameTileHitResolver
{
    private const int HitSearchRadius = 2;
    private const float DefaultProjectileTouchRadius = 0.12f;

    public static void TryHitTiles(Vector2 worldPosition, FlameProjectile projectile, string tilemapObjectName = "Background Tiles")
    {
        Vector2 projectilePosition = projectile != null
            ? (Vector2)projectile.transform.position
            : worldPosition;

        foreach (var tilemap in UnityEngine.Object.FindObjectsByType<Tilemap>())
        {
            if (!ShouldSearchTilemap(tilemap, tilemapObjectName))
            {
                continue;
            }

            if (TryHitTilemap(tilemap, worldPosition, projectile)
                || TryHitTilemap(tilemap, projectilePosition, projectile))
            {
                return;
            }
        }
    }

    public static bool TryExtinguishOnDarkness(Vector2 worldPosition, FlameProjectile projectile, string tilemapObjectName = "Background Tiles")
    {
        return TryExtinguishOnDarkness(worldPosition, worldPosition, projectile, tilemapObjectName);
    }

    public static bool TryExtinguishOnDarkness(Vector2 startPosition, Vector2 endPosition, FlameProjectile projectile, string tilemapObjectName = "Background Tiles")
    {
        if (projectile == null || !projectile.IsAlive)
        {
            return false;
        }

        float radius = GetProjectileTouchRadius(projectile);
        Bounds travelBounds = GetTravelBounds(startPosition, endPosition, radius);

        foreach (var tilemap in UnityEngine.Object.FindObjectsByType<Tilemap>())
        {
            if (!ShouldSearchTilemap(tilemap, tilemapObjectName))
            {
                continue;
            }

            Vector3Int minCell = tilemap.WorldToCell(travelBounds.min);
            Vector3Int maxCell = tilemap.WorldToCell(travelBounds.max);
            int minX = Mathf.Min(minCell.x, maxCell.x);
            int maxX = Mathf.Max(minCell.x, maxCell.x);
            int minY = Mathf.Min(minCell.y, maxCell.y);
            int maxY = Mathf.Max(minCell.y, maxCell.y);

            for (int x = minX; x <= maxX; x++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    Vector3Int cell = new Vector3Int(x, y, minCell.z);
                    if (tilemap.GetTile(cell) is DarknessTile darkness
                        && SweptCircleIntersectsBounds(startPosition, endPosition, radius, GetCellWorldBounds(tilemap, cell)))
                    {
                        darkness.OnFlameHit(tilemap, cell, projectile);
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private static bool TryHitTilemap(Tilemap tilemap, Vector2 worldPosition, FlameProjectile projectile)
    {
        Vector3Int centerCell = tilemap.WorldToCell(worldPosition);
        for (int radius = 0; radius <= HitSearchRadius; radius++)
        {
            for (int x = -radius; x <= radius; x++)
            {
                for (int y = -radius; y <= radius; y++)
                {
                    if (Mathf.Max(Mathf.Abs(x), Mathf.Abs(y)) != radius)
                    {
                        continue;
                    }

                    Vector3Int cell = centerCell + new Vector3Int(x, y, 0);
                    TileBase tile = tilemap.GetTile(cell);
                    if (TryActivateTile(tilemap, cell, tile, projectile))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private static float GetProjectileTouchRadius(FlameProjectile projectile)
    {
        if (projectile.TryGetComponent(out CircleCollider2D circle))
        {
            return Mathf.Max(circle.bounds.extents.x, circle.bounds.extents.y);
        }

        return DefaultProjectileTouchRadius;
    }

    private static Bounds GetTravelBounds(Vector2 startPosition, Vector2 endPosition, float radius)
    {
        Vector2 min = Vector2.Min(startPosition, endPosition) - Vector2.one * radius;
        Vector2 max = Vector2.Max(startPosition, endPosition) + Vector2.one * radius;
        Bounds bounds = new Bounds();
        bounds.SetMinMax(min, max);
        return bounds;
    }

    private static Bounds GetCellWorldBounds(Tilemap tilemap, Vector3Int cell)
    {
        Vector3 cellMin = tilemap.CellToWorld(cell);
        Vector3 cellMax = tilemap.CellToWorld(new Vector3Int(cell.x + 1, cell.y + 1, cell.z));
        Vector3 min = Vector3.Min(cellMin, cellMax);
        Vector3 max = Vector3.Max(cellMin, cellMax);
        Bounds bounds = new Bounds();
        bounds.SetMinMax(min, max);
        return bounds;
    }

    private static bool SweptCircleIntersectsBounds(Vector2 startPosition, Vector2 endPosition, float radius, Bounds bounds)
    {
        Bounds expandedBounds = bounds;
        expandedBounds.Expand(radius * 2f);
        return SegmentIntersectsBounds(startPosition, endPosition, expandedBounds);
    }

    private static bool SegmentIntersectsBounds(Vector2 startPosition, Vector2 endPosition, Bounds bounds)
    {
        Vector2 direction = endPosition - startPosition;
        float minT = 0f;
        float maxT = 1f;

        return SlabIntersects(startPosition.x, direction.x, bounds.min.x, bounds.max.x, ref minT, ref maxT)
            && SlabIntersects(startPosition.y, direction.y, bounds.min.y, bounds.max.y, ref minT, ref maxT);
    }

    private static bool SlabIntersects(float start, float direction, float min, float max, ref float minT, ref float maxT)
    {
        if (Mathf.Approximately(direction, 0f))
        {
            return start >= min && start <= max;
        }

        float invDirection = 1f / direction;
        float t1 = (min - start) * invDirection;
        float t2 = (max - start) * invDirection;
        if (t1 > t2)
        {
            float temp = t1;
            t1 = t2;
            t2 = temp;
        }

        minT = Mathf.Max(minT, t1);
        maxT = Mathf.Min(maxT, t2);
        return minT <= maxT;
    }

    private static bool TryActivateTile(Tilemap tilemap, Vector3Int cell, TileBase tile, FlameProjectile projectile)
    {
        if (tile == null || IsActivatedCrystal(tile))
        {
            return false;
        }

        if (tile is IFlameReactiveTile reactive)
        {
            reactive.OnFlameHit(tilemap, cell, projectile);
            return true;
        }

        if (IsCrystal(tile))
        {
            CrystalActivationHandler.RequestActivation(tilemap, cell, tile);
            return true;
        }

        return false;
    }

    private static bool ShouldSearchTilemap(Tilemap tilemap, string preferredName)
    {
        if (tilemap == null)
        {
            return false;
        }

        string objectName = tilemap.gameObject.name;
        return string.IsNullOrEmpty(preferredName)
            || objectName == preferredName
            || objectName == TilemapUtility.GetObjectName(TilemapLayer.Background)
            || objectName == TilemapUtility.GetObjectName(TilemapLayer.Foreground);
    }

    private static bool IsCrystal(TileBase tile)
    {
        string tileName = tile.name.ToLowerInvariant();
        return tileName.Contains("crystal") && !tileName.Contains("activated");
    }

    private static bool IsActivatedCrystal(TileBase tile)
    {
        string tileName = tile.name.ToLowerInvariant();
        return tileName.Contains("activated") && tileName.Contains("crystal");
    }
}

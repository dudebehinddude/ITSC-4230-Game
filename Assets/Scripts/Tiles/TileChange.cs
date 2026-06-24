using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

[Serializable]
public struct TileChange
{
    public TilemapLayer layer;
    public Vector3Int cell;
    public Vector3Int cellEnd;

    [Tooltip("Used by code definitions. Matches the tile asset name.")]
    public string replacementTileName;

    [Tooltip("Optional direct reference for inspector use.")]
    public TileBase replacementTile;

    [Tooltip("Optional hex color (#RRGGBB). Leave empty to skip particles.")]
    public string particleColorHex;

    [Min(0)]
    [Tooltip("Particle count per replaced cell. 0 = no burst.")]
    public int particleIntensity;

    [Tooltip("When true, clears the tile instead of placing a replacement.")]
    public bool clearTile;

    public static TileChange At(
        TilemapLayer layer,
        int x,
        int y,
        string tileName,
        string hexColor = null,
        int particles = 0)
    {
        var cell = new Vector3Int(x, y, 0);
        return new TileChange
        {
            layer = layer,
            cell = cell,
            cellEnd = cell,
            replacementTileName = tileName,
            particleColorHex = hexColor,
            particleIntensity = particles,
        };
    }

    public static TileChange Fill(
        TilemapLayer layer,
        int x1,
        int y1,
        int x2,
        int y2,
        string tileName,
        string hexColor = null,
        int particles = 0)
    {
        return new TileChange
        {
            layer = layer,
            cell = new Vector3Int(x1, y1, 0),
            cellEnd = new Vector3Int(x2, y2, 0),
            replacementTileName = tileName,
            particleColorHex = hexColor,
            particleIntensity = particles,
        };
    }

    public static TileChange ClearAt(TilemapLayer layer, int x, int y)
    {
        var cell = new Vector3Int(x, y, 0);
        return new TileChange
        {
            layer = layer,
            cell = cell,
            cellEnd = cell,
            clearTile = true,
        };
    }

    public TileBase ResolveReplacementTile()
    {
        return replacementTile != null ? replacementTile : TileLibrary.Get(replacementTileName);
    }
}

public static class TileChangeUtility
{
    public static void ApplyChanges(IReadOnlyList<TileChange> changes)
    {
        if (changes == null)
        {
            return;
        }

        for (int i = 0; i < changes.Count; i++)
        {
            ApplyChange(changes[i]);
        }
    }

    public static void ApplyChange(TileChange change)
    {
        Tilemap worldTilemap = TilemapUtility.Resolve(change.layer);
        if (worldTilemap == null)
        {
            return;
        }

        if (change.clearTile)
        {
            ApplyClear(change, worldTilemap);
            return;
        }

        TileBase tile = change.ResolveReplacementTile();
        if (tile == null)
        {
            return;
        }

        Color? particleColor = null;
        if (CrystalParticleVfx.TryParseHexColor(change.particleColorHex, out Color parsed))
        {
            particleColor = parsed;
        }

        int minX = Mathf.Min(change.cell.x, change.cellEnd.x);
        int maxX = Mathf.Max(change.cell.x, change.cellEnd.x);
        int minY = Mathf.Min(change.cell.y, change.cellEnd.y);
        int maxY = Mathf.Max(change.cell.y, change.cellEnd.y);
        int z = change.cell.z;

        DarknessTilemapHazard hazard = worldTilemap.GetComponent<DarknessTilemapHazard>();

        for (int x = minX; x <= maxX; x++)
        {
            for (int y = minY; y <= maxY; y++)
            {
                var targetCell = new Vector3Int(x, y, z);
                if (worldTilemap.GetTile(targetCell) is DarknessTile)
                {
                    hazard?.UnregisterDarknessCell(targetCell);
                }

                worldTilemap.SetTile(targetCell, tile);
                worldTilemap.RefreshTile(targetCell);

                if (change.particleIntensity > 0 && particleColor.HasValue)
                {
                    Vector3 center = worldTilemap.GetCellCenterWorld(targetCell);
                    CrystalParticleVfx.SpawnBurst(center, particleColor.Value, change.particleIntensity);
                }
            }
        }
    }

    private static void ApplyClear(TileChange change, Tilemap worldTilemap)
    {
        int minX = Mathf.Min(change.cell.x, change.cellEnd.x);
        int maxX = Mathf.Max(change.cell.x, change.cellEnd.x);
        int minY = Mathf.Min(change.cell.y, change.cellEnd.y);
        int maxY = Mathf.Max(change.cell.y, change.cellEnd.y);
        int z = change.cell.z;

        DarknessTilemapHazard hazard = worldTilemap.GetComponent<DarknessTilemapHazard>();

        for (int x = minX; x <= maxX; x++)
        {
            for (int y = minY; y <= maxY; y++)
            {
                var targetCell = new Vector3Int(x, y, z);
                if (worldTilemap.GetTile(targetCell) is DarknessTile)
                {
                    hazard?.UnregisterDarknessCell(targetCell);
                }

                worldTilemap.SetTile(targetCell, null);
                worldTilemap.RefreshTile(targetCell);
            }
        }
    }
}

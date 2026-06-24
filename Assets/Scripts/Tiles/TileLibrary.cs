using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.Tilemaps;

public static class TileLibrary
{
    private static Dictionary<string, TileBase> cache;

    public static TileBase Get(string tileName)
    {
        if (string.IsNullOrWhiteSpace(tileName))
        {
            return null;
        }

        cache ??= BuildCache();
        if (cache.TryGetValue(tileName, out TileBase tile))
        {
            return tile;
        }

#if UNITY_EDITOR
        tile = AssetDatabase.LoadAssetAtPath<TileBase>($"Assets/Tiles/Generated/{tileName}.asset");
        if (tile != null)
        {
            cache[tileName] = tile;
        }
#endif
        return tile;
    }

    private static Dictionary<string, TileBase> BuildCache()
    {
        var dict = new Dictionary<string, TileBase>();
        TileBase[] tiles = Resources.FindObjectsOfTypeAll<TileBase>();
        for (int i = 0; i < tiles.Length; i++)
        {
            TileBase tile = tiles[i];
            if (tile == null || dict.ContainsKey(tile.name))
            {
                continue;
            }

            dict[tile.name] = tile;
        }

        return dict;
    }
}

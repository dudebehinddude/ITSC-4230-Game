using UnityEngine;
using UnityEngine.Tilemaps;

public enum TilemapLayer
{
    Background,
    Foreground,
}

public static class TilemapUtility
{
    public static string GetObjectName(TilemapLayer layer) =>
        layer switch
        {
            TilemapLayer.Background => "Background Tiles",
            TilemapLayer.Foreground => "Foreground Tiles",
            _ => "Background Tiles",
        };

    public static Tilemap Resolve(TilemapLayer layer)
    {
        string objectName = GetObjectName(layer);
        foreach (Tilemap candidate in UnityEngine.Object.FindObjectsByType<Tilemap>())
        {
            if (candidate.gameObject.name == objectName)
            {
                return candidate;
            }
        }

        return null;
    }
}

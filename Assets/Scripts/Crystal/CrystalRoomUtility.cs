using UnityEngine;
using UnityEngine.Tilemaps;

public static class CrystalRoomUtility
{
    public static Room FindRoomContaining(Vector2 worldPoint)
    {
        Room[] rooms = UnityEngine.Object.FindObjectsByType<Room>();
        Room best = null;
        float bestDepth = float.MinValue;

        for (int i = 0; i < rooms.Length; i++)
        {
            Room room = rooms[i];
            if (room == null || !room.ContainsPoint(worldPoint))
            {
                continue;
            }

            float depth = room.InsideDepth(worldPoint);
            if (depth > bestDepth)
            {
                bestDepth = depth;
                best = room;
            }
        }

        return best;
    }

    public static bool TryFindCrystalInRoom(
        Room room,
        out Tilemap tilemap,
        out Vector3Int cell)
    {
        tilemap = null;
        cell = default;

        if (room == null)
        {
            return false;
        }

        Tilemap[] tilemaps = UnityEngine.Object.FindObjectsByType<Tilemap>();
        for (int t = 0; t < tilemaps.Length; t++)
        {
            Tilemap candidate = tilemaps[t];
            BoundsInt bounds = candidate.cellBounds;
            foreach (Vector3Int position in bounds.allPositionsWithin)
            {
                if (candidate.GetTile(position) is not CrystalTile)
                {
                    continue;
                }

                Vector3 center = candidate.GetCellCenterWorld(position);
                if (room.ContainsPoint(center))
                {
                    tilemap = candidate;
                    cell = position;
                    return true;
                }
            }
        }

        return false;
    }
}

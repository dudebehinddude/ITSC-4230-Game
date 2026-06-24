using UnityEngine;

// Shared particle/light burst helpers for crystal and cutscene tile changes.
public static class CrystalParticleVfx
{
    public static bool TryParseHexColor(string hex, out Color color)
    {
        color = Color.white;
        if (string.IsNullOrWhiteSpace(hex))
        {
            return false;
        }

        string value = hex.Trim();
        if (value.StartsWith("#"))
        {
            value = value[1..];
        }

        if (value.Length != 6 && value.Length != 8)
        {
            return false;
        }

        if (!uint.TryParse(value, System.Globalization.NumberStyles.HexNumber, null, out uint raw))
        {
            return false;
        }

        if (value.Length == 6)
        {
            color = new Color(
                ((raw >> 16) & 0xFF) / 255f,
                ((raw >> 8) & 0xFF) / 255f,
                (raw & 0xFF) / 255f,
                1f);
        }
        else
        {
            color = new Color(
                ((raw >> 24) & 0xFF) / 255f,
                ((raw >> 16) & 0xFF) / 255f,
                ((raw >> 8) & 0xFF) / 255f,
                (raw & 0xFF) / 255f);
        }

        return true;
    }

    public static void SpawnIdleEmitter(Transform parent, Color color, float rate = 14f, bool roomFill = false)
    {
        var emitterObject = new GameObject(roomFill ? "CrystalRoomParticles" : "CrystalIdleParticles");
        emitterObject.transform.SetParent(parent, false);
        var emitter = emitterObject.AddComponent<CrystalIdleParticleEmitter>();
        emitter.Configure(color, rate, roomFill);
    }

    public static void SpawnBurst(Vector2 worldPosition, Color primaryColor, Color secondaryColor, int particleCount)
    {
        if (particleCount <= 0)
        {
            return;
        }

        var burstObject = new GameObject("CrystalBurst");
        burstObject.transform.position = new Vector3(worldPosition.x, worldPosition.y, 0f);
        var burst = burstObject.AddComponent<CrystalBurstParticleEmitter>();
        burst.Play(primaryColor, secondaryColor, particleCount);
    }

    public static void SpawnBurst(Vector2 worldPosition, Color color, int particleCount)
    {
        SpawnBurst(worldPosition, color, color * 0.65f, particleCount);
    }
}

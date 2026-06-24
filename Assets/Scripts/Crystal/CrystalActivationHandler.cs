using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

// Handles crystal activation flow: burst, tile swap, then optional room stage/cutscene.
public class CrystalActivationHandler : MonoBehaviour
{
    private static CrystalActivationHandler instance;
    private static readonly Color DefaultInactiveParticleColor = new(0.859f, 0.219f, 0.733f);
    private static readonly Color DefaultActivatedParticleColor = new(1f, 0.369f, 0.078f);
    private const int DefaultActivationBurstCount = 120;

    private readonly HashSet<(Tilemap tilemap, Vector3Int cell)> activatedCells = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureExists()
    {
        if (instance != null)
        {
            return;
        }

        instance = FindAnyObjectByType<CrystalActivationHandler>();
        if (instance != null)
        {
            return;
        }

        var handlerObject = new GameObject(nameof(CrystalActivationHandler));
        instance = handlerObject.AddComponent<CrystalActivationHandler>();
    }

    public static void RequestActivation(Tilemap tilemap, Vector3Int cell, CrystalTile crystalTile)
    {
        EnsureExists();
        instance.StartCoroutine(instance.ActivateRoutine(tilemap, cell, crystalTile));
    }

    public static void RequestActivation(Tilemap tilemap, Vector3Int cell, TileBase sourceTile)
    {
        EnsureExists();
        instance.StartCoroutine(instance.ActivateRoutine(tilemap, cell, sourceTile as CrystalTile));
    }

    private IEnumerator ActivateRoutine(Tilemap tilemap, Vector3Int cell, CrystalTile crystalTile)
    {
        if (tilemap == null)
        {
            yield break;
        }

        var key = (tilemap, cell);
        if (activatedCells.Contains(key))
        {
            yield break;
        }

        activatedCells.Add(key);

        Vector3 worldCenter = tilemap.GetCellCenterWorld(cell);
        Color inactiveColor = crystalTile != null ? crystalTile.ParticleColor : DefaultInactiveParticleColor;
        Color activeColor = crystalTile?.ActivatedTile != null
            ? crystalTile.ActivatedTile.ParticleColor
            : DefaultActivatedParticleColor;
        int burstCount = crystalTile != null ? crystalTile.ActivationBurstCount : DefaultActivationBurstCount;

        CrystalParticleVfx.SpawnBurst(
            worldCenter,
            inactiveColor,
            activeColor,
            burstCount);

        TileBase activatedTile = crystalTile != null
            ? crystalTile.ActivatedTileBase
            : TileLibrary.Get("activated_crystal");
        if (activatedTile != null)
        {
            tilemap.SetTile(cell, activatedTile);
            tilemap.RefreshTile(cell);
        }
        else
        {
            Debug.LogWarning("Crystal was hit, but Assets/Tiles/Generated/activated_crystal.asset could not be resolved.");
        }

        Room room = CrystalRoomUtility.FindRoomContaining(worldCenter);
        if (room == null)
        {
            yield break;
        }

        if (room.HasStageOnCrystalActivation)
        {
            GameStageManager.RequestStage(room.StageOnCrystalActivation);
            yield break;
        }

        if (!room.HasCutscene)
        {
            yield break;
        }

        if (!CutsceneDefinitions.TryGet(room.CutsceneName, out CutsceneDefinition definition))
        {
            Debug.LogWarning(
                $"Room '{room.name}' references unknown cutscene '{room.CutsceneName}'.",
                room);
            yield break;
        }

        yield return new WaitForSeconds(definition.PostExplosionDelay);
        CutsceneDefinitions.Play(room.CutsceneName);
    }
}

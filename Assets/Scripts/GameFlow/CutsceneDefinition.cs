using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public struct CutsceneStep
{
    public CameraShot shot;
    public bool skipCameraShot;
    public Vector2 playerForce;
    public float playerControlLockDuration;
    public List<TileChange> tileChanges;
    public float tileChangeDelay;

    public static CutsceneStep Pan(
        Vector2 cameraPos,
        float cameraSize,
        float panSeconds,
        float holdSeconds,
        Vector2 playerForce = default,
        float playerControlLockDuration = 0f,
        List<TileChange> tileChanges = null,
        float tileChangeDelay = 0f)
    {
        return new CutsceneStep
        {
            shot = new CameraShot
            {
                position = cameraPos,
                orthographicSize = cameraSize,
                moveDuration = panSeconds,
                holdDuration = holdSeconds,
            },
            skipCameraShot = false,
            playerForce = playerForce,
            playerControlLockDuration = playerControlLockDuration,
            tileChanges = tileChanges ?? new List<TileChange>(),
            tileChangeDelay = tileChangeDelay,
        };
    }

    public static CutsceneStep Action(
        Vector2 playerForce = default,
        float playerControlLockDuration = 0f,
        List<TileChange> tileChanges = null,
        float tileChangeDelay = 0f,
        float holdSeconds = 0f)
    {
        return new CutsceneStep
        {
            shot = new CameraShot
            {
                holdDuration = holdSeconds,
            },
            skipCameraShot = true,
            playerForce = playerForce,
            playerControlLockDuration = playerControlLockDuration,
            tileChanges = tileChanges ?? new List<TileChange>(),
            tileChangeDelay = tileChangeDelay,
        };
    }
}

public sealed class CutsceneDefinition
{
    public float PostExplosionDelay { get; set; } = 1.1f;
    public CutsceneStep[] Steps { get; set; } = Array.Empty<CutsceneStep>();
}

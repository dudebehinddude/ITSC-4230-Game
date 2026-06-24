using System;
using UnityEngine;

[Serializable]
public struct CameraShot
{
    public Vector2 position;
    [Min(0.01f)] public float orthographicSize;
    [Min(0f)] public float moveDuration;
    [Min(0f)] public float holdDuration;
}

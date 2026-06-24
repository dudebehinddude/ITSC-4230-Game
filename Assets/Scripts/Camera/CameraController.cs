using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Camera))]
public class CameraController : MonoBehaviour
{
    public static CameraController Instance { get; private set; }

    [Header("Follow")]
    [SerializeField] private Transform followTarget;
    [SerializeField] private readonly float preferredOrthoSize = 10f;
    [Tooltip("How quickly the camera catches up to the player. Used when lag is disabled.")]
    [SerializeField] private readonly float followSmoothSpeed = 10f;
    [Tooltip("Subtle trail when the camera pans. 0 = no extra lag (plain lerp).")]
    [SerializeField] private readonly float followLagSmoothTime = 0.08f;
    [SerializeField] private readonly float orthoSizeSmoothSpeed = 8f;

    [Header("Room Transition")]
    [SerializeField] private readonly float roomTransitionDuration = 0.12f;

    [Header("Cutscene Return")]
    [SerializeField] private readonly float returnToPlayerDuration = 0.5f;

    private Camera cam;
    private Bounds? roomBounds;
    private float? roomOrthoSizeOverride;
    private Transform secondaryFollowTarget;
    private CameraMode mode = CameraMode.FollowPlayer;
    private Coroutine activeRoutine;
    private Vector3 followVelocity;

    public bool IsPlayingCutscene => mode != CameraMode.FollowPlayer;
    public float PreferredOrthoSize => preferredOrthoSize;

    public event Action OnCutsceneComplete;

    private enum CameraMode
    {
        FollowPlayer,
        Cutscene,
        ReturningToPlayer
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Multiple CameraController instances found. Using the first one.", this);
            return;
        }

        Instance = this;
        cam = GetComponent<Camera>();
        cam.orthographicSize = preferredOrthoSize;

        if (followTarget == null)
        {
            Player player = FindAnyObjectByType<Player>();
            if (player != null)
            {
                followTarget = player.transform;
            }
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void LateUpdate()
    {
        if (mode != CameraMode.FollowPlayer || followTarget == null || activeRoutine != null)
        {
            return;
        }

        UpdateFollow(Timestep.Delta);
    }

    public void SetFollowTarget(Transform target)
    {
        followTarget = target;
    }

    public void SetRoomBounds(Bounds bounds, bool instant = false, float? orthoSizeOverride = null)
    {
        roomBounds = bounds;
        roomOrthoSizeOverride = orthoSizeOverride;

        if (instant)
        {
            SnapToFollow();
            return;
        }

        RestartRoutine(TransitionToRoom());
    }

    public void ClearRoomBounds()
    {
        roomBounds = null;
        roomOrthoSizeOverride = null;
    }

    public void SetSecondaryFollowTarget(Transform target)
    {
        secondaryFollowTarget = target;
    }

    public void ClearSecondaryFollowTarget(Transform target)
    {
        if (secondaryFollowTarget == target)
        {
            secondaryFollowTarget = null;
        }
    }

    public void PlayCutscene(IReadOnlyList<CameraShot> shots, Action onComplete = null)
    {
        if (shots == null || shots.Count == 0)
        {
            onComplete?.Invoke();
            return;
        }

        RestartRoutine(RunCutscene(shots, onComplete));
    }

    public void PlayCutscene(CutsceneSequence sequence, Action onComplete = null)
    {
        if (sequence == null)
        {
            onComplete?.Invoke();
            return;
        }

        PlayCutscene(sequence.Shots, onComplete);
    }

    public void BeginCutscene()
    {
        if (activeRoutine != null)
        {
            StopCoroutine(activeRoutine);
            activeRoutine = null;
        }

        mode = CameraMode.Cutscene;
        followVelocity = Vector3.zero;
    }

    public IEnumerator AnimateToShot(CameraShot shot)
    {
        yield return LerpCamera(
            new Vector3(shot.position.x, shot.position.y, transform.position.z),
            shot.orthographicSize,
            shot.moveDuration);
    }

    public IEnumerator ReturnToPlayerFromCutscene()
    {
        mode = CameraMode.ReturningToPlayer;
        yield return ReturnToFollowTarget(returnToPlayerDuration);
        mode = CameraMode.FollowPlayer;
        OnCutsceneComplete?.Invoke();
    }

    private void RestartRoutine(IEnumerator routine)
    {
        if (activeRoutine != null)
        {
            StopCoroutine(activeRoutine);
            activeRoutine = null;
        }

        followVelocity = Vector3.zero;
        activeRoutine = StartCoroutine(WrapRoutine(routine));
    }

    private IEnumerator WrapRoutine(IEnumerator routine)
    {
        yield return routine;
        activeRoutine = null;
    }

    private void SnapToFollow()
    {
        if (followTarget == null)
        {
            return;
        }

        followVelocity = Vector3.zero;
        cam.orthographicSize = GetFollowOrthoSize();
        Vector2 target = GetClampedFollowPosition(GetFollowCenter(), cam.orthographicSize);
        transform.position = new(target.x, target.y, transform.position.z);
    }

    private IEnumerator TransitionToRoom()
    {
        if (followTarget == null)
        {
            yield break;
        }

        float startSize = cam.orthographicSize;
        Vector3 startPos = transform.position;
        float elapsed = 0f;

        while (elapsed < roomTransitionDuration)
        {
            elapsed = Timestep.AdvanceTimer(elapsed, roomTransitionDuration, Timestep.Delta);
            float t = Mathf.SmoothStep(0f, 1f, Timestep.NormalizeTimer(elapsed, roomTransitionDuration));

            cam.orthographicSize = Mathf.Lerp(startSize, GetFollowOrthoSize(), t);
            Vector2 target = GetClampedFollowPosition(GetFollowCenter(), cam.orthographicSize);
            Vector3 endPos = new(target.x, target.y, transform.position.z);
            transform.position = Vector3.Lerp(startPos, endPos, t);
            yield return null;
        }

        SnapToFollow();
    }

    private IEnumerator RunCutscene(IReadOnlyList<CameraShot> shots, Action onComplete)
    {
        mode = CameraMode.Cutscene;
        followVelocity = Vector3.zero;

        for (int i = 0; i < shots.Count; i++)
        {
            CameraShot shot = shots[i];
            yield return LerpCamera(
                new Vector3(shot.position.x, shot.position.y, transform.position.z),
                shot.orthographicSize,
                shot.moveDuration);

            if (shot.holdDuration > 0f)
            {
                yield return new WaitForSeconds(shot.holdDuration);
            }
        }

        mode = CameraMode.ReturningToPlayer;
        yield return ReturnToFollowTarget(returnToPlayerDuration);

        mode = CameraMode.FollowPlayer;
        onComplete?.Invoke();
        OnCutsceneComplete?.Invoke();
    }

    private IEnumerator ReturnToFollowTarget(float duration)
    {
        if (followTarget == null)
        {
            yield break;
        }

        Vector3 startPos = transform.position;
        float startSize = cam.orthographicSize;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed = Timestep.AdvanceTimer(elapsed, duration, Timestep.Delta);
            float t = Mathf.SmoothStep(0f, 1f, Timestep.NormalizeTimer(elapsed, duration));

            cam.orthographicSize = Mathf.Lerp(startSize, GetFollowOrthoSize(), t);

            Vector2 target = GetClampedFollowPosition(GetFollowCenter(), cam.orthographicSize);
            Vector3 targetPos = new(target.x, target.y, transform.position.z);
            transform.position = Vector3.Lerp(startPos, targetPos, t);
            yield return null;
        }

        SnapToFollow();
    }

    private IEnumerator LerpCamera(Vector3 targetPosition, float targetOrthoSize, float duration)
    {
        if (duration <= 0f)
        {
            transform.position = targetPosition;
            cam.orthographicSize = targetOrthoSize;
            yield break;
        }

        Vector3 startPos = transform.position;
        float startSize = cam.orthographicSize;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed = Timestep.AdvanceTimer(elapsed, duration, Timestep.Delta);
            float t = Mathf.SmoothStep(0f, 1f, Timestep.NormalizeTimer(elapsed, duration));
            transform.position = Vector3.Lerp(startPos, targetPosition, t);
            cam.orthographicSize = Mathf.Lerp(startSize, targetOrthoSize, t);
            yield return null;
        }

        transform.position = targetPosition;
        cam.orthographicSize = targetOrthoSize;
    }

    private void UpdateFollow(float deltaTime)
    {
        float targetOrtho = GetFollowOrthoSize();
        cam.orthographicSize = Timestep.ExpLerp(
            cam.orthographicSize,
            targetOrtho,
            orthoSizeSmoothSpeed,
            deltaTime);

        Vector2 target = GetClampedFollowPosition(GetFollowCenter(), cam.orthographicSize);
        Vector3 targetPos = new(target.x, target.y, transform.position.z);

        if (followLagSmoothTime > 0f)
        {
            transform.position = Vector3.SmoothDamp(
                transform.position,
                targetPos,
                ref followVelocity,
                followLagSmoothTime,
                Mathf.Infinity,
                deltaTime);
        }
        else
        {
            followVelocity = Vector3.zero;
            transform.position = Timestep.ExpLerp(
                transform.position,
                targetPos,
                followSmoothSpeed,
                deltaTime);
        }
    }

    private Vector2 GetFollowCenter()
    {
        if (followTarget == null)
        {
            return transform.position;
        }

        Vector2 playerPosition = followTarget.position;
        if (secondaryFollowTarget == null)
        {
            return playerPosition;
        }

        Vector2 secondaryPosition = secondaryFollowTarget.position;
        float roomMaxOrtho = roomBounds.HasValue
            ? CalculateTargetOrthoSize(roomBounds.Value)
            : preferredOrthoSize;
        float neededOrtho = CalculateOrthoForPoints(playerPosition, secondaryPosition, cam.aspect, 1.25f);
        float flamePriority = roomBounds.HasValue
            ? Mathf.InverseLerp(preferredOrthoSize, roomMaxOrtho, neededOrtho)
            : Mathf.Clamp01((neededOrtho - preferredOrthoSize) / Mathf.Max(0.01f, preferredOrthoSize));

        Vector2 midpoint = (playerPosition + secondaryPosition) * 0.5f;
        return Vector2.Lerp(playerPosition, midpoint, flamePriority);
    }

    private Vector2 GetClampedFollowPosition(Vector2 focusPosition, float orthoSize)
    {
        return ClampCameraPosition(focusPosition, orthoSize);
    }

    private float GetFollowOrthoSize()
    {
        float baseOrtho = GetBaseFollowOrthoSize();
        if (secondaryFollowTarget == null || followTarget == null)
        {
            return baseOrtho;
        }

        float neededOrtho = CalculateOrthoForPoints(
            followTarget.position,
            secondaryFollowTarget.position,
            cam.aspect,
            1.25f);

        if (!roomBounds.HasValue)
        {
            return Mathf.Max(baseOrtho, neededOrtho);
        }

        float roomMaxOrtho = CalculateTargetOrthoSize(roomBounds.Value);
        return Mathf.Clamp(Mathf.Max(baseOrtho, neededOrtho), baseOrtho, roomMaxOrtho);
    }

    private float GetBaseFollowOrthoSize()
    {
        if (roomOrthoSizeOverride.HasValue)
        {
            return roomOrthoSizeOverride.Value;
        }

        if (!roomBounds.HasValue)
        {
            return preferredOrthoSize;
        }

        return CalculateTargetOrthoSize(roomBounds.Value);
    }

    private static float CalculateOrthoForPoints(Vector2 a, Vector2 b, float aspect, float padding)
    {
        Vector2 delta = b - a;
        float halfHeight = Mathf.Abs(delta.y) * 0.5f + padding;
        float halfWidth = Mathf.Abs(delta.x) * 0.5f + padding;
        return Mathf.Max(halfHeight, halfWidth / aspect);
    }

    private Vector2 ClampCameraPosition(Vector2 cameraPosition, float orthoSize)
    {
        if (!roomBounds.HasValue)
        {
            return cameraPosition;
        }

        Bounds bounds = roomBounds.Value;
        float halfH = orthoSize;
        float halfW = halfH * cam.aspect;

        float minX = bounds.min.x + halfW;
        float maxX = bounds.max.x - halfW;
        float x = minX > maxX ? bounds.center.x : Mathf.Clamp(cameraPosition.x, minX, maxX);

        float minY = bounds.min.y + halfH;
        float maxY = bounds.max.y - halfH;
        float y = minY > maxY ? bounds.center.y : Mathf.Clamp(cameraPosition.y, minY, maxY);

        return new Vector2(x, y);
    }

    private static float CalculateTargetOrthoSize(Bounds bounds, float preferredSize, float aspect)
    {
        float sizeFromHeight = bounds.extents.y;
        float sizeFromWidth = bounds.extents.x / aspect;
        float sizeToFitRoom = Mathf.Max(sizeFromHeight, sizeFromWidth);
        return Mathf.Min(preferredSize, sizeToFitRoom);
    }

    private float CalculateTargetOrthoSize(Bounds bounds)
    {
        return CalculateTargetOrthoSize(bounds, preferredOrthoSize, cam.aspect);
    }
}

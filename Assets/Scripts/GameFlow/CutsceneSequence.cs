using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Inspector-driven cutscene. Each step pans the camera, applies tile changes when it arrives, then holds.
/// </summary>
public class CutsceneSequence : MonoBehaviour
{
    [SerializeField] private List<CutsceneStep> steps = new List<CutsceneStep>();

    public IReadOnlyList<CutsceneStep> Steps => steps;

    public IReadOnlyList<CameraShot> Shots
    {
        get
        {
            var shots = new CameraShot[steps.Count];
            for (int i = 0; i < steps.Count; i++)
            {
                shots[i] = steps[i].shot;
            }

            return shots;
        }
    }

    public void Play(Action onComplete = null)
    {
        if (CameraController.Instance == null)
        {
            Debug.LogWarning("No CameraController in scene; cutscene skipped.", this);
            onComplete?.Invoke();
            return;
        }

        StartCoroutine(PlayRoutine(onComplete));
    }

    private IEnumerator PlayRoutine(Action onComplete)
    {
        if (steps == null || steps.Count == 0)
        {
            onComplete?.Invoke();
            yield break;
        }

        yield return CutsceneStepPlayer.Play(
            steps,
            index => OnStepArrived(steps[index]),
            onComplete);
    }

    private static IEnumerator OnStepArrived(CutsceneStep step)
    {
        ApplyPlayerForce(step);

        if (step.tileChangeDelay > 0f)
        {
            yield return new WaitForSeconds(step.tileChangeDelay);
        }

        if (step.tileChanges == null || step.tileChanges.Count == 0)
        {
            yield break;
        }

        TileChangeUtility.ApplyChanges(step.tileChanges);
    }

    private static void ApplyPlayerForce(CutsceneStep step)
    {
        if (step.playerForce == Vector2.zero)
        {
            return;
        }

        Player player = FindAnyObjectByType<Player>();
        if (player == null)
        {
            Debug.LogWarning("Cutscene step tried to apply player force, but no Player was found.");
            return;
        }

        player.ApplyCutsceneForce(step.playerForce, step.playerControlLockDuration);
    }
}

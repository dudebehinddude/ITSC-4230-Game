using System;
using System.Collections;
using UnityEngine;

public static class CutscenePlayer
{
    private static CutsceneRunner runner;

    public static void Play(CutsceneDefinition definition, Action onComplete = null)
    {
        EnsureRunner();
        runner.StartCoroutine(PlayRoutine(definition, onComplete));
    }

    private static void EnsureRunner()
    {
        if (runner != null)
        {
            return;
        }

        runner = UnityEngine.Object.FindAnyObjectByType<CutsceneRunner>();
        if (runner != null)
        {
            return;
        }

        var runnerObject = new GameObject(nameof(CutsceneRunner));
        runner = runnerObject.AddComponent<CutsceneRunner>();
    }

    private static IEnumerator PlayRoutine(CutsceneDefinition definition, Action onComplete)
    {
        CutsceneStep[] steps = definition.Steps;
        if (steps == null || steps.Length == 0)
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

        Player player = UnityEngine.Object.FindAnyObjectByType<Player>();
        if (player == null)
        {
            Debug.LogWarning("Cutscene step tried to apply player force, but no Player was found.");
            return;
        }

        player.ApplyCutsceneForce(step.playerForce, step.playerControlLockDuration);
    }
}

internal sealed class CutsceneRunner : MonoBehaviour
{
}

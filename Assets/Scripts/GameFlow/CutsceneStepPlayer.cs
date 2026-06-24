using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class CutsceneStepPlayer
{
    public static IEnumerator Play(
        IReadOnlyList<CutsceneStep> steps,
        Func<int, IEnumerator> onStepArrived,
        Action onComplete)
    {
        CameraController camera = CameraController.Instance;
        if (camera == null)
        {
            Debug.LogWarning("No CameraController in scene; cutscene skipped.");
            onComplete?.Invoke();
            yield break;
        }

        if (steps == null || steps.Count == 0)
        {
            onComplete?.Invoke();
            yield break;
        }

        camera.BeginCutscene();

        for (int i = 0; i < steps.Count; i++)
        {
            CutsceneStep step = steps[i];
            if (!step.skipCameraShot)
            {
                yield return camera.AnimateToShot(step.shot);
            }

            if (onStepArrived != null)
            {
                yield return onStepArrived(i);
            }

            if (step.shot.holdDuration > 0f)
            {
                yield return new WaitForSeconds(step.shot.holdDuration);
            }
        }

        yield return camera.ReturnToPlayerFromCutscene();
        onComplete?.Invoke();
    }
}

using System;
using UnityEngine;

public static class StageEventManager
{
    public static Action<float, float> OnCameraShakeRequested;
    public static Action<int> OnForceLaneChangeRequested;

    public static void TriggerCameraShake(float duration, float intensity)
        => OnCameraShakeRequested?.Invoke(duration, intensity);

    public static void TriggerForceLaneChange(int direction)
        => OnForceLaneChangeRequested?.Invoke(direction);
}

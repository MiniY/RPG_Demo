using System;
using UnityEngine;

/// <summary>Forwards pooled disable without owning damage, rewards, animation, or pooling.</summary>
[DisallowMultipleComponent]
public sealed class GenerationScopedDestructibleLifetime : MonoBehaviour
{
    private Action disabled;
    private bool armed;

    public void Arm(Action onDisabled)
    {
        disabled = onDisabled ?? throw new ArgumentNullException(nameof(onDisabled));
        armed = true;
    }

    public void Disarm()
    {
        armed = false;
        disabled = null;
    }

    private void OnDisable()
    {
        if (!armed)
            return;

        Action callback = disabled;
        Disarm();
        callback?.Invoke();
    }
}

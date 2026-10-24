using System;
using UnityEngine;

/// <summary>Releases a generation registry entry if a pooled actor is disabled externally.</summary>
[DisallowMultipleComponent]
public sealed class GenerationScopedPopulationLifetime : MonoBehaviour
{
    private Action disabled;

    public void Arm(Action callback) =>
        disabled = callback ?? throw new ArgumentNullException(nameof(callback));

    public void Disarm() => disabled = null;

    private void OnDisable()
    {
        Action callback = disabled;
        disabled = null;
        callback?.Invoke();
    }
}

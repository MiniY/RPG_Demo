using UnityEngine;

/// <summary>One late callback on the explicit barrier; leaves Unity automatic simulation unchanged.</summary>
[DefaultExecutionOrder(900)]
[DisallowMultipleComponent]
public sealed class EcologyMotionCommitter : MonoBehaviour
{
    private EcologyDamageBarrier barrier;

    internal void Bind(EcologyDamageBarrier owner) { barrier = owner; }

    private void FixedUpdate()
    {
        if (barrier != null && barrier.isActiveAndEnabled)
            barrier.CommitMotionBodies();
    }
}

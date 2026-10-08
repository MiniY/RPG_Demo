using UnityEngine;

/// <summary>Resolves the scene-authored Player reference and its camera target as one canonical binding.</summary>
[DisallowMultipleComponent]
public sealed class CanonicalPlayerProvider : MonoBehaviour
{
    public bool TryResolve(Transform configuredPlayer, out CanonicalPlayerBinding binding, out string reason)
    {
        binding = null;
        reason = string.Empty;
        if (configuredPlayer == null)
        {
            reason = "Canonical Player reference is missing.";
            return false;
        }

        GameObject[] taggedPlayers = GameObject.FindGameObjectsWithTag("Player");
        if (taggedPlayers.Length != 1 || taggedPlayers[0].transform != configuredPlayer)
        {
            reason = $"Canonical Player reference is ambiguous; expected one tagged Player, found {taggedPlayers.Length}.";
            return false;
        }

        Rigidbody2D body = configuredPlayer.GetComponent<Rigidbody2D>();
        if (body == null)
        {
            reason = "Canonical Player requires a Rigidbody2D for physics-safe map relocation.";
            return false;
        }

        Transform cameraTarget = configuredPlayer.Find("CameraTarget");
        if (cameraTarget == null || !cameraTarget.IsChildOf(configuredPlayer))
        {
            reason = "Canonical Player CameraTarget is missing or not owned by the Player.";
            return false;
        }

        binding = new CanonicalPlayerBinding(configuredPlayer, body, cameraTarget);
        return true;
    }
}

public interface IMapTransitionResettable
{
    void ResetMapTransitionState();
}

public sealed class CanonicalPlayerBinding
{
    internal CanonicalPlayerBinding(Transform player, Rigidbody2D body, Transform cameraTarget)
    {
        Player = player;
        Body = body;
        CameraTarget = cameraTarget;
    }

    public Transform Player { get; }
    public Rigidbody2D Body { get; }
    public Transform CameraTarget { get; }
}

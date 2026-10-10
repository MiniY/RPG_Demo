using Cinemachine;
using UnityEngine;

public enum MapCameraRole
{
    PlayerFollow = 0
}

/// <summary>Provides the one explicitly authored gameplay Player-follow camera role.</summary>
[DisallowMultipleComponent]
public sealed class PlayerFollowCameraProvider : MonoBehaviour
{
    [SerializeField] private CinemachineVirtualCamera playerFollowCamera;
    [SerializeField] private CinemachineConfiner2D playerFollowConfiner;

    public MapCameraRole Role => MapCameraRole.PlayerFollow;
    public CinemachineVirtualCamera PlayerFollowCamera => playerFollowCamera;
    public CinemachineConfiner2D PlayerFollowConfiner => playerFollowConfiner;

    public bool TryResolve(out PlayerFollowCameraBinding binding, out string reason)
    {
        binding = null;
        reason = string.Empty;
        if (playerFollowCamera == null)
        {
            reason = "The Player-follow CinemachineVirtualCamera role is missing.";
            return false;
        }
        if (playerFollowConfiner == null)
        {
            reason = "The Player-follow CinemachineConfiner2D is missing.";
            return false;
        }
        if (!playerFollowCamera.isActiveAndEnabled)
        {
            reason = "The Player-follow camera role is not active and enabled.";
            return false;
        }
        if (playerFollowConfiner.gameObject != playerFollowCamera.gameObject)
        {
            reason = "The Player-follow Confiner must belong to the configured Player-follow camera.";
            return false;
        }
        if (!(playerFollowConfiner.m_BoundingShape2D is PolygonCollider2D boundsCollider))
        {
            reason = "The Player-follow Confiner requires an explicit PolygonCollider2D bounds target.";
            return false;
        }

        binding = new PlayerFollowCameraBinding(
            playerFollowCamera, playerFollowConfiner, boundsCollider);
        return true;
    }
}

public sealed class PlayerFollowCameraBinding
{
    internal PlayerFollowCameraBinding(
        CinemachineVirtualCamera virtualCamera,
        CinemachineConfiner2D confiner,
        PolygonCollider2D boundsCollider)
    {
        VirtualCamera = virtualCamera;
        Confiner = confiner;
        BoundsCollider = boundsCollider;
    }

    public CinemachineVirtualCamera VirtualCamera { get; }
    public CinemachineConfiner2D Confiner { get; }
    public PolygonCollider2D BoundsCollider { get; }
}

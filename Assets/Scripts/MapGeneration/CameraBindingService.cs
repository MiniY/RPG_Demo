using System;
using Cinemachine;
using UnityEngine;

/// <summary>Owns Current Generation Player-follow binding, bounds, and tracking refresh.</summary>
[DisallowMultipleComponent]
public sealed class CameraBindingService : MonoBehaviour
{
    public event Action<Guid> CameraReady;

    public Guid? ReadyGenerationId { get; private set; }
    public Transform ReadyFollowTarget { get; private set; }
    public Bounds AppliedWorldBounds { get; private set; }
    public int BindingCount { get; private set; }
    public int TrackingRefreshCount { get; private set; }

    public bool TryBind(
        MapRuntimeContext context,
        Guid generationId,
        MapCoordinateBoundary coordinates,
        CanonicalPlayerProvider playerProvider,
        Transform configuredPlayer,
        PlayerSpawnService playerSpawnService,
        MapDependentReinitializationService reinitializationService,
        PlayerFollowCameraProvider cameraProvider,
        out string reason)
    {
        reason = string.Empty;
        if (context == null)
            return Fail(null, generationId, MapFailureCategory.Configuration,
                "MissingRuntimeContext", "Camera binding requires the runtime context.", out reason);
        if (context.Mode != MapRuntimeMode.RandomGenerated)
            return Fail(context, generationId, MapFailureCategory.Configuration,
                "WrongRuntimeMode", "Camera binding only runs in RandomGenerated mode.", out reason);
        if (context.ActiveGenerationId != generationId || context.ActiveMap == null)
            return Fail(context, generationId, MapFailureCategory.StaleResult,
                "StaleCameraBinding", "Camera binding rejected a stale Generation ID.", out reason);
        if (context.Phase != MapLifecyclePhase.Reinitializing)
            return Fail(context, generationId, MapFailureCategory.Lifecycle,
                "IllegalCameraBindingPhase",
                $"Camera binding requires Reinitializing; current phase is {context.Phase}.", out reason);

        MapLifecycleTransitions.Advance(context, MapLifecyclePhase.BindingCamera);
        if (coordinates == null || playerProvider == null || playerSpawnService == null ||
            reinitializationService == null || cameraProvider == null)
        {
            return Fail(context, generationId, MapFailureCategory.Configuration,
                "MissingCameraBindingDependency",
                "Camera binding requires coordinates, Player readiness, reinitialization, and an explicit camera provider.", out reason);
        }
        if (playerSpawnService.ReadyGenerationId != generationId)
            return Fail(context, generationId, MapFailureCategory.StaleResult,
                "StalePlayerReady", "Camera binding requires PlayerReady for the Current Generation.", out reason);
        if (reinitializationService.ReadyGenerationId != generationId)
            return Fail(context, generationId, MapFailureCategory.StaleResult,
                "StaleReinitialization", "Camera binding requires reinitialization for the Current Generation.", out reason);
        if (!playerProvider.TryResolve(configuredPlayer, out CanonicalPlayerBinding player, out string playerReason))
            return Fail(context, generationId, MapFailureCategory.Configuration,
                "CanonicalPlayerUnavailable", playerReason, out reason);
        if (!cameraProvider.TryResolve(out PlayerFollowCameraBinding camera, out string cameraReason))
            return Fail(context, generationId, MapFailureCategory.Configuration,
                "PlayerFollowCameraUnavailable", cameraReason, out reason);
        if (!coordinates.TryCellBoundsToWorld(context.ActiveMap, out Bounds worldBounds) ||
            !IsValidBounds(worldBounds))
        {
            return Fail(context, generationId, MapFailureCategory.Coordinate,
                "InvalidCameraWorldBounds",
                "Current MapData Bounds could not be converted into valid Camera World Bounds.", out reason);
        }
        if (!IsFinite(playerSpawnService.LastTeleportDelta))
            return Fail(context, generationId, MapFailureCategory.Generation,
                "InvalidCameraTrackingDelta", "Player teleport tracking data is invalid.", out reason);

        try
        {
            camera.VirtualCamera.Follow = player.CameraTarget;
            ApplyWorldBounds(camera.BoundsCollider, worldBounds);
            camera.Confiner.InvalidateCache();
            camera.VirtualCamera.OnTargetObjectWarped(
                player.CameraTarget, playerSpawnService.LastTeleportDelta);
            camera.VirtualCamera.PreviousStateIsValid = false;
            Physics2D.SyncTransforms();
        }
        catch (Exception exception)
        {
            return Fail(context, generationId, MapFailureCategory.Generation,
                "CameraTrackingRefreshFailed", exception.Message, out reason);
        }

        if (camera.VirtualCamera.Follow != player.CameraTarget)
            return Fail(context, generationId, MapFailureCategory.Generation,
                "IncorrectCameraFollow", "The final Camera Follow is not the Canonical Player CameraTarget.", out reason);
        if (!BoundsMatch(camera.BoundsCollider.bounds, worldBounds))
            return Fail(context, generationId, MapFailureCategory.Generation,
                "CameraBoundsApplicationFailed", "The Camera bounds collider does not match Current MapData World Bounds.", out reason);

        ReadyGenerationId = generationId;
        ReadyFollowTarget = player.CameraTarget;
        AppliedWorldBounds = worldBounds;
        BindingCount++;
        TrackingRefreshCount++;
        CameraReady?.Invoke(generationId);
        return true;
    }

    private static void ApplyWorldBounds(PolygonCollider2D collider, Bounds worldBounds)
    {
        Transform colliderTransform = collider.transform;
        Vector2[] points =
        {
            colliderTransform.InverseTransformPoint(new Vector3(worldBounds.min.x, worldBounds.min.y, 0f)),
            colliderTransform.InverseTransformPoint(new Vector3(worldBounds.min.x, worldBounds.max.y, 0f)),
            colliderTransform.InverseTransformPoint(new Vector3(worldBounds.max.x, worldBounds.max.y, 0f)),
            colliderTransform.InverseTransformPoint(new Vector3(worldBounds.max.x, worldBounds.min.y, 0f))
        };
        collider.pathCount = 1;
        collider.SetPath(0, points);
    }

    private static bool BoundsMatch(Bounds actual, Bounds expected)
    {
        const float tolerance = 0.01f;
        return Mathf.Abs(actual.min.x - expected.min.x) <= tolerance &&
               Mathf.Abs(actual.min.y - expected.min.y) <= tolerance &&
               Mathf.Abs(actual.max.x - expected.max.x) <= tolerance &&
               Mathf.Abs(actual.max.y - expected.max.y) <= tolerance;
    }

    private static bool IsValidBounds(Bounds bounds)
    {
        return IsFinite(bounds.min) && IsFinite(bounds.max) &&
               bounds.size.x > 0f && bounds.size.y > 0f;
    }

    private static bool IsFinite(Vector3 value)
    {
        return !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
               !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
               !float.IsNaN(value.z) && !float.IsInfinity(value.z);
    }

    private static bool Fail(
        MapRuntimeContext context,
        Guid generationId,
        MapFailureCategory category,
        string code,
        string message,
        out string reason)
    {
        reason = message;
        if (context != null)
        {
            context.RecordFailure(new MapFailureDiagnostic(
                context.Mode, generationId, context.Phase, category, code, message));
        }
        return false;
    }
}

using System;

/// <summary>
/// Allows external runtime modules to validate and finalize a generation after authority checks but before Ready.
/// </summary>
public interface IMapRuntimeReadyFinalizer
{
    bool TryFinalizeBeforeReady(MapRuntimeContext context, Guid generationId, out string reason);
}

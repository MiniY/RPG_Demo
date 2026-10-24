using System;
using System.Collections.Generic;

/// <summary>Derived live solids for one accepted layout; never changes terrain or persisted layout identity.</summary>
public sealed class EcologyStaticCollisionRevision
{
    private readonly MapCandidateCollisionGeometry terrain;
    private readonly HashSet<string> staticIds = new HashSet<string>(StringComparer.Ordinal);
    public EcologyStaticCollisionRevision(EcologyPlanningAttemptResult accepted)
    {
        if (accepted == null || !accepted.Succeeded || accepted.Plan.Ecology == null || accepted.TerrainGeometry == null || accepted.StaticGeometry == null ||
            accepted.TerrainGeometry.GenerationId != accepted.Plan.GenerationId || accepted.StaticGeometry.GenerationId != accepted.Plan.GenerationId ||
            accepted.TerrainGeometry.MapContentIdentity != accepted.StaticGeometry.MapContentIdentity)
            throw new ArgumentException("Static revisions require the accepted same-generation terrain and layout projection.");
        terrain = accepted.TerrainGeometry; CurrentGeometry = accepted.StaticGeometry; GenerationId = accepted.Plan.GenerationId;
        LayoutSignature = accepted.Plan.Ecology.Signature; AcceptedGeometryIdentity = accepted.StaticGeometry.ProjectionIdentity;
        foreach (MapPlacementReservation reservation in accepted.Plan.Reservations)
            if (reservation.Role == MerchantPlacementService.MerchantRole || reservation.Role == PlantPlacementService.PlantRole ||
                reservation.Role == DestructiblePlacementService.DestructibleRole) staticIds.Add(reservation.LogicalObjectId);
    }
    public Guid GenerationId { get; private set; }
    public string LayoutSignature { get; private set; }
    public string AcceptedGeometryIdentity { get; private set; }
    public long Version { get; private set; }
    public MapCandidateCollisionGeometry CurrentGeometry { get; private set; }
    public bool UpdateLiveSolids(IEnumerable<MapPlannedSolidGeometry> solids)
    {
        if (solids == null) throw new ArgumentNullException(nameof(solids));
        var owned = new List<MapPlannedSolidGeometry>();
        foreach (MapPlannedSolidGeometry solid in solids)
        {
            if (solid == null || !staticIds.Contains(solid.LogicalId)) throw new ArgumentException("A live solid must belong to an accepted static role.");
            owned.Add(solid);
        }
        MapCandidateCollisionGeometry next = terrain.WithPlannedSolids(owned);
        if (CurrentGeometry.IsPhysicallyEquivalentTo(next)) return false;
        long nextVersion = checked(Version + 1);
        CurrentGeometry = next; Version = nextVersion; return true;
    }
}

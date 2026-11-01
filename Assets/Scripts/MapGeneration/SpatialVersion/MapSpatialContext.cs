using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>One immutable spatial version for one pending MapData. Null means the retained original route.</summary>
public abstract class MapSpatialContext
{
    protected MapSpatialContext(Guid generation, MapData map, string implementation)
    {
        if (generation == Guid.Empty || map == null || string.IsNullOrWhiteSpace(implementation))
            throw new ArgumentException("ExplicitSpatialIdentityRequired");
        GenerationId = generation; Map = map; ImplementationId = implementation;
    }
    public Guid GenerationId { get; }
    public MapData Map { get; }
    public string ImplementationId { get; }
    public abstract MapCoordinateBoundary Coordinates { get; }
    public abstract IReadOnlyList<Vector2[]> Boundaries { get; }
    public abstract IReadOnlyList<MapNavigationLink> NavigationLinks { get; }
    public abstract void Project(MapTilemapRenderer terrain, MapGenerationSettings settings);
    public virtual void RetireProjection(MapTilemapRenderer terrain) { }
    public void Require(Guid generation, MapData map)
    {
        if (generation != GenerationId || !ReferenceEquals(map, Map))
            throw new InvalidOperationException("StaleOrMixedSpatialContext");
    }
    public void ProjectBoundaries(MapTilemapRenderer terrain)
    {
        var edges = terrain.WaterBaseTilemap.transform.parent.GetComponentsInChildren<PolygonCollider2D>(true)
            .Single(p => p.name == "GeneratedCliffEdges");
        edges.pathCount = Boundaries.Count;
        for (int i = 0; i < Boundaries.Count; i++)
            edges.SetPath(i, Boundaries[i].Select(p => (Vector2)edges.transform.InverseTransformPoint(p)).ToArray());
    }
}

/// <summary>Explicit directed continuous route; each body qualifies the complete polyline before graph publication.</summary>
public sealed class MapNavigationLink
{
    private readonly Vector2[] points;
    public MapNavigationLink(int from, int to, IEnumerable<Vector2> route)
    {
        points = route?.ToArray() ?? throw new ArgumentNullException(nameof(route));
        if (from == to || points.Length < 2 || points.Any(p => float.IsNaN(p.x) || float.IsNaN(p.y) || float.IsInfinity(p.x) || float.IsInfinity(p.y)))
            throw new ArgumentException("InvalidContinuousNavigationLink");
        From = from; To = to;
    }
    public int From { get; }
    public int To { get; }
    public IReadOnlyList<Vector2> Points => Array.AsReadOnly(points);
    public MapNavigationLink Reverse() => new MapNavigationLink(To, From, points.Reverse());
}

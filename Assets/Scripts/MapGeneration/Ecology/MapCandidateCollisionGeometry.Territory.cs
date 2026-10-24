using System;
using System.Collections.Generic;
using UnityEngine;

public sealed partial class MapCandidateCollisionGeometry
{
    public EcologyNavigationGraph BuildHomeNavigationGraph(Collider2D solidFoot,
        IReadOnlyList<int> homeSurfaceNodes, IReadOnlyList<int> protectedSurfaceNodes)
    {
        bool[] home = SurfaceMask(homeSurfaceNodes), protection = SurfaceMask(protectedSurfaceNodes);
        EcologyNavigationGraph physical = BuildNavigationGraph(solidFoot, 0f);
        Footprint foot = CaptureFootprint(solidFoot, 0f);
        var legal = new bool[centers.Length]; var edges = new byte[centers.Length];
        for (int node = 0; node < legal.Length; node++)
            legal[node] = home[node] && physical.IsLegal(node) && QueryFootSurface(foot, node, null, home, protection);
        for (int node = 0; node < legal.Length; node++)
        {
            if (!legal[node]) continue;
            for (int d = 0; d < 4; d++)
            {
                int next = physical.Neighbor(node, d);
                if (next >= 0 && legal[next] && physical.CanMove(node, next)) edges[node] |= (byte)(1 << d);
            }
        }
        // Translation is one adjacent grid step on an axis. For the supported
        // fixed box/circle, every touched cell of that sweep belongs to the
        // union touched at its two endpoints. Both ends have full containment.
        return new EcologyNavigationGraph(GenerationId, Origin, Width, Height,
            ProjectionIdentity, physical.FootprintIdentity, legal, edges);
    }

    public int[] GetFootSurfaceNodes(Collider2D solidFoot, int bodyNode)
    {
        if (!BuildNavigationGraph(solidFoot, 0f).IsLegal(bodyNode)) throw Invalid("InvalidFootPlacementNode");
        var covered = new bool[centers.Length];
        if (!QueryFootSurface(CaptureFootprint(solidFoot, 0f), bodyNode, covered, null, null))
            throw Invalid("FootSurfaceSupportMismatch");
        return SafetyNodes(covered);
    }

    public int[] BuildBlockedPlacementNodes(Collider2D solidFoot, MapPlacementPlan reservations)
    {
        if (reservations == null || reservations.GenerationId != GenerationId) throw Invalid("StalePlacementReservations");
        var free = new bool[centers.Length];
        for (int node = 0; node < free.Length; node++)
            free[node] = !reservations.IsOccupied(Origin + new Vector2Int(node % Width, node / Width));
        EcologyNavigationGraph physical = BuildNavigationGraph(solidFoot, 0f);
        Footprint foot = CaptureFootprint(solidFoot, 0f); var blocked = new List<int>();
        for (int node = 0; node < free.Length; node++)
            if (!physical.IsLegal(node) || !QueryFootSurface(foot, node, null, free, null)) blocked.Add(node);
        return blocked.ToArray();
    }

    public bool AreFootPlacementsSeparated(Collider2D first, int firstNode, Collider2D second, int secondNode)
    {
        if (firstNode < 0 || firstNode >= centers.Length || secondNode < 0 || secondNode >= centers.Length)
            throw Invalid("InvalidFootPlacementNode");
        Footprint a = CaptureFootprint(first, 0f), b = CaptureFootprint(second, 0f);
        double dx = (double)centers[firstNode].x - centers[secondNode].x + a.Offset.x - b.Offset.x;
        double dy = (double)centers[firstNode].y - centers[secondNode].y + a.Offset.y - b.Offset.y;
        if (a.Circle && b.Circle)
        {
            double radius = (double)a.Radius + b.Radius;
            return dx * dx + dy * dy > radius * radius;
        }
        if (!a.Circle && !b.Circle)
        {
            double x = Math.Max(0d, Math.Abs(dx) - a.Half.x - b.Half.x);
            double y = Math.Max(0d, Math.Abs(dy) - a.Half.y - b.Half.y);
            double radius = (double)a.Radius + b.Radius;
            return x * x + y * y > radius * radius;
        }
        Footprint circle = a.Circle ? a : b, box = a.Circle ? b : a;
        double gapX = Math.Max(0d, Math.Abs(dx) - box.Half.x), gapY = Math.Max(0d, Math.Abs(dy) - box.Half.y);
        double combined = (double)circle.Radius + box.Radius;
        return gapX * gapX + gapY * gapY > combined * combined;
    }

    private bool[] SurfaceMask(IReadOnlyList<int> nodes)
    {
        if (nodes == null) throw new ArgumentNullException(nameof(nodes));
        var mask = new bool[centers.Length];
        for (int i = 0; i < nodes.Count; i++)
        {
            int node = nodes[i];
            if (node < 0 || node >= mask.Length || !walkable[node]) throw Invalid("InvalidSurfaceMembership");
            mask[node] = true;
        }
        return mask;
    }

    private bool QueryFootSurface(Footprint foot, int root, bool[] covered, bool[] required, bool[] forbidden)
        => QueryFootSurface(foot, centers[root], covered, required, forbidden);

    internal int[] CopyWalkableSurfaceNodes()
    {
        var result = new List<int>();
        for (int n = 0; n < walkable.Length; n++) if (walkable[n]) result.Add(n);
        return result.ToArray();
    }

    internal Rect WorldSurfaceBounds => floorBounds;
    internal Rect CellSurfaceRect(int node) => SafetySurfaceRect(node);
    internal bool IsLocalTerrainConnector(Vector2 position, int destination)
    {
        if (!TryWorldNode(position, out int source) || destination < 0 || destination >= centers.Length || !walkable[source] || !walkable[destination]) return false;
        if (source == destination) return true;
        int x = source % Width, y = source / Width;
        if (x + 1 < Width && destination == source + 1) return (terrainEdges[source] & 1) != 0;
        if (y + 1 < Height && destination == source + Width) return (terrainEdges[source] & 2) != 0;
        if (x > 0 && destination == source - 1) return (terrainEdges[source] & 4) != 0;
        return y > 0 && destination == source - Width && (terrainEdges[source] & 8) != 0;
    }

    internal bool ContainsFootInSurface(Collider2D shape, Vector2 bodyPosition, bool[] required, bool[] forbidden)
    {
        Footprint foot = CaptureFootprint(shape, 0f);
        return Clear(foot, bodyPosition, bodyPosition) && QueryFootSurface(foot, bodyPosition, null, required, forbidden);
    }

    private bool QueryFootSurface(Footprint foot, Vector2 bodyPosition, bool[] covered, bool[] required, bool[] forbidden)
    {
        Vector2 center = bodyPosition + foot.Offset;
        Vector2 half = foot.Circle ? Vector2.one * foot.Radius : foot.Half;
        float cellWidth = floorBounds.width / Width, cellHeight = floorBounds.height / Height;
        // One extra low-side cell includes exact boundary contact. Positive-area
        // coverage and closed protection contact have deliberately different tests.
        int minX = Mathf.Clamp(Mathf.FloorToInt((center.x - half.x - floorBounds.xMin) / cellWidth) - 1, 0, Width - 1);
        int maxX = Mathf.Clamp(Mathf.FloorToInt((center.x + half.x - floorBounds.xMin) / cellWidth), 0, Width - 1);
        int minY = Mathf.Clamp(Mathf.FloorToInt((center.y - half.y - floorBounds.yMin) / cellHeight) - 1, 0, Height - 1);
        int maxY = Mathf.Clamp(Mathf.FloorToInt((center.y + half.y - floorBounds.yMin) / cellHeight), 0, Height - 1);
        for (int y = minY; y <= maxY; y++)
        for (int x = minX; x <= maxX; x++)
        {
            int node = y * Width + x; Rect cell = SafetySurfaceRect(node);
            double dx = Math.Max(cell.xMin - center.x, Math.Max(0d, center.x - cell.xMax));
            double dy = Math.Max(cell.yMin - center.y, Math.Max(0d, center.y - cell.yMax));
            bool positive, touching;
            if (foot.Circle)
            {
                double squared = dx * dx + dy * dy, radiusSquared = (double)foot.Radius * foot.Radius;
                positive = squared < radiusSquared; touching = squared <= radiusSquared;
            }
            else
            {
                positive = center.x + half.x > cell.xMin && center.x - half.x < cell.xMax &&
                    center.y + half.y > cell.yMin && center.y - half.y < cell.yMax;
                touching = center.x + half.x >= cell.xMin && center.x - half.x <= cell.xMax &&
                    center.y + half.y >= cell.yMin && center.y - half.y <= cell.yMax;
            }
            if (touching && forbidden != null && forbidden[node]) return false;
            if (!positive) continue;
            if (!walkable[node] || required != null && !required[node]) return false;
            if (covered != null) covered[node] = true;
        }
        return true;
    }
}

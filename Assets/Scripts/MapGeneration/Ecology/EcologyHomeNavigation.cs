using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using UnityEngine;

public sealed class EcologyHomeNavigation
{
    private readonly MapCandidateCollisionGeometry geometry;
    private readonly bool[] homeSurface, protection, component, walkableSurface;
    private readonly EcologyMotionDomain homeDomain, movementDomain;
    private readonly Dictionary<int, EcologyReturnField> patrolFields = new Dictionary<int, EcologyReturnField>();
    private readonly Queue<int> patrolFieldOrder = new Queue<int>();
    private readonly List<float> connectorCuts = new List<float>(10);
    private readonly Dictionary<int, EcologyNavigationGraph> pursuitGraphs = new Dictionary<int, EcologyNavigationGraph>();
    private readonly Dictionary<string, EcologyReturnField> pursuitFields = new Dictionary<string, EcologyReturnField>(StringComparer.Ordinal);
    private readonly Queue<string> pursuitFieldOrder = new Queue<string>();
    internal EcologyHomeNavigation(MapCandidateCollisionGeometry geometry, EcologyNavigationGraph home,
        EcologyNavigationGraph movement, int[] nodes, IReadOnlyList<int> surface, int[] protectedNodes)
    {
        this.geometry = geometry; HomeGraph = home; MovementGraph = movement;
        ReturnField = movement.BuildReturnField(nodes); HomeNodes = Array.AsReadOnly((int[])nodes.Clone());
        homeSurface = new bool[movement.NodeCount]; protection = new bool[movement.NodeCount]; component = new bool[movement.NodeCount];
        foreach (int n in surface) homeSurface[n] = true;
        foreach (int n in protectedNodes) protection[n] = true;
        foreach (int n in nodes) component[n] = true;
        homeDomain = new EcologyMotionDomain(geometry, homeSurface, protection);
        walkableSurface = new bool[movement.NodeCount];
        foreach (int n in geometry.CopyWalkableSurfaceNodes()) walkableSurface[n] = true;
        movementDomain = new EcologyMotionDomain(geometry, walkableSurface, protection);
    }
    public EcologyNavigationGraph HomeGraph { get; }
    public EcologyNavigationGraph MovementGraph { get; }
    public EcologyReturnField ReturnField { get; }
    public IReadOnlyList<int> HomeNodes { get; }
    public Vector2 NodeWorldPosition(int node) => geometry.NodeWorldPosition(node);
    public bool TryHomeNode(Vector2 position, out int node) => geometry.TryWorldNode(position, out node) && HomeGraph.IsLegal(node);

    private EcologyNavigationGraph PursuitGraph(int maximumSteps)
    {
        if (pursuitGraphs.TryGetValue(maximumSteps, out EcologyNavigationGraph cached)) return cached;
        var legal = new bool[MovementGraph.NodeCount]; var edges = new byte[legal.Length];
        for (int n = 0; n < legal.Length; n++) legal[n] = MovementGraph.IsLegal(n) && ReturnField.Distances[n] >= 0 && ReturnField.Distances[n] <= maximumSteps;
        for (int n = 0; n < legal.Length; n++)
        {
            if (!legal[n]) continue;
            for (int d = 0; d < 4; d++)
            {
                int next = MovementGraph.Neighbor(n, d);
                if (next >= 0 && legal[next] && MovementGraph.CanMove(n, next) &&
                    (ReturnField.Distances[n] < maximumSteps || ReturnField.Distances[next] < maximumSteps)) edges[n] |= (byte)(1 << d);
            }
        }
        cached = new EcologyNavigationGraph(MovementGraph.GenerationId, MovementGraph.Origin, MovementGraph.Width, MovementGraph.Height,
            MovementGraph.ProjectionIdentity, MovementGraph.FootprintIdentity, legal, edges);
        pursuitGraphs.Add(maximumSteps, cached); return cached;
    }
    public bool TryPursuitWaypoint(Collider2D foot, Vector2 position, Vector2 target, int maximumSteps, out Vector2 waypoint)
    {
        waypoint = position;
        if (maximumSteps < 1 || !geometry.TryWorldNode(position, out int containing) || !geometry.TryWorldNode(target, out int targetCell) ||
            !TryMeasureHomeDistance(foot, position, out int distance, out _) || distance > maximumSteps) return false;
        EcologyNavigationGraph graph = PursuitGraph(maximumSteps);
        int goal = -1; float closest = float.PositiveInfinity;
        for (int d = -1; d < 4; d++)
        {
            int node = d < 0 ? targetCell : graph.Neighbor(targetCell, d);
            if (node < 0 || !graph.IsLegal(node)) continue;
            float squared = (geometry.NodeWorldPosition(node) - target).sqrMagnitude;
            if (squared >= closest) continue;
            closest = squared; goal = node;
        }
        if (goal < 0) return false;
        Vector2 goalPosition = geometry.NodeWorldPosition(goal);
        if (containing == goal || (position - goalPosition).sqrMagnitude <= .0025f)
        {
            if (!TryClipExcursionTranslation(foot, position, target, maximumSteps, out Vector2 approach) ||
                (approach - position).sqrMagnitude < .00000001f) return false;
            waypoint = approach; return true;
        }
        string key = maximumSteps.ToString(CultureInfo.InvariantCulture) + ":" + goal.ToString(CultureInfo.InvariantCulture);
        if (!pursuitFields.TryGetValue(key, out EcologyReturnField field))
        {
            field = graph.BuildReturnField(new[] { goal });
            if (pursuitFields.Count >= 16) pursuitFields.Remove(pursuitFieldOrder.Dequeue());
            pursuitFields.Add(key, field); pursuitFieldOrder.Enqueue(key);
        }
        if (position.Equals(geometry.NodeWorldPosition(containing)) && graph.IsLegal(containing))
        {
            int next = field.Next[containing];
            if (next < 0 || !graph.CanMove(containing, next)) return false;
            waypoint = geometry.NodeWorldPosition(next); return true;
        }
        int best = int.MaxValue;
        for (int d = -1; d < 4; d++)
        {
            int node = d < 0 ? containing : graph.Neighbor(containing, d);
            if (node < 0 || !graph.IsLegal(node) || field.Distances[node] < 0 || !geometry.IsLocalTerrainConnector(position, node)) continue;
            Vector2 centre = geometry.NodeWorldPosition(node);
            if (!TryClipExcursionTranslation(foot, position, centre, maximumSteps, out Vector2 clipped) || !clipped.Equals(centre)) continue;
            int cost = checked(field.Distances[node] + 1);
            if (cost >= best) continue;
            best = cost; waypoint = centre;
        }
        return best < int.MaxValue;
    }

    public bool TryPatrolWaypoint(Collider2D foot, Vector2 position, int target, out Vector2 waypoint)
    {
        waypoint = position;
        if (!HomeGraph.IsLegal(target) || !ContainsFullFoot(foot, position) || !geometry.TryWorldNode(position, out int containing)) return false;
        if (!patrolFields.TryGetValue(target, out EcologyReturnField field))
        {
            field = HomeGraph.BuildReturnField(new[] { target });
            if (patrolFields.Count >= 16) patrolFields.Remove(patrolFieldOrder.Dequeue());
            patrolFields.Add(target, field); patrolFieldOrder.Enqueue(target);
        }
        Vector2 targetPosition = geometry.NodeWorldPosition(target);
        if ((position - targetPosition).sqrMagnitude <= .0025f) { waypoint = targetPosition; return true; }
        if (position.Equals(geometry.NodeWorldPosition(containing)) && HomeGraph.IsLegal(containing))
        {
            int next = field.Next[containing];
            if (next < 0 || !HomeGraph.CanMove(containing, next)) return false;
            waypoint = geometry.NodeWorldPosition(next); return true;
        }
        int best = int.MaxValue;
        for (int direction = -1; direction < 4; direction++)
        {
            int node = direction < 0 ? containing : HomeGraph.Neighbor(containing, direction);
            if (node < 0 || !HomeGraph.IsLegal(node) || field.Distances[node] < 0 ||
                !geometry.IsLocalTerrainConnector(position, node)) continue;
            Vector2 centre = geometry.NodeWorldPosition(node);
            if (!geometry.IsTranslationClear(foot, position, centre, 0f) ||
                !homeDomain.TryClip(foot, position, centre, out Vector2 clipped) || !clipped.Equals(centre)) continue;
            int cost = checked(field.Distances[node] + 1);
            if (cost >= best) continue;
            best = cost; waypoint = centre;
        }
        return best < int.MaxValue;
    }
    public bool ContainsFullFoot(Collider2D foot, Vector2 bodyPosition)
    {
        if (!geometry.TryWorldNode(bodyPosition, out int containing) ||
            !geometry.ContainsFootInSurface(foot, bodyPosition, homeSurface, protection)) return false;
        // A continuous legal pose can belong to the home component even when
        // its containing cell's centre is not a legal whole-foot stopping pose.
        for (int direction = -1; direction < 4; direction++)
        {
            int node = direction < 0 ? containing : HomeGraph.Neighbor(containing, direction);
            if (node < 0 || !component[node]) continue;
            Vector2 centre = geometry.NodeWorldPosition(node);
            if (geometry.IsLocalTerrainConnector(bodyPosition, node) && geometry.IsTranslationClear(foot, bodyPosition, centre, 0f) &&
                homeDomain.TryClip(foot, bodyPosition, centre, out Vector2 clipped) && clipped.Equals(centre)) return true;
        }
        return false;
    }
    public bool TryClipHomeTranslation(Collider2D foot, Vector2 start, Vector2 target, out Vector2 clipped)
    {
        clipped = start;
        return ContainsFullFoot(foot, start) && homeDomain.TryClip(foot, start, target, out clipped);
    }
    public bool TryObservedHomeExit(Collider2D foot, Vector2 start, Vector2 end, out float fraction, out Vector2 witness)
    {
        fraction = 0f; witness = start;
        return ContainsFullFoot(foot, start) && homeDomain.TryFirstExcludedWitness(foot, start, end, out fraction, out witness);
    }

    public bool TryClipExcursionTranslation(Collider2D foot, Vector2 start, Vector2 target, int maximumSteps, out Vector2 clipped)
    {
        clipped = start;
        if (maximumSteps < 1 || !TryMeasureHomeDistance(foot, start, out int distance, out _) || distance > maximumSteps ||
            !movementDomain.TryClip(foot, start, target, out Vector2 safe)) return false;
        clipped = ClipCertifiedPrefix(foot, start, safe, maximumSteps, 0);
        return true;
    }
    private Vector2 ClipCertifiedPrefix(Collider2D foot, Vector2 from, Vector2 to, int maximumSteps, int depth)
    {
        if (CertifyRangeSegment(foot, from, to, maximumSteps)) return to;
        if (depth >= 12 || (to - from).sqrMagnitude < 1e-12f) return from;
        Vector2 middle = (from + to) * .5f;
        Vector2 first = ClipCertifiedPrefix(foot, from, middle, maximumSteps, depth + 1);
        return first.Equals(middle) ? ClipCertifiedPrefix(foot, middle, to, maximumSteps, depth + 1) : first;
    }
    private bool CertifyRangeSegment(Collider2D foot, Vector2 from, Vector2 to, int maximumSteps)
    {
        if (TryClipHomeTranslation(foot, from, to, out Vector2 homeEnd) && homeEnd.Equals(to)) return true;
        if (!geometry.TryWorldNode(from, out int first) || !geometry.TryWorldNode(to, out int last)) return false;
        for (int endpoint = 0; endpoint < 2; endpoint++)
        for (int direction = -1; direction < 4; direction++)
        {
            int containing = endpoint == 0 ? first : last;
            int node = direction < 0 ? containing : MovementGraph.Neighbor(containing, direction);
            if (node < 0 || !MovementGraph.IsLegal(node) || ReturnField.Distances[node] < 0 ||
                ReturnField.Distances[node] >= maximumSteps || !RootSegmentConnectsToAnchor(from, to, node)) continue;
            if (geometry.IsConnectorFanClear(foot, from, to, geometry.NodeWorldPosition(node), walkableSurface, protection)) return true;
        }
        return false;
    }
    private bool RootSegmentConnectsToAnchor(Vector2 from, Vector2 to, int anchor)
    {
        if (!geometry.IsLocalTerrainConnector(from, anchor) || !geometry.IsLocalTerrainConnector(to, anchor)) return false;
        connectorCuts.Clear(); connectorCuts.Add(0f); connectorCuts.Add(1f);
        Rect bounds = geometry.WorldSurfaceBounds;
        float width = bounds.width / MovementGraph.Width, height = bounds.height / MovementGraph.Height;
        Vector2 delta = to - from;
        for (int offset = -1; offset <= 2; offset++)
        {
            if (delta.x != 0f)
            {
                float t = (bounds.xMin + (anchor % MovementGraph.Width + offset) * width - from.x) / delta.x;
                if (t > 0f && t < 1f) connectorCuts.Add(t);
            }
            if (delta.y != 0f)
            {
                float t = (bounds.yMin + (anchor / MovementGraph.Width + offset) * height - from.y) / delta.y;
                if (t > 0f && t < 1f) connectorCuts.Add(t);
            }
        }
        connectorCuts.Sort();
        for (int i = 0; i < connectorCuts.Count; i++)
        {
            if (!geometry.IsLocalTerrainConnector(from + delta * connectorCuts[i], anchor)) return false;
            if (i > 0 && !geometry.IsLocalTerrainConnector(from + delta * ((connectorCuts[i - 1] + connectorCuts[i]) * .5f), anchor)) return false;
        }
        return true;
    }
    public bool TryMeasureHomeDistance(Collider2D foot, Vector2 position, out int steps, out Vector2 returnWaypoint)
    {
        steps = -1; returnWaypoint = position;
        if (ContainsFullFoot(foot, position)) { steps = 0; return true; }
        if (!geometry.TryWorldNode(position, out int containing) ||
            !geometry.ContainsFootInSurface(foot, position, walkableSurface, protection)) return false;
        Vector2 containingCentre = geometry.NodeWorldPosition(containing);
        int direct = ReturnField.Distances[containing];
        if (position.Equals(containingCentre) && MovementGraph.IsLegal(containing) && direct > 0)
        {
            int next = ReturnField.Next[containing];
            if (next < 0 || !MovementGraph.CanMove(containing, next)) return false;
            steps = direct; returnWaypoint = geometry.NodeWorldPosition(next); return true;
        }
        int best = int.MaxValue;
        for (int direction = -1; direction < 4; direction++)
        {
            int node = direction < 0 ? containing : MovementGraph.Neighbor(containing, direction);
            if (node < 0 || !MovementGraph.IsLegal(node) || ReturnField.Distances[node] < 0 ||
                !geometry.IsLocalTerrainConnector(position, node)) continue;
            Vector2 centre = geometry.NodeWorldPosition(node);
            if (!geometry.IsTranslationClear(foot, position, centre, 0f) ||
                !movementDomain.TryClip(foot, position, centre, out Vector2 clipped) || !clipped.Equals(centre)) continue;
            int distance = checked(ReturnField.Distances[node] + 1);
            if (distance >= best) continue;
            best = distance; returnWaypoint = centre;
        }
        if (best == int.MaxValue) return false;
        steps = best; return true;
    }
}

/// <summary>Derived same-generation, same-region, same-foot immutable navigation cache.</summary>
public sealed class EcologyHomeNavigationCache
{
    private readonly int[] walkable, protectedNodes;
    private readonly Dictionary<string, EcologyNavigationGraph> movement = new Dictionary<string, EcologyNavigationGraph>(StringComparer.Ordinal);
    private readonly Dictionary<string, EcologyHomeNavigation> homes = new Dictionary<string, EcologyHomeNavigation>(StringComparer.Ordinal);
    public EcologyHomeNavigationCache(MapEcologyLayout layout, MapCandidateCollisionGeometry geometry)
        : this(layout, geometry, geometry != null ? geometry.ProjectionIdentity : null, 0, false) { }

    private EcologyHomeNavigationCache(MapEcologyLayout layout, MapCandidateCollisionGeometry geometry, string acceptedIdentity, long version, bool trustedRevision)
    {
        if (layout == null || geometry == null || layout.GenerationId != geometry.GenerationId || layout.Origin != geometry.Origin ||
            layout.Width != geometry.Width || layout.Height != geometry.Height ||
            !trustedRevision && !(layout.GeometryIdentity == geometry.ProjectionIdentity || layout.GeometryIdentity.StartsWith(geometry.ProjectionIdentity + ":initial:", StringComparison.Ordinal)))
            throw new ArgumentException("Home navigation needs the exact accepted generation and static projection.");
        Layout = layout; Geometry = geometry; walkable = geometry.CopyWalkableSurfaceNodes();
        AcceptedGeometryIdentity = acceptedIdentity; StaticBlockerVersion = version;
        protectedNodes = Enumerable.Range(0, layout.RegionByNode.Count).Where(layout.IsProtectedNode).ToArray();
    }
    public MapEcologyLayout Layout { get; }
    public MapCandidateCollisionGeometry Geometry { get; }
    public string AcceptedGeometryIdentity { get; }
    public long StaticBlockerVersion { get; private set; }
    public EcologyHomeNavigationCache WithStaticRevision(EcologyStaticCollisionRevision revision)
    {
        if (revision == null || revision.GenerationId != Layout.GenerationId || revision.LayoutSignature != Layout.Signature ||
            revision.AcceptedGeometryIdentity != AcceptedGeometryIdentity || revision.CurrentGeometry == null ||
            revision.CurrentGeometry.MapContentIdentity != Geometry.MapContentIdentity || revision.Version < StaticBlockerVersion)
            throw new ArgumentException("Navigation cannot adopt a stale or foreign static revision.");
        if (revision.Version == StaticBlockerVersion && revision.CurrentGeometry.ProjectionIdentity == Geometry.ProjectionIdentity) return this;
        return new EcologyHomeNavigationCache(Layout, revision.CurrentGeometry, AcceptedGeometryIdentity, revision.Version, true);
    }
    public EcologyHomeNavigation Get(MapEcologyResident resident, EcologyEcotypeDefinition type)
    {
        if (resident == null || type == null || !Layout.Residents.Contains(resident) || resident.TypeId != type.TypeId ||
            !Layout.TryGetRegion(resident.HomeRegionId, out MapEcologyRegion region) || region.IsSafety || region.TypeId != type.TypeId)
            throw new ArgumentException("Resident home/type identity does not belong to this accepted layout.");
        string foot = Geometry.BuildNavigationGraph(type.MovementFoot, 0f).FootprintIdentity;
        string key = region.Id.ToString(CultureInfo.InvariantCulture) + ":" + foot;
        if (homes.TryGetValue(key, out EcologyHomeNavigation cached)) return cached;
        if (!movement.TryGetValue(foot, out EcologyNavigationGraph global))
        {
            global = Geometry.BuildHomeNavigationGraph(type.MovementFoot, walkable, protectedNodes); movement.Add(foot, global);
        }
        EcologyNavigationGraph home = Geometry.BuildHomeNavigationGraph(type.MovementFoot, region.Nodes, protectedNodes);
        int anchor = Layout.Residents.First(r => r.HomeRegionId == region.Id).InitialNode;
        var member = new bool[home.NodeCount]; var queue = new Queue<int>();
        if (!home.IsLegal(anchor)) throw new InvalidOperationException("Accepted home anchor is no longer legal.");
        member[anchor] = true; queue.Enqueue(anchor);
        while (queue.Count > 0)
        {
            int node = queue.Dequeue();
            for (int d = 0; d < 4; d++)
            {
                int next = home.Neighbor(node, d);
                if (next < 0 || member[next] || !home.CanMove(node, next) || !home.CanMove(next, node)) continue;
                member[next] = true; queue.Enqueue(next);
            }
        }
        foreach (MapEcologyResident inhabitant in Layout.Residents.Where(r => r.HomeRegionId == region.Id))
            if (!member[inhabitant.InitialNode]) throw new InvalidOperationException("Accepted residents do not share one qualified home component.");
        var edges = new byte[home.NodeCount];
        for (int n = 0; n < member.Length; n++)
            if (member[n]) for (int d = 0; d < 4; d++)
            {
                int next = home.Neighbor(n, d);
                if (next >= 0 && member[next] && home.CanMove(n, next)) edges[n] |= (byte)(1 << d);
            }
        var qualified = new EcologyNavigationGraph(home.GenerationId, home.Origin, home.Width, home.Height,
            home.ProjectionIdentity, home.FootprintIdentity, member, edges);
        cached = new EcologyHomeNavigation(Geometry, qualified, global,
            Enumerable.Range(0, member.Length).Where(n => member[n]).ToArray(), region.Nodes, protectedNodes);
        homes.Add(key, cached); return cached;
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed partial class EcologyNavigationGraph
{
    private MapNavigationLink[] links = Array.Empty<MapNavigationLink>();
    private readonly Dictionary<int, List<MapNavigationLink>> outgoing = new Dictionary<int, List<MapNavigationLink>>();
    public IReadOnlyList<MapNavigationLink> ContinuousLinks => Array.AsReadOnly(links);
    public bool HasContinuousLinks => links.Length != 0;
    private void InitializeLinks(IReadOnlyList<MapNavigationLink> source)
    {
        links = source == null ? Array.Empty<MapNavigationLink>() : source.ToArray();
        foreach (MapNavigationLink link in links)
        {
            if (link == null || !IsLegal(link.From) || !IsLegal(link.To) || FindLink(link.From, link.To) != null)
                throw new ArgumentException("InvalidOrDuplicateContinuousLink");
            if (!outgoing.TryGetValue(link.From, out var list)) outgoing.Add(link.From, list = new List<MapNavigationLink>());
            list.Add(link);
        }
    }
    private int ExtraNeighbor(int node, int index) => outgoing.TryGetValue(node, out var list) && index >= 0 && index < list.Count ? list[index].To : -1;
    public int DirectionCount(int node) => 4 + (outgoing.TryGetValue(node, out var list) ? list.Count : 0);
    public MapNavigationLink FindLink(int from, int to) => outgoing.TryGetValue(from, out var list) ? list.Find(p => p.To == to) : null;
    public int MoveCost(int from, int to)
    {
        var link = FindLink(from, to); if (link == null) return 1;
        // Grid Manhattan arc length extends original axis-step distances conservatively.
        float length = 0f;
        int cellDx = Mathf.Abs(to % Width - from % Width), cellDy = Mathf.Abs(to / Width - from / Width);
        Vector2 span = link.Points[link.Points.Count - 1] - link.Points[0];
        float sx = cellDx > 0 ? Mathf.Abs(span.x) / cellDx : 1f;
        float sy = cellDy > 0 ? Mathf.Abs(span.y) / cellDy : sx;
        if (sx <= 0 || sy <= 0) throw new InvalidOperationException("InvalidNavigationGridScale");
        for (int i = 1; i < link.Points.Count; i++)
        { Vector2 d = link.Points[i] - link.Points[i - 1]; length += Mathf.Abs(d.x) / sx + Mathf.Abs(d.y) / sy; }
        return Math.Max(cellDx + cellDy, Math.Max(1, Mathf.CeilToInt(length)));
    }
    public MapNavigationLink[] FilterLinks(bool[] allowed, Func<MapNavigationLink, bool> qualify = null) => links.Where(l => allowed[l.From] && allowed[l.To] && (qualify == null || qualify(l))).ToArray();
    private EcologyReturnField BuildWeightedReturnField(int[] roots)
    {
        var distances = Enumerable.Repeat(-1, NodeCount).ToArray(); var next = Enumerable.Repeat(-1, NodeCount).ToArray();
        var frontier = new SortedSet<(int distance, long order, int node)>(); long order = 0; int expanded = 0;
        foreach (int root in roots.OrderBy(p => p))
        { if (!IsLegal(root)) throw new ArgumentException("IllegalReturnRoot"); if (distances[root] == 0) continue; distances[root] = 0; frontier.Add((0, order++, root)); }
        while (frontier.Count > 0)
        {
            var visit = frontier.Min; frontier.Remove(visit); if (distances[visit.node] != visit.distance) continue; expanded++;
            var predecessors = new List<int>();
            for (int d = 0; d < 4; d++) predecessors.Add(Neighbor(visit.node, d));
            foreach (var link in links) if (link.To == visit.node) predecessors.Add(link.From);
            foreach (int pred in predecessors)
            {
                if (pred < 0 || !CanMove(pred, visit.node)) continue;
                int proposed = checked(visit.distance + MoveCost(pred, visit.node));
                if (distances[pred] >= 0 && distances[pred] <= proposed) continue;
                distances[pred] = proposed; next[pred] = visit.node; frontier.Add((proposed, order++, pred));
            }
        }
        return new EcologyReturnField(GenerationId, ProjectionIdentity, FootprintIdentity, distances, next, expanded);
    }
}
internal static class EcologyWeightedPartition
{
    internal static EcologyRegionPartition Build(EcologyNavigationGraph[] graphs, int[] seeds, int[] regionIds, int[] stableOrder)
    {
        int count = graphs[0].NodeCount;
        var owners = new int[count]; var distances = Enumerable.Repeat(-1, count).ToArray(); var parents = Enumerable.Repeat(-1, count).ToArray();
        var queue = new SortedSet<(int distance, int rank, long serial, int node, int owner, int parent)>(); long serial = 0; int expanded = 0;
        for (int rank = 0; rank < stableOrder.Length; rank++)
        { int owner = stableOrder[rank]; queue.Add((0, rank, serial++, seeds[owner], owner, -1)); }
        while (queue.Count > 0)
        {
            var visit = queue.Min; queue.Remove(visit); int node = visit.node;
            if (owners[node] != 0) continue;
            owners[node] = regionIds[visit.owner]; distances[node] = visit.distance; parents[node] = visit.parent; expanded++;
            var graph = graphs[visit.owner];
            for (int d = 0; d < graph.DirectionCount(node); d++)
            {
                int next = graph.Neighbor(node, d); if (next < 0 || owners[next] != 0 || !graph.CanMove(node, next)) continue;
                queue.Add((checked(visit.distance + graph.MoveCost(node, next)), visit.rank, serial++, next, visit.owner, node));
            }
        }
        return new EcologyRegionPartition(graphs[0].GenerationId, graphs[0].ProjectionIdentity, owners, distances, parents, expanded);
    }
}

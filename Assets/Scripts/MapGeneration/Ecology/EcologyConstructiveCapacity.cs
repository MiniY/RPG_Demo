using System;
using System.Collections.Generic;

/// <summary>A deterministic feasible packing lower bound, never a maximum-packing claim.</summary>
public sealed class EcologyConstructiveCapacity
{
    private EcologyConstructiveCapacity(List<int> component, List<int> placements,
        int examined, int expansions, int rejectedByFootprint)
    {
        ComponentNodes = component.AsReadOnly(); PlacementNodes = placements.AsReadOnly();
        CandidatesExamined = examined; NeighborExpansions = expansions; RejectedByFootprint = rejectedByFootprint;
    }
    public IReadOnlyList<int> ComponentNodes { get; }
    public IReadOnlyList<int> PlacementNodes { get; }
    public int CandidatesExamined { get; }
    public int NeighborExpansions { get; }
    public int RejectedByFootprint { get; }

    // The candidate geometry supplies the complete-foot pair predicate. A caller
    // must qualify static/other-role clearance before supplying member nodes.
    public static EcologyConstructiveCapacity Build(EcologyNavigationGraph graph,
        int[] memberNodes, int[] blockedPlacementNodes, ulong[] stableRanksByNode,
        int minimumSpacingSteps, Func<int, int, bool> feetSeparated)
    {
        if (graph == null || memberNodes == null || blockedPlacementNodes == null ||
            stableRanksByNode == null || stableRanksByNode.Length != graph.NodeCount ||
            minimumSpacingSteps < 1 || feetSeparated == null)
            throw new ArgumentException("Capacity requires one qualified graph, members, ranks, spacing and complete-foot clearance.");
        int count = graph.NodeCount;
        var member = new bool[count]; var blocked = new bool[count];
        foreach (int node in memberNodes)
        {
            if (!graph.IsLegal(node) || member[node]) throw new ArgumentException("Capacity members must be distinct qualified nodes.");
            member[node] = true;
        }
        foreach (int node in blockedPlacementNodes)
        {
            if (node < 0 || node >= count) throw new ArgumentException("Blocked reservation lies outside this graph.");
            blocked[node] = true;
        }
        Comparison<int> compare = (a, b) =>
        {
            int rank = stableRanksByNode[a].CompareTo(stableRanksByNode[b]);
            return rank != 0 ? rank : a.CompareTo(b);
        };
        var ordered = new List<int>(memberNodes); ordered.Sort(compare);
        var visited = new bool[count]; var queue = new int[count];
        var component = new List<int>(); int expansions = 0;
        // Both directions must be traversable: a one-way reachability chain
        // cannot certify a resident's route back to its original territory.
        foreach (int root in ordered)
        {
            if (visited[root]) continue;
            var candidate = new List<int>(); int head = 0, tail = 0;
            visited[root] = true; queue[tail++] = root;
            while (head < tail)
            {
                int node = queue[head++]; candidate.Add(node);
                for (int d = 0; d < 4; d++)
                {
                    expansions++;
                    int next = graph.Neighbor(node, d);
                    if (next < 0 || !member[next] || visited[next] ||
                        !graph.CanMove(node, next) || !graph.CanMove(next, node)) continue;
                    visited[next] = true; queue[tail++] = next;
                }
            }
            // Equal-sized components keep the first stable-ranked root.
            if (candidate.Count > component.Count) component = candidate;
        }
        component.Sort(compare);
        var placements = new List<int>(); var spacingBlocked = new bool[count];
        var seen = new int[count]; var distances = new int[count];
        int examined = 0, rejectedByFootprint = 0;
        foreach (int node in component)
        {
            examined++;
            if (blocked[node] || spacingBlocked[node]) continue;
            bool separated = true;
            foreach (int other in placements)
                if (!feetSeparated(node, other)) { separated = false; break; }
            if (!separated) { rejectedByFootprint++; continue; }
            placements.Add(node);
            int stamp = placements.Count, head = 0, tail = 0;
            queue[tail++] = node; seen[node] = stamp; distances[node] = 0;
            while (head < tail)
            {
                int current = queue[head++]; spacingBlocked[current] = true;
                if (distances[current] >= minimumSpacingSteps - 1) continue;
                for (int d = 0; d < 4; d++)
                {
                    expansions++;
                    int next = graph.Neighbor(current, d);
                    if (next < 0 || seen[next] == stamp ||
                        !graph.CanMove(current, next) || !graph.CanMove(next, current)) continue;
                    seen[next] = stamp; distances[next] = distances[current] + 1; queue[tail++] = next;
                }
            }
        }
        return new EcologyConstructiveCapacity(component, placements, examined, expansions, rejectedByFootprint);
    }
}

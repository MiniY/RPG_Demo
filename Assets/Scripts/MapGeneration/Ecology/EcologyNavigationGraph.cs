using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Frozen legal nodes/edges supplied by a candidate-projection clearance provider.</summary>
public sealed class EcologyNavigationGraph
{
    private readonly bool[] legal;
    private readonly byte[] forward;
    public EcologyNavigationGraph(Guid generationId, Vector2Int origin, int width, int height,
        string projectionIdentity, string footprintIdentity, bool[] legalNodes, byte[] forwardMasks)
    {
        if (generationId == Guid.Empty || width < 1 || height < 1 ||
            string.IsNullOrWhiteSpace(projectionIdentity) || string.IsNullOrWhiteSpace(footprintIdentity))
            throw new ArgumentException("A graph needs explicit generation/projection/footprint identity.");
        int count = checked(width * height);
        checked { _ = origin.x + width - 1; _ = origin.y + height - 1; }
        if (legalNodes == null || forwardMasks == null || legalNodes.Length != count || forwardMasks.Length != count)
            throw new ArgumentException("Graph storage does not match its dimensions.");
        GenerationId = generationId; Origin = origin; Width = width; Height = height;
        ProjectionIdentity = projectionIdentity; FootprintIdentity = footprintIdentity;
        legal = (bool[])legalNodes.Clone(); forward = (byte[])forwardMasks.Clone();
        for (int node = 0; node < count; node++)
        {
            if ((forward[node] & 0xf0) != 0 || (!legal[node] && forward[node] != 0))
                throw new ArgumentException("Invalid forward-edge mask.");
            for (int direction = 0; direction < 4; direction++)
            {
                if ((forward[node] & (1 << direction)) == 0) continue;
                int next = Neighbor(node, direction);
                if (next < 0 || !legal[next]) throw new ArgumentException("Forward edge targets an illegal node.");
            }
        }
    }
    public Guid GenerationId { get; }
    public Vector2Int Origin { get; }
    public int Width { get; }
    public int Height { get; }
    public int NodeCount => legal.Length;
    public string ProjectionIdentity { get; }
    public string FootprintIdentity { get; }
    public bool IsLegal(int node) => node >= 0 && node < legal.Length && legal[node];

    // Fixed direction order is part of v1 determinism: right, up, left, down.
    public int Neighbor(int node, int direction)
    {
        if (node < 0 || node >= NodeCount) return -1;
        int x = node % Width, y = node / Width;
        switch (direction)
        {
            case 0: return x + 1 < Width ? node + 1 : -1;
            case 1: return y + 1 < Height ? node + Width : -1;
            case 2: return x > 0 ? node - 1 : -1;
            case 3: return y > 0 ? node - Width : -1;
            default: return -1;
        }
    }
    public bool CanMove(int from, int to)
    {
        if (!IsLegal(from) || !IsLegal(to)) return false;
        for (int direction = 0; direction < 4; direction++)
            if (Neighbor(from, direction) == to) return (forward[from] & (1 << direction)) != 0;
        return false;
    }

    public EcologyReturnField BuildReturnField(int[] homeNodes)
    {
        if (homeNodes == null || homeNodes.Length == 0) throw new ArgumentException("Home must contain legal stopping nodes.");
        int[] roots = (int[])homeNodes.Clone(); Array.Sort(roots);
        var distances = new int[NodeCount]; var next = new int[NodeCount]; var queue = new int[NodeCount];
        for (int node = 0; node < NodeCount; node++) { distances[node] = -1; next[node] = -1; }
        int head = 0, tail = 0;
        foreach (int home in roots)
        {
            if (!IsLegal(home)) throw new ArgumentException("Home contains an illegal stopping node.");
            if (distances[home] == 0) continue;
            distances[home] = 0; queue[tail++] = home;
        }
        while (head < tail)
        {
            int destination = queue[head++];
            for (int direction = 0; direction < 4; direction++)
            {
                int predecessor = Neighbor(destination, direction);
                if (predecessor < 0 || distances[predecessor] >= 0 || !CanMove(predecessor, destination)) continue;
                distances[predecessor] = checked(distances[destination] + 1);
                next[predecessor] = destination; queue[tail++] = predecessor;
            }
        }
        return new EcologyReturnField(GenerationId, ProjectionIdentity, FootprintIdentity, distances, next, head);
    }
}

/// <summary>Discrete node distances. A zero node distance alone never proves continuous HomePose.</summary>
public sealed class EcologyReturnField
{
    internal EcologyReturnField(Guid generationId, string projection, string footprint,
        int[] distances, int[] next, int expanded)
    {
        GenerationId = generationId; ProjectionIdentity = projection; FootprintIdentity = footprint;
        Distances = Array.AsReadOnly(distances); Next = Array.AsReadOnly(next); ExpandedNodes = expanded;
    }
    public Guid GenerationId { get; }
    public string ProjectionIdentity { get; }
    public string FootprintIdentity { get; }
    public IReadOnlyList<int> Distances { get; }
    public IReadOnlyList<int> Next { get; }
    public int ExpandedNodes { get; }
}

public sealed class EcologyRegionPartition
{
    internal EcologyRegionPartition(Guid generationId, string projectionIdentity,
        int[] owners, int[] distances, int[] parents, int expanded)
    {
        GenerationId = generationId; ProjectionIdentity = projectionIdentity;
        RegionByNode = Array.AsReadOnly(owners); Distances = Array.AsReadOnly(distances);
        Parents = Array.AsReadOnly(parents); ExpandedNodes = expanded;
    }
    public Guid GenerationId { get; }
    public string ProjectionIdentity { get; }
    public IReadOnlyList<int> RegionByNode { get; }
    public IReadOnlyList<int> Distances { get; }
    public IReadOnlyList<int> Parents { get; }
    public int ExpandedNodes { get; }
}

/// <summary>Unit-edge multi-source BFS; spatial preferences belong to seed selection, never edge costs.</summary>
public static class EcologyRegionPartitioner
{
    public static EcologyRegionPartition Build(EcologyNavigationGraph[] seedGraphs,
        int[] seedNodes, int[] regionIds, ulong[] stableSeedScores)
    {
        if (seedGraphs == null || seedGraphs.Length == 0 || seedNodes == null || regionIds == null ||
            stableSeedScores == null || seedNodes.Length != seedGraphs.Length ||
            regionIds.Length != seedGraphs.Length || stableSeedScores.Length != seedGraphs.Length)
            throw new ArgumentException("Every region seed needs one frozen graph, stable identity and rank.");
        EcologyNavigationGraph common = seedGraphs[0] ?? throw new ArgumentException("Missing seed graph.");
        int count = common.NodeCount;
        var ids = new HashSet<int>(); var positions = new HashSet<int>();
        var order = new int[seedGraphs.Length];
        for (int i = 0; i < seedGraphs.Length; i++)
        {
            EcologyNavigationGraph graph = seedGraphs[i];
            if (graph == null || graph.GenerationId != common.GenerationId || graph.Origin != common.Origin ||
                graph.Width != common.Width || graph.Height != common.Height ||
                !string.Equals(graph.ProjectionIdentity, common.ProjectionIdentity, StringComparison.Ordinal) ||
                !graph.IsLegal(seedNodes[i]) || regionIds[i] <= 0 || !ids.Add(regionIds[i]) || !positions.Add(seedNodes[i]))
                throw new ArgumentException("Region seeds must belong to one candidate projection and distinct legal nodes.");
            order[i] = i;
        }
        Array.Sort(order, (a, b) =>
        {
            int score = stableSeedScores[a].CompareTo(stableSeedScores[b]);
            return score != 0 ? score : regionIds[a].CompareTo(regionIds[b]);
        });
        var owners = new int[count]; var distances = new int[count]; var parents = new int[count];
        var ownerIndices = new int[count]; var queue = new int[count];
        for (int node = 0; node < count; node++) { distances[node] = -1; parents[node] = -1; ownerIndices[node] = -1; }
        int head = 0, tail = 0;
        foreach (int seed in order)
        {
            int node = seedNodes[seed]; owners[node] = regionIds[seed]; distances[node] = 0;
            ownerIndices[node] = seed; queue[tail++] = node;
        }
        while (head < tail)
        {
            int node = queue[head++]; int owner = ownerIndices[node];
            EcologyNavigationGraph graph = seedGraphs[owner];
            for (int direction = 0; direction < 4; direction++)
            {
                int next = graph.Neighbor(node, direction);
                if (next < 0 || owners[next] != 0 || !graph.CanMove(node, next)) continue;
                owners[next] = regionIds[owner]; ownerIndices[next] = owner;
                parents[next] = node; distances[next] = checked(distances[node] + 1);
                queue[tail++] = next;
            }
        }
        return new EcologyRegionPartition(common.GenerationId, common.ProjectionIdentity,
            owners, distances, parents, head);
    }
}

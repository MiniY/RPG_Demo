using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Surface masks and qualified body stopping nodes are intentionally distinct.</summary>
public sealed class EcologySafetyRegions
{
    internal EcologySafetyRegions(int[] birthCore, int[] birthBuffer, int[] restCore, int[] restBuffer,
        int[] birthStopping, int[] restStopping, int merchant, int approach, int rest)
    {
        BirthCore = Array.AsReadOnly(birthCore); BirthBuffer = Array.AsReadOnly(birthBuffer);
        RestCore = Array.AsReadOnly(restCore); RestBuffer = Array.AsReadOnly(restBuffer);
        BirthStoppingNodes = Array.AsReadOnly(birthStopping); RestStoppingNodes = Array.AsReadOnly(restStopping);
        MerchantNode = merchant; MerchantApproachNode = approach; RestNode = rest;
    }
    public IReadOnlyList<int> BirthCore { get; }
    public IReadOnlyList<int> BirthBuffer { get; }
    public IReadOnlyList<int> RestCore { get; }
    public IReadOnlyList<int> RestBuffer { get; }
    public IReadOnlyList<int> BirthStoppingNodes { get; }
    public IReadOnlyList<int> RestStoppingNodes { get; }
    public int MerchantNode { get; }
    public int MerchantApproachNode { get; }
    public int RestNode { get; }
}

public sealed partial class MapCandidateCollisionGeometry
{
    public bool TryBuildSafetyRegions(Collider2D playerFoot, int spawnNode,
        int[] qualifiedMerchantNodes, int[] qualifiedApproachNodes,
        int birthCoreSteps, int restCoreSteps, int bufferSteps, ulong[] stableRanksByNode,
        out EcologySafetyRegions result, out string failure)
    {
        result = null; failure = null;
        if (qualifiedMerchantNodes == null || qualifiedApproachNodes == null ||
            qualifiedMerchantNodes.Length != qualifiedApproachNodes.Length ||
            stableRanksByNode == null || stableRanksByNode.Length != centers.Length ||
            birthCoreSteps < 1 || restCoreSteps < 1 || bufferSteps < 1)
            throw new ArgumentException("Safety planning needs qualified merchant pairs, bounded radii and stable node ranks.");
        EcologyNavigationGraph graph = BuildNavigationGraph(playerFoot, 0f);
        Footprint foot = CaptureFootprint(playerFoot, 0f);
        if (!graph.IsLegal(spawnNode)) { failure = "SpawnFootprintBlocked"; return false; }
        int[] spawnDistances = SafetyDistances(graph, new[] { spawnNode });
        int[] birthStops = SafetyBall(spawnDistances, birthCoreSteps);
        int[] birthBufferStops = SafetyRing(graph, birthStops, bufferSteps);
        if (!TryCoverStoppingFeet(foot, birthStops, out bool[] birthCore) ||
            !TryCoverStoppingFeet(foot, birthBufferStops, out bool[] birthBuffer))
        { failure = "SafetySurfaceSupportMismatch"; return false; }
        bool[] birthProtected = SafetyUnion(birthCore, birthBuffer);
        for (int n = 0; n < birthBuffer.Length; n++) if (birthCore[n]) birthBuffer[n] = false;
        if (SafetyNodes(birthBuffer).Length == 0) { failure = "InsufficientSeparateSafety"; return false; }
        var merchantOrder = new List<int>();
        for (int i = 0; i < qualifiedMerchantNodes.Length; i++)
        {
            int merchant = qualifiedMerchantNodes[i], approach = qualifiedApproachNodes[i];
            if (merchant < 0 || merchant >= centers.Length || approach < 0 || approach >= centers.Length)
                throw new ArgumentException("Merchant qualification belongs to another candidate grid.");
            if (merchant != spawnNode && birthCore[merchant] && graph.IsLegal(approach) &&
                Array.IndexOf(birthStops, approach) >= 0 && SafetyTerrainNeighbors(graph, approach, merchant)) merchantOrder.Add(i);
        }
        merchantOrder.Sort((a, b) =>
        {
            int score = stableRanksByNode[qualifiedMerchantNodes[a]].CompareTo(stableRanksByNode[qualifiedMerchantNodes[b]]);
            if (score != 0) return score;
            int node = qualifiedMerchantNodes[a].CompareTo(qualifiedMerchantNodes[b]);
            return node != 0 ? node : qualifiedApproachNodes[a].CompareTo(qualifiedApproachNodes[b]);
        });
        if (merchantOrder.Count == 0) { failure = "NoQualifiedMerchantInBirthSafety"; return false; }
        var restOrder = new List<int>();
        for (int n = 0; n < centers.Length; n++)
            if (spawnDistances[n] >= 0 && !birthProtected[n]) restOrder.Add(n);
        restOrder.Sort((a, b) =>
        {
            int distance = spawnDistances[b].CompareTo(spawnDistances[a]);
            if (distance != 0) return distance;
            int rank = stableRanksByNode[a].CompareTo(stableRanksByNode[b]);
            return rank != 0 ? rank : a.CompareTo(b);
        });
        foreach (int restNode in restOrder)
        {
            int[] restStops = SafetyBall(SafetyDistances(graph, new[] { restNode }), restCoreSteps);
            int[] restBufferStops = SafetyRing(graph, restStops, bufferSteps);
            if (!TryCoverStoppingFeet(foot, restStops, out bool[] restCore) ||
                !TryCoverStoppingFeet(foot, restBufferStops, out bool[] restBuffer)) continue;
            bool[] restProtected = SafetyUnion(restCore, restBuffer);
            if (!SafetyMasksSeparated(foot, birthProtected, restProtected)) continue;
            for (int n = 0; n < restBuffer.Length; n++) if (restCore[n]) restBuffer[n] = false;
            int[] finalRestBuffer = SafetyNodes(restBuffer);
            if (finalRestBuffer.Length == 0) continue;
            int selected = merchantOrder[0];
            result = new EcologySafetyRegions(SafetyNodes(birthCore), SafetyNodes(birthBuffer),
                SafetyNodes(restCore), finalRestBuffer, birthStops, restStops,
                qualifiedMerchantNodes[selected], qualifiedApproachNodes[selected], restNode);
            return true;
        }
        failure = "InsufficientSeparateSafety"; return false;
    }

    private bool SafetyTerrainNeighbors(EcologyNavigationGraph graph, int from, int to)
    {
        for (int d = 0; d < 4; d++)
            if (graph.Neighbor(from, d) == to && (terrainEdges[from] & (1 << d)) != 0) return true;
        return false;
    }
    private static int[] SafetyDistances(EcologyNavigationGraph graph, int[] roots)
    {
        var distances = new int[graph.NodeCount]; var queue = new int[graph.NodeCount];
        for (int n = 0; n < distances.Length; n++) distances[n] = -1;
        int head = 0, tail = 0;
        foreach (int root in roots)
        {
            if (!graph.IsLegal(root) || distances[root] >= 0) continue;
            distances[root] = 0; queue[tail++] = root;
        }
        while (head < tail)
        {
            int node = queue[head++];
            for (int d = 0; d < 4; d++)
            {
                int next = graph.Neighbor(node, d);
                if (next < 0 || distances[next] >= 0 || !graph.CanMove(node, next) || !graph.CanMove(next, node)) continue;
                distances[next] = checked(distances[node] + 1); queue[tail++] = next;
            }
        }
        return distances;
    }
    private static int[] SafetyBall(int[] distances, int radius)
    {
        var nodes = new List<int>();
        for (int n = 0; n < distances.Length; n++) if (distances[n] >= 0 && distances[n] <= radius) nodes.Add(n);
        return nodes.ToArray();
    }
    private static int[] SafetyRing(EcologyNavigationGraph graph, int[] core, int steps)
    {
        int[] distances = SafetyDistances(graph, core); var ring = new List<int>();
        for (int n = 0; n < distances.Length; n++) if (distances[n] > 0 && distances[n] <= steps) ring.Add(n);
        return ring.ToArray();
    }
    private bool TryCoverStoppingFeet(Footprint foot, int[] stoppingNodes, out bool[] surface)
    {
        surface = new bool[centers.Length];
        foreach (int root in stoppingNodes)
        {
            if (!QueryFootSurface(foot, root, surface, null, null)) return false;
        }
        return true;
    }
    private Rect SafetySurfaceRect(int node)
    {
        Vector2 half = new Vector2(floorBounds.width / Width, floorBounds.height / Height) * .5f;
        return Rect.MinMaxRect(centers[node].x - half.x, centers[node].y - half.y,
            centers[node].x + half.x, centers[node].y + half.y);
    }
    private bool SafetyMasksSeparated(Footprint foot, bool[] first, bool[] second)
    {
        int[] a = SafetyNodes(first), b = SafetyNodes(second);
        foreach (int x in a)
        foreach (int y in b)
        {
            Rect left = SafetySurfaceRect(x), right = SafetySurfaceRect(y);
            double dx = Math.Max(left.xMin - right.xMax, Math.Max(0d, right.xMin - left.xMax));
            double dy = Math.Max(left.yMin - right.yMax, Math.Max(0d, right.yMin - left.yMax));
            // These are protection surfaces, not native physical obstacles.
            // Use the original geometric solid, without polygon contact radius.
            bool canTouchBoth = foot.Circle ? dx * dx + dy * dy <= 4d * foot.Radius * foot.Radius :
                dx <= 2d * foot.Half.x && dy <= 2d * foot.Half.y;
            if (canTouchBoth) return false;
        }
        return true;
    }
    private static bool[] SafetyUnion(bool[] first, bool[] second)
    {
        var union = new bool[first.Length];
        for (int n = 0; n < union.Length; n++) union[n] = first[n] || second[n];
        return union;
    }
    private static int[] SafetyNodes(bool[] mask)
    {
        var nodes = new List<int>();
        for (int n = 0; n < mask.Length; n++) if (mask[n]) nodes.Add(n);
        return nodes.ToArray();
    }
}

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

public enum MapEcologyRegionKind { MerchantSafety = 1, RestSafety = 2, Ordinary = 3, Boss = 4 }

public sealed class MapEcologyRegion
{
    internal MapEcologyRegion(int id, MapEcologyRegionKind kind, string typeId,
        int[] nodes, int[] buffer, int capacity, int assigned)
    {
        Id = id; Kind = kind; TypeId = typeId;
        Nodes = Array.AsReadOnly((int[])nodes.Clone());
        BufferNodes = Array.AsReadOnly((int[])buffer.Clone());
        ConstructiveCapacity = capacity; AssignedPopulation = assigned;
    }
    public int Id { get; }
    public MapEcologyRegionKind Kind { get; }
    public string TypeId { get; }
    public IReadOnlyList<int> Nodes { get; }
    public IReadOnlyList<int> BufferNodes { get; }
    public int ConstructiveCapacity { get; }
    public int AssignedPopulation { get; }
    public bool IsSafety => Kind == MapEcologyRegionKind.MerchantSafety || Kind == MapEcologyRegionKind.RestSafety;
}

public sealed class MapEcologyResident
{
    internal MapEcologyResident(string id, int homeRegionId, string typeId, int initialNode)
    { Id = id; HomeRegionId = homeRegionId; TypeId = typeId; InitialNode = initialNode; }
    public string Id { get; }
    public int HomeRegionId { get; }
    public string TypeId { get; }
    public int InitialNode { get; }
}

/// <summary>One immutable generation layout; membership is canonical, query indices are derived.</summary>
public sealed class MapEcologyLayout
{
    public const int SchemaVersion = 1;
    private readonly IReadOnlyDictionary<int, MapEcologyRegion> regionIndex;

    private MapEcologyLayout(Builder builder, int[] membership,
        List<MapEcologyRegion> regions, List<MapEcologyResident> residents)
    {
        GenerationId = builder.GenerationId; Origin = builder.Origin;
        Width = builder.Width; Height = builder.Height;
        RecipeIdentity = builder.RecipeIdentity; GeometryIdentity = builder.GeometryIdentity;
        FootprintVersion = builder.FootprintVersion; AcceptedAttempt = builder.AcceptedAttempt;
        RegionByNode = Array.AsReadOnly((int[])membership.Clone());
        Regions = regions.AsReadOnly(); Residents = residents.AsReadOnly();
        var index = new Dictionary<int, MapEcologyRegion>();
        foreach (MapEcologyRegion region in regions) index.Add(region.Id, region);
        regionIndex = new ReadOnlyDictionary<int, MapEcologyRegion>(index);
        Signature = ComputeSignature();
    }

    public Guid GenerationId { get; }
    public Vector2Int Origin { get; }
    public int Width { get; }
    public int Height { get; }
    public string RecipeIdentity { get; }
    public string GeometryIdentity { get; }
    public string FootprintVersion { get; }
    public int AcceptedAttempt { get; }
    public IReadOnlyList<int> RegionByNode { get; }
    public IReadOnlyList<MapEcologyRegion> Regions { get; }
    public IReadOnlyList<MapEcologyResident> Residents { get; }
    public string Signature { get; }

    public MapEcologyRegion RegionAt(int node)
    {
        if (node < 0 || node >= RegionByNode.Count) return null;
        return regionIndex.TryGetValue(RegionByNode[node], out MapEcologyRegion region) ? region : null;
    }
    public bool IsProtectedNode(int node) => RegionAt(node)?.IsSafety ?? false;
    public bool TryGetRegion(int id, out MapEcologyRegion region) => regionIndex.TryGetValue(id, out region);
    public int NodeAt(Vector2Int cell)
    {
        long x = (long)cell.x - Origin.x, y = (long)cell.y - Origin.y;
        return x < 0 || x >= Width || y < 0 || y >= Height ? -1 : (int)(y * Width + x);
    }
    public Vector2Int CellAt(int node)
    {
        if (node < 0 || node >= RegionByNode.Count) throw new ArgumentOutOfRangeException(nameof(node));
        return new Vector2Int(checked(Origin.x + node % Width), checked(Origin.y + node / Width));
    }

    private string ComputeSignature()
    {
        using (var bytes = new MemoryStream())
        {
            using (var writer = new BinaryWriter(bytes, Encoding.UTF8, true))
            {
                writer.Write("RPG-ECOLOGY-LAYOUT"); writer.Write(SchemaVersion);
                writer.Write(Width); writer.Write(Height); writer.Write(Origin.x); writer.Write(Origin.y);
                writer.Write(RecipeIdentity); writer.Write(GeometryIdentity); writer.Write(FootprintVersion);
                writer.Write(AcceptedAttempt);
                writer.Write(RegionByNode.Count);
                foreach (int id in RegionByNode) writer.Write(id);
                writer.Write(Regions.Count);
                foreach (MapEcologyRegion region in Regions)
                {
                    writer.Write(region.Id); writer.Write((int)region.Kind); writer.Write(region.TypeId);
                    writer.Write(region.ConstructiveCapacity); writer.Write(region.AssignedPopulation);
                    writer.Write(region.Nodes.Count); foreach (int node in region.Nodes) writer.Write(node);
                    writer.Write(region.BufferNodes.Count); foreach (int node in region.BufferNodes) writer.Write(node);
                }
                writer.Write(Residents.Count);
                foreach (MapEcologyResident resident in Residents)
                {
                    writer.Write(resident.Id); writer.Write(resident.HomeRegionId);
                    writer.Write(resident.TypeId); writer.Write(resident.InitialNode);
                }
            }
            using (SHA256 sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(bytes.ToArray())).Replace("-", "").ToLowerInvariant();
        }
    }

    public sealed class Builder
    {
        private readonly Dictionary<int, MapEcologyRegion> regions = new Dictionary<int, MapEcologyRegion>();
        private readonly Dictionary<string, MapEcologyResident> residents = new Dictionary<string, MapEcologyResident>(StringComparer.Ordinal);
        private MapEcologyLayout frozen;
        public Builder(Guid generationId, Vector2Int origin, int width, int height,
            string recipeIdentity, string geometryIdentity, string footprintVersion, int acceptedAttempt)
        {
            if (generationId == Guid.Empty || width < 1 || height < 1 || acceptedAttempt < 0 ||
                string.IsNullOrWhiteSpace(recipeIdentity) || string.IsNullOrWhiteSpace(geometryIdentity) ||
                string.IsNullOrWhiteSpace(footprintVersion)) throw new ArgumentException("Invalid layout identity.");
            checked { _ = width * height; _ = origin.x + width - 1; _ = origin.y + height - 1; }
            GenerationId = generationId; Origin = origin; Width = width; Height = height;
            RecipeIdentity = recipeIdentity; GeometryIdentity = geometryIdentity;
            FootprintVersion = footprintVersion; AcceptedAttempt = acceptedAttempt;
        }
        public Guid GenerationId { get; }
        public Vector2Int Origin { get; }
        public int Width { get; }
        public int Height { get; }
        public string RecipeIdentity { get; }
        public string GeometryIdentity { get; }
        public string FootprintVersion { get; }
        public int AcceptedAttempt { get; }
        public string LastFailure { get; private set; }

        public bool TryAddRegion(int id, string kind, string typeId, int[] nodes, int[] bufferNodes,
            int constructiveCapacity, int assignedPopulation)
        {
            if (frozen != null) return Reject("LayoutFrozen");
            if (id <= 0 || regions.ContainsKey(id) ||
                !Enum.TryParse(kind, false, out MapEcologyRegionKind parsed) || !Enum.IsDefined(typeof(MapEcologyRegionKind), parsed) ||
                !CopyNodes(nodes, out int[] core) || core.Length == 0 || !CopyNodes(bufferNodes, out int[] buffer) ||
                constructiveCapacity < 0 || assignedPopulation < 0 || assignedPopulation > constructiveCapacity)
                return Reject("InvalidRegion");
            bool safety = parsed == MapEcologyRegionKind.MerchantSafety || parsed == MapEcologyRegionKind.RestSafety;
            if (typeId == null || (safety && (typeId.Length != 0 || assignedPopulation != 0 || constructiveCapacity != 0 || buffer.Length == 0)) ||
                (!safety && (string.IsNullOrWhiteSpace(typeId) || assignedPopulation < 1 || buffer.Length != 0)) ||
                (parsed == MapEcologyRegionKind.Boss && assignedPopulation != 1)) return Reject("InvalidRegionRole");
            var occupied = new HashSet<int>(core);
            foreach (int node in buffer) if (!occupied.Add(node)) return Reject("OverlappingRegionMask");
            foreach (MapEcologyRegion existing in regions.Values)
            {
                foreach (int node in existing.Nodes) if (occupied.Contains(node)) return Reject("OverlappingRegionMask");
                foreach (int node in existing.BufferNodes) if (occupied.Contains(node)) return Reject("OverlappingRegionMask");
            }
            regions.Add(id, new MapEcologyRegion(id, parsed, typeId, core, buffer, constructiveCapacity, assignedPopulation));
            LastFailure = null;
            return true;
        }

        public bool TryAddResident(string id, int homeRegionId, string typeId, int initialNode)
        {
            if (frozen != null) return Reject("LayoutFrozen");
            if (string.IsNullOrWhiteSpace(id) || residents.ContainsKey(id) ||
                !regions.TryGetValue(homeRegionId, out MapEcologyRegion home) || home.IsSafety ||
                !string.Equals(typeId, home.TypeId, StringComparison.Ordinal)) return Reject("InvalidResident");
            bool member = false;
            foreach (int node in home.Nodes) if (node == initialNode) { member = true; break; }
            if (!member) return Reject("ResidentOutsideHome");
            foreach (MapEcologyResident resident in residents.Values)
                if (resident.InitialNode == initialNode) return Reject("DuplicateResidentPosition");
            residents.Add(id, new MapEcologyResident(id, homeRegionId, typeId, initialNode));
            LastFailure = null;
            return true;
        }

        public MapEcologyLayout Freeze()
        {
            if (frozen != null) return frozen;
            int merchant = 0, rest = 0, boss = 0;
            var canonical = new int[checked(Width * Height)];
            var orderedRegions = new List<MapEcologyRegion>(regions.Values);
            orderedRegions.Sort((a, b) => a.Id.CompareTo(b.Id));
            foreach (MapEcologyRegion region in orderedRegions)
            {
                if (region.Kind == MapEcologyRegionKind.MerchantSafety) merchant++;
                if (region.Kind == MapEcologyRegionKind.RestSafety) rest++;
                if (region.Kind == MapEcologyRegionKind.Boss) boss++;
                int count = 0;
                foreach (MapEcologyResident resident in residents.Values) if (resident.HomeRegionId == region.Id) count++;
                if (count != region.AssignedPopulation) throw new MapPlanningException("ResidentBudgetMismatch", "Final residents must match the assigned population.", false);
                foreach (int node in region.Nodes) canonical[node] = region.Id;
                foreach (int node in region.BufferNodes) canonical[node] = region.Id;
            }
            if (merchant != 1 || rest != 1 || boss > 1)
                throw new MapPlanningException("InvalidSafetyOrBossCount", "Exactly two distinct safety roles and at most one populated Boss region are required.", false);
            var orderedResidents = new List<MapEcologyResident>(residents.Values);
            orderedResidents.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            frozen = new MapEcologyLayout(this, canonical, orderedRegions, orderedResidents);
            return frozen;
        }

        private bool CopyNodes(int[] source, out int[] copy)
        {
            copy = null;
            if (source == null) return false;
            copy = (int[])source.Clone(); Array.Sort(copy);
            for (int i = 0; i < copy.Length; i++)
                if (copy[i] < 0 || copy[i] >= Width * Height || (i > 0 && copy[i] == copy[i - 1])) return false;
            return true;
        }
        private bool Reject(string code) { LastFailure = code; return false; }
    }
}

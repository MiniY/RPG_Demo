using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Globalization;
using System.Linq;
using UnityEngine;

/// <summary>One explicit prefab identity, including its colour variant, is one ecotype.</summary>
public sealed class EcologyEcotypeDefinition
{
    private readonly MapPopulationSpecies eligibility;
    private readonly int[] footSiblingPath;
    private readonly int footComponent;
    public EcologyEcotypeDefinition(string typeId, GameObject prefab,
        MapPopulationTerrainMask terrains, int weight, bool isBoss)
    {
        if (prefab == null) throw new ArgumentException("An ecotype needs its exact retained prefab.");
        eligibility = new MapPopulationSpecies(typeId, terrains, weight, prefab); eligibility.Validate();
        TypeId = typeId; Prefab = prefab; Terrains = terrains; Weight = weight; IsBoss = isBoss;
        Collider2D selected = null;
        foreach (Collider2D collider in prefab.GetComponentsInChildren<Collider2D>(true))
        {
            if (!collider.enabled || collider.isTrigger) continue;
            bool active = true;
            for (Transform part = collider.transform; part != prefab.transform; part = part.parent)
                if (part == null || !part.gameObject.activeSelf) { active = false; break; }
            if (!active) continue;
            if (selected != null) throw new ArgumentException("An ecotype requires one explicit solid movement foot: " + typeId);
            selected = collider;
        }
        MovementFoot = selected != null ? selected : throw new ArgumentException("Ecotype solid foot missing: " + typeId);
        var path = new List<int>();
        for (Transform part = MovementFoot.transform; part != prefab.transform; part = part.parent) path.Add(part.GetSiblingIndex());
        path.Reverse(); footSiblingPath = path.ToArray();
        footComponent = Array.IndexOf(MovementFoot.GetComponents<Collider2D>(), MovementFoot);
    }
    public string TypeId { get; }
    public GameObject Prefab { get; }
    public MapPopulationTerrainMask Terrains { get; }
    public int Weight { get; }
    public bool IsBoss { get; }
    public Collider2D MovementFoot { get; }
    public bool Allows(MapTerrainType terrain) => eligibility.Allows(terrain);
    public Collider2D ResolveMovementFoot(GameObject instance)
    {
        if (instance == null) throw new ArgumentNullException(nameof(instance));
        Transform part = instance.transform;
        foreach (int index in footSiblingPath)
        {
            if (index < 0 || index >= part.childCount) throw new InvalidOperationException("Ecotype movement hierarchy changed: " + TypeId);
            part = part.GetChild(index);
        }
        Collider2D[] colliders = part.GetComponents<Collider2D>();
        if (footComponent < 0 || footComponent >= colliders.Length || colliders[footComponent].GetType() != MovementFoot.GetType())
            throw new InvalidOperationException("Ecotype movement component changed: " + TypeId);
        return colliders[footComponent];
    }
}

/// <summary>Versioned engineering initial values; the existing profiles never consume this recipe.</summary>
public sealed class EcologyTerritoryRecipe
{
    public EcologyTerritoryRecipe(int minimumGroup = 3, int totalBudget = 24, int maximumOrdinaryRegions = 4,
        int maximumPerRegion = 8, int densityNumerator = 1, int densityDenominator = 48,
        int minimumSpacingSteps = 3, int targetRegionArea = 300, int bossChanceDenominator = 20,
        int seedLatticeSpacing = 8, int localRepairLimit = 2, bool bossContentPending = false, bool rejectUndersizedSeedComponents = false)
    {
        if (minimumGroup < 2 || totalBudget <= minimumGroup || maximumOrdinaryRegions < 1 ||
            maximumPerRegion < minimumGroup || densityNumerator < 0 || densityDenominator < 1 ||
            minimumSpacingSteps < 1 || targetRegionArea < 1 || bossChanceDenominator < 1 ||
            seedLatticeSpacing < 1 || seedLatticeSpacing > 256 || localRepairLimit < 0 || localRepairLimit > 2)
            throw new ArgumentException("Invalid versioned ecology territory recipe.");
        MinimumGroup = minimumGroup; TotalBudget = totalBudget; MaximumOrdinaryRegions = maximumOrdinaryRegions;
        MaximumPerRegion = maximumPerRegion; DensityNumerator = densityNumerator; DensityDenominator = densityDenominator;
        MinimumSpacingSteps = minimumSpacingSteps; TargetRegionArea = targetRegionArea; BossChanceDenominator = bossChanceDenominator;
        SeedLatticeSpacing = seedLatticeSpacing; LocalRepairLimit = localRepairLimit;
        BossContentPending = bossContentPending; RejectUndersizedSeedComponents = rejectUndersizedSeedComponents;
        using (var bytes = new MemoryStream())
        {
            using (var writer = new BinaryWriter(bytes, System.Text.Encoding.UTF8, true))
            {
                writer.Write("ecology-zoning-v1-territory-r1");
                foreach (int value in new[] { minimumGroup, totalBudget, maximumOrdinaryRegions, maximumPerRegion,
                    densityNumerator, densityDenominator, minimumSpacingSteps, targetRegionArea,
                    bossChanceDenominator, seedLatticeSpacing, localRepairLimit }) writer.Write(value);
                // Preserve every existing recipe byte; only the explicit content-gap candidate adds this marker.
                if (bossContentPending) writer.Write("boss-content-pending-no-boss-v1");
                if (rejectUndersizedSeedComponents) writer.Write("reject-undersized-seed-components-r4-v1");
            }
            using (SHA256 sha = SHA256.Create())
                Identity = "ecology-zoning-v1-territory-r1:" + BitConverter.ToString(sha.ComputeHash(bytes.ToArray())).Replace("-", "").ToLowerInvariant();
        }
    }
    public string Identity { get; }
    public int MinimumGroup { get; }
    public int TotalBudget { get; }
    public int MaximumOrdinaryRegions { get; }
    public int MaximumPerRegion { get; }
    public int DensityNumerator { get; }
    public int DensityDenominator { get; }
    public int MinimumSpacingSteps { get; }
    public int TargetRegionArea { get; }
    public int BossChanceDenominator { get; }
    public int SeedLatticeSpacing { get; }
    public int LocalRepairLimit { get; }
    public bool BossContentPending { get; }
    public bool RejectUndersizedSeedComponents { get; }
}

public sealed class EcologyTerritoryDraft
{
    internal EcologyTerritoryDraft(MapPlacementPlan plan, MapEcologyLayout layout, int repairs, int activityNodes)
    { Plan = plan; Layout = layout; LocalRepairsUsed = repairs; EffectiveActivityNodes = activityNodes; }
    // Other original roles finish their reservations before the owner attaches
    // this immutable layout and freezes the original placement plan.
    public MapPlacementPlan Plan { get; }
    public MapEcologyLayout Layout { get; }
    public int LocalRepairsUsed { get; }
    public int EffectiveActivityNodes { get; }
}

public static class EcologyTerritoryPlanner
{
    private sealed class TypeContext
    {
        internal EcologyEcotypeDefinition Type;
        internal EcologyNavigationGraph Physical;
        internal int[] BlockedNodes;
        internal bool[] Blocked;
    }
    private sealed class Territory
    {
        internal int Id, Anchor, Assigned;
        internal int[] Surface, Activity;
        internal TypeContext Context;
        internal EcologyConstructiveCapacity Capacity;
    }
    private sealed class Placed
    {
        internal Territory Territory;
        internal int Node;
        internal string Id;
    }
    public static EcologyTerritoryDraft Build(int seed, int ecologyAttempt, MapData map,
        MapCandidateCollisionGeometry geometry, Collider2D playerFoot, EcologySafetyRegions safety,
        MapPlacementPlan staticPlanWithMerchant, EcologyTerritoryRecipe recipe, EcologyEcotypeDefinition[] ecotypes,
        int terrainAttempt = 0, MapCandidateCollisionGeometry initialPlacementGeometry = null)
    {
        if (map == null || geometry == null || safety == null || recipe == null || ecotypes == null || ecotypes.Length == 0 ||
            staticPlanWithMerchant == null || ecologyAttempt < 0 || terrainAttempt < 0 ||
            staticPlanWithMerchant.GenerationId != geometry.GenerationId || staticPlanWithMerchant.Ecology != null ||
            !geometry.MatchesMapData(map)) throw Failure("StaleOrInvalidEcologyInput");
        if (initialPlacementGeometry != null && (initialPlacementGeometry.GenerationId != geometry.GenerationId ||
            !initialPlacementGeometry.MatchesMapData(map))) throw Failure("StaleOrInvalidEcologyInput");
        var contexts = new List<TypeContext>(); var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (EcologyEcotypeDefinition type in ecotypes.OrderBy(t => t?.TypeId, StringComparer.Ordinal))
        {
            if (type == null || !ids.Add(type.TypeId)) throw Failure("InvalidEcotypeCatalog");
            var context = new TypeContext { Type = type, Physical = geometry.BuildNavigationGraph(type.MovementFoot, 0f),
                BlockedNodes = geometry.BuildBlockedPlacementNodes(type.MovementFoot, staticPlanWithMerchant),
                Blocked = new bool[map.Width * map.Height] };
            foreach (int n in context.BlockedNodes) context.Blocked[n] = true;
            if (initialPlacementGeometry != null)
            {
                EcologyNavigationGraph initialFootGraph = initialPlacementGeometry.BuildNavigationGraph(type.MovementFoot, 0f);
                for (int n = 0; n < context.Blocked.Length; n++) if (!initialFootGraph.IsLegal(n)) context.Blocked[n] = true;
            }
            for (int n = 0; n < context.Blocked.Length; n++)
                if (!type.Allows(map.GetCell(Cell(map, n)).terrainType)) context.Blocked[n] = true;
            context.BlockedNodes = Enumerable.Range(0, context.Blocked.Length).Where(n => context.Blocked[n]).ToArray();
            contexts.Add(context);
        }
        if (!contexts.Any(t => !t.Type.IsBoss) ||
            (recipe.BossContentPending ? contexts.Any(t => t.Type.IsBoss) : !contexts.Any(t => t.Type.IsBoss)))
            throw Failure("IncompleteEcotypeCatalog");
        var merchant = staticPlanWithMerchant.Reservations.Where(r => r.Role == MerchantPlacementService.MerchantRole).ToArray();
        var approach = staticPlanWithMerchant.Reservations.Where(r => r.Role == MerchantPlacementService.InteractionApproachRole).ToArray();
        if (merchant.Length != 1 || approach.Length != 1 || merchant[0].AnchorCell != Cell(map, safety.MerchantNode) ||
            approach[0].AnchorCell != Cell(map, safety.MerchantApproachNode)) throw Failure("UnboundSafetyMerchant");
        int count = checked(map.Width * map.Height);
        int[] protectedNodes = safety.BirthCore.Concat(safety.BirthBuffer).Concat(safety.RestCore).Concat(safety.RestBuffer)
            .Distinct().OrderBy(n => n).ToArray();
        var protectedMask = new bool[count]; foreach (int n in protectedNodes) protectedMask[n] = true;
        EcologyNavigationGraph player = geometry.BuildNavigationGraph(playerFoot, 0f);
        int spawn = Node(map, map.SpawnCell);
        if (!player.IsLegal(spawn)) throw Failure("SpawnFootprintBlocked");
        int[] playerDistances = Distances(player, spawn);
        string catalog = Hash(w =>
        {
            foreach (TypeContext t in contexts)
            { w.Write(t.Type.TypeId); w.Write((int)t.Type.Terrains); w.Write(t.Type.Weight); w.Write(t.Type.IsBoss); w.Write(t.Physical.FootprintIdentity); }
        });
        string key = recipe.Identity + ":" + catalog + ":t=" + terrainAttempt.ToString(CultureInfo.InvariantCulture) +
            ":e=" + ecologyAttempt.ToString(CultureInfo.InvariantCulture);
        string footprintVersion = "ecology-solid-feet-v1:" + Hash(w =>
        { foreach (TypeContext t in contexts) { w.Write(t.Type.TypeId); w.Write(t.Physical.FootprintIdentity); } });
        int[] outside = Enumerable.Range(0, count).Where(n => !protectedMask[n] && map.IsWalkable(Cell(map, n))).ToArray();
        Territory boss = null;
        if (!recipe.BossContentPending && Score(seed, "eco.boss-roll.v1", key, Vector2Int.zero) % (uint)recipe.BossChanceDenominator == 0)
            boss = FindBoss(seed, key, map, geometry, contexts.Where(t => t.Type.IsBoss).ToArray(), outside,
                protectedNodes, playerDistances, recipe);
        var forbidden = (bool[])protectedMask.Clone();
        if (boss != null) foreach (int n in boss.Surface) forbidden[n] = true;
        int[] ordinarySurface = Enumerable.Range(0, count).Where(n => !forbidden[n] && map.IsWalkable(Cell(map, n))).ToArray();
        EcologyNavigationGraph proto = geometry.BuildHomeNavigationGraph(playerFoot, ordinarySurface,
            Enumerable.Range(0, count).Where(n => forbidden[n]).ToArray());
        bool[] available = Enumerable.Range(0, count).Select(n => proto.IsLegal(n) && playerDistances[n] >= 0).ToArray();
        proto = Restrict(proto, available);
        int area = available.Count(v => v), budget = recipe.TotalBudget - (boss != null ? 1 : 0);
        int initial = Math.Min(recipe.MaximumOrdinaryRegions, Math.Max(1, area / recipe.TargetRegionArea));
        initial = Math.Min(initial, budget / recipe.MinimumGroup);
        string lastFailure = "NoConnectedTerritory";
        for (int repair = 0; repair <= recipe.LocalRepairLimit && initial - repair > 0; repair++)
        {
            int k = initial - repair;
            int[] seeds = SelectSeeds(seed, key, map, proto, contexts, recipe, k);
            if (seeds.Length != k) { lastFailure = "InsufficientSeparatedSeeds"; continue; }
            int[] regionIds = Enumerable.Range(0, k).Select(i => 1000 + i).ToArray();
            var partition = EcologyRegionPartitioner.Build(Enumerable.Repeat(proto, k).ToArray(), seeds, regionIds,
                seeds.Select(n => (ulong)Score(seed, "eco.region-tie.v1", key, Cell(map, n) - map.Origin)).ToArray());
            var regions = new List<Territory>(); bool qualified = true;
            for (int i = 0; i < k; i++)
            {
                int id = regionIds[i];
                int[] surface = Enumerable.Range(0, count).Where(n => partition.RegionByNode[n] == id).ToArray();
                var options = new List<Territory>();
                foreach (TypeContext type in contexts.Where(t => !t.Type.IsBoss))
                {
                    Territory option = Qualify(seed, key + ":region=" + id.ToString(CultureInfo.InvariantCulture), map, geometry,
                        type, surface, protectedNodes, seeds[i], recipe.MinimumGroup, recipe.MinimumSpacingSteps);
                    if (option != null) { option.Id = id; options.Add(option); }
                }
                if (options.Count == 0) { qualified = false; lastFailure = "InsufficientConstructiveCapacity"; break; }
                regions.Add(Weighted(options, Score(seed, "eco.type-choice.v1", key, Cell(map, seeds[i]) - map.Origin)));
            }
            if (!qualified) continue;
            var demands = new int[k];
            for (int i = 0; i < k; i++)
            {
                Territory region = regions[i];
                if (!EcologyPopulationBudget.TryComputeDemand(region.Activity.Length, recipe.DensityNumerator, recipe.DensityDenominator,
                    recipe.MinimumGroup, region.Capacity.PlacementNodes.Count, recipe.MaximumPerRegion, out demands[i], out string failure))
                    throw Failure(failure);
            }
            if (!EcologyPopulationBudget.TryAllocate(recipe.MinimumGroup, budget, regions.Select(r => r.Id).ToArray(),
                regions.Select(r => r.Capacity.PlacementNodes.Count).ToArray(), demands,
                regions.Select(r => (ulong)Score(seed, "eco.population-remainder.v1", key, Cell(map, r.Anchor) - map.Origin)).ToArray(),
                out int[] assigned, out string allocationFailure)) throw Failure(allocationFailure);
            for (int i = 0; i < k; i++) regions[i].Assigned = assigned[i];
            MapPlacementPlan candidate = CopyPlan(staticPlanWithMerchant);
            var placed = new List<Placed>();
            if (boss != null && !Place(geometry, map, candidate, boss, placed)) { lastFailure = "BossPlacementConflict"; continue; }
            foreach (Territory region in regions.OrderBy(r => Score(seed, "eco.place-region-order.v1", key, Cell(map, r.Anchor) - map.Origin)).ThenBy(r => r.Id))
                if (!Place(geometry, map, candidate, region, placed)) { qualified = false; lastFailure = "JointPopulationCapacityUnavailable"; break; }
            if (!qualified) continue;
            var builder = new MapEcologyLayout.Builder(geometry.GenerationId, map.Origin, map.Width, map.Height,
                recipe.Identity + ":" + catalog, initialPlacementGeometry == null ? geometry.ProjectionIdentity :
                    geometry.ProjectionIdentity + ":initial:" + initialPlacementGeometry.ProjectionIdentity, footprintVersion, ecologyAttempt);
            AddRegion(builder, 1, "MerchantSafety", "", safety.BirthCore.ToArray(), safety.BirthBuffer.ToArray(), 0, 0);
            AddRegion(builder, 2, "RestSafety", "", safety.RestCore.ToArray(), safety.RestBuffer.ToArray(), 0, 0);
            if (boss != null) AddRegion(builder, boss.Id, "Boss", boss.Context.Type.TypeId, boss.Surface, Array.Empty<int>(), boss.Capacity.PlacementNodes.Count, 1);
            foreach (Territory region in regions)
                AddRegion(builder, region.Id, "Ordinary", region.Context.Type.TypeId, region.Surface, Array.Empty<int>(),
                    region.Capacity.PlacementNodes.Count, region.Assigned);
            foreach (Placed resident in placed)
                if (!builder.TryAddResident(resident.Id, resident.Territory.Id, resident.Territory.Context.Type.TypeId, resident.Node))
                    throw Failure(builder.LastFailure);
            return new EcologyTerritoryDraft(candidate, builder.Freeze(), repair, regions.Sum(r => r.Activity.Length));
        }
        throw Failure(lastFailure);
    }

    private static Territory FindBoss(int seed, string key, MapData map, MapCandidateCollisionGeometry geometry,
        TypeContext[] types, int[] surface, int[] protection, int[] playerDistances, EcologyTerritoryRecipe recipe)
    {
        var options = new List<Territory>();
        foreach (TypeContext type in types)
        {
            EcologyNavigationGraph legal = geometry.BuildHomeNavigationGraph(type.Type.MovementFoot, surface, protection);
            foreach (int anchor in Enumerable.Range(0, legal.NodeCount).Where(n => legal.IsLegal(n) && !type.Blocked[n] && playerDistances[n] >= 0)
                .OrderBy(n => Score(seed, "eco.boss-anchor.v1", key, Cell(map, n) - map.Origin)).ThenBy(n => n))
            {
                int[] distances = Distances(legal, anchor, 3);
                int[] zone = Enumerable.Range(0, legal.NodeCount).Where(n => distances[n] >= 0).ToArray();
                if (zone.Length < 9) continue;
                Territory option = Qualify(seed, key + ":boss", map, geometry, type, zone, protection, anchor, 1, recipe.MinimumSpacingSteps);
                if (option == null) continue;
                option.Id = 100; option.Assigned = 1; options.Add(option); break;
            }
        }
        return options.Count == 0 ? null : Weighted(options, Score(seed, "eco.boss-type.v1", key, Vector2Int.zero));
    }

    private static Territory Qualify(int seed, string key, MapData map, MapCandidateCollisionGeometry geometry,
        TypeContext type, int[] surface, int[] protection, int preferredSeed, int minimum, int spacing)
    {
        if (surface.Length < minimum) return null;
        EcologyNavigationGraph home = geometry.BuildHomeNavigationGraph(type.Type.MovementFoot, surface, protection);
        Territory best = null;
        foreach (int[] component in Components(home).OrderByDescending(c => c.Length).ThenBy(c => c[0]))
        {
            if (component.Length < minimum || best != null && component.Length < best.Activity.Length ||
                component.Count(n => !type.Blocked[n]) < minimum) continue;
            int anchor = component.Where(n => !type.Blocked[n])
                .OrderBy(n => Manhattan(map, n, preferredSeed)).ThenBy(n => Score(seed, "eco.home-anchor.v1", key, Cell(map, n) - map.Origin))
                .ThenBy(n => n).DefaultIfEmpty(-1).First();
            if (anchor < 0) continue;
            int[] distances = Distances(home, anchor);
            ulong[] ranks = Enumerable.Range(0, home.NodeCount).Select(n => distances[n] < 0 ? ulong.MaxValue :
                ((ulong)(uint)distances[n] << 32) | Score(seed, "eco.resident-rank.v1", key, Cell(map, n) - map.Origin)).ToArray();
            EcologyConstructiveCapacity capacity = geometry.BuildCompatibleCapacity(type.Type.MovementFoot, component,
                type.BlockedNodes, ranks, spacing);
            if (capacity.PlacementNodes.Count < minimum) continue;
            if (best == null || component.Length > best.Activity.Length || component.Length == best.Activity.Length && anchor < best.Anchor)
                best = new Territory { Context = type, Surface = surface, Activity = component, Capacity = capacity, Anchor = anchor };
        }
        return best;
    }

    private static bool Place(MapCandidateCollisionGeometry geometry, MapData map, MapPlacementPlan plan,
        Territory territory, List<Placed> placed)
    {
        int before = placed.Count;
        foreach (int node in territory.Capacity.PlacementNodes)
        {
            bool compatible = true;
            foreach (Placed other in placed)
                if (!geometry.AreFootPlacementsSeparated(territory.Context.Type.MovementFoot, node, other.Territory.Context.Type.MovementFoot, other.Node))
                { compatible = false; break; }
            if (!compatible) continue;
            Vector2Int[] cells = geometry.GetFootSurfaceNodes(territory.Context.Type.MovementFoot, node).Select(n => Cell(map, n)).ToArray();
            if (cells.Any(plan.IsOccupied)) continue;
            string id = "ecology.resident." + territory.Id.ToString(CultureInfo.InvariantCulture) + "." + (placed.Count - before + 1).ToString("D3", CultureInfo.InvariantCulture);
            if (!plan.TryAdd(new MapPlacementReservation(id, MonsterPlacementService.MonsterRole, Cell(map, node), cells,
                MapPlacementOwnership.GenerationScoped), out _)) continue;
            placed.Add(new Placed { Territory = territory, Node = node, Id = id });
            if (placed.Count - before == territory.Assigned) return true;
        }
        return false;
    }

    private static int[] SelectSeeds(int seed, string key, MapData map, EcologyNavigationGraph graph,
        List<TypeContext> types, EcologyTerritoryRecipe recipe, int requested)
    {
        var seedComponent = new bool[graph.NodeCount];
        if (recipe.RejectUndersizedSeedComponents)
        {
            // A disconnected one-cell island scores as infinitely far from an
            // existing seed, but can never host the minimum group. Complete-foot
            // capacity remains independently certified later by Qualify.
            foreach (int[] component in Components(graph))
                if (component.Length >= recipe.MinimumGroup && types.Any(t => !t.Type.IsBoss && component.Count(n => !t.Blocked[n]) >= recipe.MinimumGroup))
                    foreach (int node in component) seedComponent[node] = true;
        }
        int[] candidates = Enumerable.Range(0, graph.NodeCount).Where(n => graph.IsLegal(n) &&
            (!recipe.RejectUndersizedSeedComponents || seedComponent[n]) && types.Any(t => !t.Type.IsBoss && !t.Blocked[n])).ToArray();
        var selected = new List<int>(); var nearest = Enumerable.Repeat(graph.NodeCount + 1, graph.NodeCount).ToArray();
        while (selected.Count < requested)
        {
            int best = -1; long bestPreference = long.MinValue; uint bestTie = uint.MaxValue;
            foreach (int node in candidates)
            {
                if (selected.Contains(node) || selected.Count > 0 && nearest[node] < 6) continue;
                Vector2Int local = Cell(map, node) - map.Origin;
                int q = SeedField(seed, key, local, recipe.SeedLatticeSpacing);
                long preference = selected.Count == 0 ? q : 4L * nearest[node] + 3L * q / 1023;
                uint tie = Score(seed, "eco.seed-tie.v1", key, local);
                if (preference > bestPreference || preference == bestPreference && (tie < bestTie || tie == bestTie && node < best))
                { best = node; bestPreference = preference; bestTie = tie; }
            }
            if (best < 0) break;
            selected.Add(best); int[] distances = Distances(graph, best);
            for (int node = 0; node < nearest.Length; node++) if (distances[node] >= 0) nearest[node] = Math.Min(nearest[node], distances[node]);
        }
        return selected.ToArray();
    }

    private static int SeedField(int seed, string key, Vector2Int local, int spacing)
    {
        int ix = local.x / spacing, iy = local.y / spacing, fx = local.x % spacing, fy = local.y % spacing;
        long a = Score(seed, "eco.seed-field.v1", key, new Vector2Int(ix, iy)) & 1023;
        long b = Score(seed, "eco.seed-field.v1", key, new Vector2Int(ix + 1, iy)) & 1023;
        long c = Score(seed, "eco.seed-field.v1", key, new Vector2Int(ix, iy + 1)) & 1023;
        long d = Score(seed, "eco.seed-field.v1", key, new Vector2Int(ix + 1, iy + 1)) & 1023;
        return checked((int)(((spacing - fx) * (spacing - fy) * a + fx * (spacing - fy) * b +
            (spacing - fx) * fy * c + fx * fy * d) / ((long)spacing * spacing)));
    }
    private static Territory Weighted(List<Territory> options, uint score)
    {
        options.Sort((a, b) => string.CompareOrdinal(a.Context.Type.TypeId, b.Context.Type.TypeId));
        long total = options.Sum(o => (long)o.Context.Type.Weight);
        if (total <= 0 || total > uint.MaxValue) throw Failure("NumericRangeExceeded");
        long choice = (long)((ulong)score % (ulong)total);
        foreach (Territory option in options) { if (choice < option.Context.Type.Weight) return option; choice -= option.Context.Type.Weight; }
        throw Failure("InvalidWeightedEcotypeChoice");
    }
    private static List<int[]> Components(EcologyNavigationGraph graph)
    {
        var visited = new bool[graph.NodeCount]; var queue = new int[graph.NodeCount]; var result = new List<int[]>();
        for (int root = 0; root < graph.NodeCount; root++)
        {
            if (!graph.IsLegal(root) || visited[root]) continue;
            int head = 0, tail = 0; queue[tail++] = root; visited[root] = true;
            while (head < tail)
            {
                int node = queue[head++];
                for (int d = 0; d < 4; d++)
                {
                    int next = graph.Neighbor(node, d);
                    if (next < 0 || visited[next] || !graph.CanMove(node, next) || !graph.CanMove(next, node)) continue;
                    visited[next] = true; queue[tail++] = next;
                }
            }
            var nodes = new int[tail]; Array.Copy(queue, nodes, tail); Array.Sort(nodes); result.Add(nodes);
        }
        return result;
    }
    private static int[] Distances(EcologyNavigationGraph graph, int root, int limit = int.MaxValue)
    {
        var result = Enumerable.Repeat(-1, graph.NodeCount).ToArray(); var queue = new int[graph.NodeCount];
        if (!graph.IsLegal(root)) return result;
        int head = 0, tail = 0; queue[tail++] = root; result[root] = 0;
        while (head < tail)
        {
            int node = queue[head++]; if (result[node] >= limit) continue;
            for (int d = 0; d < 4; d++)
            {
                int next = graph.Neighbor(node, d);
                if (next < 0 || result[next] >= 0 || !graph.CanMove(node, next) || !graph.CanMove(next, node)) continue;
                result[next] = checked(result[node] + 1); queue[tail++] = next;
            }
        }
        return result;
    }
    private static EcologyNavigationGraph Restrict(EcologyNavigationGraph source, bool[] legal)
    {
        var edges = new byte[source.NodeCount];
        for (int node = 0; node < source.NodeCount; node++)
        {
            legal[node] &= source.IsLegal(node); if (!legal[node]) continue;
            for (int d = 0; d < 4; d++)
            { int next = source.Neighbor(node, d); if (next >= 0 && legal[next] && source.CanMove(node, next)) edges[node] |= (byte)(1 << d); }
        }
        return new EcologyNavigationGraph(source.GenerationId, source.Origin, source.Width, source.Height,
            source.ProjectionIdentity, source.FootprintIdentity, legal, edges);
    }
    private static MapPlacementPlan CopyPlan(MapPlacementPlan source)
    {
        var plan = new MapPlacementPlan(source.GenerationId);
        foreach (MapPlacementReservation r in source.Reservations)
            if (!plan.TryAdd(new MapPlacementReservation(r.LogicalObjectId, r.Role, r.AnchorCell, r.OccupiedCells, r.Ownership), out _))
                throw Failure("InvalidStaticPlacementBase");
        return plan;
    }
    private static void AddRegion(MapEcologyLayout.Builder builder, int id, string kind, string type, int[] surface, int[] buffer, int capacity, int population)
    { if (!builder.TryAddRegion(id, kind, type, surface, buffer, capacity, population)) throw Failure(builder.LastFailure); }
    private static Vector2Int Cell(MapData map, int node) => map.Origin + new Vector2Int(node % map.Width, node / map.Width);
    private static int Node(MapData map, Vector2Int cell) => (cell.y - map.Origin.y) * map.Width + cell.x - map.Origin.x;
    private static int Manhattan(MapData map, int a, int b) => Math.Abs(a % map.Width - b % map.Width) + Math.Abs(a / map.Width - b / map.Width);
    private static uint Score(int seed, string domain, string key, Vector2Int local) => MapSemanticPlacementPrimitives.StableScore(seed, domain, key, local);
    private static string Hash(Action<BinaryWriter> write)
    {
        using (var bytes = new MemoryStream())
        {
            using (var writer = new BinaryWriter(bytes, System.Text.Encoding.UTF8, true)) write(writer);
            using (SHA256 sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes.ToArray())).Replace("-", "").ToLowerInvariant();
        }
    }
    private static MapPlanningException Failure(string code) => new MapPlanningException(code, "Ecology territory candidate failed: " + code, false);
}

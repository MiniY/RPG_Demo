using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>Versioned extension for actual base-Mountain faces omitted by R3.</summary>
public static class MapSurfaceV4Policy
{
    public const string Version = "surface-v4-native-stair-candidate-water-side-roots-v2";

    public static bool HasNativeStairAccess(MapData map, MapGenerationSettings settings,
        MapTilemapRenderer terrain, MapSimpleDecorationRenderer decoration, Collider2D foot)
    {
        var geometry=MapCandidateCollisionGeometry.Capture(Guid.NewGuid(),map,new MapSimpleDecorationData(),settings,terrain,decoration);
        var graph=geometry.BuildNavigationGraph(foot,0f);
        int Node(Vector2Int cell)=>(cell.y-map.Origin.y)*map.Width+cell.x-map.Origin.x;
        var reachable=graph.BuildReturnField(new[]{Node(map.SpawnCell)});
        foreach(var stair in map.Stairs)
        {
            int lower=Node(stair.LowerCell),upper=Node(stair.UpperCell);
            if(!graph.IsLegal(lower)||!graph.IsLegal(upper)||reachable.Distances[lower]<0||reachable.Distances[upper]<0||
                !graph.CanMove(lower,upper)||!graph.CanMove(upper,lower))return false;
        }
        return true;
    }

    public static MapRemediationSurfacePlan Build(MapData map, MapTerrainV2Palette palette, MapGenerationSettings settings)
    {
        var plan = MapRemediationSurfacePolicy.Build(map, palette);
        if (settings is MapSurfaceV4Settings)
        {
            AppendLegacyFaces(map, settings, plan);
            plan.ExcludeDryFoamCells = false;
        }
        return plan;
    }

    public static MapRemediationSurfacePlan LegacyFaces(MapData map, MapGenerationSettings settings)
    {
        var plan = new MapRemediationSurfacePlan();
        AppendLegacyFaces(map, settings, plan);
        return plan;
    }

    private static void AppendLegacyFaces(MapData map, MapGenerationSettings settings, MapRemediationSurfacePlan plan)
    {
        for (int y = map.Origin.y; y < map.Origin.y + map.Height; y++)
        for (int x = map.Origin.x; x < map.Origin.x + map.Width; x++)
        {
            var upper = new Vector2Int(x, y); var lower = upper + Vector2Int.down;
            if (!TerrainTopology.UsesElevation(map.GetCell(upper).terrainType) || !map.IsInside(lower) ||
                !map.IsWalkable(lower) || map.GetElevation(lower) != 0) continue;
            // This is the existing RenderBase command and its actual retained Tile,
            // not a box inferred from the terrain cell or an invented Sprite.
            var tile = settings.elevationAutotileSet.GetSouthFaceTile(TerrainTopology.GetElevationBoundaryMask(map, upper)) as Tile;
            if (tile != null) plan.Add(0, lower, tile);
        }
    }

    public static MapSimpleDecorationData FilterDecorations(MapData map, MapSimpleDecorationData source,
        MapGenerationSettings settings, MapTerrainV2Palette palette)
    {
        if (!(settings is MapSurfaceV4Settings)) return MapRemediationSurfacePolicy.FilterDecorations(map, source, settings, palette);
        var plan = Build(map, palette, settings); var accepted = new MapSimpleDecorationData();
        foreach (var placement in source.Placements)
        {
            var variant = settings.simpleDecorationPalette.FindVariant(placement.VariantId);
            if (variant == null) throw new InvalidOperationException("MissingSurfaceV4DecorationVariant");
            if (MapRemediationSurfacePolicy.SupportsDecoration(map, plan, placement, variant)) accepted.Add(placement, variant);
        }
        return accepted;
    }

    public static void Project(MapData map, MapTilemapRenderer terrain, MapGenerationSettings settings)
    {
        if (!(settings is MapSurfaceV4Settings)) return;
        Transform parent = terrain.TerrainCollisionTilemap.transform.parent;
        Transform existing = parent.Find("GeneratedLegacyMountainFaces_R4");
        PolygonCollider2D collider;
        if (existing == null)
        {
            var holder = new GameObject("GeneratedLegacyMountainFaces_R4");
            holder.transform.SetParent(parent, false); holder.layer = terrain.TerrainCollisionTilemap.gameObject.layer;
            holder.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Static;
            collider = holder.AddComponent<PolygonCollider2D>();
        }
        else collider = existing.GetComponent<PolygonCollider2D>();
        var paths = LegacyFaces(map, settings).WorldPolygons(terrain.WaterBaseTilemap);
        collider.pathCount = paths.Count;
        for (int i = 0; i < paths.Count; i++)
        {
            var local = new Vector2[paths[i].Length];
            for (int j = 0; j < local.Length; j++) local[j] = collider.transform.InverseTransformPoint(paths[i][j]);
            collider.SetPath(i, local);
        }
        MapSurfaceV4Visuals.Project(map, terrain, (MapSurfaceV4Settings)settings);
    }

    public static void Clear(MapTilemapRenderer terrain)
    {
        Transform parent = terrain.TerrainCollisionTilemap.transform.parent;
        Transform legacy = parent.Find("GeneratedLegacyMountainFaces_R4");
        if (legacy != null) legacy.GetComponent<PolygonCollider2D>().pathCount = 0;
        Transform visuals = parent.Find("GeneratedSurfaceVisuals_R4");
        if (visuals != null) visuals.GetComponent<MapSurfaceV4Visuals>().ClearOwned();
    }
}

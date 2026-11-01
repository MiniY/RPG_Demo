using System;
using System.Collections.Generic;
using UnityEngine;

public static class MapSpatialDecorationFilter
{
    public static MapSimpleDecorationData Filter(MapSpatialContext space, MapSimpleDecorationData source, MapGenerationSettings settings,
        MapTilemapRenderer terrain, MapSimpleDecorationRenderer decoration)
    {
        var empty = new MapSimpleDecorationData();
        var reserved = MapStaticPlacementPlanner.Build(space.GenerationId, space.Map, empty, settings);
        var geometry = MapCandidateCollisionGeometry.Capture(space.GenerationId, space.Map, empty, settings, terrain, decoration, space);
        var result = new MapSimpleDecorationData();
        foreach (var placement in source.Placements)
        {
            var variant = settings.simpleDecorationPalette.FindVariant(placement.VariantId);
            if (variant == null) throw new InvalidOperationException("UnknownVNextDecorationDependency");
            bool legal = true;
            // The retained decoration generator does not know lateral VNext approaches.
            // Reuse the strict planner's declarations before any entity plan is built.
            foreach (var offset in variant.CollisionOffsets)
                if (reserved.IsOccupied(placement.AnchorCell + offset)) { legal = false; break; }
            if (!legal) continue;
            // The existing variant explicitly declares its occupied cells; artwork/canopy overlap is not used as ground shape.
            for (int y = variant.FootprintMinimum.y; legal && y <= variant.FootprintMaximum.y; y++)
            for (int x = variant.FootprintMinimum.x; legal && x <= variant.FootprintMaximum.x; x++)
            {
                var cell = placement.AnchorCell + new Vector2Int(x, y);
                if (!space.Map.IsInside(cell) || !space.Map.IsWalkable(cell)) { legal = false; break; }
                Vector2 centre = terrain.Coordinates.CellToWorld(cell);
                Vector2 dx = ((Vector2)terrain.Coordinates.CellToWorld(cell + Vector2Int.right) - centre) / 2;
                Vector2 dy = ((Vector2)terrain.Coordinates.CellToWorld(cell + Vector2Int.up) - centre) / 2;
                legal = geometry.IsWorldGroundPolygonClear(new[] { centre - dx - dy, centre + dx - dy, centre + dx + dy, centre - dx + dy });
            }
            if (legal) result.Add(placement, variant);
        }
        return result;
    }
}

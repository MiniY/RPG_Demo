using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>R3 surface commands shared by native projection, pending geometry and root support.</summary>
public sealed class MapRemediationSurfacePlan
{
    public readonly struct Face
    {
        public readonly int Level;
        public readonly Vector2Int Cell;
        public readonly Tile Tile;
        public Face(int level, Vector2Int cell, Tile tile) { Level = level; Cell = cell; Tile = tile; }
    }
    private readonly Dictionary<(int, Vector2Int), Tile> tiles = new Dictionary<(int, Vector2Int), Tile>();
    private readonly HashSet<Vector2Int> faceCells = new HashSet<Vector2Int>();
    private readonly HashSet<Vector2Int> stairVisualCells = new HashSet<Vector2Int>();
    private readonly List<Face> faces = new List<Face>();
    public IReadOnlyList<Face> Faces => faces;
    internal bool ExcludeDryFoamCells = true;
    internal void Add(int level, Vector2Int cell, Tile tile)
    { tiles.Add((level, cell), tile); faceCells.Add(cell); faces.Add(new Face(level, cell, tile)); }
    public Tile FaceAt(int level, Vector2Int cell) => tiles.TryGetValue((level, cell), out Tile tile) ? tile : null;
    internal void AddStairVisual(Vector2Int cell) => stairVisualCells.Add(cell);

    public bool SupportsRoot(MapData map, Vector2Int cell, int elevation)
    {
        if (!map.IsInside(cell) || !map.IsWalkable(cell) || map.GetElevation(cell) != elevation || faceCells.Contains(cell) || stairVisualCells.Contains(cell)) return false;
        foreach (MapStairConnection stair in map.Stairs)
            if (cell == stair.LowerCell || cell == stair.UpperCell) return false;
        // The existing foam is drawn on dry H0 cells immediately beside water. Higher
        // tops cover that base layer, so their valid ground is not removed by adjacency.
        if (ExcludeDryFoamCells && elevation == 0)
            foreach (Vector2Int direction in MapRemediationSurfacePolicy.Cardinal)
            {
                Vector2Int next = cell + direction;
                if (map.IsInside(next) && TerrainTopology.IsWater(map.GetCell(next).terrainType)) return false;
            }
        return true;
    }

    public List<Vector2[]> WorldPolygons(Tilemap ground)
    {
        var result = new List<Vector2[]>(); var points = new List<Vector2>();
        foreach (Face face in faces)
        {
            Sprite sprite = face.Tile.sprite;
            if (face.Tile.transform != Matrix4x4.identity || sprite == null || sprite.GetPhysicsShapeCount() == 0)
                throw new MapPlanningException("MissingProjectedFaceOutline", "R3 requires the imported native outline of every actual face command.", false);
            Vector3 centre = ground.GetCellCenterWorld(new Vector3Int(face.Cell.x, face.Cell.y, 0));
            for (int path = 0; path < sprite.GetPhysicsShapeCount(); path++)
            {
                points.Clear(); sprite.GetPhysicsShape(path, points);
                if (points.Count < 3) throw new MapPlanningException("InvalidProjectedFaceOutline", "R3 native face outline has fewer than three vertices.", false);
                var world = new Vector2[points.Count];
                for (int p = 0; p < points.Count; p++) world[p] = centre + ground.transform.TransformVector(points[p]);
                result.Add(world);
            }
        }
        return result;
    }
}

public static class MapRemediationSurfacePolicy
{
    public const string Version = "r3-native-face-root-support-v1";
    internal static readonly Vector2Int[] Cardinal = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };

    public static MapRemediationSurfacePlan Build(MapData map, MapTerrainV2Palette palette)
    {
        if (map == null || palette == null || !palette.IsComplete) throw new ArgumentException("A complete actual surface palette is required.");
        var result = new MapRemediationSurfacePlan();
        for (int y = map.Origin.y; y < map.Origin.y + map.Height; y++)
        for (int x = map.Origin.x; x < map.Origin.x + map.Width; x++)
        {
            var upper = new Vector2Int(x, y); int level = map.GetElevation(upper);
            Vector2Int lower = upper + Vector2Int.down;
            if (level == 0 || !map.IsInside(lower) || map.GetElevation(lower) >= level || map.IsStairTransition(upper, lower)) continue;
            var mask = TerrainBoundaryMask.None;
            if (!Same(upper + Vector2Int.up)) mask |= TerrainBoundaryMask.North;
            if (!Same(upper + Vector2Int.right)) mask |= TerrainBoundaryMask.East;
            if (!Same(lower)) mask |= TerrainBoundaryMask.South;
            if (!Same(upper + Vector2Int.left)) mask |= TerrainBoundaryMask.West;
            bool Same(Vector2Int cell) => map.IsInside(cell) && map.GetElevation(cell) == level;
            Tile tile = palette.GrassAndCliff.GetSouthFaceTile(mask) as Tile;
            if (tile != null) result.Add(level, lower, tile);
        }
        foreach (var stair in map.Stairs)
        {
            if (stair.UpperCell != stair.LowerCell + Vector2Int.up ||
                map.IsStairTransition(stair.LowerCell + Vector2Int.left, stair.UpperCell + Vector2Int.left)) continue;
            foreach (var shoulder in new[] { stair.UpperCell + Vector2Int.left, stair.UpperCell + Vector2Int.right * 2 })
            { result.AddStairVisual(shoulder); result.AddStairVisual(shoulder + Vector2Int.down); }
        }
        return result;
    }

    public static bool SupportsDecoration(MapData map, MapRemediationSurfacePlan surface,
        MapSimpleDecorationPlacement placement, MapSimpleDecorationVariant variant)
    {
        int level = map.GetElevation(placement.AnchorCell);
        if (!surface.SupportsRoot(map, placement.AnchorCell, level)) return false;
        foreach (var part in variant.TileParts)
            if (part.RenderLayer == MapSimpleDecorationRenderLayer.Ground &&
                !surface.SupportsRoot(map, placement.AnchorCell + part.Offset, level)) return false;
        foreach (Vector2Int offset in variant.CollisionOffsets)
            if (!surface.SupportsRoot(map, placement.AnchorCell + offset, level)) return false;
        return true;
    }

    public static MapSimpleDecorationData FilterDecorations(MapData map, MapSimpleDecorationData source,
        MapGenerationSettings settings, MapTerrainV2Palette palette)
    {
        var surface = Build(map, palette); var accepted = new MapSimpleDecorationData();
        foreach (var placement in source.Placements)
        {
            var variant = settings.simpleDecorationPalette.FindVariant(placement.VariantId);
            if (variant == null) throw new ArgumentException("A retained decoration variant is missing.");
            if (SupportsDecoration(map, surface, placement, variant)) accepted.Add(placement, variant);
        }
        return accepted;
    }
}

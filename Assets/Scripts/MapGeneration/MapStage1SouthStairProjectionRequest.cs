using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>An explicit, non-persistent visual request for one existing Stage1 south stair pair.</summary>
public sealed class MapStage1SouthStairProjectionRequest
{
    private readonly MapData sourceMap;
    private readonly MapStairConnection[] selectedStairs;
    private readonly Tile[] variants;

    public MapStage1SouthStairProjectionRequest(MapData map,
        IReadOnlyList<MapStairConnection> selectedPair, Tile a, Tile b, Tile c, Tile d)
    {
        sourceMap = map;
        selectedStairs = new MapStairConnection[selectedPair?.Count ?? 0];
        for (int i = 0; i < selectedStairs.Length; i++)
            selectedStairs[i] = selectedPair[i];
        variants = new[] { a, b, c, d };
    }

    /// <summary>Uses the same preparation as rendering; it grants no topology or gameplay readiness waiver.</summary>
    public void Validate(MapData map)
    {
        Prepare(map);
    }

    internal Projection Prepare(MapData map)
    {
        if (map == null || !ReferenceEquals(map, sourceMap))
            throw Error("SouthStairMapMismatch", "The request belongs to a different MapData instance.");
        if (selectedStairs.Length != 2)
            throw Error("Stage1AtomicStairInvalid", "A native Stage1 stair requires exactly two existing lanes.");

        var pair = (MapStairConnection[])selectedStairs.Clone();
        Array.Sort(pair, (left, right) => left.LowerCell.x.CompareTo(right.LowerCell.x));
        foreach (MapStairConnection stair in pair)
        {
            if (stair.UpperCell == stair.LowerCell + Vector2Int.down)
                throw Error("SouthStairNorthEdgeForbidden", "North-edge stairs have no approved source art.");
            if (stair.UpperCell != stair.LowerCell + Vector2Int.up)
                throw Error("SouthStairDirectionUnsupported", "This request only projects south-facing stairs.");
            if (stair.VisualPlacement != MapStairVisualPlacement.Legacy ||
                stair.VisualSide != MapStairVisualSide.Both)
                throw Error("SouthStairVisualPlacementUnsupported", "Existing side/outboard compositions keep their own projection.");
            bool found = false;
            foreach (MapStairConnection actual in map.Stairs)
                if (SameConnection(actual, stair)) { found = true; break; }
            if (!found)
                throw Error("SouthStairPortNotInMap", "The requested connection is not in the current MapData.");
        }
        if (pair[1].LowerCell != pair[0].LowerCell + Vector2Int.right ||
            pair[1].VisualComposition != pair[0].VisualComposition)
            throw Error("Stage1AtomicStairInvalid", "Both lanes must be one adjacent, consistent pair.");
        if (map.IsStairTransition(pair[0].LowerCell + Vector2Int.left, pair[0].UpperCell + Vector2Int.left) ||
            map.IsStairTransition(pair[1].LowerCell + Vector2Int.right, pair[1].UpperCell + Vector2Int.right))
            throw Error("SouthStairWidthUnsupported", "Selecting two lanes from a wider group is not supported.");

        int level = map.GetElevation(pair[0].UpperCell);
        foreach (MapStairConnection stair in pair)
        {
            if (map.GetElevation(stair.UpperCell) != level ||
                map.GetElevation(stair.LowerCell) != level - 1 ||
                !map.IsWalkable(stair.LowerCell) || !map.IsWalkable(stair.UpperCell))
                throw Error("Stage1AtomicStairInvalid", "Both lanes require the same real one-level rise.");
            if (!map.CanTraverse(stair.LowerCell + Vector2Int.down, stair.LowerCell) ||
                !map.CanTraverse(stair.UpperCell, stair.UpperCell + Vector2Int.up))
                throw Error("Stage1StairOpeningBlocked", "Both real stair approaches must remain open.");
        }
        ValidateSources();

        var footprint = new HashSet<Vector2Int>();
        foreach (MapStairConnection stair in pair)
        {
            footprint.Add(stair.LowerCell + Vector2Int.up);
            footprint.Add(stair.UpperCell + Vector2Int.up);
        }
        var pieces = new List<Placement>();
        foreach (MapStairConnection stair in pair)
        foreach (Vector2Int logical in new[] { stair.LowerCell, stair.UpperCell })
        {
            Vector2Int visual = logical + Vector2Int.up;
            if (!map.IsInside(visual))
                throw Error("SouthStairVisualCellOutsideMap", "The shifted visual cell is outside MapData.");
            Neighbor left = ResolveNeighbor(map, visual + Vector2Int.left, level, footprint);
            Neighbor right = ResolveNeighbor(map, visual + Vector2Int.right, level, footprint);
            int variant = left == Neighbor.Ground ? (right == Neighbor.Ground ? 3 : 0) :
                (right == Neighbor.Ground ? 2 : 1);
            pieces.Add(new Placement(logical, visual, variants[variant]));
        }
        return new Projection(map, pair[0], level, pieces.AsReadOnly());
    }

    private enum Neighbor { Ground, Highland, CliffFaceProjection, ParallelStair }

    private static Neighbor ResolveNeighbor(MapData map, Vector2Int cell, int level,
        HashSet<Vector2Int> footprint)
    {
        if (footprint.Contains(cell)) return Neighbor.ParallelStair;
        if (!map.IsInside(cell) || !map.IsWalkable(cell))
            throw Error("SouthStairNeighborUnsupported", "No supported walkable terrain role at " + cell);
        int height = map.GetElevation(cell);
        if (height >= level) return Neighbor.Highland;
        if (height != level - 1)
            throw Error("SouthStairNeighborUnsupported", "Unsupported adjacent height at " + cell);
        Vector2Int north = cell + Vector2Int.up;
        if (map.IsInside(north) && map.GetElevation(north) == level && !map.IsStairTransition(cell, north))
            return Neighbor.CliffFaceProjection;
        return Neighbor.Ground;
    }

    private void ValidateSources()
    {
        Texture2D texture = null;
        for (int column = 0; column < variants.Length; column++)
        {
            Tile tile = variants[column];
            Sprite sprite = tile != null ? tile.sprite : null;
            if (sprite == null)
                throw Error("SouthStairSpriteMissing", "A native south-stair Tile has no Sprite.");
            if (column == 0) texture = sprite.texture;
            // Callers bind the audited assets. Runtime verifies their source roles without Editor APIs.
            if (sprite.texture != texture || texture.width != 256 || texture.height != 512 ||
                sprite.packed || sprite.rect != new Rect(column * 64, 0, 64, 64) ||
                sprite.pixelsPerUnit != 64 || sprite.pivot != new Vector2(32, 32))
                throw Error("SouthStairSpriteRoleInvalid", "The a/b/c/d source rectangles do not match the audited sheet.");
            if (tile.transform != Matrix4x4.identity || tile.color != Color.white)
                throw Error("SouthStairSourceTransformUnsupported", "Native source Tiles must not be transformed or tinted.");
        }
    }

    private static bool SameConnection(MapStairConnection left, MapStairConnection right)
    {
        return left.LowerCell == right.LowerCell && left.UpperCell == right.UpperCell &&
            left.VisualComposition == right.VisualComposition && left.VisualSide == right.VisualSide &&
            left.VisualPlacement == right.VisualPlacement;
    }

    private static MapPlanningException Error(string code, string message)
    {
        return new MapPlanningException(code, message, false);
    }

    internal readonly struct Placement
    {
        internal Placement(Vector2Int logical, Vector2Int visual, Tile tile)
        { Logical = logical; Visual = visual; Tile = tile; }
        internal Vector2Int Logical { get; }
        internal Vector2Int Visual { get; }
        internal Tile Tile { get; }
    }

    internal sealed class Projection
    {
        private readonly MapStairConnection left;
        internal Projection(MapData map, MapStairConnection leftLane, int level,
            IReadOnlyList<Placement> pieces)
        { Map = map; left = leftLane; Level = level; Pieces = pieces; }
        internal MapData Map { get; }
        internal int Level { get; }
        internal IReadOnlyList<Placement> Pieces { get; }
        internal bool SelectsLeft(MapStairConnection stair) => SameConnection(left, stair);
    }
}

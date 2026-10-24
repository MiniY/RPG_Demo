using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Checks a south-facing, two-lane V2.1 stair against a frozen upper core.
/// The core is the MapData snapshot from before the stair footprint was added.
/// This does not change V1/V2 generation or maintain a second terrain state.
/// </summary>
public static class MapOutboardStairGeometry
{
    public sealed class FrozenCore
    {
        private readonly int[,] elevations;
        private readonly bool[,] walkable;

        internal FrozenCore(MapData source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            Width = source.Width;
            Height = source.Height;
            Origin = source.Origin;
            elevations = new int[Width, Height];
            walkable = new bool[Width, Height];
            for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
            {
                Vector2Int cell = Origin + new Vector2Int(x, y);
                elevations[x, y] = source.GetElevation(cell);
                walkable[x, y] = source.IsWalkable(cell);
            }
        }

        public int Width { get; }
        public int Height { get; }
        public Vector2Int Origin { get; }

        public bool IsInside(Vector2Int cell) =>
            cell.x >= Origin.x && cell.x < Origin.x + Width &&
            cell.y >= Origin.y && cell.y < Origin.y + Height;

        public int GetElevation(Vector2Int cell)
        {
            if (!IsInside(cell)) throw new ArgumentOutOfRangeException(nameof(cell));
            return elevations[cell.x - Origin.x, cell.y - Origin.y];
        }

        public bool IsWalkable(Vector2Int cell)
        {
            if (!IsInside(cell)) throw new ArgumentOutOfRangeException(nameof(cell));
            return walkable[cell.x - Origin.x, cell.y - Origin.y];
        }
    }

    public static FrozenCore CaptureCore(MapData originalCore) =>
        new FrozenCore(originalCore);

    // A visual gate derived from the existing two-lane connection. This never
    // changes elevation, traversal or collider state, and creates no trigger object.
    public static bool TryGetWestSideVisualTransition(MapData map, MapCoordinateBoundary coordinates,
        Bounds body, Bounds visual, bool retainingPriority, out int upperElevation, out Bounds gate)
    {
        upperElevation = 0;
        gate = default;
        if (map == null || coordinates == null) return false;
        foreach (MapStairConnection stair in map.Stairs)
        {
            if (stair.VisualPlacement != MapStairVisualPlacement.WestSideAttached ||
                stair.UpperCell != stair.LowerCell + Vector2Int.right ||
                map.IsStairTransition(stair.LowerCell + Vector2Int.down, stair.UpperCell + Vector2Int.down))
                continue;
            Vector2Int lower = stair.LowerCell;
            bool companion = false;
            foreach (MapStairConnection other in map.Stairs)
                if (other.LowerCell == lower + Vector2Int.up &&
                    other.UpperCell == stair.UpperCell + Vector2Int.up &&
                    other.VisualPlacement == stair.VisualPlacement &&
                    map.CanTraverse(other.LowerCell, other.UpperCell)) companion = true;
            if (!companion || !map.CanTraverse(lower, stair.UpperCell)) continue;
            Vector3 low = coordinates.CellToWorld(lower);
            Vector3 high = coordinates.CellToWorld(stair.UpperCell);
            float width = high.x - low.x;
            float height = coordinates.CellToWorld(lower + Vector2Int.up).y - low.y;
            if (width <= 0f || height <= 0f) continue;
            float west = low.x - width * 0.5f;
            float east = high.x + width * 0.5f;
            float south = low.y - height * 0.5f;
            float north = south + height * 2f;
            gate = new Bounds(new Vector3((west + east) * 0.5f, (south + north) * 0.5f, 0f),
                new Vector3(east - west, north - south, 1f));
            // Full body must fit between the adjacent closed cliff edges. A
            // small release margin prevents jitter while backing out of the gate.
            float release = retainingPriority ? body.size.x * 0.1f : 0f;
            if (body.min.y < south || body.max.y > north ||
                body.center.x < west - release || body.center.x > east ||
                visual.max.x < (low.x + high.x) * 0.5f) continue;
            upperElevation = map.GetElevation(stair.UpperCell);
            return true;
        }
        return false;
    }

    public static void ValidateWestSideAttachment(MapData map, Vector2Int bottomLower)
    {
        if (map == null) throw new ArgumentNullException(nameof(map));
        MapStairConnection first = default;
        for (int lane = 0; lane < 2; lane++)
        {
            Vector2Int lower = bottomLower + Vector2Int.up * lane;
            Vector2Int upper = lower + Vector2Int.right;
            bool found = false;
            foreach (MapStairConnection stair in map.Stairs)
            {
                if (stair.LowerCell != lower || stair.UpperCell != upper) continue;
                if (lane == 0) first = stair;
                if (stair.VisualPlacement != MapStairVisualPlacement.WestSideAttached ||
                    stair.VisualSide != MapStairVisualSide.LeftOnly ||
                    stair.VisualComposition != MapStairVisualComposition.UpperAndLower ||
                    stair.VisualComposition != first.VisualComposition)
                    throw Invalid("WestSidePairVisualMismatch");
                found = true;
            }
            if (!found || !map.IsWalkable(lower) || !map.IsWalkable(upper) ||
                map.GetElevation(lower) != 0 || map.GetElevation(upper) != 1 ||
                !map.CanTraverse(lower, upper) ||
                !map.CanTraverse(lower, lower + Vector2Int.left) ||
                !map.CanTraverse(upper, upper + Vector2Int.right))
                throw Invalid("WestSidePassageInvalid");
        }
        if (map.IsStairTransition(bottomLower + Vector2Int.down,
                bottomLower + Vector2Int.down + Vector2Int.right) ||
            map.IsStairTransition(bottomLower + Vector2Int.up * 2,
                bottomLower + Vector2Int.up * 2 + Vector2Int.right))
            throw Invalid("WestSidePairWidthInvalid");
    }

    // The passage stays inside the existing highland. The shoulder tiles are
    // visual boundary pieces, just as a cliff face projects into a lower cell.
    public static void ValidateBoundaryShoulders(MapData map, Vector2Int leftLower)
    {
        if (map == null) throw new ArgumentNullException(nameof(map));
        Vector2Int upper = leftLower + Vector2Int.up;
        int level = map.IsInside(upper) ? map.GetElevation(upper) : 0;
        if (level == 0) throw Invalid("BoundaryUpperMissing");
        MapStairConnection first = default;
        for (int lane = 0; lane < 2; lane++)
        {
            Vector2Int lower = leftLower + Vector2Int.right * lane;
            Vector2Int high = lower + Vector2Int.up;
            bool found = false;
            foreach (MapStairConnection stair in map.Stairs)
            {
                if (stair.LowerCell != lower || stair.UpperCell != high) continue;
                if (lane == 0) first = stair;
                if (stair.VisualPlacement != MapStairVisualPlacement.BoundaryShoulders ||
                    stair.VisualSide != MapStairVisualSide.Both ||
                    stair.VisualComposition != first.VisualComposition)
                    throw Invalid("BoundaryPairVisualMismatch");
                found = true;
            }
            if (!found || !map.IsWalkable(lower) ||
                map.GetElevation(lower) != level - 1 ||
                !map.CanTraverse(lower, high) ||
                !map.CanTraverse(high, high + Vector2Int.up) ||
                map.GetElevation(high + Vector2Int.up) != level ||
                !map.CanTraverse(lower, lower + Vector2Int.down))
                throw Invalid("BoundaryPassageInvalid");
        }
        if (map.IsStairTransition(leftLower + Vector2Int.left, upper + Vector2Int.left) ||
            map.IsStairTransition(leftLower + Vector2Int.right * 2, upper + Vector2Int.right * 2))
            throw Invalid("BoundaryPairWidthInvalid");
        foreach (Vector2Int shoulder in new[] { upper + Vector2Int.left, upper + Vector2Int.right * 2 })
            if (!map.IsInside(shoulder) || !map.IsInside(shoulder + Vector2Int.down) ||
                !map.IsWalkable(shoulder + Vector2Int.down) ||
                map.GetElevation(shoulder + Vector2Int.down) != level - 1)
                throw Invalid("BoundaryShoulderLandingInvalid");
    }

    public sealed class Result
    {
        internal Result(Vector2Int[] attachment, Vector2Int[] upper,
            Vector2Int[] lower, Vector2Int[] footprint, Vector2Int[] added,
            int depth, int cornerDistance, int segmentStart, int segmentEnd)
        {
            AttachmentCells = attachment;
            UpperCells = upper;
            LowerLandingCells = lower;
            EffectiveFootprint = footprint;
            AddedElevatedCells = added;
            OutwardDepthCells = depth;
            CornerDistanceCells = cornerDistance;
            SegmentStartX = segmentStart;
            SegmentEndX = segmentEnd;
        }

        public IReadOnlyList<Vector2Int> AttachmentCells { get; }
        public IReadOnlyList<Vector2Int> UpperCells { get; }
        public IReadOnlyList<Vector2Int> LowerLandingCells { get; }
        public IReadOnlyList<Vector2Int> EffectiveFootprint { get; }
        public IReadOnlyList<Vector2Int> AddedElevatedCells { get; }
        public int OutwardDepthCells { get; }
        public int CornerDistanceCells { get; }
        public int SegmentStartX { get; }
        public int SegmentEndX { get; }
    }

    public static Result Analyze(FrozenCore frozenCore, MapData candidate,
        Vector2Int leftLower)
    {
        if (frozenCore == null || candidate == null ||
            frozenCore.Width != candidate.Width ||
            frozenCore.Height != candidate.Height ||
            frozenCore.Origin != candidate.Origin)
            throw Invalid("CoreSnapshotMismatch");

        var lower = new[] { leftLower, leftLower + Vector2Int.right };
        var upper = new[] { lower[0] + Vector2Int.up,
            lower[1] + Vector2Int.up };
        if (candidate.Stairs.Count != 2)
            throw Invalid("AtomicPairRequired");
        foreach (MapStairConnection stair in candidate.Stairs)
            if (stair.UpperCell != upper[0] && stair.UpperCell != upper[1] ||
                stair.LowerCell != stair.UpperCell + Vector2Int.down)
                throw Invalid("AtomicPairDirectionMismatch");
        if (candidate.Stairs[0].VisualComposition !=
                candidate.Stairs[1].VisualComposition ||
            candidate.Stairs[0].VisualSide != candidate.Stairs[1].VisualSide ||
            candidate.Stairs[0].VisualPlacement != candidate.Stairs[1].VisualPlacement)
            throw Invalid("AtomicPairVisualMismatch");
        if (candidate.Stairs[0].VisualPlacement != MapStairVisualPlacement.Outboard ||
            candidate.Stairs[0].VisualSide == MapStairVisualSide.Both)
            throw Invalid("OutboardVisualConnectionRequired");

        int level = candidate.GetElevation(upper[0]);
        if (level <= 0 || candidate.GetElevation(upper[1]) != level)
            throw Invalid("AtomicPairHeightMismatch");
        for (int lane = 0; lane < 2; lane++)
        {
            if (!candidate.IsInside(lower[lane] + Vector2Int.down) ||
                !candidate.IsWalkable(lower[lane]) ||
                !candidate.IsWalkable(lower[lane] + Vector2Int.down) ||
                candidate.GetElevation(lower[lane]) != level - 1 ||
                candidate.GetElevation(lower[lane] + Vector2Int.down) != level - 1)
                throw Invalid("LowerLandingInvalid");
            if (!candidate.IsStairTransition(lower[lane], upper[lane]) ||
                !candidate.CanTraverse(lower[lane], upper[lane]))
                throw Invalid("TopOpeningInvalid");
            if (frozenCore.GetElevation(upper[lane]) >= level)
                throw Invalid("StairInsideFrozenCore");
        }

        var added = new List<Vector2Int>();
        for (int y = candidate.Origin.y; y < candidate.Origin.y + candidate.Height; y++)
        for (int x = candidate.Origin.x; x < candidate.Origin.x + candidate.Width; x++)
        {
            var cell = new Vector2Int(x, y);
            int before = frozenCore.GetElevation(cell);
            int after = candidate.GetElevation(cell);
            if (before > after)
                throw Invalid("FrozenCoreCellRemoved");
            if (before < after)
                added.Add(cell);
        }

        int[] roots = new int[2];
        var footprint = new List<Vector2Int>();
        for (int lane = 0; lane < 2; lane++)
        {
            int x = upper[lane].x;
            int y = upper[lane].y;
            while (candidate.IsInside(new Vector2Int(x, y)) &&
                   frozenCore.GetElevation(new Vector2Int(x, y)) < level)
            {
                var cell = new Vector2Int(x, y);
                if (!candidate.IsWalkable(cell) ||
                    candidate.GetElevation(cell) != level)
                    throw Invalid("OutboardPathInterrupted");
                footprint.Add(cell);
                y++;
            }
            var root = new Vector2Int(x, y);
            if (!candidate.IsInside(root) ||
                frozenCore.GetElevation(root) != level ||
                candidate.GetElevation(root) != level ||
                !frozenCore.IsWalkable(root) || !candidate.IsWalkable(root))
                throw Invalid("UpperCoreNotConnected");
            roots[lane] = y;
        }
        if (roots[0] != roots[1])
            throw Invalid("AtomicPairAttachmentMismatch");
        int depth = roots[0] - upper[0].y;
        // A single new H1 row still read as a cliff cutout in the Unity probe.
        // Two rows produce a distinct outboard terrain footprint with these tiles.
        if (depth < 2)
            throw Invalid("VisibleOutboardDepthTooSmall");

        int rootY = roots[0];
        bool BoundaryAt(int x) =>
            frozenCore.IsInside(new Vector2Int(x, rootY)) &&
            frozenCore.IsInside(new Vector2Int(x, rootY - 1)) &&
            frozenCore.GetElevation(new Vector2Int(x, rootY)) == level &&
            frozenCore.GetElevation(new Vector2Int(x, rootY - 1)) < level;
        if (!BoundaryAt(upper[0].x) || !BoundaryAt(upper[1].x))
            throw Invalid("AttachmentNotOnSideSegment");
        int start = upper[0].x;
        int end = upper[1].x;
        while (BoundaryAt(start - 1)) start--;
        while (BoundaryAt(end + 1)) end++;
        int cornerDistance = Mathf.Min(upper[0].x - start,
            end - upper[1].x);
        return new Result(new[] { new Vector2Int(upper[0].x, rootY),
                new Vector2Int(upper[1].x, rootY) }, upper, lower,
            footprint.ToArray(), added.ToArray(), depth,
            cornerDistance, start, end);
    }

    private static MapPlanningException Invalid(string reason) =>
        new MapPlanningException("OutboardStair" + reason,
            "Outboard stair geometry rejected: " + reason, false);
}

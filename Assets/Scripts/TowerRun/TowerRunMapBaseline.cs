using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Computes a canonical signature for the complete generated entrance baseline that may be
/// inspected without reading the protected generation-settings asset.
/// </summary>
public static class TowerRunMapBaseline
{
    private const string LegacySignatureVersion = "TRB2";
    private const string TerrainSignatureVersion = "TRB3";

    public static string Compute(
        MapData map,
        MapPlacementPlan placementPlan,
        MapSimpleDecorationData decorations)
    {
        if (map == null || placementPlan == null || decorations == null)
            return string.Empty;

        unchecked
        {
            uint hash = 2166136261;
            string signatureVersion = map.HasElevation
                ? TerrainSignatureVersion : LegacySignatureVersion;
            Mix(ref hash, signatureVersion);
            Mix(ref hash, map.Width);
            Mix(ref hash, map.Height);
            Mix(ref hash, map.Origin.x);
            Mix(ref hash, map.Origin.y);
            Mix(ref hash, map.SpawnCell.x);
            Mix(ref hash, map.SpawnCell.y);
            Mix(ref hash, map.ExitCell.x);
            Mix(ref hash, map.ExitCell.y);

            for (int y = map.Origin.y; y < map.Origin.y + map.Height; y++)
            {
                for (int x = map.Origin.x; x < map.Origin.x + map.Width; x++)
                {
                    Vector2Int cell = new Vector2Int(x, y);
                    Mix(ref hash, (int)map.GetCell(cell).terrainType);
                    if (map.HasElevation)
                        Mix(ref hash, map.GetElevation(cell));
                }
            }

            if (map.HasElevation)
            {
                Mix(ref hash, map.Stairs.Count);
                foreach (MapStairConnection stair in map.Stairs)
                {
                    Mix(ref hash, stair.LowerCell.x);
                    Mix(ref hash, stair.LowerCell.y);
                    Mix(ref hash, stair.UpperCell.x);
                    Mix(ref hash, stair.UpperCell.y);
                }
            }

            HashDecorations(ref hash, decorations);
            HashPlacementPlan(ref hash, placementPlan);
            return $"{signatureVersion}-{hash:X8}";
        }
    }

    private static void HashDecorations(ref uint hash, MapSimpleDecorationData decorations)
    {
        List<MapSimpleDecorationPlacement> ordered =
            new List<MapSimpleDecorationPlacement>(decorations.Placements);
        ordered.Sort(CompareDecorations);
        Mix(ref hash, ordered.Count);

        for (int i = 0; i < ordered.Count; i++)
        {
            MapSimpleDecorationPlacement placement = ordered[i];
            Mix(ref hash, (int)placement.DecorationType);
            Mix(ref hash, placement.VariantId);
            Mix(ref hash, placement.AnchorCell.x);
            Mix(ref hash, placement.AnchorCell.y);
        }
    }

    private static void HashPlacementPlan(ref uint hash, MapPlacementPlan placementPlan)
    {
        List<MapPlacementReservation> ordered =
            new List<MapPlacementReservation>(placementPlan.Reservations);
        ordered.Sort(CompareReservations);
        Mix(ref hash, ordered.Count);

        for (int i = 0; i < ordered.Count; i++)
        {
            MapPlacementReservation reservation = ordered[i];
            Mix(ref hash, reservation.Role);
            Mix(ref hash, reservation.LogicalObjectId);
            Mix(ref hash, (int)reservation.Ownership);
            Mix(ref hash, reservation.AnchorCell.x);
            Mix(ref hash, reservation.AnchorCell.y);

            List<Vector2Int> occupiedCells =
                new List<Vector2Int>(reservation.OccupiedCells);
            occupiedCells.Sort(CompareCells);
            Mix(ref hash, occupiedCells.Count);

            for (int j = 0; j < occupiedCells.Count; j++)
            {
                Mix(ref hash, occupiedCells[j].x);
                Mix(ref hash, occupiedCells[j].y);
            }
        }
    }

    private static int CompareDecorations(
        MapSimpleDecorationPlacement left,
        MapSimpleDecorationPlacement right)
    {
        int comparison = left.DecorationType.CompareTo(right.DecorationType);

        if (comparison != 0)
            return comparison;

        comparison = string.Compare(left.VariantId, right.VariantId, StringComparison.Ordinal);
        return comparison != 0 ? comparison : CompareCells(left.AnchorCell, right.AnchorCell);
    }

    private static int CompareReservations(
        MapPlacementReservation left,
        MapPlacementReservation right)
    {
        int comparison = string.Compare(left.Role, right.Role, StringComparison.Ordinal);

        if (comparison != 0)
            return comparison;

        comparison = string.Compare(
            left.LogicalObjectId,
            right.LogicalObjectId,
            StringComparison.Ordinal);

        if (comparison != 0)
            return comparison;

        comparison = left.Ownership.CompareTo(right.Ownership);
        return comparison != 0
            ? comparison
            : CompareCells(left.AnchorCell, right.AnchorCell);
    }

    private static int CompareCells(Vector2Int left, Vector2Int right)
    {
        int comparison = left.x.CompareTo(right.x);
        return comparison != 0 ? comparison : left.y.CompareTo(right.y);
    }

    private static void Mix(ref uint hash, string value)
    {
        string normalized = value ?? string.Empty;
        Mix(ref hash, normalized.Length);

        for (int i = 0; i < normalized.Length; i++)
            Mix(ref hash, normalized[i]);
    }

    private static void Mix(ref uint hash, int value)
    {
        hash ^= (uint)value;
        hash *= 16777619;
    }
}

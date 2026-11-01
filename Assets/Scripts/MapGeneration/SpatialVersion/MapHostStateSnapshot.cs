using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

/// <summary>Same complete deterministic world snapshot used by the original integration regression.</summary>
public static class MapHostStateSnapshot
{
    public static string Capture(MapGenerationController controller)
    {
        var context = controller.Orchestrator.Context; var map = context.ActiveMap;
        var rows = new List<string>();
        string V(Vector2 v) => v.x.ToString("R", CultureInfo.InvariantCulture) + "," + v.y.ToString("R", CultureInfo.InvariantCulture);
        for (int y = 0; y < map.Height; y++) for (int x = 0; x < map.Width; x++)
        { var cell = map.Origin + new Vector2Int(x, y); rows.Add("cell=" + cell + ":" + map.GetCell(cell).terrainType + ":" + map.GetElevation(cell)); }
        rows.Add("spawn=" + map.SpawnCell + ";exit=" + map.ExitCell);
        foreach (var stair in map.Stairs) rows.Add("stair=" + stair.LowerCell + ":" + stair.UpperCell + ":" + map.GetElevation(stair.LowerCell) + ":" + map.GetElevation(stair.UpperCell));
        foreach (var r in context.ActivePlacementPlan.Reservations.OrderBy(r => r.LogicalObjectId, StringComparer.Ordinal))
            rows.Add("plan=" + r.LogicalObjectId + ":" + r.Role + ":" + r.AnchorCell + ":" + string.Join(";", r.OccupiedCells));
        foreach (var r in context.ActiveRegistry.Entries.OrderBy(r => r.LogicalObjectId, StringComparer.Ordinal))
        {
            var transform = (r.Instance as Component)?.transform ?? (r.Instance as GameObject)?.transform;
            rows.Add("registry=" + r.LogicalObjectId + ":" + r.Role + ":" + r.InitialCell + ":" + string.Join(";", r.OccupiedCells) + ":pose=" + (transform == null ? "none" : V(transform.position)));
            if (transform == null) continue;
            foreach (var c in transform.GetComponentsInChildren<Collider2D>(true))
            {
                rows.Add("shape=" + c.GetType().Name + ":" + c.enabled + ":" + c.isTrigger + ":" + V(c.offset));
                if (c is CircleCollider2D circle) rows.Add("circle=" + circle.radius.ToString("R", CultureInfo.InvariantCulture));
                if (c is BoxCollider2D box) rows.Add("box=" + V(box.size));
            }
        }
        foreach (var c in controller.TilemapRenderer.WaterBaseTilemap.transform.parent.GetComponentsInChildren<PolygonCollider2D>(true).OrderBy(c => c.name, StringComparer.Ordinal))
        { rows.Add("projected=" + c.name + ":" + c.pathCount); for (int i = 0; i < c.pathCount; i++) rows.Add(string.Join(";", c.GetPath(i).Select(p => V(c.transform.TransformPoint(p))))); }
        rows.Add("attempts=" + string.Join(";", context.AttemptChain.Select(a => a.Attempt.AttemptIndex + ":" + a.Attempt.AttemptSeed + ":" + a.Outcome)));
        return string.Join("\n", rows);
    }
}

#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Read-only accepted-layout display. This entire type is absent from a release Player.</summary>
[DisallowMultipleComponent]
public sealed class EcologyRegionDebugOverlay : MonoBehaviour
{
    [SerializeField] private bool visible;
    private MapGenerationController controller;
    private Guid generation;
    private string signature;
    private Matrix4x4 projection, ownerTransform;
    private GameObject display;
    private Mesh mesh;
    private Material material;
    private MeshRenderer meshRenderer;
    public int SegmentCount { get; private set; }
    public bool Visible { get => visible; set { visible = value; if (meshRenderer != null) meshRenderer.enabled = value; } }

    private void LateUpdate()
    {
        if (Keyboard.current != null && Keyboard.current.f8Key.wasPressedThisFrame) Visible = !Visible;
        Refresh();
    }

    public void Refresh()
    {
        if (controller == null) controller = GetComponent<MapGenerationController>();
        var context = controller != null ? controller.Orchestrator?.Context : null;
        MapEcologyLayout layout = context != null && context.IsReady ? context.ActivePlacementPlan?.Ecology : null;
        if (layout == null) { Clear(); return; }
        var tilemap = controller.TilemapRenderer.Coordinates.Tilemap;
        Matrix4x4 nextProjection = tilemap.transform.localToWorldMatrix;
        Matrix4x4 nextOwner = transform.localToWorldMatrix;
        if (layout.GenerationId == generation && layout.Signature == signature &&
            projection == nextProjection && ownerTransform == nextOwner)
        { if (meshRenderer != null) meshRenderer.enabled = visible; return; }
        Clear(); generation = layout.GenerationId; signature = layout.Signature;
        projection = nextProjection; ownerTransform = nextOwner;
        var vertices = new List<Vector3>(); var triangles = new List<int>(); var colours = new List<Color32>();
        var keys = new HashSet<Edge>();
        MapData map = context.ActiveMap;
        Vector2Int[] directions = { Vector2Int.down, Vector2Int.right, Vector2Int.up, Vector2Int.left };
        for (int n = 0; n < layout.RegionByNode.Count; n++)
        {
            int owner = layout.RegionByNode[n];
            if (owner == 0) continue;
            Vector2Int cell = layout.CellAt(n);
            for (int side = 0; side < 4; side++)
            {
                Vector2Int neighbour = cell + directions[side];
                int other = map.IsInside(neighbour) ? layout.RegionByNode[layout.NodeAt(neighbour)] : 0;
                if (other == owner) continue;
                Vector2Int a, b;
                if (side == 0) { a = cell; b = cell + Vector2Int.right; }
                else if (side == 1) { a = cell + Vector2Int.right; b = cell + Vector2Int.one; }
                else if (side == 2) { a = cell + Vector2Int.up; b = cell + Vector2Int.one; }
                else { a = cell; b = cell + Vector2Int.up; }
                int ca = (a.y - layout.Origin.y) * (layout.Width + 1) + a.x - layout.Origin.x;
                int cb = (b.y - layout.Origin.y) * (layout.Width + 1) + b.x - layout.Origin.x;
                // Elevation is part of the key even when two surface edges share screen coordinates.
                if (!keys.Add(new Edge(Math.Min(ca, cb), Math.Max(ca, cb), map.GetElevation(cell), Math.Min(owner, other), Math.Max(owner, other)))) continue;
                Vector3 wa = tilemap.CellToWorld(new Vector3Int(a.x, a.y, 0));
                Vector3 wb = tilemap.CellToWorld(new Vector3Int(b.x, b.y, 0));
                Vector3 d = wb - wa;
                Vector3 offset = new Vector3(-d.y, d.x, 0).normalized * .0225f;
                int index = vertices.Count;
                foreach (Vector3 p in new[] { wa - offset, wa + offset, wb + offset, wb - offset })
                { vertices.Add(transform.InverseTransformPoint(p)); colours.Add(new Color32(0, 0, 0, 255)); }
                triangles.AddRange(new[] { index, index + 1, index + 2, index, index + 2, index + 3 });
            }
        }
        SegmentCount = keys.Count;
        display = new GameObject("Accepted ecology boundaries (display only)"); display.transform.SetParent(transform, false);
        mesh = new Mesh { name = "Accepted ecology boundary cache" };
        if (vertices.Count > 65535) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(vertices); mesh.SetColors(colours); mesh.SetTriangles(triangles, 0); mesh.RecalculateBounds();
        display.AddComponent<MeshFilter>().sharedMesh = mesh;
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) throw new InvalidOperationException("Existing Sprite display shader is unavailable.");
        material = new Material(shader); meshRenderer = display.AddComponent<MeshRenderer>(); meshRenderer.sharedMaterial = material;
        SpriteRenderer player = controller.Player != null ? controller.Player.GetComponentInChildren<SpriteRenderer>() : null;
        meshRenderer.sortingLayerID = player != null ? player.sortingLayerID : 0;
        meshRenderer.sortingOrder = 32000; meshRenderer.enabled = visible;
    }

    private void Clear()
    {
        if (display != null) Destroy(display);
        if (mesh != null) Destroy(mesh);
        if (material != null) Destroy(material);
        display = null; mesh = null; material = null; meshRenderer = null;
        SegmentCount = 0; generation = Guid.Empty; signature = null;
    }
    private void OnDestroy() => Clear();
    private readonly struct Edge : IEquatable<Edge>
    {
        private readonly int a, b, level, first, second;
        public Edge(int a, int b, int level, int first, int second)
        { this.a = a; this.b = b; this.level = level; this.first = first; this.second = second; }
        public bool Equals(Edge e) => a == e.a && b == e.b && level == e.level && first == e.first && second == e.second;
        public override bool Equals(object obj) => obj is Edge e && Equals(e);
        public override int GetHashCode() { unchecked { return ((((a * 397) ^ b) * 397 ^ level) * 397 ^ first) * 397 ^ second; } }
    }
}
#endif

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>Projects the elevations in MapData onto reusable visual and collision layers.</summary>
internal sealed class MapElevationProjector
{
    private readonly MapTilemapRenderer owner;
    private readonly Dictionary<string, Tilemap> layers = new Dictionary<string, Tilemap>();
    private PolygonCollider2D cliffEdges;
    private PolygonCollider2D cliffFaces;
    private Sprite shadowSprite;
    private Sprite[] foamSprites;
    private Tile shadowTile;
    private MapFoamTile foamTile;
    private MapTerrainV2Palette loadedPalette;
    private bool loadedStage1Palette;
    private MapStage1ProjectionPlan pendingStage1;
    private MapStage1ProjectionPlan activeStage1;

    internal MapElevationProjector(MapTilemapRenderer owner)
    {
        this.owner = owner;
    }

    internal void Render(MapData map, MapTerrainV2Palette palette, bool remediation = false)
    {
        RenderCore(map, palette, false, remediation: remediation);
    }

    internal void RenderStandardTerrainV21(MapData map, MapTerrainV2Palette palette)
    {
        RenderCore(map, palette, true);
    }

    internal void RenderStandardTerrainV21(MapData map, MapTerrainV2Palette palette,
        MapTerrainV2Palette h2Palette)
    {
        RenderCore(map, palette, true, h2Palette);
    }

    internal void RenderStandardTerrainV21(MapData map, MapTerrainV2Palette palette,
        MapTerrainV2Palette h2Palette, MapStage1SouthStairProjectionRequest.Projection southStair)
    {
        RenderCore(map, palette, true, h2Palette, southStair);
    }

    internal bool IsPending(MapStage1ProjectionPlan plan) => ReferenceEquals(pendingStage1, plan);

    internal void Cancel(MapStage1ProjectionPlan plan)
    {
        if (!ReferenceEquals(pendingStage1, plan) || plan.State != 0) return;
        pendingStage1 = null;
        Release(plan);
    }

    internal MapStage1ProjectionPlan PrepareStage1(MapData map, MapGenerationSettings settings,
        MapTerrainV2Palette palette, MapTerrainV2Palette h2,
        MapStage1SouthStairProjectionRequest southRequest, MapStage1CornerSources sources)
    {
        if (pendingStage1 != null)
            throw MapStage1ProjectionPlan.Error("Stage1ProjectionAlreadyPending", "Cancel the unconsumed preparation before preparing another one.");
        if (sources == null) throw new ArgumentNullException(nameof(sources));
        if (map.Stairs.Any(s => s.VisualPlacement != MapStairVisualPlacement.Legacy))
            throw MapStage1ProjectionPlan.Error("Stage1ProjectionPlacementUnsupported", "Accepted side/outboard stairs retain their existing projection.");
        var south = southRequest != null ? southRequest.Prepare(map) : null;
        var plan = new MapStage1ProjectionPlan(this, owner, map, settings, palette, h2, south, sources, CaptureStage1LayerBinding);
        try
        {
            ValidateStage1Material(owner.WaterBaseTilemap.GetComponent<TilemapRenderer>());
            Tile interior = palette.GrassAndCliff.GetTile(TerrainBoundaryMask.None) as Tile;
            if (interior == null || interior.sprite == null)
                throw MapStage1ProjectionPlan.Error("Stage1ProjectionSpriteMissing", "The palette interior Tile has no native Sprite.");
            Texture2D atlas = interior.sprite.texture;
            sources.Validate(atlas);
            foreach (MapTerrainV2Palette p in new[] { palette, h2 })
            for (int mask = 0; mask < 16; mask++)
            {
                bool north = (mask & (int)TerrainBoundaryMask.North) != 0, southEdge = (mask & (int)TerrainBoundaryMask.South) != 0;
                bool east = (mask & (int)TerrainBoundaryMask.East) != 0, west = (mask & (int)TerrainBoundaryMask.West) != 0;
                int x = east && west ? 512 : west ? 320 : east ? 448 : 384;
                int y = north && southEdge ? 128 : north ? 320 : southEdge ? 192 : 256;
                CheckRole(p.GrassAndCliff.GetTile((TerrainBoundaryMask)mask) as Tile, new Rect(x, y, 64, 64));
                if (southEdge) CheckRole(p.GrassAndCliff.GetSouthFaceTile((TerrainBoundaryMask)mask) as Tile,
                    new Rect(x, north ? 0 : 64, 64, 64));
            }
            void CheckRole(Tile tile, Rect rect)
            {
                MapStage1ProjectionPlan.ValidateNative(tile, atlas);
                if (tile.sprite.rect != rect)
                    throw MapStage1ProjectionPlan.Error("Stage1ProjectionTileRoleInvalid", "A palette slot does not match its audited native source Rect.");
            }
            for (int y = map.Origin.y; y < map.Origin.y + map.Height; y++)
            for (int x = map.Origin.x; x < map.Origin.x + map.Width; x++)
            {
                var cell = new Vector2Int(x, y);
                int height = map.GetElevation(cell);
                if (height > 2)
                    throw MapStage1ProjectionPlan.Error("Stage1ProjectionHeightUnsupported", "Only the bounded H0/H1/H2 source family is supported.");
                plan.Foundation |= height == 2;
            }
            for (int y = map.Origin.y; y < map.Origin.y + map.Height; y++)
            for (int x = map.Origin.x; x < map.Origin.x + map.Width; x++)
            {
                var cell = new Vector2Int(x, y); int height = map.GetElevation(cell);
                if (height < 1) continue;
                TerrainBoundaryMask mask = plan.Foundation ? FoundationMask(map, cell) :
                    Stage1TopMask(map, cell, 1, BoundaryMask(map, cell, 1), true);
                plan.Top[(1, cell)] = (Tile)palette.GrassAndCliff.GetTile(mask);
                if (height == 2)
                    plan.Top[(2, cell)] = (Tile)h2.GrassAndCliff.GetTile(Stage1TopMask(map, cell, 2, BoundaryMask(map, cell, 2), true));
            }
            plan.NativePixels.Add(atlas, ReadStage1Pixels(atlas));
            if (south != null)
            {
                Texture2D stairAtlas = south.Pieces[0].Tile.sprite.texture;
                if (!plan.NativePixels.ContainsKey(stairAtlas)) plan.NativePixels.Add(stairAtlas, ReadStage1Pixels(stairAtlas));
            }
            BuildStage1Compositions(plan, atlas);
            PrepareCoveragePlacements(plan);
            plan.PrepareCoverageFootprints();
            plan.Seal();
            pendingStage1 = plan;
            return plan;
        }
        catch { Release(plan); throw; }
    }

    internal void RenderPreparedStage1(MapStage1ProjectionPlan plan)
    {
        if (!IsPending(plan) || plan.State != 0)
            throw MapStage1ProjectionPlan.Error("Stage1ProjectionNotPrepared", "The renderer has no matching pending preparation.");
        RenderCore(plan.Map, plan.Palette, true, plan.H2Palette, plan.South, plan);
    }

    private void RenderCore(MapData map, MapTerrainV2Palette palette, bool stage1,
        MapTerrainV2Palette h2Palette = null,
        MapStage1SouthStairProjectionRequest.Projection southStair = null,
        MapStage1ProjectionPlan prepared = null, bool remediation = false)
    {
        if (southStair != null && (!stage1 || !ReferenceEquals(southStair.Map, map)))
            throw new InvalidOperationException("The prepared south-stair projection belongs to another map.");
        if (prepared == null) Clear();
        else
        {
            ClearDisplayed();
            pendingStage1 = null;
            activeStage1 = prepared;
            prepared.State = 1;
        }
        if (!map.HasElevation)
            return;
        if (palette == null || !palette.IsComplete)
            throw new MissingReferenceException("Terrain V2 palette is incomplete.");

        EnsureVisualTiles(palette, stage1);
        MapRemediationSurfacePlan surface = remediation ? MapRemediationSurfacePolicy.Build(map, palette) : null;
        foamTile.GenerationSeed = map.GenerationSeed;
        Tilemap foam = GetLayer("GeneratedWaterFoam", -15);
        foam.animationFrameRate = 4f;
        int maximumElevation = 0;
        for (int y = map.Origin.y; y < map.Origin.y + map.Height; y++)
        {
            for (int x = map.Origin.x; x < map.Origin.x + map.Width; x++)
            {
                Vector2Int cell = new Vector2Int(x, y);
                maximumElevation = Mathf.Max(maximumElevation, map.GetElevation(cell));
                if (TerrainTopology.IsWater(map.GetCell(cell).terrainType) ||
                    !TouchesWater(map, cell))
                    continue;
                foam.SetTile(ToCell(cell), foamTile);
            }
        }

        for (int level = 1; level <= maximumElevation; level++)
        {
            MapTerrainV2Palette levelPalette = level == 2 && h2Palette != null
                ? h2Palette : palette;
            Tilemap shadow = GetLayer($"GeneratedElevation{level}Shadow",
                (level - 1) * 10 + 1);
            Tilemap face = GetLayer($"GeneratedElevation{level}Face", prepared != null && level == 2 ?
                prepared.CoverageLayerOrder("H2Face", 13) : stage1 ? (level - 1) * 10 + 3 : level * 10 - 3);
            Tilemap top = GetLayer($"GeneratedElevation{level}Top", prepared != null ? prepared.CoverageLayerOrder("H" + level + "Top", level * 10) : level * 10);
            Tilemap stairs = GetLayer($"GeneratedElevation{level}Stairs", prepared != null ?
                prepared.CoverageLayerOrder("H" + level + "Stairs", level * 10 + 2) : level * 10 + 2);
            shadow.color = new Color(1f, 1f, 1f, 0.25f);
            for (int y = map.Origin.y; y < map.Origin.y + map.Height; y++)
            {
                for (int x = map.Origin.x; x < map.Origin.x + map.Width; x++)
                {
                    Vector2Int cell = new Vector2Int(x, y);
                    if (prepared != null && prepared.Top.TryGetValue((level, cell), out Tile preparedTop))
                        top.SetTile(ToCell(cell), preparedTop);
                    if (map.GetElevation(cell) != level)
                        continue;
                    TerrainBoundaryMask mask = BoundaryMask(map, cell, level);
                    Vector2Int south = cell + Vector2Int.down;
                    TerrainBoundaryMask topMask = Stage1TopMask(map, cell, level, mask, stage1);
                    bool boundaryShoulder = stage1 && IsBoundaryShoulder(map, cell, level);
                    if (prepared == null && !boundaryShoulder)
                        top.SetTile(ToCell(cell), levelPalette.GrassAndCliff.GetTile(topMask));
                    if (!map.IsInside(south) || map.GetElevation(south) >= level ||
                        map.IsStairTransition(cell, south) || boundaryShoulder)
                        continue;
                    TileBase cliff = surface != null ? surface.FaceAt(level, south) : levelPalette.GrassAndCliff.GetSouthFaceTile(mask);
                    if (cliff != null && !(prepared != null && level == 2))
                        face.SetTile(ToCell(south), cliff);
                    shadow.SetTile(ToCell(south), shadowTile);
                }
            }
            if (prepared != null && level == 2)
                foreach (var piece in prepared.CoverageFaces) face.SetTile(ToCell(piece.Visual), piece.Tile);
            DrawStairs(map, levelPalette, stairs, face, level, stage1, southStair, prepared);
        }

        RebuildCliffEdges(map);
        if (surface != null) RebuildCliffFaces(surface);
    }

    internal void Clear()
    {
        if (pendingStage1 != null) Cancel(pendingStage1);
        ClearDisplayed();
    }

    private void ClearDisplayed()
    {
        foreach (Tilemap layer in layers.Values)
        {
            if (layer == null)
                continue;
            layer.ClearAllTiles();
            TilemapRenderer renderer = layer.GetComponent<TilemapRenderer>();
            if (renderer != null)
                renderer.enabled = false;
        }
        if (cliffEdges != null)
            cliffEdges.pathCount = 0;
        if (cliffFaces != null)
            cliffFaces.pathCount = 0;
        if (activeStage1 != null)
        {
            Release(activeStage1);
            activeStage1 = null;
        }
    }

    private static void Release(MapStage1ProjectionPlan plan)
    {
        // Detach displayed Tiles before reaching this method. Borrowed native assets are never owned.
        foreach (Tile tile in plan.Owned.OfType<Tile>()) DestroyRuntime(tile);
        foreach (Sprite sprite in plan.Owned.OfType<Sprite>()) DestroyRuntime(sprite);
        foreach (Texture2D texture in plan.Owned.OfType<Texture2D>()) DestroyRuntime(texture);
        plan.Owned.Clear(); plan.ReleaseCoverage(); plan.State = 2;
    }

    private static void PrepareCoveragePlacements(MapStage1ProjectionPlan plan)
    {
        MapData map = plan.Map;
        for (int y = map.Origin.y; y < map.Origin.y + map.Height; y++)
        for (int x = map.Origin.x; x < map.Origin.x + map.Width; x++)
        {
            var cell = new Vector2Int(x, y); var south = cell + Vector2Int.down;
            if (map.GetElevation(cell) != 2 || !map.IsInside(south) || map.GetElevation(south) >= 2 ||
                map.IsStairTransition(cell, south) || IsBoundaryShoulder(map, cell, 2)) continue;
            var tile = plan.H2Palette.GrassAndCliff.GetSouthFaceTile(BoundaryMask(map, cell, 2)) as Tile;
            if (tile == null || tile.sprite == null)
                throw MapStage1ProjectionPlan.Error("Stage1ProjectionSpriteMissing", "A prepared H2 face has no native Sprite.");
            plan.CoverageFaces.Add(new MapStage1ProjectionPlan.CoveragePlacement("H2Face", 2, cell, south, tile, 13));
        }
        if (plan.South != null)
            foreach (var piece in plan.South.Pieces)
                plan.CoverageStairs.Add(new MapStage1ProjectionPlan.CoveragePlacement("H" + plan.South.Level + "Stairs", plan.South.Level,
                    piece.Logical, piece.Visual, piece.Tile, plan.South.Level * 10 + 2));
    }

    private static TerrainBoundaryMask Stage1TopMask(MapData map, Vector2Int cell, int level,
        TerrainBoundaryMask mask, bool stage1)
    {
        TerrainBoundaryMask topMask = mask;
        if (stage1 && map.IsStairTransition(cell, cell + Vector2Int.down)) topMask &= ~TerrainBoundaryMask.South;
        if (stage1 && map.IsStairTransition(cell, cell + Vector2Int.up)) topMask &= ~TerrainBoundaryMask.North;
        if (stage1 && HasExteriorBoundaryShoulder(map, cell, level)) topMask &= ~TerrainBoundaryMask.West;
        if (stage1 && IsWestSideUpper(map, cell) && (mask & TerrainBoundaryMask.South) == 0)
            topMask &= ~TerrainBoundaryMask.West;
        return topMask;
    }

    private static TerrainBoundaryMask FoundationMask(MapData map, Vector2Int cell)
    {
        bool Filled(Vector2Int n) => map.IsInside(n) && map.GetElevation(n) >= 1;
        TerrainBoundaryMask mask = TerrainBoundaryMask.None;
        if (!Filled(cell + Vector2Int.up)) mask |= TerrainBoundaryMask.North;
        if (!Filled(cell + Vector2Int.right)) mask |= TerrainBoundaryMask.East;
        if (!Filled(cell + Vector2Int.down)) mask |= TerrainBoundaryMask.South;
        if (!Filled(cell + Vector2Int.left)) mask |= TerrainBoundaryMask.West;
        if (map.IsStairTransition(cell, cell + Vector2Int.up)) mask &= ~TerrainBoundaryMask.North;
        if (map.IsStairTransition(cell, cell + Vector2Int.down)) mask &= ~TerrainBoundaryMask.South;
        return mask;
    }

    private void ValidateStage1Material(TilemapRenderer renderer)
    {
        Material reference = owner.WaterBaseTilemap.GetComponent<TilemapRenderer>().sharedMaterial;
        if (renderer.sharedMaterial == null || renderer.sharedMaterial != reference ||
            reference.shader == null || reference.shader.name != "Sprites/Default" || reference.name != "Sprites-Default")
            throw MapStage1ProjectionPlan.Error("Stage1ProjectionMaterialUnsupported", "The audited Stage1 path requires the existing native sprite material.");
    }

    private string CaptureStage1LayerBinding()
    {
        var b = new StringBuilder();
        foreach (var pair in layers.OrderBy(p => p.Key))
        {
            Tilemap layer = pair.Value;
            b.Append(pair.Key).Append(layer != null ? layer.GetInstanceID() : 0);
            if (layer == null) continue;
            TilemapRenderer renderer = layer.GetComponent<TilemapRenderer>();
            ValidateStage1Material(renderer);
            b.Append(layer.transform.localToWorldMatrix.ToString("R")).Append(layer.tileAnchor.ToString("R")).Append(layer.color.ToString("R"))
                .Append(renderer.sortingLayerID).Append(renderer.sortingOrder).Append((int)renderer.mode)
                .Append(MapStage1ProjectionPlan.MaterialState(renderer.sharedMaterial));
        }
        return b.ToString();
    }

    private static void BuildStage1Compositions(MapStage1ProjectionPlan plan, Texture2D atlas)
    {
        var replacements = new Dictionary<(int Level, Vector2Int Cell), Dictionary<Vector2Int, Tile>>();
        MapData map = plan.Map;
        for (int y = map.Origin.y + 1; y < map.Origin.y + map.Height - 1; y++)
        for (int x = map.Origin.x + 1; x < map.Origin.x + map.Width - 1; x++)
        {
            var center = new Vector2Int(x, y); int level = map.GetElevation(center);
            if (level != 1 && level != 2) continue;
            foreach (int side in new[] { -1, 1 })
            {
                if (map.GetElevation(center + Vector2Int.up) != level ||
                    map.GetElevation(center + new Vector2Int(side, 0)) != level ||
                    map.GetElevation(center + new Vector2Int(side, 1)) != level - 1) continue;
                Add((level, center + Vector2Int.up), new Vector2Int(side < 0 ? 0 : 3, 0), side < 0 ? plan.Sources.NorthWest : plan.Sources.NorthEast);
                Add((level, center + new Vector2Int(side, 0)), new Vector2Int(side < 0 ? 3 : 0, 3), plan.Sources.Side);
            }
        }
        if (replacements.Count == 0) return;
        Color32[] originalPixels = plan.NativePixels[atlas];
        var cache = new Dictionary<string, Tile>();
        foreach (var entry in replacements)
        {
            if (!plan.Top.TryGetValue(entry.Key, out Tile original))
                throw MapStage1ProjectionPlan.Error("Stage1ProjectionRegionMissing", "A composited region has no real Top command.");
            var pixels = new Color32[4096]; var key = new StringBuilder().Append(original.sprite.GetInstanceID()).Append("/4x4/");
            for (int iy = 0; iy < 4; iy++)
            for (int ix = 0; ix < 4; ix++)
            {
                var quadrant = new Vector2Int(ix, iy); bool replace = entry.Value.TryGetValue(quadrant, out Tile source);
                source = replace ? source : original;
                var piece = new MapStage1ProjectionPlan.Fragment(entry.Key.Level, entry.Key.Cell, quadrant, source, replace);
                plan.Pieces.Add(piece); Rect crop = piece.SourceRect;
                key.Append(source.sprite.GetInstanceID()).Append('/');
                for (int y = 0; y < 16; y++) for (int x = 0; x < 16; x++)
                    pixels[(iy*16+y)*64+ix*16+x] = originalPixels[((int)crop.y+y)*atlas.width+(int)crop.x+x];
            }
            if (!cache.TryGetValue(key.ToString(), out Tile composed))
            {
                Color32[] output = (Color32[])originalPixels.Clone(); Rect rect = original.sprite.rect;
                for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++)
                    output[((int)rect.y+y)*atlas.width+(int)rect.x+x] = pixels[y*64+x];
                var texture = new Texture2D(atlas.width, atlas.height, TextureFormat.RGBA32, false, false);
                plan.Owned.Add(texture); texture.name = "Stage1PreparedAtlas"; texture.filterMode = FilterMode.Point;
                texture.wrapMode = TextureWrapMode.Clamp; texture.SetPixels32(output); texture.Apply(false, false);
                Sprite sprite = Sprite.Create(texture, rect, new Vector2(.5f, .5f), 64, 0, SpriteMeshType.FullRect);
                plan.Owned.Add(sprite); sprite.name = "Stage1PreparedSprite";
                var vertices = new List<Vector2>(); var indices = new List<ushort>();
                for (int iy = 0; iy < 4; iy++) for (int ix = 0; ix < 4; ix++)
                {
                    ushort start = (ushort)vertices.Count;
                    vertices.Add(new Vector2(ix*16,iy*16)); vertices.Add(new Vector2(ix*16,(iy+1)*16));
                    vertices.Add(new Vector2((ix+1)*16,(iy+1)*16)); vertices.Add(new Vector2((ix+1)*16,iy*16));
                    indices.Add(start); indices.Add((ushort)(start+1)); indices.Add((ushort)(start+2));
                    indices.Add((ushort)(start+2)); indices.Add((ushort)(start+3)); indices.Add(start);
                }
                sprite.OverrideGeometry(vertices.ToArray(), indices.ToArray());
                composed = ScriptableObject.CreateInstance<Tile>(); plan.Owned.Add(composed);
                composed.name = "Stage1PreparedTop"; composed.sprite = sprite; composed.colliderType = Tile.ColliderType.None;
                cache.Add(key.ToString(), composed);
            }
            plan.Top[entry.Key] = composed;
        }

        void Add((int Level, Vector2Int Cell) cell, Vector2Int quadrant, Tile source)
        {
            if (!replacements.TryGetValue(cell, out var regions)) replacements[cell] = regions = new Dictionary<Vector2Int, Tile>();
            if (regions.TryGetValue(quadrant, out Tile previous) && previous != source)
                throw MapStage1ProjectionPlan.Error("Stage1ProjectionRegionConflict", "Two source roles disagree on a native region.");
            regions[quadrant] = source;
        }
    }

    internal static Color32[] ReadStage1Pixels(Texture2D source)
    {
        Texture2D copy = ReadStage1Atlas(source);
        try { return copy.GetPixels32(); }
        finally { DestroyRuntime(copy); }
    }

    private static string CoverageLayerName(string role) => "GeneratedElevation" + role.Substring(1);

    internal Matrix4x4 CoverageWorld(string role, Vector2Int cell)
    {
        Tilemap layer = layers.TryGetValue(CoverageLayerName(role), out Tilemap current) && current != null ? current : owner.WaterBaseTilemap;
        return layer.transform.localToWorldMatrix * Matrix4x4.Translate(layer.CellToLocalInterpolated(new Vector3(cell.x, cell.y, 0) + layer.tileAnchor));
    }

    internal Color CoverageLayerColor(string role) => layers.TryGetValue(CoverageLayerName(role), out Tilemap layer) && layer != null ? layer.color : Color.white;

    internal bool CoverageGeometrySupported()
    {
        Tilemap basis = owner.WaterBaseTilemap;
        Vector3 zero = basis.GetCellCenterWorld(Vector3Int.zero);
        if ((basis.GetCellCenterWorld(Vector3Int.right) - zero - Vector3.right).sqrMagnitude > 1e-12f ||
            (basis.GetCellCenterWorld(Vector3Int.up) - zero - Vector3.up).sqrMagnitude > 1e-12f ||
            basis.transform.localPosition != Vector3.zero || basis.transform.localRotation != Quaternion.identity ||
            basis.transform.localScale != Vector3.one || basis.tileAnchor != new Vector3(.5f, .5f, 0) || basis.orientationMatrix != Matrix4x4.identity) return false;
        foreach (string role in new[] { "H1Top", "H2Top", "H2Face", "H2Stairs" })
            if (layers.TryGetValue(CoverageLayerName(role), out Tilemap layer) && layer != null &&
                (layer.transform.localToWorldMatrix != basis.transform.localToWorldMatrix || layer.tileAnchor != basis.tileAnchor ||
                 layer.orientationMatrix != basis.orientationMatrix)) return false;
        return true;
    }

    internal Tile ExpectedCoverageTop(MapStage1ProjectionPlan plan, Vector2Int cell, int level)
    {
        TerrainBoundaryMask mask = level == 1 && plan.Foundation ? FoundationMask(plan.Map, cell) :
            Stage1TopMask(plan.Map, cell, level, BoundaryMask(plan.Map, cell, level), true);
        return (Tile)(level == 2 ? plan.H2Palette : plan.Palette).GrassAndCliff.GetTile(mask);
    }

    internal Tile ExpectedCoverageFace(MapStage1ProjectionPlan plan, Vector2Int upper) =>
        (Tile)plan.H2Palette.GrassAndCliff.GetSouthFaceTile(BoundaryMask(plan.Map, upper, 2));

    private static Texture2D ReadStage1Atlas(Texture2D source)
    {
        RenderTexture previous = RenderTexture.active; bool oldSrgb = GL.sRGBWrite;
        RenderTexture target = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        Texture2D copy = null;
        try
        {
            target.filterMode = FilterMode.Point; GL.sRGBWrite = QualitySettings.activeColorSpace == ColorSpace.Linear;
            Graphics.Blit(source, target); RenderTexture.active = target;
            copy = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false, false);
            copy.name = "Stage1PreparedReadback"; copy.filterMode = FilterMode.Point;
            copy.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0, false); copy.Apply(false, false);
            return copy;
        }
        catch { DestroyRuntime(copy); throw; }
        finally { GL.sRGBWrite = oldSrgb; RenderTexture.active = previous; RenderTexture.ReleaseTemporary(target); }
    }

    internal void Dispose()
    {
        Clear();
        DestroyRuntime(shadowSprite);
        if (foamSprites != null)
            foreach (Sprite sprite in foamSprites)
                DestroyRuntime(sprite);
        DestroyRuntime(shadowTile);
        DestroyRuntime(foamTile);
    }

    private void EnsureVisualTiles(MapTerrainV2Palette palette, bool stage1)
    {
        if (loadedPalette == palette && loadedStage1Palette == stage1 &&
            foamTile != null && shadowTile != null)
            return;
        DestroyRuntime(shadowSprite);
        if (foamSprites != null)
            foreach (Sprite sprite in foamSprites)
                DestroyRuntime(sprite);
        DestroyRuntime(shadowTile);
        DestroyRuntime(foamTile);
        loadedPalette = palette;
        loadedStage1Palette = stage1;
        shadowSprite = Sprite.Create(palette.ShadowTexture,
            new Rect(32, 32, 128, 128), new Vector2(0.5f, 0.5f), 64f);
        shadowTile = ScriptableObject.CreateInstance<Tile>();
        shadowTile.sprite = shadowSprite;
        shadowTile.colliderType = Tile.ColliderType.None;

        foamSprites = new Sprite[16];
        for (int i = 0; i < foamSprites.Length; i++)
            foamSprites[i] = Sprite.Create(palette.FoamTexture,
                stage1 ? new Rect(i * 192 + 32, 32, 128, 128) :
                    new Rect(i * 192, 0, 128, 128),
                new Vector2(0.5f, 0.5f), 64f);
        foamTile = ScriptableObject.CreateInstance<MapFoamTile>();
        foamTile.SetFrames(foamSprites);
    }

    private Tilemap GetLayer(string name, int order)
    {
        if (!layers.TryGetValue(name, out Tilemap layer) || layer == null)
        {
            GameObject layerObject = new GameObject(name);
            layerObject.transform.SetParent(owner.WaterBaseTilemap.transform.parent, false);
            layer = layerObject.AddComponent<Tilemap>();
            layerObject.AddComponent<TilemapRenderer>();
            layers[name] = layer;
        }
        TilemapRenderer renderer = layer.GetComponent<TilemapRenderer>();
        renderer.sortingLayerID = owner.WaterBaseTilemap.GetComponent<TilemapRenderer>().sortingLayerID;
        renderer.sortingOrder = order;
        renderer.enabled = true;
        return layer;
    }

    private static void DrawStairs(MapData map, MapTerrainV2Palette palette,
        Tilemap layer, Tilemap face, int level, bool stage1,
        MapStage1SouthStairProjectionRequest.Projection southStair, MapStage1ProjectionPlan prepared = null)
    {
        foreach (MapStairConnection stair in map.Stairs)
        {
            Vector2Int lower = stair.LowerCell;
            Vector2Int upper = stair.UpperCell;
            if (map.GetElevation(upper) != level)
                continue;
            if (stage1 && stair.VisualPlacement == MapStairVisualPlacement.WestSideAttached)
            {
                if (upper != lower + Vector2Int.right)
                    throw new InvalidOperationException("The verified west-side candidate traverses east into its core.");
                if (map.IsStairTransition(lower + Vector2Int.down, upper + Vector2Int.down))
                    continue;
                MapOutboardStairGeometry.ValidateWestSideAttachment(map, lower);
                face.SetTile(ToCell(lower + Vector2Int.up), palette.StairUpperLeft);
                face.SetTile(ToCell(lower), palette.StairLowerLeft);
                continue;
            }
            if (map.GetElevation(upper) != level || upper != lower + Vector2Int.up)
                continue;
            bool leftLane = map.IsStairTransition(lower + Vector2Int.left,
                upper + Vector2Int.left);
            bool rightLane = map.IsStairTransition(lower + Vector2Int.right,
                upper + Vector2Int.right);
            if (leftLane)
                continue;
            if (stage1 && !rightLane)
                throw new InvalidOperationException("A traversable Stage 1.1 stair needs two atomic lanes.");
            if (stage1 && rightLane)
            {
                MapStairConnection companion = FindStair(map,
                    lower + Vector2Int.right, upper + Vector2Int.right);
                if (companion.VisualSide != stair.VisualSide ||
                    companion.VisualComposition != stair.VisualComposition ||
                    companion.VisualPlacement != stair.VisualPlacement)
                    throw new InvalidOperationException("Atomic stair lanes disagree on visual composition.");
                if (southStair != null && southStair.Level == level && southStair.SelectsLeft(stair))
                {
                    if (prepared != null)
                        foreach (var piece in prepared.CoverageStairs) layer.SetTile(ToCell(piece.Visual), piece.Tile);
                    else
                        foreach (MapStage1SouthStairProjectionRequest.Placement piece in southStair.Pieces)
                            layer.SetTile(ToCell(piece.Visual), piece.Tile);
                    // Keep the existing face below these shoulders; do not emit the old slope shoulders.
                    continue;
                }
                if (stair.VisualPlacement == MapStairVisualPlacement.BoundaryShoulders)
                {
                    MapOutboardStairGeometry.ValidateBoundaryShoulders(map, lower);
                    Vector2Int leftShoulder = upper + Vector2Int.left;
                    Vector2Int rightShoulder = upper + Vector2Int.right * 2;
                    layer.SetTile(ToCell(leftShoulder), palette.StairUpperLeft);
                    layer.SetTile(ToCell(rightShoulder), palette.StairUpperRight);
                    // Example 2 continues the one-cell cliff to its foot. This
                    // explicit composition is independent of passage width.
                    if (stair.VisualComposition == MapStairVisualComposition.UpperAndLower)
                    {
                        face.SetTile(ToCell(leftShoulder + Vector2Int.down), palette.StairLowerLeft);
                        face.SetTile(ToCell(rightShoulder + Vector2Int.down), palette.StairLowerRight);
                    }
                    continue;
                }
                if (stair.VisualPlacement == MapStairVisualPlacement.Outboard &&
                    stair.VisualSide == MapStairVisualSide.Both)
                    throw new InvalidOperationException(
                        "Outboard stairs need one explicit grass-rock shoulder side.");
                if (stair.VisualSide != MapStairVisualSide.Both)
                {
                    bool leftSide = stair.VisualSide == MapStairVisualSide.LeftOnly;
                    bool outboard = stair.VisualPlacement == MapStairVisualPlacement.Outboard;
                    Vector2Int shoulder = leftSide ? upper : upper + Vector2Int.right;
                    Vector2Int upperShoulder = outboard ?
                        shoulder + Vector2Int.up : shoulder;
                    if (outboard && (!map.IsInside(upperShoulder) ||
                        map.GetElevation(upperShoulder) != level))
                        throw new InvalidOperationException(
                            "Outboard stair upper shoulder must attach to elevated terrain.");
                    layer.SetTile(ToCell(upperShoulder),
                        leftSide ? palette.StairUpperLeft : palette.StairUpperRight);
                    if (stair.VisualComposition == MapStairVisualComposition.UpperAndLower)
                        layer.SetTile(ToCell(outboard ? shoulder :
                            leftSide ? lower : lower + Vector2Int.right),
                            leftSide ? palette.StairLowerLeft : palette.StairLowerRight);
                    continue;
                }
            }
            Vector2Int left = upper + Vector2Int.left;
            Vector2Int right = upper + new Vector2Int(2, 0);
            layer.SetTile(ToCell(left), palette.StairUpperLeft);
            layer.SetTile(ToCell(right), palette.StairUpperRight);
            bool lowerShoulders = !stage1 ||
                stair.VisualComposition == MapStairVisualComposition.UpperAndLower ||
                stair.VisualComposition != MapStairVisualComposition.UpperOnly &&
                map.IsInside(left) && map.IsInside(right) &&
                map.IsInside(left + Vector2Int.down) &&
                map.IsInside(right + Vector2Int.down) &&
                map.GetElevation(left) == level &&
                map.GetElevation(right) == level &&
                map.GetElevation(left + Vector2Int.down) < level &&
                map.GetElevation(right + Vector2Int.down) < level;
            if (lowerShoulders)
            {
                layer.SetTile(ToCell(left + Vector2Int.down), palette.StairLowerLeft);
                layer.SetTile(ToCell(right + Vector2Int.down), palette.StairLowerRight);
            }
        }
    }

    private static MapStairConnection FindStair(MapData map,
        Vector2Int lower, Vector2Int upper)
    {
        foreach (MapStairConnection candidate in map.Stairs)
            if (candidate.LowerCell == lower && candidate.UpperCell == upper)
                return candidate;
        throw new InvalidOperationException("Atomic stair companion was not found.");
    }

    private static bool IsBoundaryShoulder(MapData map, Vector2Int cell, int level)
    {
        foreach (MapStairConnection stair in map.Stairs)
        {
            if (stair.VisualPlacement != MapStairVisualPlacement.BoundaryShoulders ||
                map.GetElevation(stair.UpperCell) != level ||
                map.IsStairTransition(stair.LowerCell + Vector2Int.left,
                    stair.UpperCell + Vector2Int.left)) continue;
            if (cell == stair.UpperCell + Vector2Int.left ||
                cell == stair.UpperCell + Vector2Int.right * 2) return true;
        }
        return false;
    }

    private static bool IsWestSideUpper(MapData map, Vector2Int cell)
    {
        foreach (MapStairConnection stair in map.Stairs)
            if (stair.VisualPlacement == MapStairVisualPlacement.WestSideAttached &&
                stair.UpperCell == cell && stair.LowerCell == cell + Vector2Int.left)
                return true;
        return false;
    }

    private static bool HasExteriorBoundaryShoulder(MapData map, Vector2Int cell, int level)
    {
        foreach (MapStairConnection stair in map.Stairs)
            if (stair.VisualPlacement == MapStairVisualPlacement.BoundaryShoulders &&
                stair.UpperCell == cell && map.GetElevation(cell) == level &&
                !map.IsStairTransition(stair.LowerCell + Vector2Int.left, cell + Vector2Int.left) &&
                map.IsInside(cell + Vector2Int.left) && map.GetElevation(cell + Vector2Int.left) == level - 1)
                return true;
        return false;
    }

    private void RebuildCliffFaces(MapRemediationSurfacePlan surface)
    {
        if (cliffFaces == null)
        {
            var faces = new GameObject("GeneratedCliffFaces_R3");
            faces.transform.SetParent(owner.TerrainCollisionTilemap.transform.parent, false);
            faces.layer = owner.TerrainCollisionTilemap.gameObject.layer;
            faces.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Static;
            cliffFaces = faces.AddComponent<PolygonCollider2D>();
        }
        var paths = surface.WorldPolygons(owner.WaterBaseTilemap);
        cliffFaces.pathCount = paths.Count;
        for (int i = 0; i < paths.Count; i++)
        {
            Vector2[] world = paths[i]; var local = new Vector2[world.Length];
            for (int p = 0; p < world.Length; p++) local[p] = cliffFaces.transform.InverseTransformPoint(world[p]);
            cliffFaces.SetPath(i, local);
        }
    }

    private void RebuildCliffEdges(MapData map)
    {
        if (cliffEdges == null)
        {
            GameObject edges = new GameObject("GeneratedCliffEdges");
            edges.transform.SetParent(owner.TerrainCollisionTilemap.transform.parent, false);
            edges.layer = owner.TerrainCollisionTilemap.gameObject.layer;
            Rigidbody2D body = edges.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Static;
            cliffEdges = edges.AddComponent<PolygonCollider2D>();
        }

        List<Vector2[]> paths = new List<Vector2[]>();
        for (int y = map.Origin.y; y < map.Origin.y + map.Height; y++)
        {
            for (int x = map.Origin.x; x < map.Origin.x + map.Width; x++)
            {
                Vector2Int cell = new Vector2Int(x, y);
                if (!map.IsWalkable(cell))
                    continue;
                AddEdge(map, cell, cell + Vector2Int.right, true, paths);
                AddEdge(map, cell, cell + Vector2Int.up, false, paths);
            }
        }
        cliffEdges.pathCount = paths.Count;
        for (int i = 0; i < paths.Count; i++)
            cliffEdges.SetPath(i, paths[i]);
    }

    private void AddEdge(MapData map, Vector2Int from, Vector2Int to,
        bool vertical, List<Vector2[]> paths)
    {
        MapCandidateCollisionGeometry.AppendCliffEdge(map, owner.WaterBaseTilemap,
            cliffEdges.transform, from, to, vertical, paths);
    }

    private static bool TouchesWater(MapData map, Vector2Int cell)
    {
        Vector2Int[] directions =
        {
            Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left
        };
        foreach (Vector2Int direction in directions)
        {
            Vector2Int next = cell + direction;
            if (map.IsInside(next) &&
                TerrainTopology.IsWater(map.GetCell(next).terrainType))
                return true;
        }
        return false;
    }

    private static TerrainBoundaryMask BoundaryMask(MapData map, Vector2Int cell, int level)
    {
        TerrainBoundaryMask mask = TerrainBoundaryMask.None;
        if (!SameLevel(map, cell + Vector2Int.up, level)) mask |= TerrainBoundaryMask.North;
        if (!SameLevel(map, cell + Vector2Int.right, level)) mask |= TerrainBoundaryMask.East;
        if (!SameLevel(map, cell + Vector2Int.down, level)) mask |= TerrainBoundaryMask.South;
        if (!SameLevel(map, cell + Vector2Int.left, level)) mask |= TerrainBoundaryMask.West;
        return mask;
    }

    private static bool SameLevel(MapData map, Vector2Int cell, int level) =>
        map.IsInside(cell) && map.GetElevation(cell) == level;

    private static Vector3Int ToCell(Vector2Int cell) => new Vector3Int(cell.x, cell.y, 0);

    private static void DestroyRuntime(UnityEngine.Object value)
    {
        if (value == null)
            return;
        if (Application.isPlaying) UnityEngine.Object.Destroy(value);
        else UnityEngine.Object.DestroyImmediate(value);
    }
}

internal sealed class MapFoamTile : TileBase
{
    private Sprite[] frames;
    internal int GenerationSeed { get; set; }

    internal void SetFrames(Sprite[] sprites) => frames = sprites;

    public override void GetTileData(Vector3Int position, ITilemap tilemap, ref TileData tileData)
    {
        tileData.sprite = frames[0];
        tileData.color = Color.white;
        tileData.transform = Matrix4x4.identity;
        tileData.flags = TileFlags.None;
        tileData.colliderType = Tile.ColliderType.None;
    }

    public override bool GetTileAnimationData(Vector3Int position, ITilemap tilemap,
        ref TileAnimationData tileAnimationData)
    {
        tileAnimationData.animatedSprites = frames;
        tileAnimationData.animationSpeed = 4f;
        uint hash = (uint)GenerationSeed ^
            (uint)(position.x * 73856093) ^ (uint)(position.y * 19349663);
        tileAnimationData.animationStartTime = (hash % (uint)frames.Length) / 4f;
        return true;
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MapVNext
{
    public enum MapVNextFormalRevision
    {
        Legacy = 0,
        PreserveGroundConnectivity = 1
    }

    public sealed class MapVNextFormalTerrain : EcologyRetainedTerrainGenerator
    {
        public EcologyRetainedTerrainGenerator BaseTerrain;
        public MapVNextAssets Assets;
        [SerializeField] private MapVNextFormalRevision layoutRevision;
        public MapVNextFormalRevision LayoutRevision => layoutRevision;
        public override string ImplementationId
        {
            get
            {
                switch (layoutRevision)
                {
                    case MapVNextFormalRevision.Legacy: return EcologyRecipeDefinition.VNextI1ImplementationId;
                    case MapVNextFormalRevision.PreserveGroundConnectivity: return EcologyRecipeDefinition.VNextI1GroundConnectivityImplementationId;
                    default: throw new InvalidOperationException("UnknownVNextFormalRevision");
                }
            }
        }
        public override bool HasVersionedSpace => true;
        public override MapData Generate(MapGenerationSettings settings, bool terrainV2)
        {
            if (!terrainV2 || BaseTerrain == null || BaseTerrain.HasVersionedSpace || BaseTerrain.ImplementationId != "terrain-float32-retained-v1" || Assets == null)
                throw new InvalidOperationException("InvalidVNextFormalRetainedDependencies");
            MapData land = BaseTerrain.Generate(settings, false);
            switch (layoutRevision)
            {
                case MapVNextFormalRevision.Legacy:
                    return MapVNextRandomLayout.GenerateOnTerrain(land, settings.seed, settings.spawnProtectionRadius);
                case MapVNextFormalRevision.PreserveGroundConnectivity:
                    return MapVNextRandomLayout.GenerateOnTerrainPreservingGround(land, settings.seed, settings.spawnProtectionRadius);
                default: throw new InvalidOperationException("UnknownVNextFormalRevision");
            }
        }
        public override MapSimpleDecorationData GenerateDecorations(MapData map, MapGenerationSettings settings) => BaseTerrain.GenerateDecorations(map, settings);
        public override MapSpatialContext CreateSpatialContext(Guid generation, MapData map, MapTilemapRenderer terrain, MapGenerationSettings settings)
            => new MapVNextFormalSpace(generation, map, terrain, Assets, settings, ImplementationId);
    }

    public sealed class MapVNextFormalSpace : MapSpatialContext
    {
        private readonly Vector2[][] boundaries;
        private readonly MapNavigationLink[] links;
        private readonly MapVNextAssets assets;
        private PolygonCollider2D projectedEdges;
        private PhysicsMaterial2D retainedEdgeMaterial;
        public override MapCoordinateBoundary Coordinates { get; }
        public MapVNextFormalSpace(Guid generation, MapData map, MapTilemapRenderer terrain, MapVNextAssets assets, MapGenerationSettings settings)
            : this(generation, map, terrain, assets, settings, EcologyRecipeDefinition.VNextI1ImplementationId) { }
        public MapVNextFormalSpace(Guid generation, MapData map, MapTilemapRenderer terrain, MapVNextAssets assets, MapGenerationSettings settings, string implementationId)
            : base(generation, map, RequireKnownImplementation(implementationId))
        {
            Coordinates = terrain.Coordinates;
            this.assets = assets ?? throw new ArgumentNullException(nameof(assets));
            MapVNextFixture.Validate(map);
            boundaries = MapVNextStairGeometry.BuildWorldBoundaries(map, terrain).Concat(MapSurfaceV4Policy.LegacyFaces(map, settings).WorldPolygons(terrain.WaterBaseTilemap)).ToArray();
            var routes = new List<MapNavigationLink>();
            foreach (var ramp in MapVNextStairGeometry.Derive(map, terrain.Coordinates))
            {
                var a = terrain.Coordinates.WorldToCell(ramp.Entry); var b = terrain.Coordinates.WorldToCell(ramp.Exit);
                int Node(Vector2Int p) => (p.y - map.Origin.y) * map.Width + p.x - map.Origin.x;
                var link = new MapNavigationLink(Node(a), Node(b), new[] { (Vector2)terrain.Coordinates.CellToWorld(a), ramp.Entry, ramp.Middle, ramp.Exit, (Vector2)terrain.Coordinates.CellToWorld(b) });
                routes.Add(link); routes.Add(link.Reverse());
            }
            links = routes.ToArray();
        }
        private static string RequireKnownImplementation(string implementationId)
        {
            if (implementationId != EcologyRecipeDefinition.VNextI1ImplementationId &&
                implementationId != EcologyRecipeDefinition.VNextI1GroundConnectivityImplementationId)
                throw new InvalidOperationException("UnknownVNextSpatialImplementation");
            return implementationId;
        }
        public override IReadOnlyList<Vector2[]> Boundaries => Array.AsReadOnly(boundaries);
        public override IReadOnlyList<MapNavigationLink> NavigationLinks => Array.AsReadOnly(links);
        public override void RetireProjection(MapTilemapRenderer terrain)
        {
            if (projectedEdges != null) projectedEdges.sharedMaterial = retainedEdgeMaterial;
            projectedEdges = null; retainedEdgeMaterial = null;
        }
        public override void Project(MapTilemapRenderer terrain, MapGenerationSettings settings)
        {
            MapVNextProjection.Render(Map, terrain, settings, assets, GenerationId.ToString("N"), this);
            // The retained renderer also projects legacy Mountain faces. Their exact polygons
            // already belong to this context, so clear that separate owner before materialization.
            MapSurfaceV4Policy.Clear(terrain);
            var edges = terrain.WaterBaseTilemap.transform.parent.GetComponentsInChildren<PolygonCollider2D>(true).Single(p => p.name == "GeneratedCliffEdges");
            projectedEdges = edges; retainedEdgeMaterial = edges.sharedMaterial;
            edges.sharedMaterial = assets.PlayerPrefab.transform.Find("MovementFeet").GetComponent<CircleCollider2D>().sharedMaterial;
            if (settings is MapSurfaceV4Settings surface) MapSurfaceV4Visuals.Project(Map, terrain, surface);
        }
    }
}

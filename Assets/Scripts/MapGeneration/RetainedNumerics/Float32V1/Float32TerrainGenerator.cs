using System;
using UnityEngine;

namespace RpgDemo.RetainedTerrain.Float32V1
{
    public sealed class Float32TerrainGenerator : EcologyRetainedTerrainGenerator
    {
        public override string ImplementationId => "terrain-float32-retained-v1";
        public override MapData Generate(MapGenerationSettings settings, bool terrainV2)
            => RandomMapGenerator.Generate(settings, terrainV2);
        public override MapData GenerateWithElevationQualification(MapGenerationSettings settings, bool terrainV2, Func<MapData,bool> qualification)
        {
            if (!terrainV2 || qualification == null) return Generate(settings,terrainV2);
            MapData basis=RandomMapGenerator.Generate(settings,false);
            // Stay inside this retained -optimize- assembly for BOTH phases.
            return MapElevationPlanner.AddTwoLevels(basis,settings.seed,settings.spawnProtectionRadius,qualification);
        }
        public override MapSimpleDecorationData GenerateDecorations(MapData map, MapGenerationSettings settings)
            => MapSimpleDecorationGenerator.Generate(map, settings);
    }
}

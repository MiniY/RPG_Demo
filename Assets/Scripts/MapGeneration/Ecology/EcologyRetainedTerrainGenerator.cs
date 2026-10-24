using System;
using UnityEngine;

/// <summary>Pure retained numerical strategy; the original map controller still owns generation and commit.</summary>
public abstract class EcologyRetainedTerrainGenerator : ScriptableObject
{
    public abstract string ImplementationId { get; }
    public abstract MapData Generate(MapGenerationSettings settings, bool terrainV2);
    public virtual MapData GenerateWithElevationQualification(MapGenerationSettings settings, bool terrainV2, Func<MapData,bool> qualification)
    {
        if (qualification != null) throw new InvalidOperationException("Retained strategy has no qualified elevation implementation.");
        return Generate(settings,terrainV2);
    }
    public abstract MapSimpleDecorationData GenerateDecorations(MapData map, MapGenerationSettings settings);
}

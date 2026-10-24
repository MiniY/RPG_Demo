using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Flags]
public enum MapPopulationTerrainMask
{
    None = 0,
    Grass = 1 << 0,
    Path = 1 << 1,
    Forest = 1 << 2,
    Sand = 1 << 3
}

[Serializable]
public sealed class MapPopulationSpecies
{
    [SerializeField] private string typeId;
    [SerializeField] private MapPopulationTerrainMask terrains;
    [SerializeField, Min(1)] private int weight = 1;
    [SerializeField] private GameObject[] variants = Array.Empty<GameObject>();

    public MapPopulationSpecies(string typeId, MapPopulationTerrainMask terrains,
        int weight, params GameObject[] variants)
    {
        this.typeId = typeId;
        this.terrains = terrains;
        this.weight = weight;
        this.variants = variants == null ? Array.Empty<GameObject>() : (GameObject[])variants.Clone();
    }

    public string TypeId => typeId;
    public MapPopulationTerrainMask Terrains => terrains;
    public int Weight => weight;
    public IReadOnlyList<GameObject> Variants => variants;

    public bool Allows(MapTerrainType terrain)
    {
        MapPopulationTerrainMask bit;
        switch (terrain)
        {
            case MapTerrainType.Grass: bit = MapPopulationTerrainMask.Grass; break;
            case MapTerrainType.Path: bit = MapPopulationTerrainMask.Path; break;
            case MapTerrainType.Forest: bit = MapPopulationTerrainMask.Forest; break;
            case MapTerrainType.Sand: bit = MapPopulationTerrainMask.Sand; break;
            default: return false;
        }
        return (terrains & bit) != 0;
    }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(typeId) || terrains == MapPopulationTerrainMask.None ||
            weight < 1 || variants == null || variants.Length == 0)
            throw new ArgumentException("Population species needs an ID, terrain, weight, and prefab.");
        foreach (GameObject prefab in variants)
            if (prefab == null)
                throw new ArgumentException("Population species contains a missing prefab: " + typeId);
    }
}

[Serializable]
public sealed class MapPopulationTuning
{
    [SerializeField, Min(1)] private int requiredMinimum = 2;
    [SerializeField, Min(1)] private int cellsPerSpawn = 320;
    [SerializeField, Min(1)] private int maximum = 5;
    [SerializeField, Min(0)] private int minPathSteps = 10;
    [SerializeField, Min(0)] private int maxPathSteps = 80;
    [SerializeField, Min(1)] private int minimumSpacingSteps = 3;
    [SerializeField, Min(1)] private int crossRoleSpacingSteps = 2;
    [SerializeField, Min(1)] private int clusterRadius = 5;
    [SerializeField, Min(0)] private int exitSafetyRadius = 2;

    public MapPopulationTuning(int requiredMinimum, int cellsPerSpawn, int maximum,
        int minPathSteps, int maxPathSteps, int minimumSpacingSteps,
        int crossRoleSpacingSteps, int clusterRadius, int exitSafetyRadius)
    {
        this.requiredMinimum = requiredMinimum;
        this.cellsPerSpawn = cellsPerSpawn;
        this.maximum = maximum;
        this.minPathSteps = minPathSteps;
        this.maxPathSteps = maxPathSteps;
        this.minimumSpacingSteps = minimumSpacingSteps;
        this.crossRoleSpacingSteps = crossRoleSpacingSteps;
        this.clusterRadius = clusterRadius;
        this.exitSafetyRadius = exitSafetyRadius;
    }

    public int RequiredMinimum => requiredMinimum;
    public int CellsPerSpawn => cellsPerSpawn;
    public int Maximum => maximum;
    public int MinPathSteps => minPathSteps;
    public int MaxPathSteps => maxPathSteps;
    public int MinimumSpacingSteps => minimumSpacingSteps;
    public int CrossRoleSpacingSteps => crossRoleSpacingSteps;
    public int ClusterRadius => clusterRadius;
    public int ExitSafetyRadius => exitSafetyRadius;

    public int TargetForArea(int reachableCells) =>
        Mathf.Clamp(reachableCells / cellsPerSpawn, requiredMinimum, maximum);

    public void Validate()
    {
        if (requiredMinimum < 1 || cellsPerSpawn < 1 || maximum < requiredMinimum ||
            minPathSteps < 0 || maxPathSteps < minPathSteps ||
            minimumSpacingSteps < 1 || crossRoleSpacingSteps < 1 ||
            clusterRadius < 1 || exitSafetyRadius < 0)
            throw new ArgumentException("Invalid population tuning.");
    }
}

[CreateAssetMenu(fileName = "MapEcologyProfile", menuName = "RPG/Map/Ecology Profile")]
public sealed class MapEcologyProfile : ScriptableObject
{
    public const string ProfileId = "ecology-v1";
    public const string XiaoSuProfileId = "terrain-v2-xiaosu-v1";

    [SerializeField] private MapPopulationTuning monster =
        new MapPopulationTuning(2, 320, 5, 10, 80, 3, 2, 5, 2);
    [SerializeField] private MapPopulationTuning destructible =
        new MapPopulationTuning(4, 180, 8, 8, 80, 3, 2, 4, 2);
    [SerializeField] private MapPopulationSpecies[] monsterSpecies =
        Array.Empty<MapPopulationSpecies>();
    [SerializeField] private MapPopulationSpecies[] destructibleSpecies =
        Array.Empty<MapPopulationSpecies>();

    public MapPopulationTuning Monster => monster;
    public MapPopulationTuning Destructible => destructible;
    public IReadOnlyList<MapPopulationSpecies> MonsterSpecies => monsterSpecies;
    public IReadOnlyList<MapPopulationSpecies> DestructibleSpecies => destructibleSpecies;

    public IReadOnlyList<MapPopulationSpecies> MonsterSpeciesFor(string profileId)
    {
        if (string.Equals(profileId, XiaoSuProfileId, StringComparison.Ordinal))
            throw new InvalidOperationException(EcologyRecipeDefinition.RetiredContentMessage);
        return monsterSpecies;
    }

    public void Configure(MapPopulationSpecies[] monsters, MapPopulationSpecies[] destructibles)
    {
        monsterSpecies = monsters == null ? Array.Empty<MapPopulationSpecies>() :
            (MapPopulationSpecies[])monsters.Clone();
        destructibleSpecies = destructibles == null ? Array.Empty<MapPopulationSpecies>() :
            (MapPopulationSpecies[])destructibles.Clone();
        Validate();
    }

    public void Validate()
    {
        monster.Validate();
        destructible.Validate();
        ValidateSpecies(monsterSpecies, "Monster");
        ValidateSpecies(destructibleSpecies, "Destructible");
    }

    private static void ValidateSpecies(MapPopulationSpecies[] species, string role)
    {
        if (species == null || species.Length == 0)
            throw new ArgumentException(role + " species list is empty.");
        HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (MapPopulationSpecies entry in species)
        {
            if (entry == null)
                throw new ArgumentException(role + " species contains a null entry.");
            entry.Validate();
            if (!ids.Add(entry.TypeId))
                throw new ArgumentException(role + " species has duplicate ID " + entry.TypeId);
        }
    }
}

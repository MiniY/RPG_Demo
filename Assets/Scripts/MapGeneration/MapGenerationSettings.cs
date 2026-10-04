using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.Tilemaps;

/// <summary>
/// 保存随机地图生成所需的尺寸、算法参数和 Tile（瓦片）引用。
/// </summary>
[CreateAssetMenu(fileName = "MapGenerationSettings", menuName = "RPG Demo/Map Generation Settings")]
public class MapGenerationSettings : ScriptableObject
{
    /// <summary>
    /// 当前随机地图算法的数据版本。
    /// </summary>
    public const int CurrentGeneratorVersion = 5;

    /// <summary>
    /// 保存本次配置对应的生成器版本，供未来存档兼容检查使用。
    /// </summary>
    [Header("Versioning（版本信息）")]
    [Min(1)] public int generatorVersion = CurrentGeneratorVersion;

    /// <summary>
    /// 标识当前配置使用的地形调色板。
    /// </summary>
    public string terrainPaletteId = "tiny-swords-grass-sand-rock-v1";

    /// <summary>
    /// 地图的宽度，单位是 Tile（瓦片）数量。
    /// </summary>
    [Header("Map Size（地图尺寸）")]
    [Min(8)] public int mapWidth = 64;

    /// <summary>
    /// 地图的高度，单位是 Tile（瓦片）数量。
    /// </summary>
    [Min(8)] public int mapHeight = 64;

    /// <summary>
    /// 地图左下角的 Grid（网格）坐标。
    /// </summary>
    public Vector2Int mapOrigin = Vector2Int.zero;

    /// <summary>
    /// 是否把出生点自动放在地图中心。
    /// </summary>
    public bool useCenteredSpawn = true;

    /// <summary>
    /// 不使用中心出生点时采用的出生网格坐标。
    /// </summary>
    public Vector2Int spawnCell = new Vector2Int(32, 32);

    /// <summary>
    /// 地图外圈强制设置为水域的宽度。
    /// </summary>
    [Min(1)] public int borderSize = 1;

    /// <summary>
    /// 用于复现地图的伪随机种子。
    /// </summary>
    [Header("Seeded Perlin Noise（带种子的柏林噪声）")]
    public int seed = 20260929;

    /// <summary>
    /// 柏林噪声的采样缩放，数值越小，水域越连贯。
    /// </summary>
    [Min(0.001f)] public float noiseScale = 0.08f;

    /// <summary>
    /// 噪声低于此值时生成水域。
    /// </summary>
    [Range(0f, 1f)] public float waterThreshold = 0.32f;

    /// <summary>
    /// 高于深水阈值且低于此值时生成浅水。
    /// </summary>
    [Range(0f, 1f)] public float shallowWaterThreshold = 0.42f;

    /// <summary>
    /// 高于浅水阈值且低于此值时生成自然沙地。
    /// </summary>
    [Range(0f, 1f)] public float sandHeightThreshold = 0.48f;

    /// <summary>
    /// 湿度、温度和地形分类共用的噪声采样缩放。
    /// </summary>
    [Min(0.001f)] public float biomeNoiseScale = 0.055f;

    /// <summary>
    /// 湿度高于此值时，非山地单元倾向生成森林。
    /// </summary>
    [Range(0f, 1f)] public float forestMoistureThreshold = 0.58f;

    /// <summary>
    /// 森林温度通道高于此值时，湿润区域才会生成森林。
    /// </summary>
    [Range(0f, 1f)] public float forestTemperatureThreshold = 0.45f;

    /// <summary>
    /// 高度高于此值且温度较低时生成山地。
    /// </summary>
    [Range(0f, 1f)] public float mountainHeightThreshold = 0.72f;

    /// <summary>
    /// 温度低于此值时，高地才会被分类为山地。
    /// </summary>
    [Range(0f, 1f)] public float mountainTemperatureThreshold = 0.55f;

    /// <summary>
    /// 四方向连通区域小于此面积时，将其替换为周围占多数的自然地形。
    /// </summary>
    [Header("Natural Region Cleanup（自然区域清理）")]
    [Min(1)] public int minimumNaturalRegionSize = 3;

    /// <summary>
    /// 出生点周围强制保留为草地的半径。
    /// </summary>
    [Min(1)] public int spawnProtectionRadius = 5;

    /// <summary>
    /// 道路的宽度，单位是 Tile（瓦片）数量。
    /// </summary>
    [Header("Path（道路）")]
    [Min(2)] public int roadWidth = 2;

    /// <summary>
    /// 所有横向或竖向可行走通路允许的最小宽度。
    /// </summary>
    [Min(2)] public int minimumPassageWidth = 2;

    /// <summary>
    /// 随机道路每一步改变方向的概率。
    /// </summary>
    [Range(0f, 1f)] public float roadTurnChance = 0.3f;

    /// <summary>
    /// 简单装饰阶段使用的派生随机种子偏移，避免装饰随机流影响地形随机流。
    /// </summary>
    [Header("Simple Decoration（简单装饰）")]
    [FormerlySerializedAs("decorationSeedOffset")]
    public int simpleDecorationSeedOffset = 7919;

    /// <summary>
    /// 简单装饰密度噪声的采样缩放，数值越小越容易形成装饰物簇。
    /// </summary>
    [FormerlySerializedAs("decorationNoiseScale")]
    [Min(0.001f)] public float simpleDecorationNoiseScale = 0.12f;

    /// <summary>
    /// 普通草地放置完整树木的基础概率。
    /// </summary>
    [FormerlySerializedAs("grassDecorationDensity")]
    [Range(0f, 1f)] public float grassTreeDensity = 0.018f;

    /// <summary>
    /// 森林地表放置完整树木的基础概率。
    /// </summary>
    [FormerlySerializedAs("forestDecorationDensity")]
    [Range(0f, 1f)] public float forestTreeDensity = 0.1f;

    /// <summary>
    /// 普通草地放置灌木的基础概率。
    /// </summary>
    [Range(0f, 1f)] public float grassBushDensity = 0.025f;

    /// <summary>
    /// 森林地表放置灌木的基础概率。
    /// </summary>
    [Range(0f, 1f)] public float forestBushDensity = 0.06f;

    /// <summary>
    /// 自然沙地放置散落岩石的基础概率。
    /// </summary>
    [Range(0f, 1f)] public float sandRockDensity = 0.025f;

    /// <summary>
    /// 普通草地放置散落岩石的基础概率。
    /// </summary>
    [Range(0f, 1f)] public float grassRockDensity = 0.012f;

    /// <summary>
    /// 森林地表放置散落岩石的基础概率。
    /// </summary>
    [Range(0f, 1f)] public float forestRockDensity = 0.008f;

    /// <summary>
    /// 两个简单装饰物占地之间至少间隔的网格距离。
    /// </summary>
    [FormerlySerializedAs("decorationMinimumSpacing")]
    [Min(1)] public int simpleDecorationMinimumSpacing = 2;

    /// <summary>
    /// 出生点周围不放置简单装饰物的安全半径。
    /// </summary>
    [FormerlySerializedAs("decorationSpawnClearRadius")]
    [Min(0)] public int simpleDecorationSpawnClearRadius = 6;

    /// <summary>
    /// 出口周围不放置简单装饰物的净空半径。
    /// </summary>
    [FormerlySerializedAs("decorationExitClearRadius")]
    [Min(0)] public int simpleDecorationExitClearRadius = 2;

    /// <summary>
    /// Sand Base（沙地底层）使用的 16 状态自动瓦片集合。
    /// </summary>
    [Header("Layered Terrain Tiles（分层地形瓦片）")]
    public TerrainAutotileSet sandAutotileSet;

    /// <summary>
    /// Grass Overlay（草地覆盖层）使用的 16 状态自动瓦片集合。
    /// </summary>
    public TerrainAutotileSet grassAutotileSet;

    /// <summary>
    /// Elevation（高地层）使用的顶面和南侧崖面自动瓦片集合。
    /// </summary>
    public TerrainAutotileSet elevationAutotileSet;

    /// <summary>
    /// 地表层显示用的水域 Tile（瓦片）。
    /// </summary>
    public TileBase waterTile;

    /// <summary>
    /// 地表层显示用的浅水 Tile（瓦片）。
    /// </summary>
    public TileBase shallowWaterTile;

    /// <summary>
    /// 旧版固定草地瓦片引用，仅为已有资产的序列化兼容保留。
    /// </summary>
    [HideInInspector] public TileBase grassTile;

    /// <summary>
    /// 地表层显示用的道路 Tile（瓦片）。
    /// </summary>
    [HideInInspector] public TileBase pathTile;

    /// <summary>
    /// 地表层显示用的森林地表 Tile（瓦片）。
    /// </summary>
    [HideInInspector] public TileBase forestTile;

    /// <summary>
    /// 地表层显示用的山地 Tile（瓦片）。
    /// </summary>
    [HideInInspector] public TileBase mountainTile;

    /// <summary>
    /// 树木、灌木和散落岩石使用的固定简单装饰调色板。
    /// </summary>
    public MapSimpleDecorationPalette simpleDecorationPalette;

    /// <summary>
    /// 旧版单格树木 Tile（瓦片）集合，仅为已有配置的序列化兼容保留。
    /// </summary>
    [HideInInspector] public TileBase[] treeTiles = new TileBase[0];

    /// <summary>
    /// 碰撞层使用的不可见碰撞标记 Tile（瓦片）。
    /// </summary>
    public TileBase collisionMarkerTile;

    /// <summary>
    /// 树木和散落岩石使用的较小隐藏碰撞标记 Tile（瓦片）。
    /// </summary>
    public TileBase simpleDecorationCollisionMarkerTile;

    /// <summary>
    /// Water Base（水体底层）的 Sorting Order（排序顺序）。
    /// </summary>
    [Header("Rendering（渲染）")]
    public int waterBaseSortingOrder = -20;

    /// <summary>
    /// Sand Base（沙地底层）的 Sorting Order（排序顺序）。
    /// </summary>
    public int sandBaseSortingOrder = -10;

    /// <summary>
    /// Grass Overlay（草地覆盖层）的 Sorting Order（排序顺序）。
    /// </summary>
    public int grassOverlaySortingOrder = 0;

    /// <summary>
    /// Elevation（高地层）的 Sorting Order（排序顺序）。
    /// </summary>
    public int elevationSortingOrder = 2;

    /// <summary>
    /// 旧版地表排序值，仅为已有配置资产的序列化兼容保留。
    /// </summary>
    [HideInInspector] public int groundSortingOrder = 0;

    /// <summary>
    /// 简单装饰地表 Tilemap（瓦片地图）的 Sorting Order（排序顺序）。
    /// </summary>
    [FormerlySerializedAs("decorationSortingOrder")]
    public int simpleDecorationGroundSortingOrder = 4;

    /// <summary>
    /// 简单装饰树冠 Tilemap（瓦片地图）的 Sorting Order（排序顺序）。
    /// </summary>
    public int simpleDecorationCanopySortingOrder = 6;

    /// <summary>
    /// 根据当前配置计算出生点网格坐标。
    /// </summary>
    /// <returns>出生点网格坐标。</returns>
    public Vector2Int GetSpawnCell()
    {
        if (useCenteredSpawn)
        {
            return mapOrigin + new Vector2Int(mapWidth / 2, mapHeight / 2);
        }

        return spawnCell;
    }

    /// <summary>
    /// 在 Inspector（检视面板）修改参数时约束配置范围。
    /// </summary>
    private void OnValidate()
    {
        generatorVersion = Mathf.Max(1, generatorVersion);
        if (string.IsNullOrWhiteSpace(terrainPaletteId))
            terrainPaletteId = "tiny-swords-grass-sand-rock-v1";

        mapWidth = Mathf.Max(8, mapWidth);
        mapHeight = Mathf.Max(8, mapHeight);
        borderSize = Mathf.Clamp(borderSize, 1, Mathf.Min(mapWidth, mapHeight) / 2 - 1);
        noiseScale = Mathf.Max(0.001f, noiseScale);
        waterThreshold = Mathf.Clamp(waterThreshold, 0f, 0.97f);
        shallowWaterThreshold = Mathf.Clamp(
            shallowWaterThreshold,
            waterThreshold + 0.01f,
            0.98f);
        sandHeightThreshold = Mathf.Clamp(
            sandHeightThreshold,
            shallowWaterThreshold + 0.01f,
            0.99f);
        biomeNoiseScale = Mathf.Max(0.001f, biomeNoiseScale);
        forestMoistureThreshold = Mathf.Clamp01(forestMoistureThreshold);
        forestTemperatureThreshold = Mathf.Clamp01(forestTemperatureThreshold);
        mountainHeightThreshold = Mathf.Clamp(
            mountainHeightThreshold,
            sandHeightThreshold + 0.01f,
            1f);
        mountainTemperatureThreshold = Mathf.Clamp01(mountainTemperatureThreshold);
        minimumNaturalRegionSize = Mathf.Max(1, minimumNaturalRegionSize);
        spawnProtectionRadius = Mathf.Max(1, spawnProtectionRadius);
        roadWidth = Mathf.Max(2, roadWidth);
        minimumPassageWidth = Mathf.Max(2, minimumPassageWidth);
        roadTurnChance = Mathf.Clamp01(roadTurnChance);
        simpleDecorationNoiseScale = Mathf.Max(0.001f, simpleDecorationNoiseScale);
        grassTreeDensity = Mathf.Clamp01(grassTreeDensity);
        forestTreeDensity = Mathf.Clamp01(forestTreeDensity);
        grassBushDensity = Mathf.Clamp01(grassBushDensity);
        forestBushDensity = Mathf.Clamp01(forestBushDensity);
        sandRockDensity = Mathf.Clamp01(sandRockDensity);
        grassRockDensity = Mathf.Clamp01(grassRockDensity);
        forestRockDensity = Mathf.Clamp01(forestRockDensity);
        simpleDecorationMinimumSpacing = Mathf.Max(1, simpleDecorationMinimumSpacing);
        simpleDecorationSpawnClearRadius = Mathf.Max(0, simpleDecorationSpawnClearRadius);
        simpleDecorationExitClearRadius = Mathf.Max(0, simpleDecorationExitClearRadius);
    }
}

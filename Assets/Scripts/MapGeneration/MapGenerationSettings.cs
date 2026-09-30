using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 保存随机地图生成所需的尺寸、算法参数和 Tile（瓦片）引用。
/// </summary>
[CreateAssetMenu(fileName = "MapGenerationSettings", menuName = "RPG Demo/Map Generation Settings")]
public class MapGenerationSettings : ScriptableObject
{
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
    /// 出生点周围强制保留为草地的半径。
    /// </summary>
    [Min(1)] public int spawnProtectionRadius = 5;

    /// <summary>
    /// 道路的宽度，单位是 Tile（瓦片）数量。
    /// </summary>
    [Header("Path（道路）")]
    [Min(1)] public int roadWidth = 2;

    /// <summary>
    /// 随机道路每一步改变方向的概率。
    /// </summary>
    [Range(0f, 1f)] public float roadTurnChance = 0.3f;

    /// <summary>
    /// 装饰物阶段使用的派生随机种子偏移，避免装饰随机流影响地形随机流。
    /// </summary>
    [Header("Decoration（装饰物）")]
    public int decorationSeedOffset = 7919;

    /// <summary>
    /// 装饰物密度噪声的采样缩放，数值越小越容易形成装饰物簇。
    /// </summary>
    [Min(0.001f)] public float decorationNoiseScale = 0.12f;

    /// <summary>
    /// 普通草地放置树木的基础概率。
    /// </summary>
    [Range(0f, 1f)] public float grassDecorationDensity = 0.04f;

    /// <summary>
    /// 森林地表放置树木的基础概率。
    /// </summary>
    [Range(0f, 1f)] public float forestDecorationDensity = 0.38f;

    /// <summary>
    /// 两个装饰物之间至少间隔的网格距离。
    /// </summary>
    [Min(1)] public int decorationMinimumSpacing = 2;

    /// <summary>
    /// 出生点周围不放置装饰物的安全半径。
    /// </summary>
    [Min(0)] public int decorationSpawnClearRadius = 6;

    /// <summary>
    /// 出口周围不放置装饰物的净空半径。
    /// </summary>
    [Min(0)] public int decorationExitClearRadius = 2;

    /// <summary>
    /// 地表层显示用的草地 Tile（瓦片）。
    /// </summary>
    [Header("Tile References（瓦片引用）")]
    public TileBase grassTile;

    /// <summary>
    /// 地表层显示用的水域 Tile（瓦片）。
    /// </summary>
    public TileBase waterTile;

    /// <summary>
    /// 地表层显示用的浅水 Tile（瓦片）。
    /// </summary>
    public TileBase shallowWaterTile;

    /// <summary>
    /// 地表层显示用的道路 Tile（瓦片）。
    /// </summary>
    public TileBase pathTile;

    /// <summary>
    /// 地表层显示用的森林地表 Tile（瓦片）。
    /// </summary>
    public TileBase forestTile;

    /// <summary>
    /// 地表层显示用的山地 Tile（瓦片）。
    /// </summary>
    public TileBase mountainTile;

    /// <summary>
    /// 装饰层随机选择的树木 Tile（瓦片）集合。
    /// </summary>
    public TileBase[] treeTiles = new TileBase[0];

    /// <summary>
    /// 碰撞层使用的不可见碰撞标记 Tile（瓦片）。
    /// </summary>
    public TileBase collisionMarkerTile;

    /// <summary>
    /// 地表 Tilemap（瓦片地图）的 Sorting Order（排序顺序）。
    /// </summary>
    [Header("Rendering（渲染）")]
    public int groundSortingOrder = 0;

    /// <summary>
    /// 装饰 Tilemap（瓦片地图）的 Sorting Order（排序顺序）。
    /// </summary>
    public int decorationSortingOrder = 4;

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
        mapWidth = Mathf.Max(8, mapWidth);
        mapHeight = Mathf.Max(8, mapHeight);
        borderSize = Mathf.Clamp(borderSize, 1, Mathf.Min(mapWidth, mapHeight) / 2 - 1);
        noiseScale = Mathf.Max(0.001f, noiseScale);
        waterThreshold = Mathf.Clamp01(waterThreshold);
        shallowWaterThreshold = Mathf.Clamp01(shallowWaterThreshold);
        if (shallowWaterThreshold <= waterThreshold)
            shallowWaterThreshold = Mathf.Min(1f, waterThreshold + 0.01f);
        biomeNoiseScale = Mathf.Max(0.001f, biomeNoiseScale);
        forestMoistureThreshold = Mathf.Clamp01(forestMoistureThreshold);
        forestTemperatureThreshold = Mathf.Clamp01(forestTemperatureThreshold);
        mountainHeightThreshold = Mathf.Clamp01(mountainHeightThreshold);
        mountainTemperatureThreshold = Mathf.Clamp01(mountainTemperatureThreshold);
        spawnProtectionRadius = Mathf.Max(1, spawnProtectionRadius);
        roadWidth = Mathf.Max(1, roadWidth);
        roadTurnChance = Mathf.Clamp01(roadTurnChance);
        decorationNoiseScale = Mathf.Max(0.001f, decorationNoiseScale);
        grassDecorationDensity = Mathf.Clamp01(grassDecorationDensity);
        forestDecorationDensity = Mathf.Clamp01(forestDecorationDensity);
        decorationMinimumSpacing = Mathf.Max(1, decorationMinimumSpacing);
        decorationSpawnClearRadius = Mathf.Max(0, decorationSpawnClearRadius);
        decorationExitClearRadius = Mathf.Max(0, decorationExitClearRadius);
    }
}

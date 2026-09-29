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
    /// 地表层显示用的草地 Tile（瓦片）。
    /// </summary>
    [Header("Tile References（瓦片引用）")]
    public TileBase grassTile;

    /// <summary>
    /// 地表层显示用的水域 Tile（瓦片）。
    /// </summary>
    public TileBase waterTile;

    /// <summary>
    /// 地表层显示用的道路 Tile（瓦片）。
    /// </summary>
    public TileBase pathTile;

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
        spawnProtectionRadius = Mathf.Max(1, spawnProtectionRadius);
        roadWidth = Mathf.Max(1, roadWidth);
        roadTurnChance = Mathf.Clamp01(roadTurnChance);
    }
}

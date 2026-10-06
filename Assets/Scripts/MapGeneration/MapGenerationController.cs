using System;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 协调地图数据生成、Tilemap 渲染和玩家出生点定位。
/// </summary>
public class MapGenerationController : MonoBehaviour
{
    /// <summary>
    /// 当前地图使用的生成配置。
    /// </summary>
    [SerializeField] private MapGenerationSettings settings;

    /// <summary>
    /// 负责把地图数据写入 Tilemap 的渲染器。
    /// </summary>
    [SerializeField] private MapTilemapRenderer tilemapRenderer;

    /// <summary>
    /// 负责根据最终地图数据写入简单装饰 Tilemap 的渲染器。
    /// </summary>
    [FormerlySerializedAs("decorationRenderer")]
    [SerializeField] private MapSimpleDecorationRenderer simpleDecorationRenderer;

    /// <summary>
    /// 测试场景中的玩家 Transform（变换组件）。
    /// </summary>
    [SerializeField] private Transform player;

    /// <summary>
    /// 是否在进入 Play Mode（运行模式）时重新生成地图。
    /// </summary>
    [SerializeField] private bool generateOnPlay;

    /// <summary>
    /// 生成地图后是否把玩家移动到出生点。
    /// </summary>
    [SerializeField] private bool movePlayerToSpawn = true;

    /// <summary>
    /// 是否在 Console（控制台）输出本次生成摘要。
    /// </summary>
    [SerializeField] private bool logGenerationSummary = true;

    /// <summary>
    /// 最近一次生成的运行时地图数据。
    /// </summary>
    private MapData lastGeneratedMap;

    /// <summary>
    /// 最近一次生成的简单装饰物数据。
    /// </summary>
    private MapSimpleDecorationData lastGeneratedSimpleDecorations;

    /// <summary>
    /// 地图生成完成后通知小地图和其他观察者。
    /// </summary>
    public event Action<MapData> MapGenerated;

    /// <summary>
    /// 地图被清空后通知小地图和其他观察者。
    /// </summary>
    public event Action MapCleared;

    /// <summary>
    /// 获取当前地图配置。
    /// </summary>
    public MapGenerationSettings Settings => settings;

    /// <summary>
    /// 获取 Tilemap 渲染器。
    /// </summary>
    public MapTilemapRenderer TilemapRenderer => tilemapRenderer;

    /// <summary>
    /// 获取简单装饰 Tilemap 渲染器。
    /// </summary>
    public MapSimpleDecorationRenderer SimpleDecorationRenderer =>
        simpleDecorationRenderer;

    /// <summary>
    /// 获取生成地图后需要移动到出生点的玩家变换组件。
    /// </summary>
    public Transform Player => player;

    /// <summary>
    /// 获取最近一次生成的地图数据。
    /// </summary>
    public MapData LastGeneratedMap => lastGeneratedMap;

    /// <summary>
    /// 获取最近一次生成的简单装饰物数据。
    /// </summary>
    public MapSimpleDecorationData LastGeneratedSimpleDecorations =>
        lastGeneratedSimpleDecorations;

    /// <summary>
    /// 根据配置生成并渲染地图。
    /// </summary>
    public void GenerateMap()
    {
        if (settings == null)
        {
            Debug.LogError("MapGenerationController 缺少 MapGenerationSettings。", this);
            return;
        }

        if (tilemapRenderer == null)
        {
            Debug.LogError("MapGenerationController 缺少 MapTilemapRenderer。", this);
            return;
        }

        try
        {
            lastGeneratedMap = RandomMapGenerator.Generate(settings);
            tilemapRenderer.Render(lastGeneratedMap, settings);

            // 简单装饰读取最终地图数据，并使用独立随机流保持地形结果稳定。
            lastGeneratedSimpleDecorations =
                MapSimpleDecorationGenerator.Generate(lastGeneratedMap, settings);

            if (simpleDecorationRenderer != null)
            {
                simpleDecorationRenderer.Render(
                    lastGeneratedSimpleDecorations,
                    lastGeneratedMap,
                    settings);
            }

            if (movePlayerToSpawn)
                MovePlayerToSpawn(lastGeneratedMap);

            MapGenerated?.Invoke(lastGeneratedMap);

            if (logGenerationSummary)
            {
                Debug.Log(
                    $"地图生成完成：Seed={settings.seed}，尺寸={settings.mapWidth}x{settings.mapHeight}，" +
                    $"出生点={lastGeneratedMap.SpawnCell}，道路终点={lastGeneratedMap.ExitCell}，" +
                    $"简单装饰={lastGeneratedSimpleDecorations.Count}，" +
                    $"树木={lastGeneratedSimpleDecorations.CountByType(MapSimpleDecorationType.Tree)}，" +
                    $"灌木={lastGeneratedSimpleDecorations.CountByType(MapSimpleDecorationType.Bush)}，" +
                    $"散落岩石={lastGeneratedSimpleDecorations.CountByType(MapSimpleDecorationType.ScatteredRock)}。",
                    this);
            }
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception, this);
        }
    }

    /// <summary>
    /// 清空地表和碰撞 Tilemap，但不删除配置、角色或场景对象。
    /// </summary>
    public void ClearMap()
    {
        if (tilemapRenderer != null)
            tilemapRenderer.Clear();

        if (simpleDecorationRenderer != null)
            simpleDecorationRenderer.Clear();

        lastGeneratedMap = null;
        lastGeneratedSimpleDecorations = null;
        MapCleared?.Invoke();
    }

    /// <summary>
    /// 使用当前配置重新生成地图。
    /// </summary>
    public void RegenerateMap()
    {
        ClearMap();
        GenerateMap();
    }

    /// <summary>
    /// 为配置设置一个新的随机种子，但不立即生成地图。
    /// </summary>
    public void RandomizeSeed()
    {
        if (settings == null)
        {
            Debug.LogError("MapGenerationController 缺少 MapGenerationSettings。", this);
            return;
        }

        settings.seed = System.Guid.NewGuid().GetHashCode();
    }

    /// <summary>
    /// 在运行时自动生成地图，默认关闭以保留编辑器生成结果。
    /// </summary>
    private void Start()
    {
        if (generateOnPlay)
            GenerateMap();
    }

    /// <summary>
    /// 把玩家移动到地图数据中的出生单元中心。
    /// </summary>
    /// <param name="mapData">最近生成的地图数据。</param>
    private void MovePlayerToSpawn(MapData mapData)
    {
        if (player == null || tilemapRenderer == null)
            return;

        Vector3 spawnWorldPosition = tilemapRenderer.GetCellCenterWorld(mapData.SpawnCell);
        spawnWorldPosition.z = player.position.z;
        player.position = spawnWorldPosition;
    }

    /// <summary>
    /// 在 Scene（场景）视图中显示出生点和道路终点辅助图形。
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        if (lastGeneratedMap == null || tilemapRenderer == null)
            return;

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(tilemapRenderer.GetCellCenterWorld(lastGeneratedMap.SpawnCell), 0.35f);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(tilemapRenderer.GetCellCenterWorld(lastGeneratedMap.ExitCell), 0.35f);
    }
}

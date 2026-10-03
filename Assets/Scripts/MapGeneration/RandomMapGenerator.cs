using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 根据 MapGenerationSettings（地图生成配置）创建可复现的 MapData（地图数据）。
/// </summary>
public static class RandomMapGenerator
{
    /// <summary>
    /// 根据配置生成一张包含多通道生物群系和道路的地图。
    /// </summary>
    /// <param name="settings">地图生成配置。</param>
    /// <returns>生成完成的地图数据。</returns>
    public static MapData Generate(MapGenerationSettings settings)
    {
        if (settings == null)
            throw new ArgumentNullException(nameof(settings));

        ValidateSettings(settings);

        MapData mapData = new MapData(
            settings.mapWidth,
            settings.mapHeight,
            settings.mapOrigin);

        System.Random random = new System.Random(settings.seed);
        Vector2Int spawnCell = ClampToPlayableArea(settings.GetSpawnCell(), mapData, settings);
        mapData.SpawnCell = spawnCell;

        Vector2 heightOffset = CreateNoiseOffset(random);
        Vector2 moistureOffset = CreateNoiseOffset(random);
        Vector2 temperatureOffset = CreateNoiseOffset(random);
        FillWithNoise(mapData, settings, heightOffset, moistureOffset, temperatureOffset);
        MapTerrainRegionCleaner.Clean(
            mapData,
            settings.minimumNaturalRegionSize,
            settings.borderSize,
            spawnCell,
            settings.spawnProtectionRadius);
        ApplyMapBorder(mapData, settings.borderSize);
        ProtectSpawnArea(mapData, spawnCell, settings);

        Vector2Int exitCell = CreatePathToEdge(mapData, settings, random, spawnCell);
        mapData.ExitCell = exitCell;
        RemoveUnreachableWalkableCells(mapData, spawnCell);

        return mapData;
    }

    /// <summary>
    /// 校验地图生成配置，尽早报告会导致错误结果的参数。
    /// </summary>
    /// <param name="settings">待校验的地图配置。</param>
    private static void ValidateSettings(MapGenerationSettings settings)
    {
        if (settings.mapWidth <= 0 || settings.mapHeight <= 0)
            throw new InvalidOperationException("地图宽度和高度必须大于 0。");

        if (settings.borderSize < 1)
            throw new InvalidOperationException("地图边界厚度必须至少为 1，才能阻止玩家离开地图。");

        if (settings.borderSize * 2 >= settings.mapWidth || settings.borderSize * 2 >= settings.mapHeight)
            throw new InvalidOperationException("地图边界过宽，必须为内部道路保留空间。");

        if (settings.noiseScale <= 0f)
            throw new InvalidOperationException("柏林噪声缩放必须大于 0。");

        if (settings.waterThreshold < 0f || settings.waterThreshold > 1f)
            throw new InvalidOperationException("水域阈值必须位于 0 到 1 之间。");

        if (settings.shallowWaterThreshold <= settings.waterThreshold ||
            settings.sandHeightThreshold <= settings.shallowWaterThreshold ||
            settings.mountainHeightThreshold <= settings.sandHeightThreshold ||
            settings.mountainHeightThreshold > 1f)
        {
            throw new InvalidOperationException(
                "地形阈值必须满足 water < shallowWater < sand < mountain <= 1。");
        }

        if (settings.biomeNoiseScale <= 0f)
            throw new InvalidOperationException("生物群系噪声缩放必须大于 0。");

        if (settings.forestMoistureThreshold < 0f || settings.forestMoistureThreshold > 1f ||
            settings.forestTemperatureThreshold < 0f || settings.forestTemperatureThreshold > 1f)
        {
            throw new InvalidOperationException("森林湿度和温度阈值必须位于 0 到 1 之间。");
        }

        if (settings.mountainTemperatureThreshold < 0f ||
            settings.mountainTemperatureThreshold > 1f)
        {
            throw new InvalidOperationException("山地温度阈值必须位于 0 到 1 之间。");
        }

        if (settings.minimumNaturalRegionSize < 1)
            throw new InvalidOperationException("自然区域最小面积必须至少为 1。");

        if (settings.spawnProtectionRadius < 1)
            throw new InvalidOperationException("出生点保护半径必须至少为 1。");

        if (settings.roadWidth < 1)
            throw new InvalidOperationException("道路宽度必须至少为 1。");

        if (settings.roadTurnChance < 0f || settings.roadTurnChance > 1f)
            throw new InvalidOperationException("道路转向概率必须位于 0 到 1 之间。");

        int minimumSpawnMargin = settings.borderSize + settings.spawnProtectionRadius;
        if (settings.mapWidth <= minimumSpawnMargin * 2 || settings.mapHeight <= minimumSpawnMargin * 2)
            throw new InvalidOperationException("地图尺寸太小，无法容纳出生点保护区和地图边界。");
    }

    /// <summary>
    /// 根据 Seed（种子）创建稳定的噪声偏移量，避免不同种子只改变单元顺序。
    /// </summary>
    /// <param name="random">本次地图专用的伪随机生成器。</param>
    /// <returns>柏林噪声的二维偏移量。</returns>
    private static Vector2 CreateNoiseOffset(System.Random random)
    {
        float offsetX = (float)(random.NextDouble() * 100000.0);
        float offsetY = (float)(random.NextDouble() * 100000.0);
        return new Vector2(offsetX, offsetY);
    }

    /// <summary>
    /// 使用高度、湿度和温度三个独立噪声通道分类地图地形。
    /// </summary>
    /// <param name="mapData">待填充的地图数据。</param>
    /// <param name="settings">地图生成配置。</param>
    /// <param name="heightOffset">高度通道使用的二维偏移量。</param>
    /// <param name="moistureOffset">湿度通道使用的二维偏移量。</param>
    /// <param name="temperatureOffset">温度通道使用的二维偏移量。</param>
    private static void FillWithNoise(
        MapData mapData,
        MapGenerationSettings settings,
        Vector2 heightOffset,
        Vector2 moistureOffset,
        Vector2 temperatureOffset)
    {
        for (int x = mapData.Origin.x; x < mapData.Origin.x + mapData.Width; x++)
        {
            for (int y = mapData.Origin.y; y < mapData.Origin.y + mapData.Height; y++)
            {
                float localX = x - mapData.Origin.x;
                float localY = y - mapData.Origin.y;
                float heightValue = SampleNoise(localX, localY, heightOffset, settings.noiseScale);
                MapTerrainType terrainType;

                if (heightValue < settings.waterThreshold)
                {
                    terrainType = MapTerrainType.DeepWater;
                }
                else if (heightValue < settings.shallowWaterThreshold)
                {
                    terrainType = MapTerrainType.ShallowWater;
                }
                else if (heightValue < settings.sandHeightThreshold)
                {
                    terrainType = MapTerrainType.Sand;
                }
                else
                {
                    float moistureValue = SampleNoise(
                        localX,
                        localY,
                        moistureOffset,
                        settings.biomeNoiseScale);
                    float temperatureValue = SampleNoise(
                        localX,
                        localY,
                        temperatureOffset,
                        settings.biomeNoiseScale);

                    terrainType = ClassifyLandTerrain(
                        heightValue,
                        moistureValue,
                        temperatureValue,
                        settings);
                }

                mapData.SetTerrain(new Vector2Int(x, y), terrainType);
            }
        }
    }

    /// <summary>
    /// 对一个噪声通道采样，保证各通道使用统一的坐标规则。
    /// </summary>
    /// <param name="localX">相对于地图原点的局部 X 坐标。</param>
    /// <param name="localY">相对于地图原点的局部 Y 坐标。</param>
    /// <param name="offset">当前噪声通道的独立偏移量。</param>
    /// <param name="scale">当前噪声通道的采样缩放。</param>
    /// <returns>范围约为 0 到 1 的噪声值。</returns>
    private static float SampleNoise(float localX, float localY, Vector2 offset, float scale)
    {
        float sampleX = (localX + offset.x) * scale;
        float sampleY = (localY + offset.y) * scale;
        return Mathf.PerlinNoise(sampleX, sampleY);
    }

    /// <summary>
    /// 使用湿度、温度和高度把陆地分类为草地、森林或山地。
    /// </summary>
    /// <param name="heightValue">当前单元的高度通道值。</param>
    /// <param name="moistureValue">当前单元的湿度通道值。</param>
    /// <param name="temperatureValue">当前单元的温度通道值。</param>
    /// <param name="settings">地图分类阈值配置。</param>
    /// <returns>分类后的陆地地形。</returns>
    private static MapTerrainType ClassifyLandTerrain(
        float heightValue,
        float moistureValue,
        float temperatureValue,
        MapGenerationSettings settings)
    {
        if (heightValue >= settings.mountainHeightThreshold &&
            temperatureValue <= settings.mountainTemperatureThreshold)
        {
            return MapTerrainType.Mountain;
        }

        if (moistureValue >= settings.forestMoistureThreshold &&
            temperatureValue >= settings.forestTemperatureThreshold)
        {
            return MapTerrainType.Forest;
        }

        return MapTerrainType.Grass;
    }

    /// <summary>
    /// 把地图外圈设置为不可行走的深水，形成测试地图边界。
    /// </summary>
    /// <param name="mapData">地图数据。</param>
    /// <param name="borderSize">边界宽度。</param>
    private static void ApplyMapBorder(MapData mapData, int borderSize)
    {
        if (borderSize <= 0)
            return;

        for (int x = mapData.Origin.x; x < mapData.Origin.x + mapData.Width; x++)
        {
            for (int y = mapData.Origin.y; y < mapData.Origin.y + mapData.Height; y++)
            {
                Vector2Int cell = new Vector2Int(x, y);
                if (mapData.IsBorder(cell, borderSize))
                    mapData.SetTerrain(cell, MapTerrainType.DeepWater);
            }
        }
    }

    /// <summary>
    /// 把出生点周围的区域强制设置为草地，避免角色出生在水域中。
    /// </summary>
    /// <param name="mapData">地图数据。</param>
    /// <param name="spawnCell">出生点网格坐标。</param>
    /// <param name="settings">地图生成配置。</param>
    private static void ProtectSpawnArea(MapData mapData, Vector2Int spawnCell, MapGenerationSettings settings)
    {
        int radius = Mathf.Max(1, settings.spawnProtectionRadius);

        for (int x = spawnCell.x - radius; x <= spawnCell.x + radius; x++)
        {
            for (int y = spawnCell.y - radius; y <= spawnCell.y + radius; y++)
            {
                Vector2Int cell = new Vector2Int(x, y);
                if (!mapData.IsInside(cell) || mapData.IsBorder(cell, settings.borderSize))
                    continue;

                mapData.SetTerrain(cell, MapTerrainType.Grass);
            }
        }
    }

    /// <summary>
    /// 从出生点向一个随机地图边缘生成道路。
    /// </summary>
    /// <param name="mapData">地图数据。</param>
    /// <param name="settings">地图生成配置。</param>
    /// <param name="random">本次地图专用的伪随机生成器。</param>
    /// <param name="spawnCell">道路起点。</param>
    /// <returns>道路最终抵达的网格坐标。</returns>
    private static Vector2Int CreatePathToEdge(
        MapData mapData,
        MapGenerationSettings settings,
        System.Random random,
        Vector2Int spawnCell)
    {
        Vector2Int exitCell = CreateExitCell(mapData, settings.borderSize, random);
        CreatePathToEdge(mapData, settings, random, spawnCell, exitCell);
        return exitCell;
    }

    /// <summary>
    /// 根据指定终点铺设随机游走道路。
    /// </summary>
    /// <param name="mapData">地图数据。</param>
    /// <param name="settings">地图生成配置。</param>
    /// <param name="random">本次地图专用的伪随机生成器。</param>
    /// <param name="spawnCell">道路起点。</param>
    /// <param name="exitCell">道路终点。</param>
    /// <returns>道路终点。</returns>
    private static Vector2Int CreatePathToEdge(
        MapData mapData,
        MapGenerationSettings settings,
        System.Random random,
        Vector2Int spawnCell,
        Vector2Int exitCell)
    {
        Vector2Int currentCell = spawnCell;
        int maxSteps = mapData.Width * mapData.Height * 4;
        int stepCount = 0;

        while (currentCell != exitCell && stepCount++ < maxSteps)
        {
            StampRoad(mapData, currentCell, settings.roadWidth, settings.borderSize);
            currentCell = ChooseNextRoadCell(currentCell, exitCell, mapData, settings, random);
        }

        StampRoad(mapData, exitCell, settings.roadWidth, settings.borderSize);
        return exitCell;
    }

    /// <summary>
    /// 随机选择道路的一个终点，并保证终点位于边界内侧。
    /// </summary>
    /// <param name="mapData">地图数据。</param>
    /// <param name="borderSize">边界宽度。</param>
    /// <param name="random">本次地图专用的伪随机生成器。</param>
    /// <returns>道路终点。</returns>
    private static Vector2Int CreateExitCell(MapData mapData, int borderSize, System.Random random)
    {
        int minX = mapData.Origin.x + borderSize;
        int maxX = mapData.Origin.x + mapData.Width - borderSize - 1;
        int minY = mapData.Origin.y + borderSize;
        int maxY = mapData.Origin.y + mapData.Height - borderSize - 1;

        switch (random.Next(4))
        {
            case 0:
                return new Vector2Int(minX, random.Next(minY, maxY + 1));
            case 1:
                return new Vector2Int(maxX, random.Next(minY, maxY + 1));
            case 2:
                return new Vector2Int(random.Next(minX, maxX + 1), minY);
            default:
                return new Vector2Int(random.Next(minX, maxX + 1), maxY);
        }
    }

    /// <summary>
    /// 根据目标方向和转向概率选择下一格道路位置。
    /// </summary>
    /// <param name="currentCell">当前道路位置。</param>
    /// <param name="exitCell">道路目标位置。</param>
    /// <param name="mapData">地图数据。</param>
    /// <param name="settings">地图生成配置。</param>
    /// <param name="random">本次地图专用的伪随机生成器。</param>
    /// <returns>下一格道路位置。</returns>
    private static Vector2Int ChooseNextRoadCell(
        Vector2Int currentCell,
        Vector2Int exitCell,
        MapData mapData,
        MapGenerationSettings settings,
        System.Random random)
    {
        int deltaX = exitCell.x - currentCell.x;
        int deltaY = exitCell.y - currentCell.y;
        bool canMoveAlongX = deltaX != 0;
        bool canMoveAlongY = deltaY != 0;
        bool moveAlongX = canMoveAlongX && !canMoveAlongY;

        if (canMoveAlongX && canMoveAlongY)
        {
            bool preferX = Mathf.Abs(deltaX) >= Mathf.Abs(deltaY);
            moveAlongX = random.NextDouble() < settings.roadTurnChance ? !preferX : preferX;
        }

        Vector2Int direction = moveAlongX
            ? new Vector2Int(Mathf.Clamp(deltaX, -1, 1), 0)
            : new Vector2Int(0, Mathf.Clamp(deltaY, -1, 1));

        if (direction == Vector2Int.zero)
            direction = deltaX != 0
                ? new Vector2Int(Mathf.Clamp(deltaX, -1, 1), 0)
                : new Vector2Int(0, Mathf.Clamp(deltaY, -1, 1));

        Vector2Int nextCell = currentCell + direction;
        int minX = mapData.Origin.x + settings.borderSize;
        int maxX = mapData.Origin.x + mapData.Width - settings.borderSize - 1;
        int minY = mapData.Origin.y + settings.borderSize;
        int maxY = mapData.Origin.y + mapData.Height - settings.borderSize - 1;

        nextCell.x = Mathf.Clamp(nextCell.x, minX, maxX);
        nextCell.y = Mathf.Clamp(nextCell.y, minY, maxY);
        return nextCell;
    }

    /// <summary>
    /// 把道路中心点扩展成指定宽度的可行走区域。
    /// </summary>
    /// <param name="mapData">地图数据。</param>
    /// <param name="centerCell">道路中心点。</param>
    /// <param name="roadWidth">道路宽度。</param>
    /// <param name="borderSize">边界宽度。</param>
    private static void StampRoad(MapData mapData, Vector2Int centerCell, int roadWidth, int borderSize)
    {
        int minimumOffset = -(roadWidth - 1) / 2;
        int maximumOffset = roadWidth / 2;

        for (int x = centerCell.x + minimumOffset; x <= centerCell.x + maximumOffset; x++)
        {
            for (int y = centerCell.y + minimumOffset; y <= centerCell.y + maximumOffset; y++)
            {
                Vector2Int cell = new Vector2Int(x, y);
                if (!mapData.IsInside(cell) || mapData.IsBorder(cell, borderSize))
                    continue;

                mapData.SetTerrain(cell, MapTerrainType.Path);
            }
        }
    }

    /// <summary>
    /// 删除出生点无法到达的可行走孤岛，保证所有可行走区域属于同一连通区域。
    /// </summary>
    /// <param name="mapData">地图数据。</param>
    /// <param name="spawnCell">出生点网格坐标。</param>
    private static void RemoveUnreachableWalkableCells(MapData mapData, Vector2Int spawnCell)
    {
        HashSet<Vector2Int> reachableCells = FindReachableCells(mapData, spawnCell);

        for (int x = mapData.Origin.x; x < mapData.Origin.x + mapData.Width; x++)
        {
            for (int y = mapData.Origin.y; y < mapData.Origin.y + mapData.Height; y++)
            {
                Vector2Int cell = new Vector2Int(x, y);
                if (mapData.IsWalkable(cell) && !reachableCells.Contains(cell))
                    mapData.SetTerrain(cell, MapTerrainType.DeepWater);
            }
        }
    }

    /// <summary>
    /// 使用四方向 Flood Fill（洪水填充）寻找出生点可以到达的单元。
    /// </summary>
    /// <param name="mapData">地图数据。</param>
    /// <param name="startCell">Flood Fill（洪水填充）起点。</param>
    /// <returns>所有可到达的网格坐标。</returns>
    private static HashSet<Vector2Int> FindReachableCells(MapData mapData, Vector2Int startCell)
    {
        HashSet<Vector2Int> reachableCells = new HashSet<Vector2Int>();
        Queue<Vector2Int> pendingCells = new Queue<Vector2Int>();

        if (!mapData.IsWalkable(startCell))
            return reachableCells;

        reachableCells.Add(startCell);
        pendingCells.Enqueue(startCell);

        Vector2Int[] directions =
        {
            Vector2Int.up,
            Vector2Int.down,
            Vector2Int.left,
            Vector2Int.right
        };

        while (pendingCells.Count > 0)
        {
            Vector2Int currentCell = pendingCells.Dequeue();

            foreach (Vector2Int direction in directions)
            {
                Vector2Int nextCell = currentCell + direction;
                if (!mapData.IsInside(nextCell) || !mapData.IsWalkable(nextCell))
                    continue;

                if (reachableCells.Add(nextCell))
                    pendingCells.Enqueue(nextCell);
            }
        }

        return reachableCells;
    }

    /// <summary>
    /// 把出生坐标限制在边界和出生保护区允许的范围内。
    /// </summary>
    /// <param name="cell">待限制的网格坐标。</param>
    /// <param name="mapData">地图数据。</param>
    /// <param name="settings">地图配置。</param>
    /// <returns>限制后的出生网格坐标。</returns>
    private static Vector2Int ClampToPlayableArea(
        Vector2Int cell,
        MapData mapData,
        MapGenerationSettings settings)
    {
        int margin = settings.borderSize + settings.spawnProtectionRadius;
        return new Vector2Int(
            Mathf.Clamp(cell.x, mapData.Origin.x + margin, mapData.Origin.x + mapData.Width - margin - 1),
            Mathf.Clamp(cell.y, mapData.Origin.y + margin, mapData.Origin.y + mapData.Height - margin - 1));
    }
}

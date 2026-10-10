using System;

/// <summary>
/// 保存一个地图网格单元的逻辑信息。
/// </summary>
[Serializable]
public struct MapCell
{
    /// <summary>
    /// 当前网格单元的地形类型。
    /// </summary>
    public MapTerrainType terrainType;

    /// <summary>
    /// 根据地形类型判断该单元是否允许玩家行走。
    /// </summary>
    public bool IsWalkable => terrainType != MapTerrainType.DeepWater &&
                               terrainType != MapTerrainType.ShallowWater &&
                               terrainType != MapTerrainType.Mountain;
}

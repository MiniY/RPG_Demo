/// <summary>
/// 地图网格中使用的地形类型。
/// </summary>
public enum MapTerrainType
{
    /// <summary>
    /// 可行走的草地。
    /// </summary>
    Grass,

    /// <summary>
    /// 不可行走的深水。
    /// </summary>
    DeepWater,

    /// <summary>
    /// 旧版本使用的水域名称，保留它以兼容已有代码和测试。
    /// </summary>
    Water = DeepWater,

    /// <summary>
    /// 可行走的道路。
    /// </summary>
    Path,

    /// <summary>
    /// 不可行走的浅水。
    /// </summary>
    ShallowWater,

    /// <summary>
    /// 可行走的森林地表，树木等对象属于后续装饰层。
    /// </summary>
    Forest,

    /// <summary>
    /// 不可行走的山地。
    /// </summary>
    Mountain,

    /// <summary>
    /// 可行走的自然沙地，与道路共同露出 Sand Base（沙地底层）。
    /// </summary>
    Sand
}

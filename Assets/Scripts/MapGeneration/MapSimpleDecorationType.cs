using System;

/// <summary>
/// 区分随机地图中的简单装饰物类别。
/// </summary>
public enum MapSimpleDecorationType
{
    /// <summary>
    /// 由多格树冠、树体和树桩组成的树木。
    /// </summary>
    Tree,

    /// <summary>
    /// 不阻挡玩家移动的地表灌木。
    /// </summary>
    Bush,

    /// <summary>
    /// 可阻挡玩家移动的单体散落岩石。
    /// </summary>
    ScatteredRock
}

/// <summary>
/// 指定一个装饰瓦片应写入地表层还是树冠层。
/// </summary>
public enum MapSimpleDecorationRenderLayer
{
    /// <summary>
    /// 位于玩家下方的地表装饰层。
    /// </summary>
    Ground,

    /// <summary>
    /// 位于玩家上方的树冠遮挡层。
    /// </summary>
    Canopy
}

/// <summary>
/// 用位标记描述简单装饰物允许出现的地形类型。
/// </summary>
[Flags]
public enum MapSimpleDecorationTerrainMask
{
    /// <summary>
    /// 不允许任何地形。
    /// </summary>
    None = 0,

    /// <summary>
    /// 允许普通草地。
    /// </summary>
    Grass = 1 << 0,

    /// <summary>
    /// 允许森林地表。
    /// </summary>
    Forest = 1 << 1,

    /// <summary>
    /// 允许自然沙地。
    /// </summary>
    Sand = 1 << 2
}

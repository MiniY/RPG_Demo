using System;

/// <summary>
/// 使用四个位记录一个地形单元需要显示的北、东、南、西边界。
/// </summary>
[Flags]
public enum TerrainBoundaryMask
{
    /// <summary>
    /// 四个方向都可以与相同视觉地形连接。
    /// </summary>
    None = 0,

    /// <summary>
    /// 北侧需要显示边界。
    /// </summary>
    North = 1,

    /// <summary>
    /// 东侧需要显示边界。
    /// </summary>
    East = 2,

    /// <summary>
    /// 南侧需要显示边界。
    /// </summary>
    South = 4,

    /// <summary>
    /// 西侧需要显示边界。
    /// </summary>
    West = 8
}

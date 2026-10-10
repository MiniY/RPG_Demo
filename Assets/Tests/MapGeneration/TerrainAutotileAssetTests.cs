using NUnit.Framework;
using UnityEditor;
using UnityEngine.Tilemaps;

/// <summary>
/// 验证第一阶段创建的自动瓦片资产与 Tiny Swords（小剑）图集编号映射一致。
/// </summary>
public class TerrainAutotileAssetTests
{
    /// <summary>
    /// 草地自动瓦片资产路径。
    /// </summary>
    private const string GrassSetPath =
        "Assets/MapGeneration/Terrain/GrassAutotileSet.asset";

    /// <summary>
    /// 沙地自动瓦片资产路径。
    /// </summary>
    private const string SandSetPath =
        "Assets/MapGeneration/Terrain/SandAutotileSet.asset";

    /// <summary>
    /// 高地自动瓦片资产路径。
    /// </summary>
    private const string ElevationSetPath =
        "Assets/MapGeneration/Terrain/ElevationAutotileSet.asset";

    /// <summary>
    /// 草地图集中 0 到 15 掩码对应的瓦片编号。
    /// </summary>
    private static readonly int[] GrassTileIndices =
    {
        11, 1, 12, 2, 19, 27, 20, 28,
        10, 0, 13, 3, 18, 26, 21, 29
    };

    /// <summary>
    /// 沙地图集中 0 到 15 掩码对应的瓦片编号。
    /// </summary>
    private static readonly int[] SandTileIndices =
    {
        15, 6, 16, 7, 23, 31, 24, 32,
        14, 5, 17, 8, 22, 30, 25, 33
    };

    /// <summary>
    /// 高地顶面图集中 0 到 15 掩码对应的瓦片编号。
    /// </summary>
    private static readonly int[] ElevationTileIndices =
    {
        5, 1, 6, 2, 9, 17, 10, 18,
        4, 0, 7, 3, 8, 16, 11, 19
    };

    /// <summary>
    /// 验证草地的全部 16 种边界组合使用正确瓦片。
    /// </summary>
    [Test]
    public void GrassAutotileSetMatchesAtlasLayout()
    {
        AssertTopologySet(
            GrassSetPath,
            "tiny-swords-grass-v1",
            "Tilemap_Flat",
            GrassTileIndices);
    }

    /// <summary>
    /// 验证沙地的全部 16 种边界组合使用正确瓦片。
    /// </summary>
    [Test]
    public void SandAutotileSetMatchesAtlasLayout()
    {
        AssertTopologySet(
            SandSetPath,
            "tiny-swords-sand-v1",
            "Tilemap_Flat",
            SandTileIndices);
    }

    /// <summary>
    /// 验证高地顶面与八种南侧崖面使用正确瓦片。
    /// </summary>
    [Test]
    public void ElevationAutotileSetMatchesAtlasLayout()
    {
        TerrainAutotileSet autotileSet = AssertTopologySet(
            ElevationSetPath,
            "tiny-swords-elevation-v1",
            "Tilemap_Elevation",
            ElevationTileIndices);

        TerrainBoundaryMask[] faceMasks =
        {
            TerrainBoundaryMask.South | TerrainBoundaryMask.West,
            TerrainBoundaryMask.South,
            TerrainBoundaryMask.East | TerrainBoundaryMask.South,
            TerrainBoundaryMask.East | TerrainBoundaryMask.South | TerrainBoundaryMask.West,
            TerrainBoundaryMask.North | TerrainBoundaryMask.South | TerrainBoundaryMask.West,
            TerrainBoundaryMask.North | TerrainBoundaryMask.South,
            TerrainBoundaryMask.North | TerrainBoundaryMask.East | TerrainBoundaryMask.South,
            TerrainBoundaryMask.North | TerrainBoundaryMask.East |
            TerrainBoundaryMask.South | TerrainBoundaryMask.West
        };
        int[] faceTileIndices = { 12, 13, 14, 15, 20, 21, 22, 23 };

        Assert.That(autotileSet.HasCompleteSouthFaces, Is.True);
        for (int index = 0; index < faceMasks.Length; index++)
        {
            TileBase expectedTile = LoadTile("Tilemap_Elevation", faceTileIndices[index]);
            Assert.That(
                autotileSet.GetSouthFaceTile(faceMasks[index]),
                Is.EqualTo(expectedTile),
                $"南侧崖面掩码 {faceMasks[index]} 映射错误。");
        }
    }

    /// <summary>
    /// 加载自动瓦片集合并逐一检查 0 到 15 的拓扑映射。
    /// </summary>
    /// <param name="setPath">自动瓦片集合资产路径。</param>
    /// <param name="expectedSetId">预期稳定标识。</param>
    /// <param name="tilePrefix">预期瓦片名称前缀。</param>
    /// <param name="tileIndices">每个掩码对应的预期瓦片编号。</param>
    /// <returns>通过基础检查的自动瓦片集合。</returns>
    private static TerrainAutotileSet AssertTopologySet(
        string setPath,
        string expectedSetId,
        string tilePrefix,
        int[] tileIndices)
    {
        TerrainAutotileSet autotileSet =
            AssetDatabase.LoadAssetAtPath<TerrainAutotileSet>(setPath);

        Assert.That(autotileSet, Is.Not.Null, $"缺少自动瓦片资产：{setPath}");
        Assert.That(autotileSet.SetId, Is.EqualTo(expectedSetId));
        Assert.That(autotileSet.HasCompleteTopology, Is.True);

        for (int maskValue = 0; maskValue < tileIndices.Length; maskValue++)
        {
            TileBase expectedTile = LoadTile(tilePrefix, tileIndices[maskValue]);
            Assert.That(
                autotileSet.GetTile((TerrainBoundaryMask)maskValue),
                Is.EqualTo(expectedTile),
                $"边界掩码 {maskValue} 映射错误。");
        }

        return autotileSet;
    }

    /// <summary>
    /// 从现有地形瓦片目录加载指定编号的 Tile（瓦片）。
    /// </summary>
    /// <param name="tilePrefix">瓦片名称前缀。</param>
    /// <param name="tileIndex">瓦片编号。</param>
    /// <returns>加载到的瓦片资产。</returns>
    private static TileBase LoadTile(string tilePrefix, int tileIndex)
    {
        string tilePath = $"Assets/Sprites/GrassTiles/{tilePrefix}_{tileIndex}.asset";
        TileBase tile = AssetDatabase.LoadAssetAtPath<TileBase>(tilePath);
        Assert.That(tile, Is.Not.Null, $"缺少测试依赖瓦片：{tilePath}");
        return tile;
    }
}

using NUnit.Framework;
using UnityEngine;

/// <summary>
/// 验证地图探索状态的范围、边界和重复探索行为。
/// </summary>
public class MapExplorationStateTests
{
    /// <summary>
    /// 验证半径为一的圆形探索范围包含中心和四个正交相邻网格。
    /// </summary>
    [Test]
    public void RevealAroundUsesCircularGridRadius()
    {
        MapData mapData = new MapData(5, 5, new Vector2Int(10, 20));
        MapExplorationState explorationState = new MapExplorationState(mapData);

        int newlyExploredCount = explorationState.RevealAround(
            new Vector2Int(12, 22),
            1);

        Assert.That(newlyExploredCount, Is.EqualTo(5));
        Assert.That(explorationState.IsExplored(new Vector2Int(12, 22)), Is.True);
        Assert.That(explorationState.IsExplored(new Vector2Int(11, 22)), Is.True);
        Assert.That(explorationState.IsExplored(new Vector2Int(13, 22)), Is.True);
        Assert.That(explorationState.IsExplored(new Vector2Int(12, 21)), Is.True);
        Assert.That(explorationState.IsExplored(new Vector2Int(12, 23)), Is.True);
        Assert.That(explorationState.IsExplored(new Vector2Int(11, 21)), Is.False);
    }

    /// <summary>
    /// 验证重复揭示同一区域不会重复增加探索数量。
    /// </summary>
    [Test]
    public void RevealingSameAreaDoesNotIncreaseExploredCount()
    {
        MapData mapData = new MapData(7, 7, Vector2Int.zero);
        MapExplorationState explorationState = new MapExplorationState(mapData);

        int firstRevealCount = explorationState.RevealAround(new Vector2Int(3, 3), 2);
        int secondRevealCount = explorationState.RevealAround(new Vector2Int(3, 3), 2);

        Assert.That(firstRevealCount, Is.GreaterThan(0));
        Assert.That(secondRevealCount, Is.EqualTo(0));
        Assert.That(explorationState.ExploredCount, Is.EqualTo(firstRevealCount));
    }

    /// <summary>
    /// 验证探索半径超出边界时不会访问地图外数组。
    /// </summary>
    [Test]
    public void RevealAroundClampsToMapBounds()
    {
        MapData mapData = new MapData(3, 3, new Vector2Int(10, 20));
        MapExplorationState explorationState = new MapExplorationState(mapData);

        int newlyExploredCount = explorationState.RevealAround(
            new Vector2Int(10, 20),
            20);

        Assert.That(newlyExploredCount, Is.EqualTo(9));
        Assert.That(explorationState.ExploredCount, Is.EqualTo(9));
        Assert.That(explorationState.IsExplored(new Vector2Int(9, 19)), Is.False);
    }
}

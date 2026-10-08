using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 验证 Origin-aware Cell/World 和 Bounds 坐标契约。
/// </summary>
public sealed class MapCoordinateBoundaryTests
{
    private GameObject gridObject;
    private GameObject tilemapObject;
    private MapCoordinateBoundary coordinates;

    [SetUp]
    public void SetUp()
    {
        gridObject = new GameObject("CoordinateBoundaryGrid", typeof(Grid));
        gridObject.transform.position = new Vector3(10f, -4f, 0f);

        tilemapObject = new GameObject("CoordinateBoundaryTilemap", typeof(Tilemap));
        tilemapObject.transform.SetParent(gridObject.transform, false);
        coordinates = new MapCoordinateBoundary(tilemapObject.GetComponent<Tilemap>());
    }

    [TearDown]
    public void TearDown()
    {
        UnityEngine.Object.DestroyImmediate(gridObject);
    }

    [Test]
    public void NonZeroOriginUsesAbsoluteCellWithoutConsumerCompensation()
    {
        MapData mapData = new MapData(4, 3, new Vector2Int(7, -3));

        Vector3 worldPosition = coordinates.CellToWorld(mapData, mapData.Origin);

        Assert.That(worldPosition.x, Is.EqualTo(17.5f).Within(0.0001f));
        Assert.That(worldPosition.y, Is.EqualTo(-6.5f).Within(0.0001f));
        Assert.That(worldPosition, Is.Not.EqualTo(coordinates.CellToWorld(Vector2Int.zero)),
            "consumer 不应先减去 MapData.Origin 再执行坐标转换。");

        Vector3 continuousPosition = worldPosition + new Vector3(0.2f, -0.2f, 0f);
        Assert.That(
            coordinates.TryWorldToCell(mapData, continuousPosition, out Vector2Int cell),
            Is.True);
        Assert.That(cell, Is.EqualTo(mapData.Origin));
        Assert.That(continuousPosition, Is.Not.EqualTo(worldPosition),
            "动态对象的连续世界位置不需要吸附到 Cell 中心。");
    }

    [Test]
    public void BoundaryCellsRoundTripThroughSingleCoordinateBoundary()
    {
        MapData mapData = new MapData(5, 4, new Vector2Int(-6, 9));
        Vector2Int[] boundaryCells =
        {
            mapData.Origin,
            mapData.Origin + new Vector2Int(mapData.Width - 1, 0),
            mapData.Origin + new Vector2Int(0, mapData.Height - 1),
            mapData.Origin + new Vector2Int(mapData.Width - 1, mapData.Height - 1)
        };

        foreach (Vector2Int boundaryCell in boundaryCells)
        {
            Vector3 worldPosition = coordinates.CellToWorld(mapData, boundaryCell);
            Assert.That(
                coordinates.TryWorldToCell(mapData, worldPosition, out Vector2Int result),
                Is.True);
            Assert.That(result, Is.EqualTo(boundaryCell));
        }
    }

    [Test]
    public void CellBoundsConvertToWorldBoundsWithOriginAndGridTransform()
    {
        MapData mapData = new MapData(4, 3, new Vector2Int(7, -3));

        BoundsInt cellBounds = MapCoordinateBoundary.GetCellBounds(mapData);
        Bounds worldBounds = coordinates.CellBoundsToWorld(mapData);

        Assert.That(cellBounds.position, Is.EqualTo(new Vector3Int(7, -3, 0)));
        Assert.That(cellBounds.size, Is.EqualTo(new Vector3Int(4, 3, 1)));
        Assert.That(worldBounds.min.x, Is.EqualTo(17f).Within(0.0001f));
        Assert.That(worldBounds.min.y, Is.EqualTo(-7f).Within(0.0001f));
        Assert.That(worldBounds.max.x, Is.EqualTo(21f).Within(0.0001f));
        Assert.That(worldBounds.max.y, Is.EqualTo(-4f).Within(0.0001f));
        Assert.That(worldBounds.size.x, Is.EqualTo(4f).Within(0.0001f));
        Assert.That(worldBounds.size.y, Is.EqualTo(3f).Within(0.0001f));
    }

    [Test]
    public void InvalidCellsAndWorldPositionsFailExplicitly()
    {
        MapData mapData = new MapData(4, 3, new Vector2Int(7, -3));
        Vector2Int beforeOrigin = mapData.Origin + Vector2Int.left;
        Vector2Int afterMaximum = mapData.Origin + new Vector2Int(mapData.Width, 0);

        Assert.That(coordinates.TryCellToWorld(mapData, beforeOrigin, out _), Is.False);
        Assert.That(coordinates.TryCellToWorld(mapData, afterMaximum, out _), Is.False);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            coordinates.CellToWorld(mapData, beforeOrigin));

        Vector3 outsideWorld = new Vector3(16.99f, -6.5f, 0f);
        Assert.That(coordinates.TryWorldToCell(mapData, outsideWorld, out _), Is.False);
        Assert.That(coordinates.ContainsWorldPosition(mapData, outsideWorld), Is.False);
        Assert.That(
            coordinates.TryWorldToCell(
                mapData,
                new Vector3(float.NaN, 0f, 0f),
                out _),
            Is.False);
    }
}

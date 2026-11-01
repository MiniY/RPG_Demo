using System;
using NUnit.Framework;
using UnityEngine;

public sealed class MapVNextI1SeamTests
{
    [Test] public void OriginalGraphKeepsOriginalUnitStepOrder()
    {
        var legal = new[] { true, true, true, true }; var edges = new byte[] { 3, 6, 9, 12 };
        var graph = new EcologyNavigationGraph(Guid.NewGuid(), Vector2Int.zero, 2, 2, "original", "foot", legal, edges);
        var field = graph.BuildReturnField(new[] { 0 });
        Assert.That(graph.HasContinuousLinks, Is.False); Assert.That(field.Distances, Is.EqualTo(new[] { 0, 1, 1, 2 }));
        Assert.That(field.Next[3], Is.EqualTo(1));
    }
    [Test] public void MultiCellStairHasConservativeCostAndRetainsItsWaypoints()
    {
        var legal = new bool[16]; legal[0] = legal[15] = true;
        var route = new MapNavigationLink(0, 15, new[] { Vector2.zero, new Vector2(1, 1), new Vector2(3, 3) });
        var graph = new EcologyNavigationGraph(Guid.NewGuid(), Vector2Int.zero, 4, 4, "vnext", "own-foot", legal, new byte[16], new[] { route, route.Reverse() });
        Assert.That(graph.CanMove(0, 15), Is.True); Assert.That(graph.MoveCost(0, 15), Is.GreaterThanOrEqualTo(6));
        Assert.That(graph.BuildReturnField(new[] { 15 }).Distances[0], Is.EqualTo(graph.MoveCost(0, 15)));
        Assert.That(graph.FindLink(0, 15).Points.Count, Is.EqualTo(3));
        legal[15] = false; Assert.That(graph.FilterLinks(legal), Is.Empty);
    }
    [Test] public void UnknownHostAndMixedVersionAreRejected()
    {
        Assert.Throws<InvalidOperationException>(() => MapHostCompatibility.ValidateDocument("{}", EcologyRecipeDefinition.MainV1ProfileId, "unknown"));
        Assert.That(EcologyRecipeDefinition.ResourcePathFor("unknown-profile"), Is.Null);
        Assert.That(EcologyRecipeDefinition.ResourcePathFor(EcologyRecipeDefinition.VNextI1ProfileId), Is.Not.Null);
    }
    [Test] public void IllegalAndDuplicateContinuousConnectionsAreRejected()
    {
        var route = new MapNavigationLink(0, 1, new[] { Vector2.zero, Vector2.right });
        Assert.Throws<ArgumentException>(() => new EcologyNavigationGraph(Guid.NewGuid(), Vector2Int.zero, 2, 1, "vnext", "foot", new[] { true, false }, new byte[2], new[] { route }));
        Assert.Throws<ArgumentException>(() => new EcologyNavigationGraph(Guid.NewGuid(), Vector2Int.zero, 2, 1, "vnext", "foot", new[] { true, true }, new byte[2], new[] { route, route }));
    }
}

using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

public sealed class MapMinimapExitAvailabilityTests
{
    private GameObject controllerObject;
    private GameObject minimapObject;

    [TearDown]
    public void TearDown()
    {
        if (minimapObject != null)
            UnityEngine.Object.DestroyImmediate(minimapObject);
        if (controllerObject != null)
            UnityEngine.Object.DestroyImmediate(controllerObject);
    }

    [Test]
    public void ExitMarkerStateStartsLocked_IsIdempotent_AndRejectsStaleGeneration()
    {
        controllerObject = new GameObject("MapController");
        MapGenerationController controller =
            controllerObject.AddComponent<MapGenerationController>();
        MapIntegrationOrchestrator orchestrator = controller.Orchestrator;

        minimapObject = new GameObject("Minimap");
        MapMinimapController minimap = minimapObject.AddComponent<MapMinimapController>();
        SetPrivateField(minimap, "mapController", controller);
        RawImage minimapImage = CreateUiChild<RawImage>(minimapObject.transform, "MinimapImage");
        RectTransform exitMarker = CreateUiChild<RawImage>(
            minimapObject.transform,
            "ExitMarker").rectTransform;
        SetPrivateField(minimap, "minimapImage", minimapImage);
        SetPrivateField(minimap, "exitMarker", exitMarker);

        MapData firstMap = CreateMap(8);
        MapGenerationExecutionResult first = Commit(orchestrator, firstMap, 1001);
        Assert.That(first.Succeeded, Is.True, first.Failure?.Reason);
        Guid firstGenerationId = first.GenerationId.Value;

        minimap.RenderMap(firstMap);

        Assert.That(minimap.DisplayedGenerationId, Is.EqualTo(firstGenerationId));
        Assert.That(minimap.IsExitAvailable, Is.False);
        Assert.That(exitMarker.gameObject.activeSelf, Is.False);
        Assert.That(minimap.TrySetExitAvailable(firstGenerationId, true), Is.True);
        Assert.That(minimap.IsExitAvailable, Is.True);
        Assert.That(exitMarker.gameObject.activeSelf, Is.True);
        Assert.That(minimap.TrySetExitAvailable(firstGenerationId, true), Is.True,
            "Repeated completion events should be idempotent.");

        MapData secondMap = CreateMap(10);
        MapGenerationExecutionResult second = Commit(orchestrator, secondMap, 1002);
        Assert.That(second.Succeeded, Is.True, second.Failure?.Reason);
        Guid secondGenerationId = second.GenerationId.Value;

        minimap.RenderMap(secondMap);

        Assert.That(minimap.DisplayedGenerationId, Is.EqualTo(secondGenerationId));
        Assert.That(minimap.IsExitAvailable, Is.False,
            "A new map instance must start with its exit locked.");
        Assert.That(exitMarker.gameObject.activeSelf, Is.False);
        Assert.That(minimap.TrySetExitAvailable(firstGenerationId, true), Is.False,
            "A delayed completion event from the old map must be rejected.");
        Assert.That(minimap.IsExitAvailable, Is.False);
        Assert.That(minimap.TrySetExitAvailable(secondGenerationId, true), Is.True);
        Assert.That(minimap.IsExitAvailable, Is.True);
        Assert.That(exitMarker.gameObject.activeSelf, Is.True);

        minimap.ClearMap();
        Assert.That(minimap.DisplayedGenerationId, Is.Null);
        Assert.That(minimap.IsExitAvailable, Is.False);
        Assert.That(exitMarker.gameObject.activeSelf, Is.False);
        Assert.That(minimap.TrySetExitAvailable(secondGenerationId, true), Is.False);
    }

    private static MapGenerationExecutionResult Commit(
        MapIntegrationOrchestrator orchestrator,
        MapData map,
        int seed)
    {
        return orchestrator.Execute(
            new MapGenerationRequest(Guid.NewGuid(), seed),
            attempt => new MapPendingGeneration(attempt.GenerationId, map, null),
            (attempt, _) => new MapPlacementPlan(attempt.GenerationId),
            _ => { });
    }

    private static MapData CreateMap(int size)
    {
        MapData map = new MapData(size, size, Vector2Int.zero)
        {
            SpawnCell = Vector2Int.one,
            ExitCell = new Vector2Int(size - 2, size - 2)
        };
        return map;
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        target.GetType()
            .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(target, value);
    }

    private static T CreateUiChild<T>(Transform parent, string name)
        where T : Component
    {
        GameObject child = new GameObject(
            name,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(T));
        child.transform.SetParent(parent, false);
        return child.GetComponent<T>();
    }
}

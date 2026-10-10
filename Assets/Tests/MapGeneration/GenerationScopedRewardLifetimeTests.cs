using NUnit.Framework;
using UnityEngine;

public sealed class GenerationScopedRewardLifetimeTests
{
    private GameObject controllerObject;
    private GameObject rewardObject;

    [TearDown]
    public void TearDown()
    {
        if (rewardObject != null)
            Object.DestroyImmediate(rewardObject);
        if (controllerObject != null)
            Object.DestroyImmediate(controllerObject);
    }

    [Test]
    public void MapClearInvokesOnceAndDisarmAllowsRearm()
    {
        controllerObject = new GameObject("MapController");
        MapGenerationController controller =
            controllerObject.AddComponent<MapGenerationController>();
        rewardObject = new GameObject("Reward");
        GenerationScopedRewardLifetime lifetime =
            rewardObject.AddComponent<GenerationScopedRewardLifetime>();
        int cleanupCount = 0;

        lifetime.Arm(controller, () => cleanupCount++);
        controller.ClearMap();
        controller.ClearMap();
        Assert.That(cleanupCount, Is.EqualTo(1));

        lifetime.Arm(controller, () => cleanupCount++);
        lifetime.Disarm();
        controller.ClearMap();
        Assert.That(cleanupCount, Is.EqualTo(1),
            "解除绑定后旧地图不能再次清理该奖励。");

        lifetime.Arm(controller, () => cleanupCount++);
        controller.ClearMap();
        Assert.That(cleanupCount, Is.EqualTo(2),
            "对象池再次租用后必须能够绑定下一次地图生命周期。");
    }
}

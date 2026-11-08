using System;
using NUnit.Framework;
using UnityEngine;

public sealed class MapHostSourcePolicyPlayTests
{
    [Test] public void FormalTestsRequireStrictSourceValidation()
    {
        Assert.That(Application.isPlaying, Is.True);
#if UNITY_EDITOR
        Assert.That(MapHostCompatibility.RequiresSourceVerification(), Is.True,
            "Formal PlayMode tests must not use the development source policy.");
        Assert.That(MapHostCompatibility.EditorTestRunActive?.Invoke(), Is.True,
            "The public Test Runner callbacks must mark this run across domain reloads.");
        var recipe = UnityEditor.AssetDatabase.LoadAssetAtPath<EcologyRecipeDefinition>(
            "Assets/MapGeneration/MapVNext/I1/Recipe_MyTMPV1.asset");
        Assert.That(recipe, Is.Not.Null);
        if (Environment.GetEnvironmentVariable("RPG_HOST_PROBE_OWNED_DRIFT") == "1")
        {
            Assert.That(Application.isBatchMode, Is.False, "This fixture must prove interactive Test Runner behavior.");
            var error = Assert.Throws<InvalidOperationException>(() => recipe.ResolveTypes());
            Assert.That(error.Message, Does.Contain("UnapprovedOriginalManifestDifference"));
        }
        else Assert.DoesNotThrow(() => recipe.ResolveTypes());
#else
        Assert.That(MapHostCompatibility.RequiresSourceVerification(), Is.False,
            "Players verify embedded identity and do not have an Editor source tree.");
#endif
    }
}

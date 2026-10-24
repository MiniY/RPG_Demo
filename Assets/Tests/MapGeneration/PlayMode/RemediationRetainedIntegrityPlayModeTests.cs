using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class RemediationRetainedIntegrityPlayModeTests
{
    [Test] public void CurrentMainSameImportedFootCannotAdoptUnsavedGeometryUnderItsFrozenIdentity()
    {
        var recipe = Resources.Load<EcologyRecipeDefinition>(EcologyRecipeDefinition.ResourcePathFor(EcologyRecipeDefinition.MainV1ProfileId));
        var type = recipe.ResolveTypes().First(t => t.Prefab.name == "Torch_Blue");
        var foot = (BoxCollider2D)type.MovementFoot;
        Assert.That(EditorUtility.IsDirty(foot), Is.False);
        Vector2 size = foot.size; string before = EditorJsonUtility.ToJson(foot);
        string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(foot));
        try
        {
            foot.size = size + new Vector2(.125f, 0f); EditorUtility.SetDirty(foot);
            var failure = Assert.Throws<InvalidOperationException>(() => recipe.ResolveTypes());
            Assert.That(failure.Message, Does.StartWith("RetainedAssetDirty:"));
            Assert.That(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(foot)), Is.EqualTo(guid));
        }
        finally { foot.size = size; EditorUtility.ClearDirty(foot); }
        Assert.That(EditorJsonUtility.ToJson(foot), Is.EqualTo(before));
        recipe.ResolveTypes(); recipe.VerifyRetainedSourceFiles(true);
    }

    [Test] public void CurrentMainSameNamedSettingsCannotAdoptUnsavedNumbersUnderItsFrozenIdentity()
    {
        var recipe = Resources.Load<EcologyRecipeDefinition>(EcologyRecipeDefinition.ResourcePathFor(EcologyRecipeDefinition.MainV1ProfileId)); recipe.ResolveTypes();
        var settings = recipe.Settings; Assert.That(EditorUtility.IsDirty(settings), Is.False);
        int seed = settings.seed; string before = EditorJsonUtility.ToJson(settings);
        try
        {
            settings.seed = seed ^ 17; EditorUtility.SetDirty(settings);
            var failure = Assert.Throws<InvalidOperationException>(() => recipe.ResolveTypes());
            Assert.That(failure.Message, Does.StartWith("RetainedAssetDirty:"));
        }
        finally { settings.seed = seed; EditorUtility.ClearDirty(settings); }
        Assert.That(EditorJsonUtility.ToJson(settings), Is.EqualTo(before));
        recipe.ResolveTypes(); recipe.VerifyRetainedSourceFiles(true);
    }
}

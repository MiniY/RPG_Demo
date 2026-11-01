using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class MapVNextI1StartupTests
{
    [Test]
    public void DefaultEditorEarlyCallbackPreservesOriginalSelectors()
    {
        var setup = EditorSceneManager.GetSceneManagerSetup();
        var startScene = EditorSceneManager.playModeStartScene;
        TowerRunRuntimeEnvironment.ResetOverrides();
        try
        {
            EditorSceneManager.playModeStartScene = null;
            EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity", OpenSceneMode.Single);
            string slots = TowerRunRuntimeEnvironment.SlotRoot;
            string legacy = TowerRunRuntimeEnvironment.LegacySavePath;
            var preferences = TowerRunRuntimeEnvironment.Preferences;
            var type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("MapVNextI1Bootstrap")).Single(t => t != null);
            type.GetMethod("IsolateBeforeSession", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
            Assert.That(TowerRunRuntimeEnvironment.SlotRoot, Is.EqualTo(slots), "A default Editor entry must not install I1 slots before GameSession.");
            Assert.That(TowerRunRuntimeEnvironment.LegacySavePath, Is.EqualTo(legacy));
            Assert.That(TowerRunRuntimeEnvironment.Preferences, Is.SameAs(preferences));
        }
        finally
        {
            TowerRunRuntimeEnvironment.ResetOverrides();
            EditorSceneManager.playModeStartScene = startScene;
            if (setup.Any(p => p.isLoaded && p.isActive)) EditorSceneManager.RestoreSceneManagerSetup(setup);
            else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }
    }
    private static Type BootstrapType => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("MapVNextI1Bootstrap")).Single(t => t != null);
    [Test]
    public void ReviewedHostAcceptsMainV1WithoutEditorI1StartupCapability()
    {
        Assert.That(MapHostBuildStamp.IsReviewedCompatibleHost, Is.True);
        Assert.That(MapHostBuildStamp.HasExplicitI1StartupCapability, Is.False);
        var source = Resources.Load<TextAsset>("MapVNextI1Host");
        Assert.That(MapHostCompatibility.ValidateDocument(source.text, EcologyRecipeDefinition.MainV1ProfileId, MapHostBuildStamp.OldContentVersion), Is.Not.Null);
    }
    [Test]
    public void EditorI1EntryWithoutCapabilityIsQuarantinedBeforeAnySession()
    {
        var setup=EditorSceneManager.GetSceneManagerSetup();var selected=EditorSceneManager.playModeStartScene;
        TowerRunRuntimeEnvironment.ResetOverrides();
        try
        {
            EditorSceneManager.playModeStartScene=null;
            EditorSceneManager.OpenScene("Assets/Scenes/MapVNext/I1/I1Game.unity",OpenSceneMode.Single);
            Assert.That(MapHostBuildStamp.HasExplicitI1StartupCapability,Is.False);
            var error=Assert.Throws<TargetInvocationException>(()=>BootstrapType.GetMethod("IsolateBeforeSession",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,null));
            Assert.That(error.InnerException.Message,Does.Contain("I1StartupCapabilityOrEntryMismatch"));
            Assert.That(TowerRunRuntimeEnvironment.SlotRoot,Does.Contain("RejectedStartup"));
            Assert.That(TowerRunRuntimeEnvironment.Preferences,Is.TypeOf<TowerRunMemoryPreferenceStore>());
            var sessionType=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("GameSession")).Single(t=>t!=null);
            Assert.That(sessionType.GetProperty("Instance").GetValue(null),Is.Null,"No session or formal slot read is allowed in this direct early-callback test.");
        }
        finally
        {
            TowerRunRuntimeEnvironment.ResetOverrides();EditorSceneManager.playModeStartScene=selected;
            if(setup.Any(p=>p.isLoaded&&p.isActive))EditorSceneManager.RestoreSceneManagerSetup(setup);else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        }
    }
    [TestCase("Assets/Scenes/SampleScene.unity",false,"normal",false)]
    [TestCase("Assets/Scenes/MapVNext/I1/I1Game.unity",true,"i1",false)]
    [TestCase("Assets/Scenes/MapVNext/I1/I1Game.unity",false,"i1",true)]
    [TestCase("Assets/Scenes/SampleScene.unity",true,"i1",true)]
    [TestCase("Assets/Scenes/MapVNext/I1/I1Game.unity",true,"normal",true)]
    [TestCase("Assets/Scenes/SampleScene.unity",false,"unknown",true)]
    public void ExplicitEntryAndCapabilityAreAnExactPair(string entry,bool capability,string request,bool rejected)
    {
        Action call=()=>BootstrapType.GetMethod("ValidateStartupTarget").Invoke(null,new object[]{entry,capability,request});
        if(rejected)Assert.Throws<TargetInvocationException>(()=>call());else Assert.DoesNotThrow(()=>call());
    }

}

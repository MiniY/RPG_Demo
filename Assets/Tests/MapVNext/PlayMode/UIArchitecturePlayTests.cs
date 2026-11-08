using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed class UIArchitecturePlayTests
{
    private Component probe;
    [UnityTearDown] public IEnumerator FinishUIFixture()
    {
        if (probe != null)
        {
            try { probe.GetType().GetMethod("CleanupShopEditorTest").Invoke(probe, null); }
            finally { UnityEngine.Object.Destroy(probe.gameObject); probe = null; }
        }
        yield return null;
    }
    [UnityTest] public IEnumerator SavedScenePagesAreRegisteredOnce()
    {
        string runtime = Environment.GetEnvironmentVariable("RPG_I1_RUNTIME_ROOT");
        Assert.That(runtime, Is.Not.Null.And.Not.Empty);
        Assert.That(Application.companyName, Is.EqualTo("CodexShopEditorAcceptance"));
        TowerRunRuntimeEnvironment.ConfigureTestIsolation(Path.Combine(runtime, "AuthoredUI"));
#if UNITY_EDITOR
        yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/SampleScene.unity", new LoadSceneParameters(LoadSceneMode.Single));
#endif
        yield return null;
        Type rootType = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("UIRoot")).First(t => t != null);
        var root = (Component)UnityEngine.Object.FindObjectOfType(rootType);
        Assert.That(root, Is.Not.Null);
        rootType.GetMethod("ValidateConfiguration").Invoke(root, null);
        Type pageType = rootType.Assembly.GetType("UIPage", true);
        Assert.That(root.GetComponentsInChildren(pageType, true).Length, Is.EqualTo(5), "Authored pages were recreated at runtime");
        object manager = rootType.GetProperty("Manager").GetValue(root);
        Assert.That(manager.GetType().GetProperty("RegisteredCount").GetValue(manager), Is.EqualTo(4));
        Type viewsType = rootType.Assembly.GetType("TowerRunUiViews", true);
        Assert.That(root.GetComponents(viewsType).Length, Is.EqualTo(1));
        foreach (string name in new[] { "WindowLayer", "SystemLayer", "ModalLayer", "BlockingLayer", "TowerRunSettingsOverlay", "TowerRunFlowOverlay" })
            Assert.That(root.GetComponentsInChildren<Transform>(true).Count(t => t.name == name), Is.EqualTo(1), "Duplicate authored object: " + name);
    }
    [UnityTest] public IEnumerator FormalSceneLayersSettingsModalAndReturn()
    {
        string root=Environment.GetEnvironmentVariable("RPG_I1_RUNTIME_ROOT");
        Assert.That(root,Is.Not.Null.And.Not.Empty);
        Assert.That(Application.companyName,Is.EqualTo("CodexShopEditorAcceptance"));
        TowerRunRuntimeEnvironment.ConfigureTestIsolation(Path.Combine(root,"UIArchitecture"));
#if UNITY_EDITOR
        yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/SampleScene.unity",new LoadSceneParameters(LoadSceneMode.Single));
#endif
        yield return null;
        Type type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("MapVNextI1Acceptance")).First(t=>t!=null);
        var actor=new GameObject("ExplicitUIArchitectureTest");probe=actor.AddComponent(type);
        var sequence=(IEnumerator)type.GetMethod("RunUIArchitectureEditorTest").Invoke(probe,new object[]{Environment.GetEnvironmentVariable("RPG_I1_EVIDENCE")});
        yield return sequence;
    }
}

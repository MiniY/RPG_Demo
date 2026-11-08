using System;
using System.Reflection;
using NUnit.Framework;

public sealed class UIPageAuthoringTests
{
    [TestCase("SavedSceneHasPagesAndLayers")]
    [TestCase("RuntimeDefinitionDoesNotMutateCatalog")]
    [TestCase("DuplicateCatalogIdRejected")]
    [TestCase("InvalidTemplatePolicyRejected")]
    [TestCase("InvalidDimmingRejected")]
    [TestCase("MissingSceneBindingRejected")]
    [TestCase("UncachedScenePageRejected")]
    [TestCase("MissingRootReferencesRejected")]
    [TestCase("MissingSystemControlRejected")]
    [TestCase("MissingConfirmationControlsRejected")]
    public void AuthoringContract(string name)
    {
        var type = Type.GetType("UIPageAuthoringChecks, Assembly-CSharp-Editor", true);
        try { type.GetMethod("RunCase", BindingFlags.Public | BindingFlags.Static).Invoke(null, new object[] { name }); }
        catch (TargetInvocationException error) { throw error.InnerException; }
    }
}

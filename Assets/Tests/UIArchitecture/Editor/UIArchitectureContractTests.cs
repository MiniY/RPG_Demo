using System;
using System.Reflection;
using NUnit.Framework;

public sealed class UIArchitectureContractTests
{
    [TestCase("IdempotencyAndStaleHandles")]
    [TestCase("WorkspaceReplacementRollsBack")]
    [TestCase("LayersDimmerAndCoverage")]
    [TestCase("RejectedBackIsConsumed")]
    [TestCase("NestedConfirmAndOwnerCleanup")]
    [TestCase("CancellationAndSceneUnload")]
    [TestCase("ConfirmationRejectionsDestroyRequestInstances")]
    [TestCase("CancellationDuringNestedOpen")]
    [TestCase("LeaseOwnershipAndRepeatedCycles")]
    [TestCase("ReentrancyAndCloseFailure")]
    [TestCase("PrefabCachePoliciesAndInactiveCreation")]
    [TestCase("ExplicitContextReplacementAndRollback")]
    [TestCase("ConfirmationCloseFailureCannotConfirmBusiness")]
    [TestCase("TransparentOverlayStillBlocksInput")]
    public void UIContract(string name)
    {
        var type = Type.GetType("UIArchitectureChecks, Assembly-CSharp-Editor", true);
        try { type.GetMethod("RunCase", BindingFlags.Public | BindingFlags.Static).Invoke(null, new object[] { name }); }
        catch (TargetInvocationException error) { throw error.InnerException; }
    }
}

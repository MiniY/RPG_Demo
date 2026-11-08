using System.Collections;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed partial class MapVNextI1IntegrationTests
{
    [UnityTest]
    public IEnumerator RecordedMerchantSeedKeepsLegalGroundFootprint()
    {
        Assert.That(Application.companyName, Is.EqualTo("CodexShopEditorAcceptance"));
        yield return LoadFormalScene("RecordedMerchantFootprint");
        Call("BeginNewRunWithSeed", -1577089866);
        Ready(true);
        var context = controller.Orchestrator.Context;
        var entry = context.ActiveRegistry.Entries.Single(e => e.LogicalObjectId == MerchantPlacementService.MainMerchantLogicalObjectId);
        var actor = (Component)entry.Instance;
        Assert.That(MapSpatialPlacement.CanPlace(context, context.ActiveGenerationId.Value, actor.transform,
            (Vector2)actor.transform.position + Vector2.up * .01f, out string reason), Is.False,
            "A real overlap beside the recorded boundary must remain rejected");
        Assert.That(reason, Is.EqualTo("ForbiddenSpatialFootprint"));
        SaveAndReopen(true, "RecordedMerchantFootprint");
        yield return new WaitForFixedUpdate();
        Ready(true);
    }

    [UnityTest]
    public IEnumerator MerchantGroundFootprintRefreshesAcrossGenerations()
    {
        Assert.That(Application.companyName, Is.EqualTo("CodexShopEditorAcceptance"));
        yield return LoadFormalScene("MerchantGenerationSequence");
        // All four inputs reached actual Playing in stopped Run3216001b; legacy7727 is covered separately.
        foreach (int seed in new[] { 21193, -1577089866, 8675309, -367763121 })
        {
            Call("ReturnToStartupChoice");
            Call("BeginNewRunWithSeed", seed);
            Ready(true);
            SaveAndReopen(true, "MerchantSequence-" + seed, overwrite: true);
            yield return new WaitForFixedUpdate();
            Ready(true);
        }
    }

    [UnityTest]
    public IEnumerator AuthoredMerchantGroundDeclarationIsNotReplaced()
    {
        Assert.That(Application.companyName, Is.EqualTo("CodexShopEditorAcceptance"));
        yield return LoadFormalScene("MerchantAuthoredDeclaration");
        Call("BeginNewRunWithSeed", -1577089866);
        Ready(true);
        var context = controller.Orchestrator.Context;
        var entry = context.ActiveRegistry.Entries.Single(e => e.LogicalObjectId == MerchantPlacementService.MainMerchantLogicalObjectId);
        var actor = (Component)entry.Instance;
        var control = new GameObject("OversizeAuthoredGroundControl");
        try
        {
            var declaration = control.AddComponent<MapGroundFootprint>();
            var vertices = new[] { new Vector2(-500, -500), new Vector2(500, -500), new Vector2(500, 500), new Vector2(-500, 500) };
            declaration.Configure(vertices);
            Assert.That(MapSpatialPlacement.Prepare(context, context.ActiveGenerationId.Value, control,
                actor.transform.position, out string reason, MerchantPlacementService.MainMerchantLogicalObjectId), Is.False);
            Assert.That(reason, Is.EqualTo("ForbiddenSpatialFootprint"));
            Assert.That(declaration.Vertices, Is.EqualTo(vertices));
            Assert.That(declaration.IsReservationDerived, Is.False);
        }
        finally { Object.Destroy(control); }
    }

    [UnityTest]
    public IEnumerator MerchantRejectedPoseRestoresSceneLocalPosition()
        => VerifyMerchantMaterializationRollback(false);

    [UnityTest]
    public IEnumerator MerchantRejectedRegistryKeepsOnlyUnboundReusableCache()
        => VerifyMerchantMaterializationRollback(true);

    private IEnumerator VerifyMerchantMaterializationRollback(bool conflict)
    {
        Assert.That(Application.companyName, Is.EqualTo("CodexShopEditorAcceptance"));
        yield return LoadFormalScene("MerchantRollback-" + conflict);
        var service = Object.FindObjectOfType<MerchantPlacementService>();
        var originalTarget = service.MerchantTarget;
        Transform placement = ((IMerchantPlacementTarget)originalTarget).PlacementTransform;
        Vector3 originalLocal = placement.localPosition;
        var target = placement.gameObject.AddComponent<MerchantMaterializationFailureTarget>();
        target.Context = controller.Orchestrator.Context;
        target.Conflict = conflict;
        typeof(MerchantPlacementService).GetField("merchantTarget", Private).SetValue(service, target);
        try
        {
            LogAssert.Expect(LogType.Error, new Regex(conflict
                ? "^Required Merchant materialization failed: Runtime instance is already bound for merchant.main"
                : "^Required Merchant materialization failed: ForbiddenSpatialFootprint"));
            Call("BeginNewRunWithSeed", -1577089866);
            Assert.That(controller.Orchestrator.Context.IsReady, Is.False);
            Assert.That(service.MaterializationCount, Is.Zero);
            Assert.That(placement.localPosition, Is.EqualTo(originalLocal));
            if (conflict)
            {
                var ground = placement.GetComponent<MapGroundFootprint>();
                var lease = placement.GetComponent<MapSpatialActor>();
                Assert.That(ground.IsReservationDerived, Is.True);
                Assert.That(lease.CanMove(placement.position, placement.position), Is.False);
                typeof(MerchantPlacementService).GetField("merchantTarget", Private).SetValue(service, originalTarget);
                Call("ReturnToStartupChoice");
                Call("BeginNewRunWithSeed", -1577089866);
                Ready(true);
                Assert.That(placement.GetComponent<MapGroundFootprint>(), Is.SameAs(ground));
                Assert.That(placement.GetComponent<MapSpatialActor>(), Is.SameAs(lease));
                yield return null;
                Ready(true);
                Assert.That(placement.GetComponents<MapGroundFootprint>().Length, Is.EqualTo(1));
                Assert.That(placement.GetComponents<MapSpatialActor>().Length, Is.EqualTo(1));
                Assert.That(lease.CanMove(placement.position, placement.position), Is.True);
            }
            else
            {
                Assert.That(target.AuthoredGround.Vertices, Is.EqualTo(target.AuthoredVertices));
                Assert.That(target.AuthoredGround.IsReservationDerived, Is.False);
            }
        }
        finally
        {
            typeof(MerchantPlacementService).GetField("merchantTarget", Private).SetValue(service, originalTarget);
            if (target.AuthoredGround != null) Object.Destroy(target.AuthoredGround);
            if (target.ConflictingInstance != null) Object.Destroy(target.ConflictingInstance);
            Object.Destroy(target);
        }
    }
}

public sealed class MerchantMaterializationFailureTarget : MonoBehaviour, IMerchantPlacementTarget
{
    public MapRuntimeContext Context;
    public bool Conflict;
    public GameObject ConflictingInstance;
    public MapGroundFootprint AuthoredGround;
    public Vector2[] AuthoredVertices;
    public Transform PlacementTransform => transform;
    public Transform BoundPlayer { get; private set; }
    public bool TryBindCanonicalPlayer(Transform player, out string reason)
    {
        BoundPlayer = player;
        if (Conflict)
        {
            ConflictingInstance = new GameObject("DeliberateRegistryConflict");
            return Context.ActiveRegistry.TryBindInstance(Context.ActiveGenerationId.Value,
                MerchantPlacementService.MainMerchantLogicalObjectId, MerchantPlacementService.MerchantRole,
                ConflictingInstance, out reason);
        }
        AuthoredVertices = new[] { new Vector2(-500, -500), new Vector2(500, -500), new Vector2(500, 500), new Vector2(-500, 500) };
        AuthoredGround = gameObject.AddComponent<MapGroundFootprint>();
        AuthoredGround.Configure(AuthoredVertices);
        reason = string.Empty;
        return true;
    }
}

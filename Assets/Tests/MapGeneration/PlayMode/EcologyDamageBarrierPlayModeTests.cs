#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;

/// <summary>First executable production-damage slice, not the complete V0-01..08 acceptance matrix.</summary>
public sealed class EcologyDamageBarrierPlayModeTests
{
    private readonly List<GameObject> owned = new List<GameObject>();
    private string isolatedSlots;
    private Mouse mouse;
    private float previousTimeScale;
    private GameObject player;
    private GameObject monster;
    private Component playerDamage;
    private Component monsterDamage;
    private Component attack;
    private EcologyDamageBarrier barrier;
    private EcologyCombatBinding playerBinding;
    private EcologyCombatBinding monsterBinding;
    private Component sessionStats;
    private object previousStatsSnapshot;
    private InputSettings.BackgroundBehavior previousBackgroundBehavior;
    private InputSettings.EditorInputBehaviorInPlayMode previousEditorInputBehavior;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        previousTimeScale = Time.timeScale;
        previousBackgroundBehavior = InputSystem.settings.backgroundBehavior;
        previousEditorInputBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
        Assert.That(Physics2D.simulationMode, Is.EqualTo(SimulationMode2D.FixedUpdate),
            "Do not silently change the project's simulation contract.");
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode =
            InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        Time.timeScale = 1f;
        isolatedSlots = Path.Combine(Path.GetTempPath(), "RPG_Demo-EcologyP0V-" + Guid.NewGuid().ToString("N"));
        TowerRunRuntimeEnvironment.ConfigureTestIsolation(isolatedSlots);
        Component session = (Component)FindType("GameSession").GetProperty("Instance").GetValue(null);
        Assert.That(session, Is.Not.Null, "The production BeforeSceneLoad bootstrap owns the sole session.");
        sessionStats = (Component)Property(session, "PlayerStats");
        previousStatsSnapshot = Invoke(sessionStats, "CaptureSnapshot");
        Assert.That(UnityEngine.Object.FindObjectOfType<MapGenerationController>(), Is.Null,
            "A live generated map would change the fixture's elevation premise.");
        Assert.That(FindType("GameInput").GetProperty("Instance").GetValue(null), Is.Null,
            "This filtered experiment must not replace another active input owner.");

        GameObject input = Own(new GameObject("P0V-Input"));
        input.AddComponent(FindType("GameInput"));
        Invoke(input.GetComponent("GameInput"), "SetGameplayInputEnabled", true);
        mouse = InputSystem.AddDevice<Mouse>();
        if (!mouse.enabled)
            InputSystem.EnableDevice(mouse);
        InputSystem.QueueStateEvent(mouse, new MouseState());

        Component stats = sessionStats;
        Invoke(stats, "SetCurrentHealth", 100f);
        player = Clone("Assets/Prefabs/Players/Warrior_Blue.prefab", new Vector3(200f, 200f));
        monster = Clone("Assets/Prefabs/Monsters/Torch/Torch_Blue.prefab", new Vector3(201.25f, 200f));
        ((Behaviour)monster.GetComponent("MonsterAIController")).enabled = false;
        playerDamage = player.GetComponent("PlayerDamageController");
        monsterDamage = monster.GetComponent("BaseMonster");
        attack = monster.GetComponent("MonsterAttackController");
        SetField(playerDamage, "playerStatsRuntime", stats);
        SetField(player.GetComponent("PlayerAction"), "playerStatsRuntime", stats);
        Invoke(playerDamage, "SynchronizeAfterRestore");
        Assert.That(player.GetComponent<Rigidbody2D>().bodyType, Is.EqualTo(RigidbodyType2D.Dynamic));
        Assert.That(monster.GetComponent<Rigidbody2D>().bodyType, Is.EqualTo(RigidbodyType2D.Kinematic));
        barrier = Own(new GameObject("P0V-Barrier")).AddComponent<EcologyDamageBarrier>();
        playerBinding = player.AddComponent<EcologyCombatBinding>();
        monsterBinding = monster.AddComponent<EcologyCombatBinding>();
        playerBinding.Bind(barrier, 1, 1, () => !(bool)Property(playerDamage, "IsDefeated"));
        monsterBinding.Bind(barrier, 1, 2, () => !(bool)Property(monsterDamage, "IsDefeated"));
        barrier.OnTrace += (request, phase) => Debug.Log(
            $"P0V_ATTACK phase={phase} frame={Time.frameCount} completed={barrier.CompletedStep} " +
            $"G={request.GenerationId} attacker={request.AttackerId} target={request.TargetId} " +
            $"id={request.AttackId} required={request.RequiredCompletedStep} status={request.Status} " +
            $"real={Time.realtimeSinceStartup:R} world={Time.time:R} applied={request.DamageApplied}");
        Debug.Log($"P0V_PREMISE Unity={Application.unityVersion} simulation={Physics2D.simulationMode} " +
                  $"fixedDelta={Time.fixedDeltaTime:R} input={InputSystem.settings.updateMode}");
        yield return null;
        yield return new WaitForFixedUpdate();
        Debug.Log($"P0V_INITIAL playerX={player.GetComponent<Rigidbody2D>().position.x:R} " +
                  $"playerY={player.GetComponent<Rigidbody2D>().position.y:R} " +
                  $"solidGap={Physics2D.Distance(player.transform.Find("MovementFeet").GetComponent<Collider2D>(), monster.GetComponent<Collider2D>()).distance:R}");
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        InputSystem.settings.backgroundBehavior = previousBackgroundBehavior;
        InputSystem.settings.editorInputBehaviorInPlayMode = previousEditorInputBehavior;
        Time.timeScale = previousTimeScale;
        if (mouse != null && mouse.added)
            InputSystem.RemoveDevice(mouse);
        for (int i = owned.Count - 1; i >= 0; i--)
            if (owned[i] != null)
                UnityEngine.Object.Destroy(owned[i]);
        owned.Clear();
        yield return null;
        if (sessionStats != null && previousStatsSnapshot != null)
            Invoke(sessionStats, "RestoreSnapshot", previousStatsSnapshot, false);
        sessionStats = null;
        previousStatsSnapshot = null;
        TowerRunRuntimeEnvironment.ResetOverrides();
        if (isolatedSlots != null && Directory.Exists(isolatedSlots))
            Directory.Delete(isolatedSlots, true);
    }

    [UnityTest]
    public IEnumerator RealPlayerInputDamagesFormalTorchOnceAfterRequiredStep()
    {
        float before = Health(monsterDamage);
        int events = 0;
        SubscribeDamage(monsterDamage, () => events++);
        int inputEvents = 0;
        int attackStarts = 0;
        float startWorld=-1,dueWorld=-1;
        Component action=player.GetComponent("PlayerAction");
        float originalDelay=(float)action.GetType().GetField("attackHitDelay",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(action);
        Component input = (Component)FindType("GameInput").GetProperty("Instance").GetValue(null);
        SubscribeEvent(input, "OnBattlePressed", () =>
        {
            inputEvents++;
            Debug.Log($"P0V_INPUT Battle frame={Time.frameCount} real={Time.realtimeSinceStartup:R}");
        });
        SubscribeEvent(player.GetComponent("PlayerAction"), "OnAttackPerformed", () =>
        {
            attackStarts++;
            startWorld=Time.time;
            Debug.Log($"P0V_INPUT AttackStart frame={Time.frameCount} real={Time.realtimeSinceStartup:R}");
        });
        Debug.Log($"P0V_INPUT Before enabled={mouse.enabled} gate={Property(input, "IsGameplayInputEnabled")} " +
                  $"control={Property(player.GetComponent("PlayerAction"), "IsControlLocked")} " +
                  $"stamina={Property(sessionStats, "CurrentStamina")} focus={Application.isFocused}");
        EcologyAttackRequest queued = null;
        float healthAtEnqueue = -1f;
        barrier.OnTrace += (request, phase) =>
        {
            if (phase != "Enqueued") return;
            queued = request;
            dueWorld=Time.time;
            healthAtEnqueue = Health(monsterDamage);
        };
        InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Left));
        yield return Until(() => inputEvents > 0, 2f);
        Assert.That(attackStarts, Is.EqualTo(1), "The real input must begin one player attack.");
        yield return Until(() => queued != null, 2f);
        Assert.That(healthAtEnqueue, Is.EqualTo(before), "Nominal due must not call TakeDamage.");
        InputSystem.QueueStateEvent(mouse, new MouseState());
        yield return Until(() => events > 0, 2f);
        Assert.That(Health(monsterDamage), Is.EqualTo(before - 20f).Within(0.001f));
        Assert.That(queued.Status, Is.EqualTo(EcologyAttackStatus.Consumed));
        Assert.That(barrier.CompletedStep, Is.GreaterThanOrEqualTo(queued.RequiredCompletedStep));
        yield return new WaitForSeconds(0.1f);
        Assert.That(events, Is.EqualTo(1));
        Debug.Log($"T13_B1_REAL_PLAYER nominalDueWorld={startWorld+originalDelay:R} originalDueCallbackWorld={dueWorld:R} enqueueReal={queued.DueAtRealTime:R} consumeReal={queued.ConsumedAtRealTime:R} completed={barrier.CompletedStep} required={queued.RequiredCompletedStep} hpBefore={before:R} hpAfter={Health(monsterDamage):R}");
    }

    [UnityTest]
    public IEnumerator NewMonsterAttackDoesNotEraseQueuedAttackAndReentryCannotDuplicateDamage()
    {
        float before = Health(playerDamage);
        int events = 0;
        EcologyAttackRequest first = PrepareMonsterAttack();
        SubscribeDamage(playerDamage, () =>
        {
            events++;
            Assert.That(first.Status, Is.EqualTo(EcologyAttackStatus.Consumed));
            Assert.That((bool)Invoke(attack, "QueueEcologyDamage", first, player.transform, Vector2.left), Is.False);
        });
        Assert.That((bool)Invoke(attack, "QueueEcologyDamage", first, player.transform, Vector2.left), Is.True);
        EcologyAttackRequest nextWindup = PrepareMonsterAttack();
        Assert.That(nextWindup.AttackId, Is.GreaterThan(first.AttackId));
        yield return Until(() => events > 0, 2f);
        Assert.That(Health(playerDamage), Is.EqualTo(before - 5f).Within(0.001f),
            "The formal Torch prefab's inherited attack damage is five.");
        nextWindup.CancelWindup();
        yield return new WaitForSeconds(0.1f);
        Assert.That(events, Is.EqualTo(1));
    }

    [UnityTest]
    public IEnumerator ReusedTargetLeaseCannotReceiveOldQueuedDamage()
    {
        float before = Health(playerDamage);
        int events = 0;
        SubscribeDamage(playerDamage, () => events++);
        EcologyAttackRequest request = PrepareMonsterAttack();
        Assert.That((bool)Invoke(attack, "QueueEcologyDamage", request, player.transform, Vector2.left), Is.True);
        playerBinding.Bind(barrier, 2, 1, () => !(bool)Property(playerDamage, "IsDefeated"));
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        Assert.That(Health(playerDamage), Is.EqualTo(before));
        Assert.That(events, Is.Zero);
        Assert.That(request.Status, Is.EqualTo(EcologyAttackStatus.IdentityCancelled));
        Assert.That(barrier.PendingCount, Is.Zero);
    }

    [UnityTest]
    public IEnumerator UnregisteredMonsterKeepsOriginalImmediateReceiverPath()
    {
        monsterBinding.RetireLease();
        playerBinding.RetireLease();
        float before = Health(playerDamage);
        Assert.That((bool)Invoke(attack, "TryDealDamage", player.transform, Vector2.left), Is.True);
        Assert.That(Health(playerDamage), Is.EqualTo(before - 5f).Within(0.001f));
        Assert.That(barrier.PendingCount, Is.Zero);
        yield return null;
    }

    private EcologyAttackRequest PrepareMonsterAttack()
    {
        var request = (EcologyAttackRequest)Invoke(attack, "PrepareEcologyAttack", player.transform);
        Assert.That(request, Is.Not.Null);
        Invoke(attack, "BeginAttack", Vector2.left);
        return request;
    }

    [UnityTest]
    public IEnumerator FinalHurtIntentWinsOnePhysicsSubmission()
    {
        Type motionType = AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType("EcologyMotionBody")).FirstOrDefault(type => type != null);
        Assert.That(motionType, Is.Not.Null, "The approved opt-in motion ownership seam is not implemented yet.");
        monster.GetComponent<Rigidbody2D>().position = new Vector2(210f, 200f);
        Component motion = player.AddComponent(motionType);
        Collider2D solid = player.transform.Find("MovementFeet").GetComponent<Collider2D>();
        Invoke(motion, "Configure", solid, Physics2D.AllLayers, 0.02f);
        var records = new List<object>();
        ListenMotion(motion, records);
        object normal = Enum.Parse(FindType("EcologyMotionKind"), "Normal");
        object hurt = Enum.Parse(FindType("EcologyMotionKind"), "Hurt");
        EcologyMotionIntentProducer producer = player.AddComponent<EcologyMotionIntentProducer>();
        producer.Submission = () =>
        {
            Invoke(motion, "Request", new Vector2(202f, 200f), normal);
            Invoke(motion, "Request", new Vector2(200f, 200.25f), hurt);
            // A later normal producer simulates control becoming available on the last Hurt step.
            Invoke(motion, "Request", new Vector2(201f, 200f), normal);
        };
        yield return Until(() => records.Any(record =>
            (string)Value(record, "Phase") == "PhysicsObserved" &&
            Value(record, "Kind").ToString() == "Hurt"), 2f);
        object submitted = records.First(record =>
            (string)Value(record, "Phase") == "Submitted" && Value(record, "Kind").ToString() == "Hurt");
        long step = Convert.ToInt64(Value(submitted, "Step"));
        Assert.That(records.Count(record => (string)Value(record, "Phase") == "Submitted" &&
            Convert.ToInt64(Value(record, "Step")) == step), Is.EqualTo(1));
        Assert.That(player.GetComponent<Rigidbody2D>().position.x, Is.EqualTo(200f).Within(0.002f));
        Assert.That(player.GetComponent<Rigidbody2D>().position.y, Is.EqualTo(200.25f).Within(0.002f));
    }

    [UnityTest]
    public IEnumerator RealMonsterHurtLastStepKeepsItsReturnAgainstNormalIntent()
    {
        Vector2 home = new Vector2(210f, 200f);
        monster.GetComponent<Rigidbody2D>().position = home;
        Component motion = monster.AddComponent(FindType("EcologyMotionBody"));
        Invoke(motion, "Configure", monster.GetComponent<Collider2D>(), Physics2D.AllLayers, 0.02f);
        var records = new List<object>();
        ListenMotion(motion, records);
        long contestedStep = -1;
        object normal = Enum.Parse(FindType("EcologyMotionKind"), "Normal");
        EcologyMotionIntentProducer producer = monster.AddComponent<EcologyMotionIntentProducer>();
        producer.Repeat = true;
        producer.Submission = () =>
        {
            long step = barrier.CurrentStep;
            bool returningHomeNow = records.Any(record => (string)Value(record, "Phase") == "Requested" &&
                Convert.ToInt64(Value(record, "Step")) == step && Value(record, "Kind").ToString() == "Hurt" &&
                Vector2.Distance((Vector2)Value(record, "Position"), home) < 0.001f);
            if (!returningHomeNow) return;
            contestedStep = step;
            Invoke(motion, "Request", new Vector2(211f, 200f), normal);
            producer.Submission = null;
        };
        Invoke(monster.GetComponent("MonsterMovementController"), "SetDestination", new Vector2(212f, 200f));
        GameObject source = Own(new GameObject("P0V-NonMonsterImpact"));
        source.transform.position = new Vector3(211f, 200f);
        float before = Health(monsterDamage);
        monsterDamage.GetType().GetMethod("TakeDamage", new[] { typeof(float), typeof(Transform) })
            .Invoke(monsterDamage, new object[] { 1f, source.transform });
        Assert.That(Health(monsterDamage), Is.EqualTo(before - 1f).Within(0.001f));
        yield return Until(() => contestedStep > 0 && records.Any(record =>
            (string)Value(record, "Phase") == "PhysicsObserved" &&
            Convert.ToInt64(Value(record, "Step")) == contestedStep), 2f);
        object observed = records.First(record => (string)Value(record, "Phase") == "PhysicsObserved" &&
            Convert.ToInt64(Value(record, "Step")) == contestedStep);
        Assert.That(Vector2.Distance((Vector2)Value(observed, "Position"), home), Is.LessThan(0.002f));
        Assert.That(records.Count(record => (string)Value(record, "Phase") == "Submitted" &&
            Convert.ToInt64(Value(record, "Step")) == contestedStep), Is.EqualTo(1));
        Assert.That(records.Any(record => (string)Value(record, "Phase") == "LateIntentRejected"), Is.False);
        Assert.That(Health(monsterDamage), Is.EqualTo(before - 1f).Within(0.001f), "Returning home must not heal.");
    }

    private GameObject Clone(string path, Vector3 position)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        Assert.That(prefab, Is.Not.Null, path);
        return Own(UnityEngine.Object.Instantiate(prefab, position, Quaternion.identity));
    }

    [UnityTest]
    public IEnumerator RealPlayerHurtLastStepStillOwnsMovementAfterUnlock()
    {
        monster.GetComponent<Rigidbody2D>().position = new Vector2(210f, 200f);
        Component motion = player.AddComponent(FindType("EcologyMotionBody"));
        Invoke(motion, "Configure", player.transform.Find("MovementFeet").GetComponent<Collider2D>(),
            Physics2D.AllLayers, 0.02f);
        var records = new List<object>();
        ListenMotion(motion, records);
        Component hurt = player.GetComponent("PlayerHurtController");
        long contestedStep = -1;
        object normal = Enum.Parse(FindType("EcologyMotionKind"), "Normal");
        EcologyMotionIntentProducer producer = player.AddComponent<EcologyMotionIntentProducer>();
        producer.Repeat = true;
        producer.Submission = () =>
        {
            if ((bool)Property(hurt, "IsKnockbackActive")) return;
            contestedStep = barrier.CurrentStep;
            Invoke(motion, "Request", new Vector2(201f, 200f), normal);
            producer.Submission = null;
        };
        GameObject source = Own(new GameObject("P0V-PlayerImpact"));
        source.transform.position = new Vector3(199f, 200f);
        float before = Health(playerDamage);
        Assert.That((bool)Invoke(playerDamage, "TakeDamage", 1f, source.transform), Is.True);
        Assert.That((bool)Property(hurt, "IsKnockbackActive"), Is.True);
        yield return Until(() => contestedStep > 0 && records.Any(record =>
            (string)Value(record, "Phase") == "PhysicsObserved" &&
            Convert.ToInt64(Value(record, "Step")) == contestedStep), 2f);
        object observed = records.First(record => (string)Value(record, "Phase") == "PhysicsObserved" &&
            Convert.ToInt64(Value(record, "Step")) == contestedStep);
        Assert.That(Value(observed, "Kind").ToString(), Is.EqualTo("Hurt"),
            "Unlocking on the last Hurt step must not hand that step to normal movement.");
        Vector2 position = (Vector2)Value(observed, "Position");
        Assert.That(position.x, Is.EqualTo(200.65f).Within(0.002f));
        Assert.That(position.y, Is.EqualTo(200f).Within(0.002f));
        Assert.That(records.Count(record => (string)Value(record, "Phase") == "Submitted" &&
            Convert.ToInt64(Value(record, "Step")) == contestedStep), Is.EqualTo(1));
        Assert.That(Health(playerDamage), Is.EqualTo(before - 1f).Within(0.001f));
    }

    [UnityTest]
    public IEnumerator PostPhysicsPoseReportsContactCorrectionInsteadOfSubmittedHold()
    {
        Rigidbody2D playerBody = player.GetComponent<Rigidbody2D>();
        Assert.That(playerBody.position.x, Is.EqualTo(200f).Within(0.002f));
        Component motion = player.AddComponent(FindType("EcologyMotionBody"));
        Collider2D foot = player.transform.Find("MovementFeet").GetComponent<Collider2D>();
        Collider2D monsterSolid = monster.GetComponent<Collider2D>();
        Invoke(motion, "Configure", foot, Physics2D.AllLayers, 0.02f);
        var records = new List<object>();
        ListenMotion(motion, records);
        monster.GetComponent<Rigidbody2D>().position = new Vector2(200.7f, 200f);
        Physics2D.SyncTransforms();
        Assert.That(Physics2D.Distance(foot, monsterSolid).isOverlapped, Is.True,
            "This subcase explicitly introduces the known initial penetration.");
        long step = -1;
        object normal = Enum.Parse(FindType("EcologyMotionKind"), "Normal");
        EcologyMotionIntentProducer producer = player.AddComponent<EcologyMotionIntentProducer>();
        producer.Submission = () =>
        {
            step = barrier.CurrentStep;
            Invoke(motion, "Request", new Vector2(200f, 200f), normal);
        };
        yield return Until(() => step > 0 && records.Any(record =>
            (string)Value(record, "Phase") == "PhysicsObserved" &&
            Convert.ToInt64(Value(record, "Step")) == step), 2f);
        object submitted = records.First(record => (string)Value(record, "Phase") == "Submitted" &&
            Convert.ToInt64(Value(record, "Step")) == step);
        object observed = records.First(record => (string)Value(record, "Phase") == "PhysicsObserved" &&
            Convert.ToInt64(Value(record, "Step")) == step);
        Assert.That(((Vector2)Value(submitted, "Position")).x, Is.EqualTo(200f).Within(0.002f));
        Assert.That(((Vector2)Value(observed, "Position")).x, Is.LessThan(199.95f),
            "A submitted hold is not the actual path or even the final pose when contacts correct the body.");
    }

    private GameObject Own(GameObject value) { owned.Add(value); return value; }

    [UnityTest]
    public IEnumerator SolidFootCastStopsAtWallAndTriggerCannotBeConfiguredAsMotionSource()
    {
        monster.GetComponent<Rigidbody2D>().position = new Vector2(210f, 200f);
        CircleCollider2D foot = player.transform.Find("MovementFeet").GetComponent<CircleCollider2D>();
        Collider2D trigger = player.GetComponents<Collider2D>().Single(collider => collider.isTrigger);
        Assert.That(foot.radius, Is.EqualTo(0.3f).Within(0.000001f));
        Assert.That(foot.offset.x, Is.EqualTo(0.010397434f).Within(0.000001f));
        Assert.That(player.transform.lossyScale.x, Is.EqualTo(1f).Within(0.000001f));
        GameObject wallObject = Own(new GameObject("P0V-SolidWall"));
        wallObject.transform.position = new Vector3(201f, 200f);
        BoxCollider2D wall = wallObject.AddComponent<BoxCollider2D>();
        wall.size = new Vector2(0.1f, 4f);
        Physics2D.SyncTransforms();
        var filter = new ContactFilter2D();
        filter.SetLayerMask(Physics2D.AllLayers);
        filter.useTriggers = false;
        var hits = new RaycastHit2D[16];
        Func<Collider2D, float> wallDistance = source =>
        {
            int count = source.Cast(Vector2.right, filter, hits, 2f, true);
            return hits.Take(count).Where(hit => hit.collider == wall).Select(hit => hit.distance)
                .DefaultIfEmpty(float.PositiveInfinity).Min();
        };
        float footDistance = wallDistance(foot);
        float triggerDistance = wallDistance(trigger);
        Assert.That(float.IsInfinity(footDistance), Is.False);
        Assert.That(triggerDistance, Is.LessThan(footDistance - 0.1f),
            "Target useTriggers=false does not replace selecting the correct source shape.");
        Debug.Log($"P0V_CAST_SOURCE foot={footDistance:R} trigger={triggerDistance:R} " +
                  $"footId={foot.GetInstanceID()} triggerId={trigger.GetInstanceID()}");
        Component motion = player.AddComponent(FindType("EcologyMotionBody"));
        TargetInvocationException rejected = Assert.Throws<TargetInvocationException>(() =>
            Invoke(motion, "Configure", trigger, Physics2D.AllLayers, 0.02f));
        Assert.That(rejected.InnerException, Is.TypeOf<ArgumentException>());
        Invoke(motion, "Configure", foot, Physics2D.AllLayers, 0.02f);
        var records = new List<object>();
        ListenMotion(motion, records);
        long step = -1;
        object normal = Enum.Parse(FindType("EcologyMotionKind"), "Normal");
        EcologyMotionIntentProducer producer = player.AddComponent<EcologyMotionIntentProducer>();
        producer.Submission = () =>
        {
            step = barrier.CurrentStep;
            Invoke(motion, "Request", new Vector2(202f, 200f), normal);
        };
        yield return Until(() => step > 0 && records.Any(record =>
            (string)Value(record, "Phase") == "PhysicsObserved" &&
            Convert.ToInt64(Value(record, "Step")) == step), 2f);
        object observed = records.First(record => (string)Value(record, "Phase") == "PhysicsObserved" &&
            Convert.ToInt64(Value(record, "Step")) == step);
        Vector2 actual = (Vector2)Value(observed, "Position");
        // Cast has native shape-query tolerances; the contract subtracts skin from that result.
        // Keep a separate geometric gap check so a larger trigger cannot masquerade as the foot.
        Assert.That(actual.x, Is.EqualTo(200f + footDistance - 0.02f).Within(0.0001f));
        Assert.That(actual.y, Is.EqualTo(200f).Within(0.002f));
        ColliderDistance2D gap = Physics2D.Distance(foot, wall);
        Debug.Log($"P0V_WALL_GAP measured={gap.distance:R} analyticX={200.6196f:R} actualX={actual.x:R}");
        Assert.That(gap.isOverlapped, Is.False);
        Assert.That(gap.distance, Is.GreaterThan(0f));
        Assert.That(actual.x, Is.InRange(200.6196f - 0.01f, 200.6196f + 0.01f));
    }

    [UnityTest]
    public IEnumerator RealPlayerCrossingProtectionAndLeavingBeforeDueCancelsOnlyOldAttack()
    {
        MethodInfo configure = typeof(EcologyDamageBarrier).GetMethod("ConfigureProtection");
        Assert.That(configure, Is.Not.Null, "A real geometry observer must replace manual protection toggles.");
        monster.GetComponent<Rigidbody2D>().position = new Vector2(200f, 201.5f);
        Physics2D.SyncTransforms();
        configure.Invoke(barrier, new object[] { new[] { new Rect(200.8f, 199f, 0.35f, 2f) } });
        Component motion = player.AddComponent(FindType("EcologyMotionBody"));
        Invoke(motion, "Configure", player.transform.Find("MovementFeet").GetComponent<Collider2D>(),
            Physics2D.AllLayers, 0.02f);
        Component monsterMotion = monster.AddComponent(FindType("EcologyMotionBody"));
        Invoke(monsterMotion, "Configure", monster.GetComponents<Collider2D>().Single(c => !c.isTrigger),
            Physics2D.AllLayers, 0.02f);
        var records = new List<object>();
        ListenMotion(motion, records);
        var attacks = new List<EcologyAttackRequest>();
        barrier.OnTrace += (request, phase) => { if (phase == "Started") attacks.Add(request); };
        int damageEvents = 0;
        SubscribeDamage(monsterDamage, () => damageEvents++);
        float before = Health(monsterDamage);
        InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Left));
        yield return Until(() => attacks.Count == 1, 2f);
        object normal = Enum.Parse(FindType("EcologyMotionKind"), "Normal");
        var producer = player.AddComponent<EcologyMotionIntentProducer>();
        producer.Submission = () => Invoke(motion, "Request", new Vector2(201.9f, 200f), normal);
        yield return Until(() => records.Any(record => (string)Value(record, "Phase") == "PhysicsObserved" &&
            ((Vector2)Value(record, "Position")).x > 201.8f), 2f);
        Assert.That(playerBinding.ProtectionNow, Is.False, "Both actual endpoints lie outside the region.");
        Assert.That(playerBinding.ProtectionRevision, Is.EqualTo(2), "The actual swept foot entered then left.");
        yield return Until(() => attacks[0].Status != EcologyAttackStatus.Windup &&
            attacks[0].Status != EcologyAttackStatus.Pending, 2f);
        Assert.That(attacks[0].Status, Is.EqualTo(EcologyAttackStatus.ProtectionCancelled));
        Assert.That(Health(monsterDamage), Is.EqualTo(before));
        Assert.That(damageEvents, Is.Zero);
        InputSystem.QueueStateEvent(mouse, new MouseState());
        producer.Submission = () => Invoke(motion, "Request", new Vector2(200f, 200f), normal);
        yield return Until(() => player.GetComponent<Rigidbody2D>().position.x < 200.01f, 2f);
        yield return new WaitForFixedUpdate();
        Assert.That(playerBinding.ProtectionNow, Is.False);
        yield return new WaitForSeconds(0.5f);
        InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Left));
        yield return Until(() => damageEvents > 0, 2f);
        InputSystem.QueueStateEvent(mouse, new MouseState());
        Assert.That(Health(monsterDamage), Is.EqualTo(before - 20f).Within(0.001f));
        Assert.That(damageEvents, Is.EqualTo(1));
        Assert.That(attacks[0].Status, Is.EqualTo(EcologyAttackStatus.ProtectionCancelled));
        Assert.That(attacks[1].Status, Is.EqualTo(EcologyAttackStatus.Consumed));
    }
    [UnityTest]
    public IEnumerator EnteringAfterOriginalDueButBeforeRequiredPhysicsCancelsDamage()
    {
        monster.GetComponent<Rigidbody2D>().position = new Vector2(200f, 201.5f);
        Physics2D.SyncTransforms();
        barrier.ConfigureProtection(new[] { new Rect(200.8f, 199f, 0.35f, 2f) });
        Component motion = ConfigurePlayerMotion();
        object normal = Enum.Parse(FindType("EcologyMotionKind"), "Normal");
        var producer = player.AddComponent<EcologyMotionIntentProducer>();
        EcologyAttackRequest pending = null;
        float before = Health(monsterDamage);
        int events = 0;
        SubscribeDamage(monsterDamage, () => events++);
        barrier.OnTrace += (request, phase) =>
        {
            if (phase != "Enqueued") return;
            pending = request;
            Assert.That(Health(monsterDamage), Is.EqualTo(before));
            producer.Submission = () => Invoke(motion, "Request", new Vector2(201f, 200f), normal);
        };
        InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Left));
        yield return Until(() => pending != null && pending.Status != EcologyAttackStatus.Pending, 2f);
        InputSystem.QueueStateEvent(mouse, new MouseState());
        Assert.That(pending.Status, Is.EqualTo(EcologyAttackStatus.ProtectionCancelled));
        Assert.That(playerBinding.ProtectionNow, Is.True);
        Assert.That(Health(monsterDamage), Is.EqualTo(before));
        Assert.That(events, Is.Zero);
    }

    [UnityTest]
    public IEnumerator WallClippedRequestDoesNotInventProtectionEntryOrCancelRealAttack()
    {
        monster.GetComponent<Rigidbody2D>().position = new Vector2(200f, 201.5f);
        GameObject wallObject = Own(new GameObject("P0V-WallBeforeProtection"));
        wallObject.transform.position = new Vector3(201f, 200f);
        wallObject.AddComponent<BoxCollider2D>().size = new Vector2(0.1f, 4f);
        Physics2D.SyncTransforms();
        barrier.ConfigureProtection(new[] { new Rect(201.2f, 199f, 0.2f, 2f) });
        Component motion = ConfigurePlayerMotion();
        object normal = Enum.Parse(FindType("EcologyMotionKind"), "Normal");
        var producer = player.AddComponent<EcologyMotionIntentProducer>();
        producer.Submission = () => Invoke(motion, "Request", new Vector2(202f, 200f), normal);
        float before = Health(monsterDamage);
        int events = 0;
        SubscribeDamage(monsterDamage, () => events++);
        InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Left));
        yield return Until(() => events > 0, 2f);
        InputSystem.QueueStateEvent(mouse, new MouseState());
        Assert.That(playerBinding.ProtectionNow, Is.False);
        Assert.That(playerBinding.ProtectionRevision, Is.Zero);
        Assert.That(playerBinding.MotionFaultRevision, Is.Zero);
        Assert.That(Health(monsterDamage), Is.EqualTo(before - 20f).Within(0.001f));
        Assert.That(events, Is.EqualTo(1));
        Assert.That(player.GetComponent<Rigidbody2D>().position.x, Is.LessThan(200.7f));
    }

    [UnityTest]
    public IEnumerator NormalDynamicContactMustNotBecomeUnknownForTwentyRepetitions()
    {
        barrier.ConfigureProtection(new[] { new Rect(199.3f, 199f, 0.1f, 2f) });
        Component motion = ConfigurePlayerMotion();
        var records = new List<object>();
        ListenMotion(motion, records);
        int unknownSteps = 0;
        int faultAttacks = 0;
        for (int repetition = 0; repetition < 20; repetition++)
        {
            // Reproduce ordinary Dynamic-player/Kinematic-monster contact correction. No force,
            // new simulation, body-type change, production pause, or fake protection toggle.
            player.GetComponent<Rigidbody2D>().position = new Vector2(200f, 200f);
            monster.GetComponent<Rigidbody2D>().position = new Vector2(201.25f, 200f);
            Physics2D.SyncTransforms();
            Invoke(motion, "Configure", player.transform.Find("MovementFeet").GetComponent<Collider2D>(),
                Physics2D.AllLayers, 0.02f);
            yield return new WaitForFixedUpdate();
            EcologyAttackRequest request = PrepareMonsterAttack();
            Assert.That((bool)Invoke(attack, "QueueEcologyDamage", request, player.transform, Vector2.left), Is.True);
            long faultBefore = playerBinding.MotionFaultRevision;
            monster.GetComponent<Rigidbody2D>().position = new Vector2(200.7f, 200f);
            Physics2D.SyncTransforms();
            Assert.That(Physics2D.Distance(player.transform.Find("MovementFeet").GetComponent<Collider2D>(),
                monster.GetComponents<Collider2D>().Single(c => !c.isTrigger)).isOverlapped, Is.True);
            yield return Until(() => request.Status != EcologyAttackStatus.Pending, 2f);
            if (playerBinding.MotionFaultRevision > faultBefore) unknownSteps++;
            if (request.Status == EcologyAttackStatus.FaultRejected) faultAttacks++;
            Debug.Log($"P0V_NORMAL_CONTACT repetition={repetition} status={request.Status} " +
                      $"faultBefore={faultBefore} faultAfter={playerBinding.MotionFaultRevision} " +
                      $"player={player.GetComponent<Rigidbody2D>().position} " +
                      $"protectionRevision={playerBinding.ProtectionRevision}");
        }
        Debug.Log($"P0V_NORMAL_CONTACT_TOTAL repetitions=20 unknown={unknownSteps} faultAttacks={faultAttacks}");
        Assert.That(unknownSteps, Is.Zero, "Normal contact is a required supported case, not an intentional unknown.");
        Assert.That(faultAttacks, Is.Zero);
    }

    [UnityTest]
    public IEnumerator BothRegisteredBodiesCanStillMeetInsideOneAutomaticPhysicsStep()
    {
        barrier.ConfigureProtection(new[] { new Rect(200.34f, 199f, 0.02f, 2f) });
        Component playerMotion = ConfigurePlayerMotion();
        Rigidbody2D playerBody = player.GetComponent<Rigidbody2D>();
        Rigidbody2D torchBody = monster.GetComponent<Rigidbody2D>();
        Component torchMotion = monster.AddComponent(FindType("EcologyMotionBody"));
        Collider2D foot = player.transform.Find("MovementFeet").GetComponent<Collider2D>();
        Collider2D torchSolid = monster.GetComponents<Collider2D>().Single(c => !c.isTrigger);
        var playerRows = new List<object>();
        var torchRows = new List<object>();
        ListenMotion(playerMotion, playerRows);
        ListenMotion(torchMotion, torchRows);
        var playerProducer = player.AddComponent<EcologyMotionIntentProducer>();
        var torchProducer = monster.AddComponent<EcologyMotionIntentProducer>();
        object normal = Enum.Parse(FindType("EcologyMotionKind"), "Normal");
        int contactSteps = 0, unknownSteps = 0;
        for (int repetition = 0; repetition < 20; repetition++)
        {
            playerBody.position = new Vector2(200f, 200f);
            torchBody.position = new Vector2(200.95f, 200f);
            Physics2D.SyncTransforms();
            Invoke(playerMotion, "Configure", foot, Physics2D.AllLayers, 0.02f);
            Invoke(torchMotion, "Configure", torchSolid, Physics2D.AllLayers, 0.02f);
            Assert.That(Physics2D.Distance(foot, torchSolid).isOverlapped, Is.False);
            yield return new WaitForFixedUpdate();
            long step = -1;
            playerProducer.Submission = () =>
            {
                step = barrier.CurrentStep;
                Invoke(playerMotion, "Request", new Vector2(200.06f, 200f), normal);
            };
            torchProducer.Submission = () => Invoke(torchMotion, "Request", new Vector2(200.89f, 200f), normal);
            yield return Until(() => step > 0 && playerRows.Any(record =>
                Convert.ToInt64(Value(record, "Step")) == step &&
                (string)Value(record, "Phase") == "PhysicsObserved"), 2f);
            object[] playerStep = playerRows.Where(record => Convert.ToInt64(Value(record, "Step")) == step).ToArray();
            object[] torchStep = torchRows.Where(record => Convert.ToInt64(Value(record, "Step")) == step).ToArray();
            Assert.That(playerStep.Count(record => (string)Value(record, "Phase") == "Submitted"), Is.EqualTo(1));
            Assert.That(torchStep.Count(record => (string)Value(record, "Phase") == "Submitted"), Is.EqualTo(1));
            if (playerStep.Any(record => (string)Value(record, "Phase") == "ContactCallbackObserved")) contactSteps++;
            if (playerStep.Any(record => (string)Value(record, "Phase") == "MotionHistoryUnknown")) unknownSteps++;
            Vector2 submitted = (Vector2)Value(playerStep.Single(record => (string)Value(record, "Phase") == "Submitted"), "Position");
            Debug.Log($"P0V_BOTH_REGISTERED repetition={repetition} step={step} submitted={submitted} " +
                      $"actual={playerBody.position} torch={torchBody.position}");
        }
        Debug.Log($"P0V_BOTH_REGISTERED_TOTAL repetitions=20 contacts={contactSteps} unknown={unknownSteps}");
        Assert.That(contactSteps, Is.EqualTo(20));
        Assert.That(unknownSteps, Is.Zero, "Both bodies used the one-submit owner and explicit solid Cast; normal contact remains required.");
    }

    [UnityTest]
    public IEnumerator IntentionalUnobservedPositionWriteIsFaultRejectedWithoutFakeProtectionEntry()
    {
        barrier.ConfigureProtection(new[] { new Rect(200.8f, 199f, 0.35f, 2f) });
        ConfigurePlayerMotion();
        float before = Health(playerDamage);
        EcologyAttackRequest request = PrepareMonsterAttack();
        Assert.That((bool)Invoke(attack, "QueueEcologyDamage", request, player.transform, Vector2.left), Is.True);
        player.GetComponent<Rigidbody2D>().position = new Vector2(200f, 200.5f);
        yield return Until(() => request.Status != EcologyAttackStatus.Pending, 2f);
        Assert.That(request.Status, Is.EqualTo(EcologyAttackStatus.FaultRejected));
        Assert.That(playerBinding.MotionFaultRevision, Is.GreaterThan(0));
        Assert.That(playerBinding.ProtectionRevision, Is.Zero);
        Assert.That(Health(playerDamage), Is.EqualTo(before));
    }

    [UnityTest]
    public IEnumerator ActualContactPoseInsideProtectionCancelsOldDamageWithoutInventingPath()
    {
        Rect region = new Rect(199.45f, 199.4f, 0.15f, 0.8f);
        barrier.ConfigureProtection(new[] { region });
        Rigidbody2D playerBody = player.GetComponent<Rigidbody2D>();
        Rigidbody2D torchBody = monster.GetComponent<Rigidbody2D>();
        torchBody.position = new Vector2(201.25f, 200f);
        Physics2D.SyncTransforms();
        Collider2D foot = player.transform.Find("MovementFeet").GetComponent<Collider2D>();
        Collider2D torchSolid = monster.GetComponents<Collider2D>().Single(c => !c.isTrigger);
        Component motion = ConfigurePlayerMotion();
        GameObject oracleObject = Own(new GameObject("P0V-IndependentProtectionOracle"));
        oracleObject.transform.position = region.center;
        BoxCollider2D oracle = oracleObject.AddComponent<BoxCollider2D>();
        oracle.isTrigger = true;
        oracle.size = region.size;
        Physics2D.SyncTransforms();
        Assert.That(Physics2D.Distance(foot, oracle).isOverlapped, Is.False);
        var records = new List<object>();
        ListenMotion(motion, records);
        float before = Health(playerDamage);
        int damageEvents = 0;
        SubscribeDamage(playerDamage, () => damageEvents++);
        EcologyAttackRequest request = PrepareMonsterAttack();
        Assert.That((bool)Invoke(attack, "QueueEcologyDamage", request, player.transform, Vector2.left), Is.True);
        object normal = Enum.Parse(FindType("EcologyMotionKind"), "Normal");
        var playerProducer = player.AddComponent<EcologyMotionIntentProducer>();
        var torchProducer = monster.AddComponent<EcologyMotionIntentProducer>();
        playerProducer.Submission = () => Invoke(motion, "Request", new Vector2(200.06f, 200f), normal);
        torchProducer.Submission = () => torchBody.MovePosition(new Vector2(200.7f, 200f));
        yield return Until(() => request.Status != EcologyAttackStatus.Pending, 2f);
        Assert.That(Physics2D.Distance(foot, oracle).isOverlapped, Is.True,
            "The independent native shape query must confirm actual complete-foot contact.");
        Assert.That(request.Status, Is.EqualTo(EcologyAttackStatus.ProtectionCancelled));
        Assert.That(playerBinding.ProtectionNow, Is.True);
        Assert.That(Health(playerDamage), Is.EqualTo(before));
        Assert.That(damageEvents, Is.Zero);
        Assert.That(records.Any(row => (string)Value(row, "Phase") == "ProtectionEntered"), Is.True);
    }

    private Component ConfigurePlayerMotion()
    {
        Component motion = player.AddComponent(FindType("EcologyMotionBody"));
        Invoke(motion, "Configure", player.transform.Find("MovementFeet").GetComponent<Collider2D>(),
            Physics2D.AllLayers, 0.02f);
        return motion;
    }

    [UnityTest]
    public IEnumerator B1NativeMovingContactUsesRealReceiverExactlyOnce()
    {
        barrier.ConfigureProtection(new[]{new Rect(199.3f,199f,.1f,2f)});
        ConfigurePlayerMotion();
        Rigidbody2D torchBody=monster.GetComponent<Rigidbody2D>();
        var producer=monster.AddComponent<EcologyMotionIntentProducer>();
        float before=Health(playerDamage);int events=0;
        SubscribeDamage(playerDamage,()=>events++);
        EcologyAttackRequest request=PrepareMonsterAttack();
        Assert.That((bool)Invoke(attack,"QueueEcologyDamage",request,player.transform,Vector2.left),Is.True);
        producer.Submission=()=>torchBody.MovePosition(new Vector2(200.7f,200f));
        yield return Until(()=>request.Status!=EcologyAttackStatus.Pending,2f);
        Assert.That(playerBinding.ProtectionNow,Is.False);
        Assert.That(playerBinding.ProtectionRevision,Is.Zero);
        Assert.That(request.Status,Is.EqualTo(EcologyAttackStatus.Consumed));
        Assert.That(request.DamageApplied,Is.True);
        Assert.That(Health(playerDamage),Is.EqualTo(before-5f).Within(.001f));
        Assert.That(events,Is.EqualTo(1));
        yield return new WaitForFixedUpdate();
        Assert.That(events,Is.EqualTo(1));
        Debug.Log($"T13_B1_NATIVE_HP status={request.Status} before={before:R} after={Health(playerDamage):R} OnDamaged={events} classification=observed-outside-not-continuous-proof due={request.DueAtRealTime:R} consumed={request.ConsumedAtRealTime:R}");
    }

    [UnityTest] public IEnumerator B1BothControlledBodiesOutsideOriginalNarrowProtectionDealDamageOnce()=>B1ControlledContactAtOriginalBoundary(false);
    [UnityTest] public IEnumerator B1BothControlledBodiesOriginalContactEnteringAttackerProtectionCancelsOldAttack()=>B1ControlledContactAtOriginalBoundary(true);
    private IEnumerator B1ControlledContactAtOriginalBoundary(bool attackerFromRight)
    {
        Rect region=new Rect(200.34f,199f,.02f,2f);
        barrier.ConfigureProtection(new[]{region});
        Rigidbody2D playerBody=player.GetComponent<Rigidbody2D>(),torchBody=monster.GetComponent<Rigidbody2D>();
        playerBody.position=new Vector2(200,200);
        // Mirror the native foot-center gap, including Torch's asymmetric collider offset.
        torchBody.position=new Vector2(attackerFromRight?200.95f:199.1613f,200);
        Physics2D.SyncTransforms();
        Debug.Log($"T13_B1_DUAL_ARRANGE fromRight={attackerFromRight} native={torchBody.position} transform={monster.transform.position}");
        yield return Until(()=>Vector2.Distance(monster.transform.position,torchBody.position)<.00001f,2f);
        Collider2D foot=player.transform.Find("MovementFeet").GetComponent<Collider2D>();
        Collider2D torchFoot=monster.GetComponents<Collider2D>().Single(c=>!c.isTrigger);
        Component playerMotion=ConfigurePlayerMotion(),torchMotion=monster.AddComponent(FindType("EcologyMotionBody"));
        Invoke(torchMotion,"Configure",torchFoot,Physics2D.AllLayers,.02f);
        Assert.That(foot.bounds.max.x,Is.LessThan(region.xMin));
        Assert.That(attackerFromRight?torchFoot.bounds.min.x>region.xMax:torchFoot.bounds.max.x<region.xMin,Is.True);
        Assert.That(Physics2D.Distance(foot,torchFoot).isOverlapped,Is.False);
        var playerRows=new List<object>();var torchRows=new List<object>();
        ListenMotion(playerMotion,playerRows);ListenMotion(torchMotion,torchRows);
        float before=Health(playerDamage);int events=0;bool outsideAtDamage=false;
        SubscribeDamage(playerDamage,()=>
        {
            events++;
            outsideAtDamage=foot.bounds.max.x<region.xMin&&torchFoot.bounds.max.x<region.xMin;
        });
        var request=(EcologyAttackRequest)Invoke(attack,"PrepareEcologyAttack",player.transform);
        Vector2 aim=attackerFromRight?Vector2.left:Vector2.right;
        Invoke(attack,"BeginAttack",aim);
        Assert.That((bool)Invoke(attack,"QueueEcologyDamage",request,player.transform,aim),Is.True);
        object normal=Enum.Parse(FindType("EcologyMotionKind"),"Normal");long step=-1;
        player.AddComponent<EcologyMotionIntentProducer>().Submission=()=>
        {
            step=barrier.CurrentStep;
            Invoke(playerMotion,"Request",new Vector2(attackerFromRight?200.06f:199.94f,200),normal);
        };
        monster.AddComponent<EcologyMotionIntentProducer>().Submission=()=>Invoke(torchMotion,"Request",new Vector2(attackerFromRight?200.89f:199.2213f,200),normal);
        yield return Until(()=>request.Status!=EcologyAttackStatus.Pending,2f);
        Assert.That(playerRows.Count(r=>Convert.ToInt64(Value(r,"Step"))==step&&(string)Value(r,"Phase")=="Submitted"),Is.EqualTo(1));
        Assert.That(torchRows.Count(r=>Convert.ToInt64(Value(r,"Step"))==step&&(string)Value(r,"Phase")=="Submitted"),Is.EqualTo(1));
        Assert.That(playerRows.Any(r=>Convert.ToInt64(Value(r,"Step"))==step&&(string)Value(r,"Phase")=="ContactCallbackObserved"),Is.True);
        if(attackerFromRight)
        {
            Assert.That(request.Status,Is.EqualTo(EcologyAttackStatus.ProtectionCancelled));
            Assert.That(monsterBinding.ProtectionRevision,Is.GreaterThan(0));
            Assert.That(torchFoot.bounds.min.x<=region.xMax&&torchFoot.bounds.max.x>=region.xMin,Is.True);
            Assert.That(events,Is.Zero);Assert.That(Health(playerDamage),Is.EqualTo(before));
        }
        else
        {
            Assert.That(request.Status,Is.EqualTo(EcologyAttackStatus.Consumed));
            Assert.That(request.DamageApplied,Is.True);Assert.That(outsideAtDamage,Is.True,"Native full-shape bounds at the actual receiver must be outside; this is not continuous-path proof");
            Assert.That(events,Is.EqualTo(1));Assert.That(Health(playerDamage),Is.EqualTo(before-5).Within(.001f));
        }
        Debug.Log($"T13_B1_DUAL_CONTACT fromRight={attackerFromRight} region=[200.34,200.36] step={step} status={request.Status} hpBefore={before:R} hpAfter={Health(playerDamage):R} OnDamaged={events} outsideAtReceiver={outsideAtDamage} attackerRevision={monsterBinding.ProtectionRevision}");
    }

    [UnityTest] public IEnumerator B1CriticalProtectionCasesRepeatTwentyTimes()
    {
        Func<IEnumerator>[] cases={()=>B1ControlledContactAtOriginalBoundary(false),()=>B1ControlledContactAtOriginalBoundary(true),
            ActualContactPoseInsideProtectionCancelsOldDamageWithoutInventingPath,RealPlayerCrossingProtectionAndLeavingBeforeDueCancelsOnlyOldAttack};
        int completed=0;
        for(int repetition=0;repetition<20;repetition++)
        for(int variant=0;variant<cases.Length;variant++)
        {
            yield return cases[variant]();completed++;
            if(repetition<19||variant<cases.Length-1){yield return TearDown();yield return SetUp();}
        }
        Assert.That(completed,Is.EqualTo(80));
        Debug.Log("T13_B1_BOUNDARY_REPEATS cycles=20 variants=4 completed=80 failures=0; native outside contact / observed attacker entry / observed pushed foot / proven sweep then exit and new attack");
    }

    [UnityTest] public IEnumerator OriginalUnboundAiRecordsActualWindupAndImmediateReceiver()
    {
        playerBinding.RetireLease();monsterBinding.RetireLease();
        float start=-1,receiverWorld=-1,receiverReal=-1;bool receiverInFixed=true;int events=0;
        float before=Health(playerDamage);
        SubscribeEvent(attack,"OnAttackStarted",()=>start=Time.time);
        SubscribeDamage(playerDamage,()=>{events++;receiverWorld=Time.time;receiverReal=Time.realtimeSinceStartup;receiverInFixed=Time.inFixedTimeStep;});
        var ai=(Behaviour)monster.GetComponent("MonsterAIController");ai.enabled=true;Invoke(ai,"ReinitializeForMap");
        yield return Until(()=>events>0,4f);ai.enabled=false;
        float windup=Convert.ToSingle(Property(attack,"AttackWindupDuration"));
        Assert.That(receiverWorld-start,Is.GreaterThanOrEqualTo(windup-.003f));
        Assert.That(receiverInFixed,Is.False);Assert.That(barrier.PendingCount,Is.Zero);
        Assert.That(events,Is.EqualTo(1));Assert.That(Health(playerDamage),Is.EqualTo(before-5).Within(.001f));
        Debug.Log($"T13_LEGACY_REAL_AI nominalDueWorld={start+windup:R} actualReceiverWorld={receiverWorld:R} actualReceiverReal={receiverReal:R} receiverInFixed={receiverInFixed} queue=0 hpBefore={before:R} hpAfter={Health(playerDamage):R}");
    }

    [UnityTest] public IEnumerator OriginalUnboundPlayerInputRecordsActualDelayAndImmediateReceiver()
    {
        playerBinding.RetireLease();monsterBinding.RetireLease();
        Component action=player.GetComponent("PlayerAction");
        float delay=(float)action.GetType().GetField("attackHitDelay",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(action);
        float start=-1,receiverWorld=-1,receiverReal=-1;int events=0;
        float before=Health(monsterDamage);
        SubscribeEvent(action,"OnAttackPerformed",()=>start=Time.time);
        SubscribeDamage(monsterDamage,()=>{events++;receiverWorld=Time.time;receiverReal=Time.realtimeSinceStartup;});
        InputSystem.QueueStateEvent(mouse,new MouseState().WithButton(MouseButton.Left));
        yield return Until(()=>events>0,3f);InputSystem.QueueStateEvent(mouse,new MouseState());
        Assert.That(receiverWorld-start,Is.GreaterThanOrEqualTo(delay-.003f));
        Assert.That(barrier.PendingCount,Is.Zero);Assert.That(events,Is.EqualTo(1));
        Assert.That(Health(monsterDamage),Is.EqualTo(before-20).Within(.001f));
        Debug.Log($"T13_LEGACY_REAL_PLAYER nominalDueWorld={start+delay:R} actualReceiverWorld={receiverWorld:R} actualReceiverReal={receiverReal:R} queue=0 hpBefore={before:R} hpAfter={Health(monsterDamage):R}");
    }

    [UnityTest]
    public IEnumerator B1RealPerceptionAiWindupQueuesThenConsumesOriginalDamage()
    {
        barrier.ConfigureProtection(new[]{new Rect(199.3f,199f,.1f,2f)});
        ConfigurePlayerMotion();
        Component torchMotion=monster.AddComponent(FindType("EcologyMotionBody"));
        Invoke(torchMotion,"Configure",monster.GetComponents<Collider2D>().Single(c=>!c.isTrigger),Physics2D.AllLayers,.02f);
        EcologyAttackRequest observed=null;
        float startWorld=0,dueWorld=0;int events=0;
        float before=Health(playerDamage);
        SubscribeDamage(playerDamage,()=>events++);
        barrier.OnTrace+=(request,phase)=>
        {
            if(request.AttackerId!=monsterBinding.ResidentId)return;
            if(phase=="Started"&&observed==null){observed=request;startWorld=Time.time;}
            if(request==observed&&phase=="Enqueued")
            {
                dueWorld=Time.time;
                Assert.That(Health(playerDamage),Is.EqualTo(before),"Actual AI nominal-due callback must enqueue, not damage early");
            }
        };
        Behaviour ai=(Behaviour)monster.GetComponent("MonsterAIController");
        ai.enabled=true;
        Invoke(ai,"ReinitializeForMap");
        yield return Until(()=>observed!=null&&observed.Status==EcologyAttackStatus.Consumed,4f);
        ai.enabled=false;
        float windup=Convert.ToSingle(Property(attack,"AttackWindupDuration"));
        Assert.That(dueWorld-startWorld,Is.GreaterThanOrEqualTo(windup-.003f));
        Assert.That(observed.DamageApplied,Is.True);
        Assert.That(events,Is.EqualTo(1));
        Assert.That(Health(playerDamage),Is.EqualTo(before-5).Within(.001f));
        Debug.Log($"T13_B1_REAL_AI startWorld={startWorld:R} nominalDueWorld={startWorld+windup:R} originalDueCallbackWorld={dueWorld:R} enqueueReal={observed.DueAtRealTime:R} consumeReal={observed.ConsumedAtRealTime:R} required={observed.RequiredCompletedStep} completed={barrier.CompletedStep} hpBefore={before:R} hpAfter={Health(playerDamage):R} OnDamaged={events}");
    }

    [UnityTest]
    public IEnumerator B1UnsupportedFootGeometryStillFaultsDuringActualContact()
    {
        barrier.ConfigureProtection(new[]{new Rect(199.3f,199f,.1f,2f)});
        Collider2D foot=player.transform.Find("MovementFeet").GetComponent<Collider2D>();
        ConfigurePlayerMotion();
        long beforeFault=playerBinding.MotionFaultRevision;
        float before=Health(playerDamage);
        EcologyAttackRequest request=PrepareMonsterAttack();
        Assert.That((bool)Invoke(attack,"QueueEcologyDamage",request,player.transform,Vector2.left),Is.True);
        foot.transform.localScale=new Vector3(1,1.1f,1);
        Physics2D.SyncTransforms();
        monster.AddComponent<EcologyMotionIntentProducer>().Submission=()=>monster.GetComponent<Rigidbody2D>().MovePosition(new Vector2(200.7f,200f));
        yield return Until(()=>request.Status!=EcologyAttackStatus.Pending,2f);
        Assert.That(request.Status,Is.EqualTo(EcologyAttackStatus.FaultRejected));
        Assert.That(playerBinding.MotionFaultRevision,Is.GreaterThan(beforeFault));
        Assert.That(playerBinding.ProtectionRevision,Is.Zero);
        Assert.That(Health(playerDamage),Is.EqualTo(before));
    }

    [UnityTest]
    public IEnumerator B1EachPendingConsumptionRefreshesActualFootAfterEarlierReceiverCallback()
    {
        barrier.ConfigureProtection(new[]{new Rect(199.1f,199.8f,.2f,.4f)});
        ConfigurePlayerMotion();
        GameObject oracleObject=Own(new GameObject("B1-consume-native-protection-oracle"));
        oracleObject.transform.position=new Vector3(199.2f,200);
        BoxCollider2D oracle=oracleObject.AddComponent<BoxCollider2D>();oracle.size=new Vector2(.2f,.4f);oracle.isTrigger=true;
        Collider2D foot=player.transform.Find("MovementFeet").GetComponent<Collider2D>();
        int events=0;float before=Health(playerDamage);
        SubscribeDamage(playerDamage,()=>
        {
            events++;
            if(events==1)
            {
                player.GetComponent<Rigidbody2D>().position=new Vector2(199.4f,200);Physics2D.SyncTransforms();
                ColliderDistance2D native=Physics2D.Distance(foot,oracle);
                Debug.Log($"T13_B1_CONSUME_POSE body={player.GetComponent<Rigidbody2D>().position} transform={player.transform.position} nativeBounds={foot.bounds.center} nativeOverlap={native.isOverlapped}");
                Assert.That(native.isOverlapped,Is.True,"Independent native query must witness the arranged contact before testing protection refresh");
            }
        });
        EcologyAttackRequest first=PrepareMonsterAttack(),second=PrepareMonsterAttack();
        Assert.That((bool)Invoke(attack,"QueueEcologyDamage",first,player.transform,Vector2.left),Is.True);
        Assert.That((bool)Invoke(attack,"QueueEcologyDamage",second,player.transform,Vector2.left),Is.True);
        yield return Until(()=>second.Status!=EcologyAttackStatus.Pending,2f);
        Assert.That(first.Status,Is.EqualTo(EcologyAttackStatus.Consumed));
        Assert.That(second.Status,Is.EqualTo(EcologyAttackStatus.ProtectionCancelled));
        Assert.That(playerBinding.ProtectionNow,Is.True);
        Assert.That(events,Is.EqualTo(1));
        Assert.That(Health(playerDamage),Is.EqualTo(before-5).Within(.001f));
    }

    [UnityTest]
    public IEnumerator B1ActualDefeatReturnAndPoolRespawnRejectsBothOldCallbacks()
    {
        Type pool=FindType("ObjectPoolManager");
        bool ownManager=(UnityEngine.Object)pool.GetProperty("Instance").GetValue(null)==null;
        GameObject prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Monsters/Torch/Torch_Blue.prefab");
        monster.SetActive(false);
        monster=(GameObject)pool.GetMethod("Spawn").Invoke(null,new object[]{prefab,new Vector3(201.25f,200),Quaternion.identity});
        Own(monster);
        if(ownManager)Own(((Component)pool.GetProperty("Instance").GetValue(null)).gameObject);
        ((Behaviour)monster.GetComponent("MonsterAIController")).enabled=false;
        monsterDamage=monster.GetComponent("BaseMonster");attack=monster.GetComponent("MonsterAttackController");
        monsterDamage.GetType().BaseType.GetField("rewardList",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(monsterDamage,null);
        monsterBinding=monster.GetComponent<EcologyCombatBinding>()??monster.AddComponent<EcologyCombatBinding>();
        monsterBinding.Bind(barrier,1,2,()=>!(bool)Property(monsterDamage,"IsDefeated"));
        int instance=monster.GetInstanceID();long lease=monsterBinding.Lease;
        EcologyAttackRequest pending=PrepareMonsterAttack(),oldWindup=PrepareMonsterAttack();
        float before=Health(playerDamage);int events=0;
        SubscribeDamage(playerDamage,()=>events++);
        Assert.That((bool)Invoke(attack,"QueueEcologyDamage",pending,player.transform,Vector2.left),Is.True);
        monsterDamage.GetType().GetMethod("TakeDamage",new[]{typeof(float)}).Invoke(monsterDamage,new object[]{float.MaxValue});
        yield return Until(()=>!monster.activeSelf,3f);
        Assert.That(pending.Status,Is.EqualTo(EcologyAttackStatus.IdentityCancelled));
        GameObject reused=(GameObject)pool.GetMethod("Spawn").Invoke(null,new object[]{prefab,new Vector3(201.25f,200),Quaternion.identity});
        Assert.That(reused.GetInstanceID(),Is.EqualTo(instance));
        ((Behaviour)reused.GetComponent("MonsterAIController")).enabled=false;
        monsterBinding.Bind(barrier,1,2,()=>!(bool)Property(monsterDamage,"IsDefeated"));
        Assert.That(monsterBinding.Lease,Is.GreaterThan(lease));
        Assert.That((bool)Invoke(attack,"QueueEcologyDamage",oldWindup,player.transform,Vector2.left),Is.False);
        Assert.That(oldWindup.Status,Is.EqualTo(EcologyAttackStatus.IdentityCancelled));
        EcologyAttackRequest fresh=PrepareMonsterAttack();
        Assert.That((bool)Invoke(attack,"QueueEcologyDamage",fresh,player.transform,Vector2.left),Is.True);
        yield return Until(()=>fresh.Status!=EcologyAttackStatus.Pending,2f);
        Assert.That(fresh.Status,Is.EqualTo(EcologyAttackStatus.Consumed));
        Assert.That(events,Is.EqualTo(1));
        Assert.That(Health(playerDamage),Is.EqualTo(before-5).Within(.001f));
        Assert.That((bool)Invoke(attack,"QueueEcologyDamage",pending,player.transform,Vector2.left),Is.False);
        Assert.That(events,Is.EqualTo(1));
    }

    [UnityTest]
    public IEnumerator NativeMovingKinematicContactMustNotBecomeUnknown()
    {
        barrier.ConfigureProtection(new[] { new Rect(199.3f, 199f, 0.1f, 2f) });
        Component motion = ConfigurePlayerMotion();
        var records = new List<object>();
        ListenMotion(motion, records);
        Rigidbody2D torchBody = monster.GetComponent<Rigidbody2D>();
        var producer = monster.AddComponent<EcologyMotionIntentProducer>();
        int contacts = 0, unknown = 0;
        for (int repetition = 0; repetition < 20; repetition++)
        {
            player.GetComponent<Rigidbody2D>().position = new Vector2(200f, 200f);
            torchBody.position = new Vector2(201.25f, 200f);
            Physics2D.SyncTransforms();
            Invoke(motion, "Configure", player.transform.Find("MovementFeet").GetComponent<Collider2D>(),
                Physics2D.AllLayers, 0.02f);
            yield return new WaitForFixedUpdate();
            int begin = records.Count;
            long step = -1;
            producer.Submission = () =>
            {
                step = barrier.CurrentStep;
                torchBody.MovePosition(new Vector2(200.7f, 200f));
            };
            yield return Until(() => step > 0 && records.Skip(begin).Any(record =>
                (string)Value(record, "Phase") == "PhysicsObserved" &&
                Convert.ToInt64(Value(record, "Step")) == step), 2f);
            object[] stepRows = records.Skip(begin).Where(record =>
                Convert.ToInt64(Value(record, "Step")) == step).ToArray();
            bool touched = stepRows.Any(record => (string)Value(record, "Phase") == "ContactCallbackObserved");
            bool failed = stepRows.Any(record => (string)Value(record, "Phase") == "MotionHistoryUnknown");
            if (touched) contacts++;
            if (failed) unknown++;
            Debug.Log($"P0V_NATIVE_PUSH repetition={repetition} contact={touched} unknown={failed} " +
                      $"player={player.GetComponent<Rigidbody2D>().position} torch={torchBody.position}");
        }
        Debug.Log($"P0V_NATIVE_PUSH_TOTAL repetitions=20 contacts={contacts} unknown={unknown}");
        Assert.That(contacts, Is.EqualTo(20), "The moving native Kinematic body must actually contact the Dynamic player.");
        Assert.That(unknown, Is.Zero, "Native motion contact is not an intentionally unsupported teleport.");
    }

    [UnityTest]
    public IEnumerator MultipleAutomaticPhysicsStepsInOneRenderFrameKeepEveryProtectionCrossing()
    {
        monster.GetComponent<Rigidbody2D>().position = new Vector2(210f, 200f);
        Physics2D.SyncTransforms();
        barrier.ConfigureProtection(new[] { new Rect(200.8f, 199f, 0.35f, 2f) });
        Component motion = ConfigurePlayerMotion();
        var records = new List<object>();
        ListenMotion(motion, records);
        var entries = new List<(int frame, long step)>();
        EventInfo traceEvent = motion.GetType().GetEvent("OnTrace");
        ParameterExpression parameter = Expression.Parameter(traceEvent.EventHandlerType.GetMethod("Invoke").GetParameters()[0].ParameterType);
        Action<object> receive = record =>
        {
            if ((string)Value(record, "Phase") == "ProtectionEntered")
                entries.Add((Time.frameCount, Convert.ToInt64(Value(record, "Step"))));
        };
        traceEvent.AddEventHandler(motion, Expression.Lambda(traceEvent.EventHandlerType,
            Expression.Invoke(Expression.Constant(receive), Expression.Convert(parameter, typeof(object))), parameter).Compile());
        object normal = Enum.Parse(FindType("EcologyMotionKind"), "Normal");
        var producer = player.AddComponent<EcologyMotionIntentProducer>();
        producer.Repeat = true;
        bool right = false;
        producer.Submission = () =>
        {
            right = !right;
            Invoke(motion, "Request", new Vector2(right ? 201.8f : 200f, 200f), normal);
        };
        player.AddComponent<EcologyOneFrameStall>();
        yield return Until(() => entries.GroupBy(row => row.frame).Any(group => group.Select(row => row.step).Distinct().Count() >= 3), 3f);
        producer.Repeat = false;
        producer.Submission = null;
        Debug.Log($"P0V_MULTISTEP entries={entries.Count} maximumPerFrame={entries.GroupBy(row => row.frame).Max(group => group.Count())}");
        Assert.That(records.Count(record => (string)Value(record, "Phase") == "MotionHistoryUnknown"), Is.Zero);
        Assert.That(playerBinding.ProtectionRevision, Is.EqualTo(entries.Count * 2));
    }

    [UnityTest]
    public IEnumerator OriginalWorldPauseStopsPendingButInputGateDoesNot()
    {
        Component session = (Component)FindType("GameSession").GetProperty("Instance").GetValue(null);
        Component coordinator = session.GetComponent("TowerRunCoordinator");
        Assert.That(Property(coordinator, "State").ToString(), Is.EqualTo("StartupChoice"));
        Assert.That((bool)Property(coordinator, "HasStagedOperation"), Is.False);
        // Arrange only this filtered process's bootstrap-owned startup leases, then restore
        // them through its public ReturnToStartupChoice. No global gate clear or fake hub.
        foreach (string name in new[] { "startupGameplayGate", "startupPauseGate" })
        {
            FieldInfo field = coordinator.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            ((IDisposable)field.GetValue(coordinator))?.Dispose();
            field.SetValue(coordinator, null);
        }
        Type hub = FindType("TowerRunGateHub");
        IDisposable pause = null, inputGate = null;
        try
        {
            pause = (IDisposable)hub.GetMethod("AcquireWorldPause").Invoke(null, new object[] { "ecology.p0v.test.pause" });
            inputGate = (IDisposable)hub.GetMethod("AcquireGameplay").Invoke(null, new object[] { "ecology.p0v.test.input" });
            Assert.That(Time.timeScale, Is.Zero);
            Assert.That((bool)hub.GetProperty("IsWorldPaused").GetValue(null), Is.True);
            float before = Health(playerDamage);
            EcologyAttackRequest request = PrepareMonsterAttack();
            Assert.That((bool)Invoke(attack, "QueueEcologyDamage", request, player.transform, Vector2.left), Is.True);
            long step = barrier.CurrentStep;
            yield return new WaitForSecondsRealtime(0.12f);
            Assert.That(request.Status, Is.EqualTo(EcologyAttackStatus.Pending));
            Assert.That(barrier.CurrentStep, Is.EqualTo(step));
            Assert.That(Health(playerDamage), Is.EqualTo(before));
            pause.Dispose(); pause = null;
            Assert.That((bool)hub.GetProperty("IsWorldPaused").GetValue(null), Is.False);
            Assert.That((bool)hub.GetProperty("IsGameplayBlocked").GetValue(null), Is.True);
            yield return Until(() => request.Status == EcologyAttackStatus.Consumed, 2f);
            Assert.That(Health(playerDamage), Is.EqualTo(before - 5f).Within(0.001f));
        }
        finally
        {
            pause?.Dispose(); inputGate?.Dispose();
            Invoke(coordinator, "ReturnToStartupChoice");
            Time.timeScale = 1f;
        }
    }

    private static Type FindType(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(assembly => assembly.GetType(name)).First(type => type != null);
    private static object Value(object source, string name) => source.GetType().GetProperty(name).GetValue(source);
    private static object Property(Component source, string name) => Value(source, name);
    private static float Health(Component source) => Convert.ToSingle(Property(source, "CurrentHealth"));

    private void ListenMotion(Component motion, List<object> records)
    {
        EventInfo traceEvent = motion.GetType().GetEvent("OnTrace");
        ParameterExpression parameter = Expression.Parameter(
            traceEvent.EventHandlerType.GetMethod("Invoke").GetParameters()[0].ParameterType, "trace");
        Action<object> receive = record =>
        {
            records.Add(record);
            Vector2 position = (Vector2)Value(record, "Position");
            Debug.Log($"P0V_MOTION step={Value(record, "Step")} phase={Value(record, "Phase")} " +
                      $"kind={Value(record, "Kind")} x={position.x:R} y={position.y:R} " +
                      $"resident={Value(record, "ResidentId")} lease={Value(record, "Lease")} " +
                      $"source={Value(record, "SourceColliderId")} G={Value(record, "GenerationId")} " +
                      $"contact={Value(record, "ContactColliderId")} normal={Value(record, "ContactNormal")} " +
                      $"separation={Value(record, "ContactSeparation")} frame={Time.frameCount} real={Time.realtimeSinceStartup:R}");
        };
        Delegate handler = Expression.Lambda(traceEvent.EventHandlerType,
            Expression.Call(Expression.Constant(receive), typeof(Action<object>).GetMethod("Invoke"),
                Expression.Convert(parameter, typeof(object))), parameter).Compile();
        traceEvent.AddEventHandler(motion, handler);
    }
    private static void SetField(Component source, string name, object value) => source.GetType()
        .GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(source, value);
    private static object Invoke(Component source, string name, params object[] args) => source.GetType()
        .GetMethod(name, BindingFlags.Public | BindingFlags.Instance).Invoke(source, args);

    private static void SubscribeDamage(Component source, Action callback)
    {
        SubscribeEvent(source, "OnDamaged", callback);
    }

    private static void SubscribeEvent(Component source, string eventName, Action callback)
    {
        EventInfo damage = source.GetType().GetEvent(eventName);
        ParameterExpression[] parameters = damage.EventHandlerType.GetMethod("Invoke").GetParameters()
            .Select(parameter => Expression.Parameter(parameter.ParameterType, parameter.Name)).ToArray();
        Delegate handler = Expression.Lambda(damage.EventHandlerType,
            Expression.Call(Expression.Constant(callback), typeof(Action).GetMethod("Invoke")), parameters).Compile();
        damage.AddEventHandler(source, handler);
    }

    private static IEnumerator Until(Func<bool> condition, float timeout)
    {
        float deadline = Time.realtimeSinceStartup + timeout;
        while (!condition() && Time.realtimeSinceStartup < deadline)
            yield return null;
        Assert.That(condition(), Is.True, "Timed out waiting for the real production damage chain.");
    }
}

public sealed class EcologySceneLifecyclePlayModeTests
{
    private MonoBehaviour coordinator;
    private MapRuntimeBootstrap bootstrap;
    private Keyboard keyboard;
    private bool previousBackground;
    private InputSettings.BackgroundBehavior previousInputBackground;
    private InputSettings.EditorInputBehaviorInPlayMode previousEditorInput;
    private static object Get(object target,string name)=>target.GetType().GetProperty(name).GetValue(target);
    private static object Call(object target,string name,params object[] args)=>target.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).Invoke(target,args);
    [SetUp] public void Isolate()
    {
        TowerRunRuntimeEnvironment.ConfigureTestIsolation(Path.Combine(Path.GetTempPath(),"EcologySceneLifetime",Guid.NewGuid().ToString("N")));
        previousBackground=Application.runInBackground;
        previousInputBackground=InputSystem.settings.backgroundBehavior;
        previousEditorInput=InputSystem.settings.editorInputBehaviorInPlayMode;
        Application.runInBackground=true;
        InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
    }
    private IEnumerator Load()
    {
        yield return SceneManager.LoadSceneAsync("SampleScene",LoadSceneMode.Single);
        yield return null;
        coordinator=UnityEngine.Object.FindObjectsOfType<MonoBehaviour>(true).Single(c=>c.GetType().Name=="TowerRunCoordinator");
        Call(coordinator,"ReturnToStartupChoice");Call(coordinator,"BeginNewRun");yield return null;
        bootstrap=UnityEngine.Object.FindObjectOfType<MapRuntimeBootstrap>(true);
        Assert.That(Get(coordinator,"State").ToString(),Is.EqualTo("Playing"));
        Assert.That(bootstrap.DiagnosticSnapshot.IsReady,Is.True);
    }
    [UnityTearDown] public IEnumerator Cleanup()
    {
        if(keyboard!=null&&keyboard.added){InputSystem.QueueStateEvent(keyboard,new KeyboardState());InputSystem.Update();InputSystem.RemoveDevice(keyboard);}
        if(coordinator!=null)Call(coordinator,"ReturnToStartupChoice");
        Scene old=SceneManager.GetActiveScene(),empty=SceneManager.CreateScene("EcologySceneCleanup-"+Guid.NewGuid().ToString("N"));
        SceneManager.SetActiveScene(empty);
        if(old.IsValid()&&old.isLoaded)yield return SceneManager.UnloadSceneAsync(old);
        TowerRunRuntimeEnvironment.ResetOverrides();
        Application.runInBackground=previousBackground;
        InputSystem.settings.backgroundBehavior=previousInputBackground;
        InputSystem.settings.editorInputBehaviorInPlayMode=previousEditorInput;
        yield return null;
    }
    private (GameObject player,GameObject monster,EcologyDamageBarrier barrier,EcologyAttackRequest pending,EcologyAttackRequest windup,float hp) QueueSceneAttack()
    {
        GameObject player=GameObject.FindGameObjectWithTag("Player");
        foreach(MonoBehaviour ai in UnityEngine.Object.FindObjectsOfType<MonoBehaviour>().Where(m=>m.GetType().Name=="MonsterAIController"))ai.enabled=false;
        GameObject prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Monsters/Torch/Torch_Blue.prefab");
        GameObject source=UnityEngine.Object.Instantiate(prefab,player.transform.position+Vector3.right,Quaternion.identity);
        ((Behaviour)source.GetComponent("MonsterAIController")).enabled=false;
        Physics2D.SyncTransforms();
        var barrier=new GameObject("Ecology-real-scene-scope").AddComponent<EcologyDamageBarrier>();
        var targetBinding=player.GetComponent<EcologyCombatBinding>()??player.AddComponent<EcologyCombatBinding>();
        Component receiver=player.GetComponent("PlayerDamageController"),damage=source.GetComponent("BaseMonster");
        targetBinding.Bind(barrier,1,1,()=>!(bool)Get(receiver,"IsDefeated"));
        var sourceBinding=source.AddComponent<EcologyCombatBinding>();
        sourceBinding.Bind(barrier,1,2,()=>!(bool)Get(damage,"IsDefeated"));
        Guid? generation=bootstrap.Context.ActiveGenerationId;
        MethodInfo lifetime=typeof(EcologyDamageBarrier).GetMethod("BindLifetime");
        if(lifetime!=null)
        {
            EventInfo changed=coordinator.GetType().GetEvent("StateChanged");
            lifetime.Invoke(barrier,new object[]{(Func<bool>)(()=>coordinator!=null&&Get(coordinator,"State").ToString()=="Playing"&&bootstrap.Context.ActiveGenerationId==generation),
                (Action<Action>)(handler=>changed.AddEventHandler(coordinator,handler)),
                (Action<Action>)(handler=>changed.RemoveEventHandler(coordinator,handler))});
        }
        Component attack=source.GetComponent("MonsterAttackController");
        var pending=(EcologyAttackRequest)Call(attack,"PrepareEcologyAttack",player.transform);
        var windup=(EcologyAttackRequest)Call(attack,"PrepareEcologyAttack",player.transform);
        Vector2 aim=(Vector2)Call(attack,"GetDirectionToTarget",player.transform);
        Assert.That((bool)Call(attack,"QueueEcologyDamage",pending,player.transform,aim),Is.True);
        return(player,source,barrier,pending,windup,Convert.ToSingle(Get(receiver,"CurrentHealth")));
    }
    [UnityTest] public IEnumerator ActualStartupExitRetiresPendingEvenWhileWorldPaused()
    {
        yield return Load();
        var arranged=QueueSceneAttack();
        Call(coordinator,"ReturnToStartupChoice");
        Assert.That(Time.timeScale,Is.Zero);
        Assert.That(arranged.pending.Status,Is.EqualTo(EcologyAttackStatus.IdentityCancelled),"Real exit must retire the map-owned queue without waiting for another physics step");
        Assert.That(arranged.barrier.PendingCount,Is.Zero);
        Assert.That(arranged.windup.Queue(()=>true,()=>throw new Exception("Old exit callback dispatched")),Is.False);
        Assert.That(arranged.windup.Status,Is.EqualTo(EcologyAttackStatus.IdentityCancelled));
        Assert.That(Convert.ToSingle(Get(arranged.player.GetComponent("PlayerDamageController"),"CurrentHealth")),Is.EqualTo(arranged.hp));
        Debug.Log("T13_B1_REAL_EXIT startup=StartupChoice paused=True pending=IdentityCancelled count=0 oldCallback=False");
    }
    [UnityTest] public IEnumerator ActualFExitFloorTransitionRetiresOldQueueAndGeneration()
    {
        yield return Load();
        var context=bootstrap.Context;Guid? generation=context.ActiveGenerationId;var registry=context.ActiveRegistry;
        foreach(var entry in registry.Entries.Where(e=>e.Role==MonsterPlacementService.MonsterRole).ToArray())
        {
            Component damage=(Component)entry.Instance;
            damage.GetType().GetMethod("TakeDamage",new[]{typeof(float)}).Invoke(damage,new object[]{float.MaxValue});
        }
        MonoBehaviour floor=UnityEngine.Object.FindObjectsOfType<MonoBehaviour>(true).Single(c=>c.GetType().Name=="TowerFloorRuntimeController");
        Assert.That(Get(floor,"State").ToString(),Is.EqualTo("Available"));
        GameObject player=GameObject.FindGameObjectWithTag("Player");
        Vector3 portal=UnityEngine.Object.FindObjectOfType<MapGenerationController>().TilemapRenderer.Coordinates.CellToWorld(context.ActiveMap.ExitCell);
        player.GetComponent<Rigidbody2D>().position=portal;player.transform.position=portal;Physics2D.SyncTransforms();
        yield return null;
        var arranged=QueueSceneAttack();
        keyboard=InputSystem.AddDevice<Keyboard>("EcologyRealFloorF");
        if(!keyboard.enabled)InputSystem.EnableDevice(keyboard);
        Type inputType=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("GameInput")).First(t=>t!=null);
        object input=inputType.GetProperty("Instance").GetValue(null);int interactions=0;
        Action count=()=>interactions++;
        inputType.GetEvent("OnInteractPressed").AddEventHandler(input,count);
        InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.F));InputSystem.Update();
        Debug.Log($"T13_B1_REAL_F_INPUT key={keyboard.fKey.wasPressedThisFrame} events={interactions} inputGate={Get(input,"IsGameplayInputEnabled")} floor={Get(floor,"State")} coordinator={Get(coordinator,"State")}");
        inputType.GetEvent("OnInteractPressed").RemoveEventHandler(input,count);
        Assert.That(interactions,Is.EqualTo(1),"Actual F input must reach the original GameInput event");
        InputSystem.QueueStateEvent(keyboard,new KeyboardState());InputSystem.Update();
        float deadline=Time.realtimeSinceStartup+5;
        while(context.ActiveGenerationId==generation&&Time.realtimeSinceStartup<deadline)yield return null;
        Assert.That(context.ActiveGenerationId,Is.Not.EqualTo(generation));
        Assert.That(registry.IsRetired,Is.True);
        Assert.That(Get(coordinator,"State").ToString(),Is.EqualTo("Playing"));
        Assert.That(arranged.pending.Status,Is.EqualTo(EcologyAttackStatus.IdentityCancelled));
        Assert.That(arranged.barrier.PendingCount,Is.Zero);
        Assert.That(arranged.windup.Queue(()=>true,()=>throw new Exception("Old floor callback dispatched")),Is.False);
        Assert.That(Convert.ToSingle(Get(arranged.player.GetComponent("PlayerDamageController"),"CurrentHealth")),Is.EqualTo(arranged.hp));
        Debug.Log($"T13_B1_REAL_FLOOR old={generation} next={context.ActiveGenerationId} oldRegistryRetired={registry.IsRetired} pending={arranged.pending.Status}");
    }
    [UnityTest] public IEnumerator ActualPlayerDeathClosesScopeBeforePhysicsAndNewRunRejectsOldAttack()
    {
        yield return Load();var arranged=QueueSceneAttack();
        Component receiver=arranged.player.GetComponent("PlayerDamageController");
        Assert.That((bool)Call(receiver,"TakeDamage",float.MaxValue,arranged.monster.transform),Is.True);
        Assert.That(Get(coordinator,"State").ToString(),Is.EqualTo("DeathChoice"));
        Assert.That(arranged.pending.Status,Is.EqualTo(EcologyAttackStatus.IdentityCancelled));
        Assert.That(arranged.barrier.PendingCount,Is.Zero);
        Assert.That(arranged.windup.Queue(()=>true,()=>throw new Exception("Old death callback dispatched")),Is.False);
        Guid? old=bootstrap.Context.ActiveGenerationId;
        Call(coordinator,"BeginNewRun");yield return null;
        Assert.That(Get(coordinator,"State").ToString(),Is.EqualTo("Playing"));
        Assert.That(bootstrap.Context.ActiveGenerationId,Is.Not.EqualTo(old));
        Assert.That(arranged.pending.Queue(()=>true,()=>throw new Exception("Old run callback dispatched")),Is.False);
        Debug.Log($"T13_B1_REAL_PLAYER_DEATH pausedBeforePhysics=True pending=IdentityCancelled newRun={bootstrap.Context.ActiveGenerationId} old={old}");
    }
    [UnityTest] public IEnumerator ThreeSupportedLegacyProfilesKeepUnadaptedMovementAndOriginalDamage()
    {
        yield return Load();
        foreach(string profile in new[]{"default","ecology-v1","terrain-v2"})
        {
            var field=coordinator.GetType().GetField("currentRecord",BindingFlags.Instance|BindingFlags.NonPublic);
            TowerRunCheckpointRecord record=((TowerRunCheckpointRecord)field.GetValue(coordinator)).DeepClone();
            record.generation.profileId=profile;record.generation.requestedSeed=20260929;record.generation.attemptIndex=0;record.generation.staticBaselineSignature=string.Empty;
            MethodInfo begin=coordinator.GetType().GetMethod("BeginOperation",BindingFlags.Instance|BindingFlags.NonPublic);
            begin.Invoke(coordinator,new[]{Enum.Parse(begin.GetParameters()[0].ParameterType,"NewRun"),record,false});
            yield return null;
            Assert.That(bootstrap.DiagnosticSnapshot.IsReady,Is.True,bootstrap.DiagnosticSnapshot.StateSummary);
            Assert.That(bootstrap.DiagnosticSnapshot.ActiveRequest.ProfileId,Is.EqualTo(profile));
            Assert.That(UnityEngine.Object.FindObjectsOfType<EcologyMotionBody>(),Is.Empty,profile+" must not gain opt-in adapters");
            GameObject player=GameObject.FindGameObjectWithTag("Player");
            foreach(MonoBehaviour ai in UnityEngine.Object.FindObjectsOfType<MonoBehaviour>().Where(m=>m.GetType().Name=="MonsterAIController"))ai.enabled=false;
            MapData map=bootstrap.Context.ActiveMap;var coordinates=UnityEngine.Object.FindObjectOfType<MapGenerationController>().TilemapRenderer.Coordinates;
            Vector2Int cell=coordinates.WorldToCell(player.GetComponent<Rigidbody2D>().position);
            var choices=new[]{(Vector2Int.up,Key.W),(Vector2Int.right,Key.D),(Vector2Int.down,Key.S),(Vector2Int.left,Key.A)};
            var choice=choices.First(c=>map.CanTraverse(cell,cell+c.Item1));
            keyboard=InputSystem.AddDevice<Keyboard>("EcologyLegacyMovement");Vector2 before=player.GetComponent<Rigidbody2D>().position;
            if(!keyboard.enabled)InputSystem.EnableDevice(keyboard);
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(choice.Item2));
            InputSystem.Update();
            yield return new WaitForSeconds(.25f);
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;
            float moved=Vector2.Distance(before,player.GetComponent<Rigidbody2D>().position);
            Assert.That(moved,Is.GreaterThan(.05f),profile+" original real input did not move");
            InputSystem.RemoveDevice(keyboard);keyboard=null;
            GameObject source=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Monsters/Torch/Torch_Blue.prefab"),player.transform.position+Vector3.right,Quaternion.identity);
            ((Behaviour)source.GetComponent("MonsterAIController")).enabled=false;Physics2D.SyncTransforms();
            Component attack=source.GetComponent("MonsterAttackController"),receiver=player.GetComponent("PlayerDamageController");
            float health=Convert.ToSingle(Get(receiver,"CurrentHealth"));
            Vector2 aim=(Vector2)Call(attack,"GetDirectionToTarget",player.transform);
            Assert.That((bool)Call(attack,"TryDealDamage",player.transform,aim),Is.True);
            Assert.That(Convert.ToSingle(Get(receiver,"CurrentHealth")),Is.EqualTo(health-5).Within(.001f));
            Assert.That(player.GetComponent<EcologyCombatBinding>(),Is.Null);
            UnityEngine.Object.Destroy(source);yield return null;
            Debug.Log($"T13_B1_LEGACY profile={profile} ready=True move={moved:R} originalDamage=5 optIn=False");
        }
    }
}

[DefaultExecutionOrder(100)]
public sealed class EcologyMotionIntentProducer : MonoBehaviour
{
    public Action Submission;
    public bool Repeat;

    private void FixedUpdate()
    {
        Action current = Submission;
        if (!Repeat)
            Submission = null;
        current?.Invoke();
    }
}

public sealed class EcologyOneFrameStall : MonoBehaviour
{
    private void Update()
    {
        // One bounded real long frame lets Unity's ordinary loop catch up in several fixed steps.
        var watch = System.Diagnostics.Stopwatch.StartNew();
        while (watch.Elapsed.TotalSeconds < 0.12d) System.Threading.Thread.SpinWait(100);
        enabled = false;
    }
}
#endif

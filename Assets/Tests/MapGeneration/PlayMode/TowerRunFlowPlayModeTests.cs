using System.Collections;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

public sealed class TowerRunFlowPlayModeTests
{
    private string testRoot;
    private Keyboard testKeyboard;

    [SetUp]
    public void SetUp()
    {
        testRoot = Path.Combine(
            Path.GetTempPath(),
            "RPG_Demo-TowerRunFlowTests",
            System.Guid.NewGuid().ToString("N"));
        TowerRunRuntimeEnvironment.ConfigureTestIsolation(testRoot);
        testKeyboard = InputSystem.AddDevice<Keyboard>("TowerRunFlowTestKeyboard");
    }

    [TearDown]
    public void TearDown()
    {
        if (testKeyboard != null && testKeyboard.added)
            InputSystem.RemoveDevice(testKeyboard);

        TowerRunRuntimeEnvironment.ResetOverrides();

        if (Directory.Exists(testRoot))
            Directory.Delete(testRoot, true);
    }

    [UnityTest]
    public IEnumerator TutorialToFloorTwoRequiresTargetsAndActualInteractInput()
    {
        yield return SceneManager.LoadSceneAsync("SampleScene", LoadSceneMode.Single);
        yield return null;

        MonoBehaviour coordinator = FindBehaviour("TowerRunCoordinator");
        MonoBehaviour floor = FindBehaviour("TowerFloorRuntimeController");
        Assert.That(coordinator, Is.Not.Null);
        Assert.That(floor, Is.Not.Null);
        AssertIsolatedSlotRoot(coordinator);
        Invoke(coordinator, "ReturnToStartupChoice");
        Invoke(coordinator, "BeginNewRun");
        yield return null;

        GameObject runtimeRoot = FindRootObject(SceneManager.GetActiveScene(), "RandomMapRuntime");
        MapRuntimeBootstrap bootstrap = runtimeRoot.GetComponent<MapRuntimeBootstrap>();
        MapGenerationController controller =
            runtimeRoot.GetComponentInChildren<MapGenerationController>(true);
        MapMinimapController minimap =
            runtimeRoot.GetComponentInChildren<MapMinimapController>(true);

        AssertPlaying(coordinator, "新手关卡");
        Assert.That(GetProperty(floor, "State").ToString(), Is.EqualTo("Locked"));
        Assert.That((int)GetProperty(floor, "TargetCount"), Is.GreaterThan(0));
        Assert.That(minimap.IsExitAvailable, Is.False);
        Assert.That(GetPortal(floor).activeSelf, Is.False);

        DefeatFirstRole(bootstrap.Context, DestructiblePlacementService.DestructibleRole);
        yield return null;
        Assert.That(GetProperty(floor, "State").ToString(), Is.EqualTo("Locked"),
            "A non-target Destructible must not complete a combat floor.");
        Assert.That(minimap.IsExitAvailable, Is.False);

        GameObject tutorialTarget = DefeatAllRoleTargets(
            bootstrap.Context,
            MonsterPlacementService.MonsterRole);
        yield return null;
        Assert.That(GetProperty(floor, "State").ToString(), Is.EqualTo("Available"));
        Assert.That(minimap.IsExitAvailable, Is.True);
        GameObject tutorialPortal = GetPortal(floor);
        Assert.That(tutorialPortal.activeInHierarchy, Is.True);

        System.Guid tutorialGeneration = bootstrap.Context.ActiveGenerationId.Value;
        controller.Player.position = tutorialPortal.transform.position;
        MonoBehaviour runtimeInventory = FindBehaviour("PlayerInventory");
        Assert.That(runtimeInventory, Is.Not.Null);
        SetPrivateField(coordinator, "inventory", null);
        LogAssert.Expect(LogType.Error, new Regex("换层请求未被接受"));
        PressInteract();
        yield return null;
        AssertPlaying(coordinator, "新手关卡");
        Assert.That(GetProperty(floor, "State").ToString(), Is.EqualTo("Available"));
        Assert.That(tutorialPortal.activeInHierarchy, Is.True,
            "An exit request must remain unaccepted when carry capture cannot complete.");
        SetPrivateField(coordinator, "inventory", runtimeInventory);

        PressInteract();
        yield return null;

        AssertPlaying(coordinator, "第 1 层");
        Assert.That(bootstrap.Context.ActiveGenerationId, Is.Not.EqualTo(tutorialGeneration));
        Assert.That(GetProperty(floor, "State").ToString(), Is.EqualTo("Locked"));
        Assert.That(minimap.IsExitAvailable, Is.False);
        Assert.That(minimap.TrySetExitAvailable(tutorialGeneration, true), Is.False,
            "A stale generation callback must not unlock the current minimap exit.");

        PressInteract();
        yield return null;
        AssertPlaying(coordinator, "第 1 层");

        yield return new WaitForSecondsRealtime(0.65f);
        Assert.That(tutorialTarget.activeInHierarchy, Is.True,
            "The previous life pool-return coroutine must not retire the reused target.");
        Assert.That(GetBoolProperty(tutorialTarget, "BaseDamageable", "IsDefeated"), Is.False);

        DefeatAllRoleTargets(bootstrap.Context, MonsterPlacementService.MonsterRole);
        yield return null;
        GameObject firstFloorPortal = GetPortal(floor);
        Assert.That(GetProperty(floor, "State").ToString(), Is.EqualTo("Available"));
        Assert.That(minimap.IsExitAvailable, Is.True);
        controller.Player.position = firstFloorPortal.transform.position;
        PressInteract();
        yield return null;

        AssertPlaying(coordinator, "第 2 层");
        Assert.That(GetProperty(floor, "State").ToString(), Is.EqualTo("Locked"));
        Assert.That(minimap.IsExitAvailable, Is.False);
    }

    [UnityTest]
    public IEnumerator StartupAndTwoDeathRestoresUseFormalSlotAndReleaseModalOwners()
    {
        yield return SceneManager.LoadSceneAsync("SampleScene", LoadSceneMode.Single);
        yield return null;

        MonoBehaviour coordinator = FindBehaviour("TowerRunCoordinator");
        MonoBehaviour runUi = FindBehaviour("TowerRunUiController");
        MonoBehaviour gameInput = FindBehaviour("GameInput");
        Assert.That(coordinator, Is.Not.Null);
        Assert.That(runUi, Is.Not.Null);
        Assert.That(gameInput, Is.Not.Null);
        AssertIsolatedSlotRoot(coordinator);
        Invoke(coordinator, "ReturnToStartupChoice");
        Invoke(coordinator, "BeginNewRun");
        yield return null;
        AssertPlaying(coordinator, "新手关卡");

        MonoBehaviour package = FindBehaviour("PackagePanelController");
        Assert.That(package, Is.Not.Null);
        PressTab();
        yield return null;
        Assert.That((bool)GetProperty(package, "IsPackagePanelVisible"), Is.True);
        yield return PressEscape(runUi);
        Assert.That((bool)GetProperty(package, "IsPackagePanelVisible"), Is.False,
            "Esc must be consumed by the topmost package panel before settings can open.");
        Assert.That(GetPrivateField<GameObject>(runUi, "settingsOverlay").activeSelf, Is.False);

        yield return PressEscape(runUi);
        Assert.That(GetPrivateField<GameObject>(runUi, "settingsOverlay").activeSelf, Is.True,
            "Esc must open the shared settings window while gameplay is active.");
        PressTab();
        yield return null;
        Assert.That((bool)GetProperty(package, "IsPackagePanelVisible"), Is.False,
            "Tab must not pass through the settings gameplay gate.");
        yield return PressEscape(runUi);
        Assert.That(GetPrivateField<GameObject>(runUi, "settingsOverlay").activeSelf, Is.False);

        GameObject settingsButtonObject = FindSceneGameObject("SettingButton");
        Assert.That(settingsButtonObject, Is.Not.Null);
        Button settingsButton = settingsButtonObject.GetComponent<Button>();
        Assert.That(settingsButton, Is.Not.Null);
        settingsButton.onClick.Invoke();
        Assert.That(GetPrivateField<GameObject>(runUi, "settingsOverlay").activeSelf, Is.True,
            "The existing HUD settings button must open the same settings overlay.");
        Slider volumeSlider = GetPrivateField<Slider>(runUi, "volumeSlider");
        Toggle muteToggle = GetPrivateField<Toggle>(runUi, "muteToggle");
        volumeSlider.value = 0.37f;
        InvokePrivate(runUi, "PlayPreviewSound");
        AudioSource previewSource = GetPrivateField<AudioSource>(runUi, "previewSource");
        Assert.That(previewSource.isPlaying, Is.True);
        InvokePrivate(runUi, "PlayPreviewSound");
        Assert.That(runUi.GetComponents<AudioSource>(), Has.Length.EqualTo(1),
            "Repeated preview must reuse one AudioSource instead of stacking playback owners.");
        Assert.That(previewSource.isPlaying, Is.True);
        muteToggle.isOn = true;
        Invoke(runUi, "CloseSettings");

        Invoke(runUi, "OpenSettings");
        InvokePrivate(runUi, "ShowSavePage");
        InvokePrivate(runUi, "BeginSaveRequest");
        TowerRunCheckpointRecord requestedCandidate =
            GetPrivateField<TowerRunCheckpointRecord>(runUi, "requestedSaveCandidate");
        Assert.That(requestedCandidate, Is.Not.Null);
        Assert.That(requestedCandidate.savedAtUtc, Is.Empty,
            "A pending candidate must not impersonate a completed disk save.");
        InvokePrivate(runUi, "SelectSaveSlot", 1);
        TowerRunSlotReadResult saved =
            (TowerRunSlotReadResult)Invoke(coordinator, "ReadSlot", 1);
        Assert.That(saved.state, Is.EqualTo(TowerRunSlotState.Valid), saved.message);
        Invoke(runUi, "CloseSettings");

        yield return RecreateApplicationRuntime();
        coordinator = FindBehaviour("TowerRunCoordinator");
        runUi = FindBehaviour("TowerRunUiController");
        gameInput = FindBehaviour("GameInput");
        Assert.That(coordinator, Is.Not.Null);
        Assert.That(runUi, Is.Not.Null);
        Assert.That(gameInput, Is.Not.Null);
        AssertIsolatedSlotRoot(coordinator);
        Assert.That(GetProperty(coordinator, "State").ToString(), Is.EqualTo("StartupChoice"));
        Assert.That((float)GetPrivateField<Slider>(runUi, "volumeSlider").value,
            Is.EqualTo(0.37f).Within(0.001f));
        Assert.That(GetPrivateField<Toggle>(runUi, "muteToggle").isOn, Is.True,
            "Audio preferences must survive an equivalent independent runtime rebuild.");
        saved = (TowerRunSlotReadResult)Invoke(coordinator, "ReadSlot", 1);
        Assert.That(saved.state, Is.EqualTo(TowerRunSlotState.Valid), saved.message);
        Invoke(coordinator, "BeginRestoreSlot", 1);
        yield return null;
        AssertPlaying(coordinator, "新手关卡");

        Invoke(runUi, "OpenSettings");
        InvokePrivate(runUi, "ShowSavePage");
        InvokePrivate(runUi, "BeginSaveRequest");
        InvokePrivate(runUi, "SelectSaveSlot", 1);
        Assert.That(GetPrivateFieldValue<int>(runUi, "pendingOverwriteSlot"), Is.EqualTo(1));
        yield return PressEscape(runUi);
        Assert.That(GetPrivateField<GameObject>(runUi, "settingsOverlay").activeSelf, Is.True,
            "Esc during overwrite confirmation must return to slot selection, not close settings.");
        Assert.That(GetPrivateFieldValue<int>(runUi, "pendingOverwriteSlot"), Is.Zero);
        Invoke(runUi, "CloseSettings");

        GameObject runtimeRoot = FindRootObject(SceneManager.GetActiveScene(), "RandomMapRuntime");
        MapRuntimeBootstrap bootstrap = runtimeRoot.GetComponent<MapRuntimeBootstrap>();
        MapGenerationController controller =
            runtimeRoot.GetComponentInChildren<MapGenerationController>(true);
        GameObject pendingAttackTarget = FindFirstRoleTarget(
            bootstrap.Context,
            MonsterPlacementService.MonsterRole);
        controller.Player.position = pendingAttackTarget.transform.position;
        float targetHealthBeforeDeath =
            (float)GetComponentProperty(pendingAttackTarget, "BaseDamageable", "CurrentHealth");
        MonoBehaviour playerAction = FindBehaviour("PlayerAction");
        Assert.That(playerAction, Is.Not.Null);
        InvokePrivate(playerAction, "HandleBattlePressed");

        Invoke(runUi, "OpenSettings");
        GetPrivateField<Toggle>(runUi, "muteToggle").isOn = false;
        InvokePrivate(runUi, "PlayPreviewSound");
        Assert.That(Time.timeScale, Is.Zero);
        Assert.That((bool)GetProperty(gameInput, "IsGameplayInputEnabled"), Is.False);
        MapObjectRegistry firstDeathRegistry = bootstrap.Context.ActiveRegistry;
        DefeatPlayer();
        yield return null;
        Assert.That(GetProperty(coordinator, "State").ToString(), Is.EqualTo("DeathChoice"));
        Assert.That(GetPrivateField<GameObject>(runUi, "settingsOverlay").activeSelf, Is.False);
        Assert.That(GetPrivateField<AudioSource>(runUi, "previewSource").clip, Is.Null);
        Assert.That(Time.timeScale, Is.Zero,
            "Closing settings must release only its owner; the death pause owner remains.");
        yield return new WaitForSecondsRealtime(0.35f);
        Assert.That(
            (float)GetComponentProperty(pendingAttackTarget, "BaseDamageable", "CurrentHealth"),
            Is.EqualTo(targetHealthBeforeDeath),
            "A pending attack from the defeated life must be cancelled before its hit frame.");

        Invoke(coordinator, "BeginRestoreSlot", 1);
        yield return null;
        AssertPlaying(coordinator, "新手关卡");
        Assert.That(firstDeathRegistry.IsRetired, Is.True);
        AssertSingleRuntimeOwners();
        Assert.That(Time.timeScale, Is.EqualTo(1f));
        Assert.That((bool)GetProperty(gameInput, "IsGameplayInputEnabled"), Is.True);

        MapObjectRegistry secondDeathRegistry = bootstrap.Context.ActiveRegistry;
        DefeatPlayer();
        yield return null;
        Assert.That(GetProperty(coordinator, "State").ToString(), Is.EqualTo("DeathChoice"));
        Invoke(coordinator, "BeginRestoreSlot", 1);
        yield return null;
        AssertPlaying(coordinator, "新手关卡");
        Assert.That(secondDeathRegistry.IsRetired, Is.True);
        AssertSingleRuntimeOwners();
        Assert.That(Time.timeScale, Is.EqualTo(1f));
    }

    [UnityTest]
    public IEnumerator FinalizerExceptionEntersRetryableFailureAndRetryKeepsFrozenCarry()
    {
        yield return SceneManager.LoadSceneAsync("SampleScene", LoadSceneMode.Single);
        yield return null;

        MonoBehaviour coordinator = FindBehaviour("TowerRunCoordinator");
        MonoBehaviour floor = FindBehaviour("TowerFloorRuntimeController");
        AssertIsolatedSlotRoot(coordinator);
        Invoke(coordinator, "ReturnToStartupChoice");
        Invoke(coordinator, "BeginNewRun");
        yield return null;
        AssertPlaying(coordinator, "新手关卡");

        MonoBehaviour playerDamage = FindBehaviour("PlayerDamageController");
        DamagePlayer(playerDamage, 7f);
        float frozenHealth = (float)GetProperty(playerDamage, "CurrentHealth");
        MonoBehaviour inventory = FindBehaviour("PlayerInventory");
        MonoBehaviour shop = FindBehaviour("ShopTradeController");
        Assert.That(inventory, Is.Not.Null);
        Assert.That(shop, Is.Not.Null);
        object goldReward = GetProperty(inventory, "GoldReward");
        Assert.That(goldReward, Is.Not.Null);
        Assert.That((bool)Invoke(inventory, "TryAddItem", goldReward, 10000L), Is.True);
        object finiteProduct = FindFiniteStockProduct(shop);
        int stockBeforePurchase = (int)Invoke(shop, "GetCurrentStock", finiteProduct);
        object purchase = Invoke(shop, "TryPurchase", finiteProduct);
        Assert.That(GetPublicFieldValue<bool>(purchase, "succeeded"), Is.True);
        int frozenStock = (int)Invoke(shop, "GetCurrentStock", finiteProduct);
        Assert.That(frozenStock, Is.EqualTo(stockBeforePurchase - 1));
        GameObject runtimeRoot = FindRootObject(SceneManager.GetActiveScene(), "RandomMapRuntime");
        MapRuntimeBootstrap bootstrap = runtimeRoot.GetComponent<MapRuntimeBootstrap>();
        MapGenerationController controller =
            runtimeRoot.GetComponentInChildren<MapGenerationController>(true);
        ThrowOnceTowerRunReadyFinalizer throwOnce =
            runtimeRoot.AddComponent<ThrowOnceTowerRunReadyFinalizer>();

        DefeatAllRoleTargets(bootstrap.Context, MonsterPlacementService.MonsterRole);
        yield return null;
        GameObject portal = GetPortal(floor);
        controller.Player.position = portal.transform.position;
        LogAssert.Expect(
            LogType.Exception,
            new Regex("Injected ready-finalizer exception"));
        PressInteract();
        yield return null;

        Assert.That(GetProperty(coordinator, "State").ToString(), Is.EqualTo("Failure"));
        Assert.That((bool)GetProperty(coordinator, "CanRetryFailedOperation"), Is.True);
        Assert.That(Time.timeScale, Is.Zero);
        TowerRunCheckpointRecord frozenCarry =
            GetPrivateField<TowerRunCheckpointRecord>(coordinator, "stagedRecord").DeepClone();
        Assert.That(frozenCarry.player.shops, Has.Some.Matches<TowerRunShopRecord>(shopRecord =>
            shopRecord.catalogId == (string)GetProperty(shop, "CatalogId") &&
            shopRecord.productStocks.Exists(stock =>
                stock.productId == (string)GetObjectProperty(finiteProduct, "ProductId") &&
                stock.quantity == frozenStock)));

        Assert.That((bool)Invoke(inventory, "TryAddItem", goldReward, 37L), Is.True);
        Invoke(shop, "ResetRuntimeStocks");
        Invoke(coordinator, "RetryFailedOperation");
        yield return null;

        AssertPlaying(coordinator, "第 1 层");
        Assert.That((float)GetProperty(playerDamage, "CurrentHealth"), Is.EqualTo(frozenHealth));
        object[] saveRequestArguments = { null, null };
        Assert.That((bool)Invoke(coordinator, "TryCreateSaveRequest", saveRequestArguments), Is.True);
        TowerRunCheckpointRecord restoredCandidate =
            (TowerRunCheckpointRecord)saveRequestArguments[0];
        Assert.That(
            JsonUtility.ToJson(restoredCandidate.player),
            Is.EqualTo(JsonUtility.ToJson(frozenCarry.player)),
            "Retry must restore the complete frozen inventory, gold and shop state without duplication.");
        Assert.That(throwOnce.InvocationCount, Is.EqualTo(2));
        Assert.That(Time.timeScale, Is.EqualTo(1f));
    }

    private void PressInteract()
    {
        InputSystem.QueueStateEvent(testKeyboard, new KeyboardState(Key.F));
        InputSystem.Update();
        InputSystem.QueueStateEvent(testKeyboard, new KeyboardState());
        InputSystem.Update();
    }

    private void PressTab()
    {
        InputSystem.QueueStateEvent(testKeyboard, new KeyboardState(Key.Tab));
        InputSystem.Update();
        InputSystem.QueueStateEvent(testKeyboard, new KeyboardState());
        InputSystem.Update();
    }

    private IEnumerator PressEscape(MonoBehaviour runUi)
    {
        InputSystem.QueueStateEvent(testKeyboard, new KeyboardState(Key.Escape));
        InputSystem.Update();
        InvokePrivate(runUi, "Update");
        InputSystem.QueueStateEvent(testKeyboard, new KeyboardState());
        InputSystem.Update();
        yield return null;
    }

    private IEnumerator RecreateApplicationRuntime()
    {
        MonoBehaviour session = FindBehaviour("GameSession");
        Assert.That(session, Is.Not.Null);
        System.Type sessionType = session.GetType();
        Object.Destroy(session.gameObject);
        yield return null;

        GameObject recreatedSession = new GameObject("GameSession-TestRecreated");
        recreatedSession.AddComponent(sessionType);
        yield return SceneManager.LoadSceneAsync("SampleScene", LoadSceneMode.Single);
        yield return null;
    }

    private void AssertIsolatedSlotRoot(MonoBehaviour coordinator)
    {
        Assert.That(coordinator, Is.Not.Null);
        string actualRoot = GetPrivateField<string>(coordinator, "slotStoreRoot");
        string expectedRoot = Path.Combine(testRoot, "tower-run-slots");
        Assert.That(actualRoot, Is.EqualTo(expectedRoot),
            "The coordinator must rebuild its slot store after test isolation is configured.");
    }

    private static void AssertSingleRuntimeOwners()
    {
        Assert.That(CountSceneBehaviours("PlayerAction"), Is.EqualTo(1));
        Assert.That(CountSceneBehaviours("PlayerDamageController"), Is.EqualTo(1));
        Assert.That(CountSceneBehaviours("TowerFloorRuntimeController"), Is.EqualTo(1));

        int portalCount = 0;
        GameObject[] objects = Resources.FindObjectsOfTypeAll<GameObject>();

        for (int i = 0; i < objects.Length; i++)
        {
            if (objects[i] != null && objects[i].scene.IsValid() &&
                objects[i].name == "TowerFloorExitPortal")
            {
                portalCount++;
            }
        }

        Assert.That(portalCount, Is.EqualTo(1));
    }

    private static int CountSceneBehaviours(string typeName)
    {
        int count = 0;
        MonoBehaviour[] behaviours = Resources.FindObjectsOfTypeAll<MonoBehaviour>();

        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] != null && behaviours[i].gameObject.scene.IsValid() &&
                behaviours[i].GetType().Name == typeName)
            {
                count++;
            }
        }

        return count;
    }

    private static void DefeatPlayer()
    {
        MonoBehaviour damage = FindBehaviour("PlayerDamageController");
        Assert.That(damage, Is.Not.Null);
        DamagePlayer(damage, float.MaxValue);
    }

    private static void DamagePlayer(MonoBehaviour damage, float amount)
    {
        MethodInfo takeDamage = damage.GetType().GetMethod(
            "TakeDamage",
            new[] { typeof(float), typeof(Transform) });
        Assert.That(takeDamage, Is.Not.Null);
        takeDamage.Invoke(damage, new object[] { amount, null });
    }

    private static GameObject DefeatAllRoleTargets(MapRuntimeContext context, string role)
    {
        GameObject first = null;
        int defeated = 0;

        foreach (MapObjectRegistryEntry entry in context.ActiveRegistry.Entries)
        {
            if (entry.Role != role)
                continue;

            GameObject target = ResolveGameObject(entry.Instance);
            Assert.That(target, Is.Not.Null);
            first ??= target;
            Defeat(target);
            defeated++;
        }

        Assert.That(defeated, Is.GreaterThan(0));
        return first;
    }

    private static GameObject FindFirstRoleTarget(MapRuntimeContext context, string role)
    {
        foreach (MapObjectRegistryEntry entry in context.ActiveRegistry.Entries)
        {
            if (entry.Role != role)
                continue;

            GameObject target = ResolveGameObject(entry.Instance);
            Assert.That(target, Is.Not.Null);
            return target;
        }

        Assert.Fail($"No active registry entry was found for role {role}.");
        return null;
    }

    private static void DefeatFirstRole(MapRuntimeContext context, string role)
    {
        foreach (MapObjectRegistryEntry entry in context.ActiveRegistry.Entries)
        {
            if (entry.Role != role)
                continue;

            GameObject target = ResolveGameObject(entry.Instance);
            Assert.That(target, Is.Not.Null);
            Defeat(target);
            return;
        }

        Assert.Fail($"No active registry entry was found for role {role}.");
    }

    private static void Defeat(GameObject target)
    {
        Component damageable = target.GetComponent("BaseDamageable");
        Assert.That(damageable, Is.Not.Null);
        PropertyInfo maxHealth = damageable.GetType().GetProperty("MaxHealth");
        MethodInfo takeDamage = damageable.GetType().GetMethod("TakeDamage", new[] { typeof(float) });
        Assert.That(maxHealth, Is.Not.Null);
        Assert.That(takeDamage, Is.Not.Null);
        takeDamage.Invoke(damageable, new[] { maxHealth.GetValue(damageable) });
    }

    private static bool GetBoolProperty(GameObject target, string componentName, string propertyName)
    {
        Component component = target.GetComponent(componentName);
        PropertyInfo property = component?.GetType().GetProperty(propertyName);
        Assert.That(property, Is.Not.Null);
        return (bool)property.GetValue(component);
    }

    private static object GetComponentProperty(
        GameObject target,
        string componentName,
        string propertyName)
    {
        Component component = target.GetComponent(componentName);
        Assert.That(component, Is.Not.Null);
        return GetObjectProperty(component, propertyName);
    }

    private static object FindFiniteStockProduct(MonoBehaviour shop)
    {
        FieldInfo catalogField = shop.GetType().GetField(
            "shopCatalog",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(catalogField, Is.Not.Null);
        object catalog = catalogField.GetValue(shop);
        Assert.That(catalog, Is.Not.Null);
        System.Collections.IEnumerable categories =
            GetObjectProperty(catalog, "Categories") as System.Collections.IEnumerable;
        Assert.That(categories, Is.Not.Null);

        foreach (object category in categories)
        {
            System.Collections.IEnumerable products =
                GetObjectProperty(category, "Products") as System.Collections.IEnumerable;

            if (products == null)
                continue;

            foreach (object product in products)
            {
                if (product != null &&
                    !(bool)GetObjectProperty(product, "InfiniteStock") &&
                    (int)GetObjectProperty(product, "InitialStock") > 0)
                {
                    return product;
                }
            }
        }

        Assert.Fail("The formal scene must expose at least one finite-stock product.");
        return null;
    }

    private static object GetObjectProperty(object source, string propertyName)
    {
        Assert.That(source, Is.Not.Null);
        PropertyInfo property = source.GetType().GetProperty(propertyName);
        Assert.That(property, Is.Not.Null);
        return property.GetValue(source);
    }

    private static T GetPublicFieldValue<T>(object source, string fieldName)
    {
        Assert.That(source, Is.Not.Null);
        FieldInfo field = source.GetType().GetField(fieldName);
        Assert.That(field, Is.Not.Null);
        return (T)field.GetValue(source);
    }

    private static GameObject ResolveGameObject(Object instance)
    {
        if (instance is GameObject gameObject)
            return gameObject;

        return instance is Component component ? component.gameObject : null;
    }

    private static GameObject GetPortal(MonoBehaviour floor)
    {
        FieldInfo field = floor.GetType().GetField(
            "exitPortalInstance",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null);
        return (GameObject)field.GetValue(floor);
    }

    private static void AssertPlaying(MonoBehaviour coordinator, string floorDisplayName)
    {
        Assert.That(GetProperty(coordinator, "State").ToString(), Is.EqualTo("Playing"));
        Assert.That((string)GetProperty(coordinator, "FloorDisplayName"), Is.EqualTo(floorDisplayName));
    }

    private static object GetProperty(MonoBehaviour behaviour, string propertyName)
    {
        PropertyInfo property = behaviour.GetType().GetProperty(propertyName);
        Assert.That(property, Is.Not.Null);
        return property.GetValue(behaviour);
    }

    private static object Invoke(MonoBehaviour behaviour, string methodName, params object[] arguments)
    {
        MethodInfo method = behaviour.GetType().GetMethod(methodName);
        Assert.That(method, Is.Not.Null);
        return method.Invoke(behaviour, arguments);
    }

    private static object InvokePrivate(
        MonoBehaviour behaviour,
        string methodName,
        params object[] arguments)
    {
        MethodInfo method = behaviour.GetType().GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);
        return method.Invoke(behaviour, arguments);
    }

    private static T GetPrivateField<T>(MonoBehaviour behaviour, string fieldName)
        where T : class
    {
        FieldInfo field = behaviour.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null);
        return field.GetValue(behaviour) as T;
    }

    private static T GetPrivateFieldValue<T>(MonoBehaviour behaviour, string fieldName)
        where T : struct
    {
        FieldInfo field = behaviour.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null);
        return (T)field.GetValue(behaviour);
    }

    private static void SetPrivateField(MonoBehaviour behaviour, string fieldName, object value)
    {
        FieldInfo field = behaviour.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null);
        field.SetValue(behaviour, value);
    }

    private static MonoBehaviour FindBehaviour(string typeName)
    {
        MonoBehaviour[] behaviours = Resources.FindObjectsOfTypeAll<MonoBehaviour>();

        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] != null && behaviours[i].gameObject.scene.IsValid() &&
                behaviours[i].GetType().Name == typeName)
                return behaviours[i];
        }

        return null;
    }

    private static GameObject FindSceneGameObject(string name)
    {
        GameObject[] objects = Resources.FindObjectsOfTypeAll<GameObject>();

        for (int i = 0; i < objects.Length; i++)
        {
            if (objects[i] != null && objects[i].scene.IsValid() && objects[i].name == name)
                return objects[i];
        }

        return null;
    }

    private static GameObject FindRootObject(Scene scene, string name)
    {
        GameObject[] roots = scene.GetRootGameObjects();

        for (int i = 0; i < roots.Length; i++)
        {
            if (roots[i].name == name)
                return roots[i];
        }

        return null;
    }
}

public sealed class ThrowOnceTowerRunReadyFinalizer : MonoBehaviour, IMapRuntimeReadyFinalizer
{
    public int InvocationCount { get; private set; }

    public bool TryFinalizeBeforeReady(
        MapRuntimeContext context,
        System.Guid generationId,
        out string reason)
    {
        InvocationCount++;

        if (InvocationCount == 1)
            throw new System.InvalidOperationException("Injected ready-finalizer exception.");

        reason = string.Empty;
        return true;
    }
}

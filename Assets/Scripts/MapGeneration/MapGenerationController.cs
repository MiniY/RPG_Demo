using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public class MapGenerationController : MonoBehaviour
{
    [SerializeField] private MapGenerationSettings settings;
    [SerializeField] private MapTilemapRenderer tilemapRenderer;
    [FormerlySerializedAs("decorationRenderer")]
    [SerializeField] private MapSimpleDecorationRenderer simpleDecorationRenderer;
    [SerializeField] private Transform player;
    [SerializeField] private bool generateOnPlay;
    [SerializeField] private bool logGenerationSummary = true;

    private MapData lastGeneratedMap;
    private MapSimpleDecorationData lastGeneratedSimpleDecorations;
    private MapIntegrationOrchestrator orchestrator;
    private MapGenerationRequest requestedGeneration;
    private IEcologyMapSession ecologySession;
    private EcologyRecipeDefinition zoningRecipe;
    private EcologyRecipeDefinition[] configuredRetainedRecipes;
    private EcologyEcotypeDefinition[] zoningTypes;
    private EcologyPlanningAttemptResult zoningAccepted;
    private EcologyMapRuntime zoningRuntime;
    public EcologyMapRuntime EcologyRuntime => zoningRuntime;
    public void BindEcologySession(IEcologyMapSession session) => ecologySession = session;

    public event Action<MapData> MapGenerated;
    public event Action MapCleared;

    public MapGenerationSettings Settings => zoningRecipe != null ? zoningRecipe.Settings : settings;
    public MapTilemapRenderer TilemapRenderer => tilemapRenderer;
    public MapSimpleDecorationRenderer SimpleDecorationRenderer => simpleDecorationRenderer;
    public Transform Player => player;
    public MapData LastGeneratedMap => lastGeneratedMap;
    public MapSimpleDecorationData LastGeneratedSimpleDecorations => lastGeneratedSimpleDecorations;
    public MapIntegrationOrchestrator Orchestrator
    {
        get { EnsureOrchestrator(); return orchestrator; }
    }

    public void GenerateMap()
    {
        MapRuntimeBootstrap bootstrap = GetComponentInParent<MapRuntimeBootstrap>();
        if (bootstrap != null && !bootstrap.CanRunRandomGeneration)
        {
            Debug.LogWarning($"Random generation is unavailable in {bootstrap.Mode} mode or after bootstrap validation failure.", this);
            return;
        }
        if (settings == null)
        {
            Debug.LogError("MapGenerationController is missing MapGenerationSettings.", this);
            return;
        }
        if (tilemapRenderer == null)
        {
            Debug.LogError("MapGenerationController is missing MapTilemapRenderer.", this);
            return;
        }

        EnsureOrchestrator();
        MapGenerationRequest request = requestedGeneration ??
            new MapGenerationRequest(Guid.NewGuid(), settings.seed);
        if (EcologyRecipeDefinition.IsRetiredProfile(request.ProfileId))
        { FailPreGenerationConfiguration("RetiredPurchasedMonsterContent", EcologyRecipeDefinition.RetiredContentMessage); return; }
        if (EcologyRecipeDefinition.IsArchiveOnlyProfile(request.ProfileId))
        { FailPreGenerationConfiguration("ArchivedEcologyContent", EcologyRecipeDefinition.ArchiveOnlyMessage(request.ProfileId)); return; }
        zoningRuntime?.Dispose(); zoningRuntime = null; zoningAccepted = null;
        string zoningResource = EcologyRecipeDefinition.ResourcePathFor(request.ProfileId);
        bool zoning = zoningResource != null;
        zoningRecipe = null;
        zoningTypes = null;
        if (zoning)
        {
            if (ecologySession == null || ecologySession.PlayerMovementFoot == null)
            { FailPreGenerationConfiguration("ZoningInputsMissing", "Retained zoning inputs, canonical Player foot and original session are required."); return; }
            try
            {
                zoningRecipe = ResolveRetainedEcologyRecipe(request.ProfileId, request.EcologyEntry);
                if (zoningRecipe == null)
                { FailPreGenerationConfiguration("ExactRetainedEcologyRecipeUnavailable", "The exact requested content is absent from the finite retained catalog."); return; }
                zoningTypes = zoningRecipe.ResolveTypes();
                if (request.EcologyEntry != null) zoningRecipe.VerifyRetainedSourceFiles(true);
                if (request.EcologyEntry != null && (request.EcologyEntry.RecipeVersion != zoningRecipe.RecipeVersion ||
                    request.EcologyEntry.ConfigDigest != zoningRecipe.ConfigurationDigest || request.EcologyEntry.ContentVersion != zoningRecipe.ContentVersion))
                    throw new InvalidOperationException("ExactEcologyRecipeUnavailable");
            }
            catch (Exception error)
            { FailPreGenerationConfiguration("RetainedEcologyContentMismatch", error.Message); return; }
            if (!string.Equals(zoningRecipe.DeclaredProfileId, request.ProfileId, StringComparison.Ordinal))
            { FailPreGenerationConfiguration("ZoningProfileMismatch", "The explicitly selected retained recipe has a different content profile."); return; }
        }
        if (!zoning && request.EcologyEntry != null)
        { FailPreGenerationConfiguration("UnknownRetainedEcologyProfile", "A versioned ecology entry cannot fall through to a legacy profile."); return; }
        bool terrainV2 = string.Equals(request.ProfileId, "terrain-v2", StringComparison.Ordinal) ||
            string.Equals(request.ProfileId, MapEcologyProfile.XiaoSuProfileId, StringComparison.Ordinal) || zoning;
        bool ecologyProfile = string.Equals(
            request.ProfileId, MapEcologyProfile.ProfileId, StringComparison.Ordinal) || terrainV2;
        MerchantPlacementService merchantPlacementService = null;
        MonsterPlacementService monsterPlacementService = null;
        DestructiblePlacementService destructiblePlacementService = null;
        MapEcologyPopulationService ecologyPopulationService = null;
        AnimalPlacementService animalPlacementService = null;
        PlantPlacementService plantPlacementService = null;
        if (bootstrap != null)
        {
            MerchantPlacementService[] merchantPlacementServices =
                GetComponentsInParent<MerchantPlacementService>(true);
            if (merchantPlacementServices.Length != 1)
            {
                FailPreGenerationConfiguration(
                    "MerchantPlacementOwnerCount",
                    $"RandomGenerated requires exactly one Required Merchant placement service; " +
                    $"found {merchantPlacementServices.Length}.");
                return;
            }
            merchantPlacementService = merchantPlacementServices[0];

            MonsterPlacementService[] monsterPlacementServices =
                GetComponentsInParent<MonsterPlacementService>(true);
            if (monsterPlacementServices.Length != 1)
            {
                FailPreGenerationConfiguration(
                    "MonsterPlacementOwnerCount",
                    $"RandomGenerated requires exactly one Required Monster placement service; " +
                    $"found {monsterPlacementServices.Length}.");
                return;
            }
            monsterPlacementService = monsterPlacementServices[0];

            DestructiblePlacementService[] destructiblePlacementServices =
                GetComponentsInParent<DestructiblePlacementService>(true);
            if (destructiblePlacementServices.Length != 1)
            {
                FailPreGenerationConfiguration(
                    "DestructiblePlacementOwnerCount",
                    $"RandomGenerated requires exactly one Destructible population service; " +
                    $"found {destructiblePlacementServices.Length}.");
                return;
            }
            destructiblePlacementService = destructiblePlacementServices[0];

            MapEcologyPopulationService[] ecologyServices =
                GetComponentsInParent<MapEcologyPopulationService>(true);
            if (ecologyProfile && ecologyServices.Length != 1)
            {
                FailPreGenerationConfiguration(
                    "EcologyPopulationOwnerCount",
                    $"Ecology profile requires exactly one population service; " +
                    $"found {ecologyServices.Length}.");
                return;
            }
            if (ecologyServices.Length > 1)
            {
                FailPreGenerationConfiguration("EcologyPopulationOwnerCount",
                    "Multiple ecology population services are not allowed.");
                return;
            }
            ecologyPopulationService = ecologyServices.Length == 1 ? ecologyServices[0] : null;

            AnimalPlacementService[] animalPlacementServices =
                GetComponentsInParent<AnimalPlacementService>(true);
            if (animalPlacementServices.Length != 1)
            {
                FailPreGenerationConfiguration(
                    "AnimalPlacementOwnerCount",
                    $"RandomGenerated requires exactly one Animal semantic placement service; " +
                    $"found {animalPlacementServices.Length}.");
                return;
            }
            animalPlacementService = animalPlacementServices[0];

            PlantPlacementService[] plantPlacementServices =
                GetComponentsInParent<PlantPlacementService>(true);
            if (plantPlacementServices.Length != 1)
            {
                FailPreGenerationConfiguration(
                    "PlantPlacementOwnerCount",
                    $"RandomGenerated requires exactly one Plant semantic placement service; " +
                    $"found {plantPlacementServices.Length}.");
                return;
            }
            plantPlacementService = plantPlacementServices[0];
        }
        MapGenerationExecutionResult result = orchestrator.Execute(
            request,
            attempt => GeneratePending(attempt, terrainV2),
            (attempt, pending) => BuildPlacementPlan(
                attempt,
                pending,
                merchantPlacementService,
                monsterPlacementService,
                destructiblePlacementService,
                ecologyPopulationService,
                animalPlacementService,
                plantPlacementService,
                ecologyProfile, request.ProfileId),
            ProjectCommitted);

        if (!result.Succeeded)
        {
            Debug.LogError($"Map generation failed [{result.Failure.Code}]: {result.Failure.Reason}", this);
            return;
        }

        if (zoning)
        {
            zoningRuntime = new EcologyMapRuntime(orchestrator.Context, zoningAccepted, zoningTypes, ecologySession, transform);
            zoningRuntime.ConfigurePopulation(ecologyPopulationService);
        }

        PlayerSpawnService[] spawnServices = GetComponentsInParent<PlayerSpawnService>(true);
        CanonicalPlayerProvider[] playerProviders = GetComponentsInParent<CanonicalPlayerProvider>(true);
        if (spawnServices.Length != 1)
        {
            FailPlayerSpawnConfiguration(result.GenerationId.Value, "PlayerSpawnOwnerCount",
                $"RandomGenerated requires exactly one PlayerSpawnService; found {spawnServices.Length}.");
            return;
        }
        if (playerProviders.Length != 1)
        {
            FailPlayerSpawnConfiguration(result.GenerationId.Value, "CanonicalPlayerProviderCount",
                $"RandomGenerated requires exactly one CanonicalPlayerProvider; found {playerProviders.Length}.");
            return;
        }

        PlayerSpawnService spawnService = spawnServices[0];
        CanonicalPlayerProvider playerProvider = playerProviders[0];
        if (merchantPlacementService != null &&
            !merchantPlacementService.TryMaterialize(
                orchestrator.Context,
                result.GenerationId.Value,
                tilemapRenderer.Coordinates,
                playerProvider,
                player,
                out string merchantFailure))
        {
            Debug.LogError($"Required Merchant materialization failed: {merchantFailure}", this);
            return;
        }
        if (ecologyProfile)
        {
            destructiblePlacementService?.RetireForEcologyProfile();
            GameObject legacyMonster = monsterPlacementService?.ReinitializationTarget;
            if (legacyMonster != null)
                legacyMonster.SetActive(false);

            if (plantPlacementService != null &&
                !plantPlacementService.TryMaterialize(
                    orchestrator.Context, result.GenerationId.Value,
                    tilemapRenderer.Coordinates, true, out string ecologyPlantFailure))
            {
                Debug.LogError($"Ecology Plant materialization failed: {ecologyPlantFailure}", this);
                return;
            }
            string ecologyFailure = "Ecology population service is missing.";
            if (ecologyPopulationService == null ||
                !ecologyPopulationService.TryMaterialize(
                    orchestrator.Context, result.GenerationId.Value,
                    tilemapRenderer.Coordinates, out ecologyFailure))
            {
                Debug.LogError($"Ecology materialization failed: {ecologyFailure}", this);
                return;
            }
        }
        else
        {
            ecologyPopulationService?.RetireForLegacyProfile();
            if (monsterPlacementService != null &&
                !monsterPlacementService.TryMaterialize(
                    orchestrator.Context, result.GenerationId.Value,
                    tilemapRenderer.Coordinates, out string monsterFailure))
            {
                Debug.LogError($"Required Monster materialization failed: {monsterFailure}", this);
                return;
            }
            if (destructiblePlacementService != null &&
                !destructiblePlacementService.TryMaterialize(
                    orchestrator.Context, result.GenerationId.Value,
                    tilemapRenderer.Coordinates, out string destructibleFailure))
            {
                Debug.LogError($"Destructible materialization failed: {destructibleFailure}", this);
                return;
            }
        }
        if (animalPlacementService != null &&
            !animalPlacementService.TryMaterialize(
                orchestrator.Context, result.GenerationId.Value,
                tilemapRenderer.Coordinates, out string animalFailure))
        {
            Debug.LogError($"Animal materialization failed: {animalFailure}", this);
            return;
        }
        if (!ecologyProfile && plantPlacementService != null &&
            !plantPlacementService.TryMaterialize(
                orchestrator.Context, result.GenerationId.Value,
                tilemapRenderer.Coordinates, out string plantFailure))
        {
            Debug.LogError($"Plant materialization failed: {plantFailure}", this);
            return;
        }
        bool projectionReady = tilemapRenderer.TerrainCollisionTilemap != null &&
                               tilemapRenderer.TerrainCollisionTilemap.GetUsedTilesCount() > 0;
        string spawnFailure = null;
        bool spawned = spawnService.TrySpawn(
                orchestrator.Context,
                result.GenerationId.Value,
                tilemapRenderer.Coordinates,
                playerProvider,
                player,
                projectionReady,
                out spawnFailure);
        if (!spawned)
        {
            Debug.LogError($"Player spawn failed: {spawnFailure}", this);
            return;
        }

        // The standalone MapGeneration test scene intentionally stops at the Stage 3 seam.
        if (bootstrap == null)
            return;

        MapDependentReinitializationService[] reinitializationServices =
            GetComponentsInParent<MapDependentReinitializationService>(true);
        if (reinitializationServices.Length != 1)
        {
            FailStageConfiguration(result.GenerationId.Value, MapLifecyclePhase.Reinitializing,
                "ReinitializationOwnerCount",
                $"RandomGenerated requires exactly one reinitialization service; " +
                $"found {reinitializationServices.Length}.");
            return;
        }

        MapDependentReinitializationService reinitializationService = reinitializationServices[0];
        IReadOnlyList<GameObject> reinitializationTargets = BuildReinitializationTargets(
            monsterPlacementService,
            ecologyPopulationService,
            animalPlacementService,
            plantPlacementService,
            ecologyProfile);
        if (!reinitializationService.TryReinitialize(
                orchestrator.Context,
                result.GenerationId.Value,
                reinitializationTargets,
                out string reinitializationFailure))
        {
            Debug.LogError($"Map-dependent reinitialization failed: {reinitializationFailure}", this);
            return;
        }

        CameraBindingService[] cameraBindingServices =
            GetComponentsInParent<CameraBindingService>(true);
        PlayerFollowCameraProvider[] cameraProviders =
            GetComponentsInParent<PlayerFollowCameraProvider>(true);
        if (cameraBindingServices.Length != 1 || cameraProviders.Length != 1)
        {
            FailStageConfiguration(result.GenerationId.Value, MapLifecyclePhase.BindingCamera,
                "CameraIntegrationOwnerCount",
                $"RandomGenerated requires exactly one CameraBindingService and Player-follow Camera provider; " +
                $"found {cameraBindingServices.Length} and {cameraProviders.Length}.");
            return;
        }

        if (!cameraBindingServices[0].TryBind(
                orchestrator.Context,
                result.GenerationId.Value,
                tilemapRenderer.Coordinates,
                playerProvider,
                player,
                spawnService,
                reinitializationService,
                cameraProviders[0],
                out string cameraFailure))
        {
            Debug.LogError($"Camera binding failed: {cameraFailure}", this);
            return;
        }

        MapRuntimeContext context = orchestrator.Context;
        MapLifecycleTransitions.Advance(context, MapLifecyclePhase.ValidatingRuntime);
        if (!bootstrap.TryValidateCurrentAuthority(
                MapLifecyclePhase.ValidatingRuntime,
                out string authorityFailure))
        {
            Debug.LogError($"Runtime authority validation failed: {authorityFailure}", this);
            return;
        }

        MonoBehaviour[] behaviours = GetComponentsInParent<MonoBehaviour>(true);

        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] == null || !behaviours[i].isActiveAndEnabled ||
                !(behaviours[i] is IMapRuntimeReadyFinalizer finalizer))
                continue;

            bool finalized;
            string finalizerFailure;

            try
            {
                finalized = finalizer.TryFinalizeBeforeReady(
                    context,
                    result.GenerationId.Value,
                    out finalizerFailure);
            }
            catch (Exception exception)
            {
                finalizerFailure =
                    $"{finalizer.GetType().Name} threw during runtime-ready finalization: {exception.Message}";
                context.RecordFailure(new MapFailureDiagnostic(
                    context.Mode,
                    result.GenerationId.Value,
                    context.Phase,
                    MapFailureCategory.Lifecycle,
                    "RuntimeReadyFinalizerException",
                    finalizerFailure));
                Debug.LogException(exception, behaviours[i]);
                return;
            }

            if (finalized)
                continue;

            context.RecordFailure(new MapFailureDiagnostic(
                context.Mode,
                result.GenerationId.Value,
                context.Phase,
                MapFailureCategory.Lifecycle,
                "RuntimeReadyFinalizationFailed",
                string.IsNullOrWhiteSpace(finalizerFailure)
                    ? "A runtime ready finalizer rejected the generation."
                    : finalizerFailure));
            Debug.LogError($"Runtime ready finalization failed: {finalizerFailure}", this);
            return;
        }

        // The restore finalizer applies the complete checkpoint before the player acquires a live ecology motion lease.
        zoningRuntime?.FinalizeBeforeReady();

        MapLifecycleTransitions.Advance(context, MapLifecyclePhase.Ready);
        if (!orchestrator.PublishGameplayReady(result.GenerationId.Value))
        {
            context.RecordFailure(new MapFailureDiagnostic(
                context.Mode,
                result.GenerationId.Value,
                context.Phase,
                MapFailureCategory.Lifecycle,
                "GameplayReadyPublicationRejected",
                "GameplayReady could not be published for the Current Generation."));
            Debug.LogError("GameplayReady publication was rejected for the Current Generation.", this);
        }
    }

    public void ClearMap()
    {
        if (tilemapRenderer != null) tilemapRenderer.Clear();
        if (simpleDecorationRenderer != null) simpleDecorationRenderer.Clear();
        lastGeneratedMap = null;
        lastGeneratedSimpleDecorations = null;
        MapCleared?.Invoke();
    }

    // Keep the previous projection intact until a new pending generation commits.
    public void RegenerateMap() => GenerateMap();

    /// <summary>Runs one explicit generation request without mutating the settings asset.</summary>
    public bool TryGenerateMap(MapGenerationRequest request)
    {
        if (request == null)
            return false;

        requestedGeneration = request;

        try
        {
            GenerateMap();
            return orchestrator != null && orchestrator.Context.IsReady;
        }
        catch (Exception exception)
        {
            EnsureOrchestrator();
            MapRuntimeContext context = orchestrator.Context;
            context.RecordFailure(new MapFailureDiagnostic(
                context.Mode,
                context.ActiveGenerationId,
                context.Phase,
                MapFailureCategory.Lifecycle,
                "UnhandledGenerationException",
                $"Unhandled generation exception: {exception.Message}"));
            Debug.LogException(exception, this);
            return false;
        }
        finally
        {
            requestedGeneration = null;
        }
    }

    public void RandomizeSeed()
    {
        if (settings == null)
        {
            Debug.LogError("MapGenerationController is missing MapGenerationSettings.", this);
            return;
        }
        settings.seed = Guid.NewGuid().GetHashCode();
    }

    private void Start()
    {
        if (generateOnPlay) GenerateMap();
    }

    private void EnsureOrchestrator()
    {
        if (orchestrator != null) return;
        MapRuntimeBootstrap bootstrap = GetComponentInParent<MapRuntimeBootstrap>();
        MapRuntimeContext context;
        if (bootstrap != null)
        {
            context = bootstrap.Context;
        }
        else
        {
            context = new MapRuntimeContext(MapRuntimeMode.RandomGenerated);
            context.SetPhase(MapLifecyclePhase.Initialized);
        }
        orchestrator = new MapIntegrationOrchestrator(context);
    }

    private MapPendingGeneration GeneratePending(MapGenerationAttempt attempt, bool terrainV2)
    {
        MapGenerationSettings attemptSettings = Instantiate(Settings);
        attemptSettings.seed = attempt.AttemptSeed;
        try
        {
            EcologyRetainedTerrainGenerator retained = zoningRecipe != null ? zoningRecipe.TerrainGenerator : null;
            MapData map;
            if (attemptSettings is MapSurfaceV4Settings && terrainV2)
            {
                // Same fixed seed and original finite candidate order. Qualify
                // imported native faces against the actual complete player foot.
                if (retained == null) throw new InvalidOperationException("R4 requires its retained numerical strategy.");
                map = retained.GenerateWithElevationQualification(attemptSettings, true,
                    candidate => MapSurfaceV4Policy.HasNativeStairAccess(candidate, attemptSettings,
                        tilemapRenderer, simpleDecorationRenderer, ecologySession.PlayerMovementFoot));
            }
            else map = retained != null ? retained.Generate(attemptSettings, terrainV2) : RandomMapGenerator.Generate(attemptSettings, terrainV2);
            MapSimpleDecorationData decorations = retained != null ? retained.GenerateDecorations(map, attemptSettings) : MapSimpleDecorationGenerator.Generate(map, attemptSettings);
            if (attemptSettings.remediationProjectedSurfaces)
                decorations = MapSurfaceV4Policy.FilterDecorations(map, decorations, attemptSettings, tilemapRenderer.TerrainV2Palette);
            return new MapPendingGeneration(attempt.GenerationId, map, decorations);
        }
        finally
        {
            if (Application.isPlaying) Destroy(attemptSettings);
            else DestroyImmediate(attemptSettings);
        }
    }

    private MapPlacementPlan BuildPlacementPlan(
        MapGenerationAttempt attempt,
        MapPendingGeneration pending,
        MerchantPlacementService merchantPlacementService,
        MonsterPlacementService monsterPlacementService,
        DestructiblePlacementService destructiblePlacementService,
        MapEcologyPopulationService ecologyPopulationService,
        AnimalPlacementService animalPlacementService,
        PlantPlacementService plantPlacementService,
        bool ecologyProfile, string profileId)
    {
        if (EcologyRecipeDefinition.ResourcePathFor(profileId) != null)
        {
            zoningAccepted = EcologyJointPlacementPlanner.Build(attempt, pending.Map, pending.Decorations, Settings,
                tilemapRenderer, simpleDecorationRenderer, ecologySession.PlayerMovementFoot, merchantPlacementService,
                plantPlacementService, ecologyPopulationService, animalPlacementService,
                zoningRecipe.ResolveTerritoryRecipe((ecologySession as IEcologyFloorContext)?.EcologyFloorNumber ?? 0),
                zoningTypes, ecologySession.ExitInteractionRadius, requestedGeneration?.EcologyEntry?.EcologyAttempt, zoningRecipe.Population);
            if (!zoningAccepted.Succeeded)
                throw new MapPlanningException("EcologyBudgetExhaustedOnFixedTerrain", string.Join(";", zoningAccepted.Failures), false);
            EcologyEntryIdentity expected = requestedGeneration?.EcologyEntry;
            if (expected != null)
            {
                EcologyEntryIdentity actual = CaptureEcologyEntryIdentity();
                if (actual.RecipeVersion != expected.RecipeVersion || actual.ConfigDigest != expected.ConfigDigest || actual.ContentVersion != expected.ContentVersion ||
                    actual.FootprintVersion != expected.FootprintVersion || actual.ContextIdentity != expected.ContextIdentity || actual.LayoutDigest != expected.LayoutDigest ||
                    actual.EcologyAttempt != expected.EcologyAttempt || actual.RegionCount != expected.RegionCount || actual.ResidentCount != expected.ResidentCount || actual.MerchantCount != expected.MerchantCount)
                    throw new MapPlanningException("EcologyEntryReconstructionMismatch", "Exact recorded attempt, content, footprint, context, digest or count manifest differs.", false);
            }
            return zoningAccepted.Plan;
        }
        MapPlacementPlan plan = MapStaticPlacementPlanner.Build(
            attempt.GenerationId,
            pending.Map,
            pending.Decorations,
            settings);
        merchantPlacementService?.Plan(attempt, pending.Map, plan);
        if (ecologyProfile)
        {
            plantPlacementService?.Plan(attempt, pending.Map, plan, true);
            if (ecologyPopulationService == null)
                throw new MapPlanningException("EcologyServiceMissing",
                    "Ecology population service is required for ecology-v1.", false);
            ecologyPopulationService.Plan(attempt, pending.Map, plan, profileId);
            animalPlacementService?.Plan(attempt, pending.Map, plan);
        }
        else
        {
            // The default profile preserves the exact TRB2 placement order.
            monsterPlacementService?.Plan(attempt, pending.Map, plan);
            destructiblePlacementService?.Plan(attempt, pending.Map, plan);
            animalPlacementService?.Plan(attempt, pending.Map, plan);
            plantPlacementService?.Plan(attempt, pending.Map, plan);
        }
        return plan;
    }

    public EcologyEntryIdentity CaptureEcologyEntryIdentity()
    {
        if (zoningAccepted == null || !zoningAccepted.Succeeded || zoningRecipe == null)
            throw new InvalidOperationException("No accepted retained ecology entry exists.");
        MapEcologyLayout layout = zoningAccepted.Plan.Ecology;
        int merchants = 0;
        foreach (var reservation in zoningAccepted.Plan.Reservations)
            if (reservation.Role == MerchantPlacementService.MerchantRole) merchants++;
        return new EcologyEntryIdentity(zoningRecipe.RecipeVersion, zoningRecipe.ConfigurationDigest, zoningRecipe.ContentVersion,
            layout.FootprintVersion, layout.GeometryIdentity, layout.Signature, layout.AcceptedAttempt,
            layout.Regions.Count, layout.Residents.Count, merchants);
    }

    /// <summary>Explicit finite direct references; configure only while the owning session is gated.</summary>
    public void ConfigureRetainedEcologyRecipes(IReadOnlyList<EcologyRecipeDefinition> recipes)
    {
        if (recipes == null) throw new ArgumentNullException(nameof(recipes));
        if (ecologySession != null && (ecologySession.IsEcologyPlaying || ecologySession.IsEcologyPreparing))
            throw new InvalidOperationException("Cannot change retained content during an active or preparing map.");
        var copy = new EcologyRecipeDefinition[recipes.Count]; var unique = new HashSet<EcologyRecipeDefinition>();
        for (int i = 0; i < copy.Length; i++)
        {
            if (recipes[i] == null || !unique.Add(recipes[i])) throw new ArgumentException("Retained catalog contains a missing or duplicate reference.");
            copy[i] = recipes[i];
        }
        configuredRetainedRecipes = copy;
    }

    private EcologyRecipeDefinition ResolveRetainedEcologyRecipe(string profile, EcologyEntryIdentity expected)
    {
        if (configuredRetainedRecipes == null)
            return Resources.Load<EcologyRecipeDefinition>(EcologyRecipeDefinition.ResourcePathFor(profile));
        EcologyRecipeDefinition found = null;
        foreach (var candidate in configuredRetainedRecipes)
        {
            if (candidate.DeclaredProfileId != profile) continue;
            if (expected == null) return candidate; // First explicitly registered current version for this profile.
            if (candidate.RecipeVersion != expected.RecipeVersion || candidate.ConfigurationDigest != expected.ConfigDigest) continue;
            candidate.ResolveTypes();
            if (candidate.ContentVersion != expected.ContentVersion) continue;
            if (found != null) throw new InvalidOperationException("AmbiguousRetainedEcologyVersion");
            found = candidate;
        }
        return found; // An explicit catalog never falls back to current Resources/default.
    }

    private static IReadOnlyList<GameObject> BuildReinitializationTargets(
        MonsterPlacementService monsterPlacementService,
        MapEcologyPopulationService ecologyPopulationService,
        AnimalPlacementService animalPlacementService,
        PlantPlacementService plantPlacementService,
        bool ecologyProfile)
    {
        List<GameObject> targets = new List<GameObject>();
        if (ecologyProfile && ecologyPopulationService != null)
        {
            foreach (GameObject target in ecologyPopulationService.MonsterReinitializationTargets)
                targets.Add(target);
        }
        else if (monsterPlacementService != null)
            targets.Add(monsterPlacementService.ReinitializationTarget);
        if (animalPlacementService != null)
        {
            foreach (GameObject target in animalPlacementService.ReinitializationTargets)
                targets.Add(target);
        }
        if (plantPlacementService != null)
        {
            foreach (GameObject target in plantPlacementService.ReinitializationTargets)
                targets.Add(target);
        }
        return targets.AsReadOnly();
    }

    private void ProjectCommitted(MapPendingGeneration pending)
    {
        lastGeneratedMap = pending.Map;
        lastGeneratedSimpleDecorations = pending.Decorations;
        tilemapRenderer.Render(lastGeneratedMap, Settings);
        if (simpleDecorationRenderer != null)
            simpleDecorationRenderer.Render(lastGeneratedSimpleDecorations, lastGeneratedMap, Settings);

        if (Settings is MapSurfaceV4Settings)
        {
            var visual = tilemapRenderer.GroundTilemap.transform.parent.GetComponentInChildren<MapSurfaceV4Visuals>();
            if (visual != null) visual.AddDecorationShadows(lastGeneratedMap, lastGeneratedSimpleDecorations);
        }
        MapGenerated?.Invoke(lastGeneratedMap);

        if (logGenerationSummary)
            Debug.Log($"Map generation committed: GenerationId={orchestrator.Context.ActiveGenerationId}; " +
                $"RequestedSeed={settings.seed}; Size={settings.mapWidth}x{settings.mapHeight}; " +
                $"Spawn={lastGeneratedMap.SpawnCell}; Exit={lastGeneratedMap.ExitCell}; " +
                $"Decorations={lastGeneratedSimpleDecorations?.Count ?? 0}.", this);
    }

    private void OnDrawGizmosSelected()
    {
        if (lastGeneratedMap == null || tilemapRenderer == null) return;
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(tilemapRenderer.Coordinates.CellToWorld(lastGeneratedMap, lastGeneratedMap.SpawnCell), 0.35f);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(tilemapRenderer.Coordinates.CellToWorld(lastGeneratedMap, lastGeneratedMap.ExitCell), 0.35f);
    }

    private void OnDisable() { zoningRuntime?.Dispose(); zoningRuntime = null; }

    private void FailPlayerSpawnConfiguration(Guid generationId, string code, string reason)
    {
        MapRuntimeContext context = orchestrator.Context;
        if (context.Phase == MapLifecyclePhase.Materializing)
            MapLifecycleTransitions.Advance(context, MapLifecyclePhase.SpawningPlayer);
        context.RecordFailure(new MapFailureDiagnostic(context.Mode, generationId, context.Phase,
            MapFailureCategory.Configuration, code, reason));
        Debug.LogError($"Player spawn failed [{code}]: {reason}", this);
    }

    private void FailPreGenerationConfiguration(string code, string reason)
    {
        MapRuntimeContext context = orchestrator.Context;
        context.RecordFailure(new MapFailureDiagnostic(
            context.Mode,
            null,
            context.Phase,
            MapFailureCategory.Configuration,
            code,
            reason));
        Debug.LogError($"Map integration failed [{code}]: {reason}", this);
    }

    private void FailStageConfiguration(
        Guid generationId,
        MapLifecyclePhase failurePhase,
        string code,
        string reason)
    {
        MapRuntimeContext context = orchestrator.Context;
        if (failurePhase == MapLifecyclePhase.Reinitializing &&
            context.Phase == MapLifecyclePhase.SpawningPlayer)
        {
            MapLifecycleTransitions.Advance(context, MapLifecyclePhase.Reinitializing);
        }
        else if (failurePhase == MapLifecyclePhase.BindingCamera)
        {
            if (context.Phase == MapLifecyclePhase.SpawningPlayer)
                MapLifecycleTransitions.Advance(context, MapLifecyclePhase.Reinitializing);
            if (context.Phase == MapLifecyclePhase.Reinitializing)
                MapLifecycleTransitions.Advance(context, MapLifecyclePhase.BindingCamera);
        }

        context.RecordFailure(new MapFailureDiagnostic(context.Mode, generationId, context.Phase,
            MapFailureCategory.Configuration, code, reason));
        Debug.LogError($"Map integration failed [{code}]: {reason}", this);
    }
}

using System;
using System.Linq;
using UnityEngine;

/// <summary>Explicit retained candidate inputs. No runtime GUID lookup or mutable default fallback.</summary>
public sealed class EcologyRecipeDefinition : ScriptableObject
{
    public const string ProfileId = "ecology-zoning-v1";
    public const string UnitsCandidateProfileId = "ecology-zoning-units-candidate-v1";
    public const string UnitsStableCandidateProfileId = "ecology-zoning-units-float32-candidate-v2";
    public const string SurfaceV4ProfileId = "ecology-zoning-units-surface-v4";
    public const string SurfaceV4FloorPolicyVersion = "units-surface-v4-floor-budget-v1";
    public const string MainV1ProfileId = "ecology-zoning-units-surface-v4-main-v1";
    public const string MainV1FloorPolicyVersion = "units-surface-v4-main-v1-floor-budget-v1";
    public const string LegacyR3ArchiveOnlyMessage = "这份进度属于保留的旧 R3 版本，不能在新版 Main R4 中直接恢复。原始存档没有修改，请用私有归档里的原 R3 独立程序继续；新版不会自动迁移这份旧进度。";
    public const string LegacyR4ArchiveOnlyMessage = "这份进度属于保留的原 R4 候选，不能在新版 Main 中直接恢复。原始存档没有修改，请使用原 R4 独立程序继续；新版不会自动迁移旧内容身份。";
    public static bool IsArchiveOnlyProfile(string profileId) =>
        profileId == RemediationProfileId || profileId == SurfaceV4ProfileId;
    public static string ArchiveOnlyMessage(string profileId) =>
        profileId == SurfaceV4ProfileId ? LegacyR4ArchiveOnlyMessage : LegacyR3ArchiveOnlyMessage;
    public static bool IsSurfaceProfile(string id) => id == SurfaceV4ProfileId || id == MainV1ProfileId;
    public static bool IsRemediationProfile(string id) => id == RemediationProfileId || IsSurfaceProfile(id);
    public const string RemediationProfileId = "ecology-zoning-units-remediation-v3";
    public const string RemediationFloorPolicyVersion = "units-remediation-v3-floor-budget-v1";
    [Serializable] private sealed class Ecotype
    {
        public string typeId;
        public GameObject prefab;
        public MapPopulationTerrainMask terrains;
        public int weight;
        public bool isBoss;
    }
    [SerializeField] private string configurationDigest;
    [SerializeField] private string candidateProfileId;
    [SerializeField] private bool bossContentPending;
    [SerializeField] private MapGenerationSettings retainedSettings;
    [SerializeField] private MapEcologyProfile retainedPopulation;
    [SerializeField] private Ecotype[] ecotypes;
    [SerializeField] private TextAsset retainedContentManifest;
    [SerializeField] private EcologyRetainedTerrainGenerator retainedTerrain;
    private EcologyRetainedContentManifest resolvedContent;
    public string ConfigurationDigest => configurationDigest;
    public MapGenerationSettings Settings => retainedSettings;
    public MapEcologyProfile Population => retainedPopulation;
    public bool BossContentPending => bossContentPending;
    public EcologyRetainedTerrainGenerator TerrainGenerator => retainedTerrain;
    public string ContentVersion => resolvedContent != null ? resolvedContent.Identity : throw new InvalidOperationException("Resolve the retained recipe before capturing its content identity.");
    public string RecipeVersion => DeclaredProfileId == MainV1ProfileId ? MainV1FloorPolicyVersion : DeclaredProfileId == SurfaceV4ProfileId ? SurfaceV4FloorPolicyVersion : DeclaredProfileId == RemediationProfileId ? RemediationFloorPolicyVersion :
        new EcologyTerritoryRecipe(bossContentPending: bossContentPending).Identity;
    public EcologyTerritoryRecipe ResolveTerritoryRecipe(int floorNumber)
    {
        if (floorNumber < 0) throw new ArgumentOutOfRangeException(nameof(floorNumber));
        if (!IsRemediationProfile(DeclaredProfileId)) return new EcologyTerritoryRecipe(bossContentPending: bossContentPending);
        return new EcologyTerritoryRecipe(minimumGroup: 3, totalBudget: 24 + 4 * Math.Min(floorNumber, 6),
            maximumOrdinaryRegions: 4 + Math.Min(floorNumber, 6) / 3,
            maximumPerRegion: 8 + Math.Min(floorNumber, 4), bossContentPending: true,
            rejectUndersizedSeedComponents: IsSurfaceProfile(DeclaredProfileId));
    }
    public static bool IsRetiredProfile(string profileId) =>
        string.Equals(profileId, MapEcologyProfile.XiaoSuProfileId, StringComparison.Ordinal) ||
        string.Equals(profileId, ProfileId, StringComparison.Ordinal) ||
        string.Equals(profileId, UnitsCandidateProfileId, StringComparison.Ordinal) ||
        string.Equals(profileId, UnitsStableCandidateProfileId, StringComparison.Ordinal);
    public const string RetiredContentMessage = "此存档的原配方或冻结内容清单依赖已退役的四购买怪物，无法在此版本精确恢复。原始存档未修改，请用私有归档中的旧版程序在独立目录体验。";
    public void VerifyRetainedSourceFiles(bool force)
    {
        if (resolvedContent == null) ResolveTypes();
        resolvedContent.VerifySourceFiles(force);
    }
    public string DeclaredProfileId => string.IsNullOrEmpty(candidateProfileId) ? ProfileId : candidateProfileId;
    public static string ResourcePathFor(string profileId)
    {
        if (string.Equals(profileId, ProfileId, StringComparison.Ordinal)) return "Ecology/ZoningV1/Recipe";
        if (string.Equals(profileId, UnitsCandidateProfileId, StringComparison.Ordinal)) return "Ecology/UnitsCandidateV1/Recipe";
        if (string.Equals(profileId, UnitsStableCandidateProfileId, StringComparison.Ordinal)) return "Ecology/UnitsStableCandidateV2/Recipe";
        if (string.Equals(profileId, MainV1ProfileId, StringComparison.Ordinal)) return "Ecology/SurfaceV4MainV1/Recipe";
        if (string.Equals(profileId, SurfaceV4ProfileId, StringComparison.Ordinal)) return "Ecology/SurfaceV4/Recipe";
        if (string.Equals(profileId, RemediationProfileId, StringComparison.Ordinal)) return "Ecology/RemediationV3/Recipe";
        return null;
    }
    public EcologyEcotypeDefinition[] ResolveTypes()
    {
        if (IsRetiredProfile(DeclaredProfileId)) throw new InvalidOperationException(RetiredContentMessage);
        if (IsArchiveOnlyProfile(DeclaredProfileId)) throw new InvalidOperationException(ArchiveOnlyMessage(DeclaredProfileId));
        if (string.IsNullOrWhiteSpace(configurationDigest) || retainedSettings == null || retainedPopulation == null || ecotypes == null || ecotypes.Length == 0)
            throw new InvalidOperationException("Retained zoning candidate inputs are incomplete.");
        retainedPopulation.Validate();
#if UNITY_EDITOR
        RequireCleanRetainedAsset(this); RequireCleanRetainedAsset(retainedSettings); RequireCleanRetainedAsset(retainedPopulation);
        if (retainedTerrain != null) RequireCleanRetainedAsset(retainedTerrain);
        foreach (Ecotype type in ecotypes)
        {
            if (type == null || type.prefab == null) throw new InvalidOperationException("RetainedPrefabMissing");
            RequireCleanRetainedAsset(type.prefab);
            foreach (Component component in type.prefab.GetComponentsInChildren<Component>(true))
                if (component != null) { RequireCleanRetainedAsset(component); RequireCleanRetainedAsset(component.gameObject); }
        }
#endif
        var types = ecotypes.Select(t => new EcologyEcotypeDefinition(t.typeId, t.prefab, t.terrains, t.weight, t.isBoss))
            .OrderBy(t => t.TypeId, StringComparer.Ordinal).ToArray();
        bool stableCandidate = string.Equals(DeclaredProfileId, UnitsStableCandidateProfileId, StringComparison.Ordinal) ||
            IsRemediationProfile(DeclaredProfileId);
        if (stableCandidate ? retainedTerrain == null || retainedTerrain.ImplementationId != "terrain-float32-retained-v1" : retainedTerrain != null)
            throw new InvalidOperationException("RetainedTerrainImplementationMismatch");
        bool pendingCandidate = stableCandidate || string.Equals(DeclaredProfileId, UnitsCandidateProfileId, StringComparison.Ordinal);
        if (ResourcePathFor(DeclaredProfileId) == null || pendingCandidate != bossContentPending ||
            types.Select(t => t.TypeId).Distinct(StringComparer.Ordinal).Count() != types.Length ||
            types.Count(t => t.IsBoss) != (bossContentPending ? 0 : 1))
            throw new InvalidOperationException("Retained ecotype mapping has duplicate identities or an invalid Boss definition.");
        resolvedContent = new EcologyRetainedContentManifest(retainedContentManifest, DeclaredProfileId, configurationDigest, types);
        resolvedContent.VerifySourceFiles(false);
        return types;
    }
#if UNITY_EDITOR
    private static void RequireCleanRetainedAsset(UnityEngine.Object asset)
    {
        // Unsaved Inspector/native-object changes are absent from the source-file hash.
        // A retained version must be explicitly authored and frozen before it is eligible.
        if (UnityEditor.EditorUtility.IsDirty(asset))
            throw new InvalidOperationException("RetainedAssetDirty: " + UnityEditor.AssetDatabase.GetAssetPath(asset));
    }
#endif
}

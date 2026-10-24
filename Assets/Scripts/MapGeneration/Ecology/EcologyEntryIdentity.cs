using System;

/// <summary>Only immutable regeneration inputs and accepted-output checks, never live AI/HP/lease state.</summary>
public sealed class EcologyEntryIdentity
{
    public EcologyEntryIdentity(string recipeVersion, string configDigest, string contentVersion, string footprintVersion,
        string contextIdentity, string layoutDigest, int ecologyAttempt, int regionCount, int residentCount, int merchantCount)
    {
        foreach (string value in new[] { recipeVersion, configDigest, contentVersion, footprintVersion, contextIdentity, layoutDigest })
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("An exact ecology entry identity is incomplete.");
        if (ecologyAttempt < 0 || ecologyAttempt >= EcologyPlanningAttempts.MaximumAttempts || regionCount < 3 || residentCount < 1 || merchantCount != 1)
            throw new ArgumentException("An exact ecology entry manifest is invalid.");
        RecipeVersion = recipeVersion; ConfigDigest = configDigest; ContentVersion = contentVersion; FootprintVersion = footprintVersion;
        ContextIdentity = contextIdentity; LayoutDigest = layoutDigest; EcologyAttempt = ecologyAttempt;
        RegionCount = regionCount; ResidentCount = residentCount; MerchantCount = merchantCount;
    }
    public string RecipeVersion { get; }
    public string ConfigDigest { get; }
    public string ContentVersion { get; }
    public string FootprintVersion { get; }
    public string ContextIdentity { get; }
    public string LayoutDigest { get; }
    public int EcologyAttempt { get; }
    public int RegionCount { get; }
    public int ResidentCount { get; }
    public int MerchantCount { get; }
}

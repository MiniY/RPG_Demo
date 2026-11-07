public static class MapHostBuildStamp
{
    public const bool IsReviewedCompatibleHost=true;
#if RPG_I1_ISOLATED_STARTUP && !UNITY_EDITOR
    public const bool HasExplicitI1StartupCapability=true;
    public const string StartupTarget="I1_PRIVATE";
#else
    public const bool HasExplicitI1StartupCapability=false;
    public const string StartupTarget="DEFAULT";
#endif
    public const string ReviewEvidenceSha256="b5d622093f9470c6cadf4c8a3f17bb11898eeb6c0a9fa3f02fc9b916ad580cf6";
    public const string SourceClosureSha256="363384b8e6cb9aa2b0764ca28b4f9c8b7bbfdbdc900b1b86b811bbe71a526115";
    public const string ApprovedDifferencesDigest="14d3d3d660acba8846c896c0326610bb9cd8037ff9af24411dc1c17ed2c82d8c";
    public const string BuildId="reward-pickup-standalone-20261108-rpd1";
    public const string ImplementationDigest="aa632904f7416aa8ff4e9a3a59d2ec843748ca3159ec2c7e249914f175641acb";
    public const string OldContentVersion="content-r1:c55fb3b4b0362e8bb59e84f4624fbb22c2f9ef96f3dcd5f61481a92dab9c9802";
    public const string NewContentVersion="content-r1:a4d222a9bf0aa3952ea231ed097e746a6348ff2e571a7a193ea7a61a55f4daac";
    public const string RetainedContentVersion="content-r1:c1b30f168534f17598f385ad32ab67078f9fb28b5f8cb6f346dcb6b800249ec9";
    public const string CurrentContentDifferencesDigest="ef6a8dc2498db161b70ecad5058af633a8778a1c20162912dd242bf3d87e2e77";
}

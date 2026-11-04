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
    public const string ReviewEvidenceSha256="ab7db70d85c24dcc2fd78410fba9af5fceb40c1c716dde6f077a3cd56e0c9c86";
    public const string SourceClosureSha256="22ecfdbeccea9a22d391f6c2ef54dabd99d2d8aa313609d5dfe41a5354c2f307";
    public const string ApprovedDifferencesDigest="68e6156c98a9b31c1bbde7a056eaf2cea9dab4438ded0209821222002dd1eb72";
    public const string BuildId="map-shop-ui-v35-20261105-g04";
    public const string ImplementationDigest="420e3f3cda380800c25ae249100845ccebf69d355dfbf9a504c44bc644efc1ea";
    public const string OldContentVersion="content-r1:c55fb3b4b0362e8bb59e84f4624fbb22c2f9ef96f3dcd5f61481a92dab9c9802";
    public const string NewContentVersion="content-r1:a4d222a9bf0aa3952ea231ed097e746a6348ff2e571a7a193ea7a61a55f4daac";
    public const string RetainedContentVersion="content-r1:c1b30f168534f17598f385ad32ab67078f9fb28b5f8cb6f346dcb6b800249ec9";
    public const string CurrentContentDifferencesDigest="21866fc463ee30410213e0946a3dc465a4e1bc4543acc4b0395c32ad75757bf1";
}

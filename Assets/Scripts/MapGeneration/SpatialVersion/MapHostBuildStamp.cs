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
    public const string ReviewEvidenceSha256="09f693dcb37b13a798e213ea2ba32870792030fd9d285ec1de7de41fb5ddd1a1";
    public const string SourceClosureSha256="a79b7efd82c0328cab2f10dae8dd52c989056d95e74490e6bc4044ea56fb6b18";
    public const string ApprovedDifferencesDigest="42f8034c613bfbd46ba75e1bc8169afe402df1a02ab36067a45003925d75d1f6";
    public const string BuildId="map-formal-default-20261104-d03";
    public const string ImplementationDigest="45f461e9f9301ba60747fd7d6aa502585312d1022447343395ffc170422a2dd1";
    public const string OldContentVersion="content-r1:c55fb3b4b0362e8bb59e84f4624fbb22c2f9ef96f3dcd5f61481a92dab9c9802";
    public const string NewContentVersion="content-r1:c1b30f168534f17598f385ad32ab67078f9fb28b5f8cb6f346dcb6b800249ec9";
}

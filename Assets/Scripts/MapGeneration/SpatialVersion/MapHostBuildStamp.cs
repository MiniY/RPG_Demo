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
    public const string ReviewEvidenceSha256="f940314d9097107a0c803608a46ff24ed224c57320065f0b304cdf19490e0f7c";
    public const string SourceClosureSha256="420dd226ed63a8d8c8084a898ef10eb2797ab461e969c27e13aac2182e7522d6";
    public const string ApprovedDifferencesDigest="b06a20123bcbbb198b150fc1c2db9ae0c183ff870f97df57132716ef0e8e887e";
    public const string BuildId="map-play-error-fix-20261104-e02";
    public const string ImplementationDigest="1fd96fc163f4d6b2466985ea3234a1a16a8ed082ad28f398a4eaeeaf83462b97";
    public const string OldContentVersion="content-r1:c55fb3b4b0362e8bb59e84f4624fbb22c2f9ef96f3dcd5f61481a92dab9c9802";
    public const string NewContentVersion="content-r1:c1b30f168534f17598f385ad32ab67078f9fb28b5f8cb6f346dcb6b800249ec9";
}

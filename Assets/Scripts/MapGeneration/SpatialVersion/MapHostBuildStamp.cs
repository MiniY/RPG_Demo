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
    public const string ReviewEvidenceSha256="826f7d38cf841da140a8fe2005b254d7aa9d24a6c0b61c3492297e52f18b937f";
    public const string SourceClosureSha256="5b870712b2a1583a5c2ae2053be3a54c142d9c9c0f8f39a0f0c21af1e5db2a34";
    public const string ApprovedDifferencesDigest="4127ec9c0ca20a269fd1fd52ce33c4e9976da8e73104132dbb9150abe4025ba7";
    public const string BuildId="map-main-host-c01";
    public const string ImplementationDigest="20bf8438a5eda76c13d8c1a65c76244fe075594f4d2f771b34e08d141dfd96a1";
    public const string OldContentVersion="content-r1:c55fb3b4b0362e8bb59e84f4624fbb22c2f9ef96f3dcd5f61481a92dab9c9802";
    public const string NewContentVersion="content-r1:c1b30f168534f17598f385ad32ab67078f9fb28b5f8cb6f346dcb6b800249ec9";
}

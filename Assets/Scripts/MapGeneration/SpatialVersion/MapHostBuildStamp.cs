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
    public const string ReviewEvidenceSha256="2a1317e43c4ad0f2750f9e794c848e47f553ffb410a3efef645b13e43ba546ae";
    public const string SourceClosureSha256="6c7a4d7f970ac20ae942097c59203a6667a9e991a4a428dda72a8477e7f22be0";
    public const string ApprovedDifferencesDigest="4127ec9c0ca20a269fd1fd52ce33c4e9976da8e73104132dbb9150abe4025ba7";
    public const string BuildId="map-vnext-m0-startup-20261101-b01";
    public const string ImplementationDigest="6b478efe1d85d8c12dba5840279641ae2dc7425807ffae0d0b112168f448333c";
    public const string OldContentVersion="content-r1:c55fb3b4b0362e8bb59e84f4624fbb22c2f9ef96f3dcd5f61481a92dab9c9802";
    public const string NewContentVersion="content-r1:c1b30f168534f17598f385ad32ab67078f9fb28b5f8cb6f346dcb6b800249ec9";
}

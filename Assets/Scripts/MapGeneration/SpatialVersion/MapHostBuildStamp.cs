public static class MapHostBuildStamp
{
    public const bool IsReviewedCompatibleHost=true;
    public const int HostSchemaVersion=3;
#if RPG_I1_ISOLATED_STARTUP && !UNITY_EDITOR
    public const bool HasExplicitI1StartupCapability=true;
    public const string StartupTarget="I1_PRIVATE";
#else
    public const bool HasExplicitI1StartupCapability=false;
    public const string StartupTarget="DEFAULT";
#endif
    public const string ReviewEvidenceSha256="15e0d685d083a62ef1fcf9bc0036047106efb15dec6a2bf1cc3e1c221b475754";
    public const string SourceClosureSha256="4421f98999529dc9f830693c4ab82a3c56815bcda75a8e5339548fec956eb1bb";
    public const string ApprovedDifferencesDigest="3e0cbf73b4170f9b887a3b81c520e6d58d597423684b1a9ab79f8cee02d2a820";
    public const string BuildId="reward-main-sync-20261108-ms1";
    public const string ImplementationDigest="f3c6b42714e5d48814662f2d6f3301e0dd7769c6e3b920753d038ea9a76c759f";
    public const string OldContentVersion="content-r1:c55fb3b4b0362e8bb59e84f4624fbb22c2f9ef96f3dcd5f61481a92dab9c9802";
    public const string NewContentVersion="content-r1:40e66c55cdbcb0505de387e5c495f7472a5d12f23f855b41fa5c41c34d8fcdc2";
    public const string RetainedContentVersion="content-r1:c1b30f168534f17598f385ad32ab67078f9fb28b5f8cb6f346dcb6b800249ec9";
    public const string PreviousCurrentContentVersion="content-r1:a4d222a9bf0aa3952ea231ed097e746a6348ff2e571a7a193ea7a61a55f4daac";
    public const string CurrentContentDifferencesDigest="6b2dc778f08ba57b0cd0afc4524e07abd2e0993014060c1cf7baa4b5dc35430b";
}

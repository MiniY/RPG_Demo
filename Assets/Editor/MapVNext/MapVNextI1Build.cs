using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>Build only the isolated formal scene; no PlayerSettings or retained asset edits.</summary>
public static class MapVNextI1Build
{
    [Serializable] private sealed class BuildEvidence
    {
        public string status, identity, implementationDigest, executable, scene, unity;
        public string strictOriginalCheck, compatibilityCheck, startupTarget, companyName, productName;
        public string[] extraScriptingDefines;
        public int originalDifferences, errors, warnings;
        public ulong bytes;
        public string[] packedSourceAssets;
    }
    public static void Build()
    {
        try
        {
            string output=Environment.GetEnvironmentVariable("RPG_I1_BUILD_ROOT");
            if(string.IsNullOrWhiteSpace(output) || !MapHostBuildStamp.IsReviewedCompatibleHost) throw new InvalidOperationException("ExplicitSealedI1BuildRootRequired");
            string target=Environment.GetEnvironmentVariable("RPG_I1_BUILD_TARGET")??"I1_PRIVATE";
            if(target!="DEFAULT"&&target!="I1_PRIVATE") throw new InvalidOperationException("ExplicitStartupBuildTargetRequired");
            if ((PlayerSettings.GetScriptingDefineSymbolsForGroup(BuildTargetGroup.Standalone)??"").Split(';').Contains("RPG_I1_ISOLATED_STARTUP")) throw new InvalidOperationException("I1StartupCapabilityMustNotBeAGlobalProjectDefine");
            string root=Path.GetFullPath(output);
            string expected=Path.GetFullPath("Artifacts/MapAcceptance/MapVNext-Integration-I1-20261029/Build-"+MapHostBuildStamp.BuildId+"/"+target);
            if(!string.Equals(root,expected,StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("BuildIdentityOutputMismatch");
            string player=Path.Combine(root,"MVP_Windows_x64");
            if(Directory.Exists(player)) throw new InvalidOperationException("I1BuildIdentityAlreadyHasPlayer");
            var old=Resources.Load<EcologyRecipeDefinition>(EcologyRecipeDefinition.ResourcePathFor(EcologyRecipeDefinition.MainV1ProfileId));
            var next=AssetDatabase.LoadAssetAtPath<EcologyRecipeDefinition>(MapVNextI1Authoring.AssetRoot+"/Recipe.asset");
            if(old==null || next==null || next.DeclaredProfileId!=EcologyRecipeDefinition.VNextI1ProfileId) throw new InvalidOperationException("BuildRecipesMissing");
            old.ResolveTypes();old.VerifyRetainedSourceFiles(true);
            next.ResolveTypes();next.VerifyRetainedSourceFiles(true);
            string strict=MapHostCompatibility.LastStrictSourceResult,compatible=MapHostCompatibility.LastCompatibilityResult;
            int differences=MapHostCompatibility.LastStrictDifferenceCount;
            Directory.CreateDirectory(player);
            string executable=Path.Combine(player,"RPG_Demo_MapVNext_I1.exe");
            string originalCompany=PlayerSettings.companyName,originalProduct=PlayerSettings.productName;
            string company="CodexMapAcceptance",product="RPG_M0_"+MapHostBuildStamp.BuildId+"_"+target;
            string[] defines=target=="I1_PRIVATE"?new[]{"RPG_I1_ISOLATED_STARTUP"}:new string[0];
            BuildReport report;
            try
            {
                PlayerSettings.companyName=company;PlayerSettings.productName=product;
                report=BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes=new[] {target=="I1_PRIVATE"?MapVNextI1Authoring.ScenePath:"Assets/Scenes/SampleScene.unity"},locationPathName=executable,
                target=BuildTarget.StandaloneWindows64,options=BuildOptions.DetailedBuildReport,extraScriptingDefines=defines
                });
            }
            finally
            {
                PlayerSettings.companyName=originalCompany;PlayerSettings.productName=originalProduct;
                AssetDatabase.SaveAssets();
            }
            var result=new BuildEvidence
            {
                status=report.summary.result==BuildResult.Succeeded?"BUILD_SUCCEEDED_PLAYER_NOT_RUN":"BUILD_FAILED",
                identity=MapHostBuildStamp.BuildId,implementationDigest=MapHostBuildStamp.ImplementationDigest,
                executable=executable,scene=target=="I1_PRIVATE"?MapVNextI1Authoring.ScenePath:"Assets/Scenes/SampleScene.unity",unity=Application.unityVersion,
                startupTarget=target,companyName=company,productName=product,extraScriptingDefines=defines,
                strictOriginalCheck=strict,originalDifferences=differences,compatibilityCheck=compatible,
                errors=(int)report.summary.totalErrors,warnings=(int)report.summary.totalWarnings,bytes=report.summary.totalSize,
                packedSourceAssets=report.packedAssets.SelectMany(p=>p.contents).Select(p=>p.sourceAssetPath)
                    .Where(p=>!string.IsNullOrEmpty(p)).Distinct().OrderBy(p=>p,StringComparer.Ordinal).ToArray()
            };
            File.WriteAllText(Path.Combine(root,"BuildReport.json"),JsonUtility.ToJson(result,true));
            if(report.summary.result!=BuildResult.Succeeded) throw new InvalidOperationException("I1WindowsBuildFailed");
            EditorApplication.Exit(0);
        }
        catch(Exception error) {Debug.LogException(error);EditorApplication.Exit(1);}
    }
}

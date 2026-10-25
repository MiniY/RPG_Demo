#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor.Compilation;
using UnityEditor.PackageManager;
using UnityEngine;

/// <summary>Actual non-test Player compilation inputs; generated output DLLs cannot be source dependencies.</summary>
public static class MainConsolidationPlayerSourceGraph
{
    [Serializable] public sealed class Graph
    {
        public string unity, toolchainRoot;
        public string target;
        public string[] activeDefines;
        public FileDigest[] inputDigests;
        public string scope = "Active-target non-test Player compilation graph. BuildReport separately records actual extraScriptingDefines; this graph alone does not certify compiler command lines.";
        public string[] projectFiles, toolchainFiles, assemblies;
    }
    [Serializable] public sealed class FileDigest { public string scope, path, sha256; }
    public static Graph Capture()
    {
        string root=Path.GetFullPath(Path.Combine(Application.dataPath,".."));
        string sdk=Path.GetDirectoryName(UnityEditor.EditorApplication.applicationPath);
        var assemblies=CompilationPipeline.GetAssemblies(AssembliesType.PlayerWithoutTestAssemblies);
        string Full(string path)=>Path.GetFullPath(Path.IsPathRooted(path)?path:Path.Combine(root,path));
        var generated=new HashSet<string>(assemblies.Select(a=>Full(a.outputPath)),StringComparer.OrdinalIgnoreCase);
        var project=new HashSet<string>(StringComparer.Ordinal);var toolchain=new HashSet<string>(StringComparer.Ordinal);
        var packageRoots=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string Actual(string value)
        {
            string path=Path.IsPathRooted(value)?value:Path.Combine(root,value);
            if(File.Exists(path))return Path.GetFullPath(path);
            string asset=value.Replace('\\','/');
            if(asset.StartsWith("Packages/",StringComparison.Ordinal))
            {
                var package=PackageInfo.FindForAssetPath(asset);
                if(package!=null)path=Path.Combine(package.resolvedPath,asset.Substring(("Packages/"+package.name+"/").Length));
            }
            if(!File.Exists(path))throw new InvalidOperationException("PlayerCompilationInputMissing: "+value);
            return Path.GetFullPath(path);
        }
        void Add(string value,bool metadata=true)
        {
            string file=Actual(value);
            if(file.StartsWith(root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))
            {
                project.Add(file.Substring(root.Length+1).Replace('\\','/'));
                string cache=Path.Combine(root,"Library","PackageCache")+Path.DirectorySeparatorChar;
                if(file.StartsWith(cache,StringComparison.OrdinalIgnoreCase))
                {
                    string package=file.Substring(cache.Length).Split(Path.DirectorySeparatorChar)[0];
                    packageRoots.Add(Path.Combine(cache,package));
                }
            }
            else if(file.StartsWith(sdk+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))
                toolchain.Add(file.Substring(sdk.Length+1).Replace('\\','/'));
            else throw new InvalidOperationException("PlayerCompilationExternalInputUnknown: "+file);
            if(metadata&&File.Exists(file+".meta"))Add(file+".meta",false);
        }
        foreach(var assembly in assemblies)
        {
            foreach(string file in assembly.sourceFiles)Add(file);
            string definition=CompilationPipeline.GetAssemblyDefinitionFilePathFromAssemblyName(assembly.name);
            if(!string.IsNullOrEmpty(definition))Add(definition);
            foreach(string reference in assembly.compiledAssemblyReferences)
                if(!generated.Contains(Full(reference)))Add(reference);
        }
        // Project response files and definitions also control the actual Player assembly topology.
        foreach(string file in Directory.EnumerateFiles(Application.dataPath,"*",SearchOption.AllDirectories))
            if(file.EndsWith(".asmdef",StringComparison.Ordinal)||file.EndsWith(".asmref",StringComparison.Ordinal)||file.EndsWith(".rsp",StringComparison.Ordinal))Add(file);
        Add(Path.Combine(root,"Packages","manifest.json"));
        Add(Path.Combine(root,"Packages","packages-lock.json"));
        // Package topology can move sources between assemblies even when a source itself is unchanged.
        foreach(string package in packageRoots.ToArray())
        {
            Add(Path.Combine(package,"package.json"));
            foreach(string file in Directory.EnumerateFiles(package,"*",SearchOption.AllDirectories))
                if(file.EndsWith(".asmdef",StringComparison.Ordinal)||file.EndsWith(".asmref",StringComparison.Ordinal)||file.EndsWith(".rsp",StringComparison.Ordinal))Add(file);
        }
        Add(UnityEditor.EditorApplication.applicationPath,false);
        return new Graph{unity=Application.unityVersion,toolchainRoot=sdk,projectFiles=project.OrderBy(p=>p,StringComparer.Ordinal).ToArray(),
            toolchainFiles=toolchain.OrderBy(p=>p,StringComparer.Ordinal).ToArray(),assemblies=assemblies.Select(a=>a.name).OrderBy(p=>p,StringComparer.Ordinal).ToArray()};
    }
    public static void Export()
    {
        string output=Environment.GetEnvironmentVariable("RPG_DEMO_PLAYER_GRAPH_OUTPUT");
        if(string.IsNullOrEmpty(output))throw new InvalidOperationException("Explicit graph export output required.");
        Directory.CreateDirectory(output);var graph=Capture();
        graph.target = UnityEditor.EditorUserBuildSettings.activeBuildTarget.ToString();
        graph.activeDefines = UnityEditor.PlayerSettings.GetScriptingDefineSymbolsForGroup(
            UnityEditor.BuildPipeline.GetBuildTargetGroup(UnityEditor.EditorUserBuildSettings.activeBuildTarget)).Split(';');
        string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string Digest(string path)
        {
            string full = Path.GetFullPath(path);
            if (Application.platform == RuntimePlatform.WindowsEditor && !full.StartsWith(@"\\?\", StringComparison.Ordinal)) full = @"\\?\" + full;
            using (var stream = File.OpenRead(full)) using (var sha = System.Security.Cryptography.SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }
        graph.inputDigests = graph.projectFiles.Select(path => new FileDigest {scope="project",path=path,sha256=Digest(Path.Combine(root,path))})
            .Concat(graph.toolchainFiles.Select(path => new FileDigest {scope="toolchain",path=path,sha256=Digest(Path.Combine(graph.toolchainRoot,path))})).ToArray();
        File.WriteAllText(Path.Combine(output,"PlayerSourceGraph.json"),JsonUtility.ToJson(graph,true));
        Debug.Log("PLAYER_SOURCE_GRAPH project="+graph.projectFiles.Length+" toolchain="+graph.toolchainFiles.Length+" assemblies="+graph.assemblies.Length);
    }
}
#endif

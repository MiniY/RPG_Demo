using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MapVNext;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

public static class MapVNextBuild
{
    public const string AssetRoot="Assets/MapGeneration/MapVNext";
    public const string ScenePath="Assets/Scenes/MapVNext/MapVNextStairs.unity";
    public static string Root => Path.GetFullPath("Artifacts/MapAcceptance/MapVNextStairs-20261028-r3-ReviewFix");
    [Serializable] class ImportEvidence { public string status;public ImportedTile[] tiles;public SourceMap[] savedMaps;public float footRadius;public bool hurtboxTrigger; }
    [Serializable] class ImportedTile { public int elevation,family,role;public string tilePath,tileGuid,textureGuid,spriteName;public long tileFileId,spriteFileId;public Rect rect;public float ppu; }
    [Serializable] class SourceMap {public string name;public int cells,sortingOrder;public Vector3 anchor,localPosition,localScale;}
    [Serializable] class BuildEvidence {public string status,result,executable,scene,identity,inputFingerprint,sourceFingerprint;public ulong bytes;public int warnings,errors;public string[] packedSourceAssets;}
    [Serializable] class Fingerprint {public string inputFingerprint;}

    public static void Prepare()
    {
        try
        {
            Directory.CreateDirectory(AssetRoot);Directory.CreateDirectory("Assets/Scenes/MapVNext");AssetDatabase.Refresh();
            if(AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath)!=null)
                throw new InvalidOperationException("CandidateSceneAlreadyPreparedDoNotOverwrite");
            var assets=AssetDatabase.LoadAssetAtPath<MapVNextAssets>(AssetRoot+"/MapVNextAssets.asset");
            if(assets==null)
            {
            assets=ScriptableObject.CreateInstance<MapVNextAssets>();
            assets.NativeTiles=new Tile[220];
            assets.SourceSettings=AssetDatabase.LoadAssetAtPath<MapGenerationSettings>("Assets/MapGeneration/MapGenerationSettings_Default.asset");
            assets.SourcePalette=AssetDatabase.LoadAssetAtPath<MapTerrainV2Palette>("Assets/MapGeneration/Terrain/MapTerrainV2Palette.asset");
            assets.PlayerPrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Players/Warrior_Blue.prefab");
            assets.InputFingerprint=JsonUtility.FromJson<Fingerprint>(File.ReadAllText(Path.Combine(Root,"input-fingerprint.json"))).inputFingerprint;
            var rows=new List<ImportedTile>();
            for(int h=0;h<5;h++)for(int role=0;role<44;role++)
            {
                int family=MapVNextAssets.Families[h];string path=$"Assets/Sprites/GrassTiles/Tilemap_color{family}_{role}.asset";
                Tile tile=AssetDatabase.LoadAssetAtPath<Tile>(path);
                if(tile==null||tile.sprite==null||tile.sprite.pixelsPerUnit!=64f)throw new InvalidOperationException("NativeImportMissing:"+path);
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(tile,out string tileGuid,out long tileId);
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(tile.sprite,out string textureGuid,out long spriteId);
                assets.NativeTiles[h*44+role]=tile;
                rows.Add(new ImportedTile {elevation=h,family=family,role=role,tilePath=path,tileGuid=tileGuid,tileFileId=tileId,textureGuid=textureGuid,spriteFileId=spriteId,spriteName=tile.sprite.name,rect=tile.sprite.rect,ppu=tile.sprite.pixelsPerUnit});
            }
            var source=EditorSceneManager.OpenScene("Assets/Scenes/MapGeneration/MapGenerationTest.unity",OpenSceneMode.Additive);
            var sourceMaps=source.GetRootGameObjects().SelectMany(o=>o.GetComponentsInChildren<Tilemap>(true)).Select(tm=>new SourceMap {name=tm.name,cells=CountCells(tm),sortingOrder=tm.GetComponent<TilemapRenderer>()?.sortingOrder??0,anchor=tm.tileAnchor,localPosition=tm.transform.localPosition,localScale=tm.transform.localScale}).ToArray();
            if(sourceMaps.Sum(m=>m.cells)!=138)throw new InvalidOperationException("SavedTemplateRuntimeCellCountMismatch");
            EditorSceneManager.CloseScene(source,true);
            AssetDatabase.CreateAsset(assets,AssetRoot+"/MapVNextAssets.asset");
            float footRadius=assets.PlayerPrefab.transform.Find("MovementFeet").GetComponent<CircleCollider2D>().radius;
            bool trigger=assets.PlayerPrefab.GetComponent<CapsuleCollider2D>().isTrigger;
            File.WriteAllText(Path.Combine(Root,"runtime-import.json"),JsonUtility.ToJson(new ImportEvidence {status="PASS",tiles=rows.ToArray(),savedMaps=sourceMaps,footRadius=footRadius,hurtboxTrigger=trigger},true));
            }
            else if(!File.Exists(Path.Combine(Root,"runtime-import.json"))) throw new InvalidOperationException("IncompleteBindingCannotResume");

            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var gridObject=new GameObject("MapVNextGrid",typeof(Grid));
            var terrain=gridObject.AddComponent<MapTilemapRenderer>();
            Tilemap water=Layer(gridObject,"GeneratedWaterBase",-30),sand=Layer(gridObject,"GeneratedSandBase",-20),grass=Layer(gridObject,"GeneratedGrassOverlay",0),elevation=Layer(gridObject,"GeneratedElevation",10),collision=Layer(gridObject,"GeneratedTerrainCollision",0);
            collision.gameObject.AddComponent<Rigidbody2D>().bodyType=RigidbodyType2D.Static;
            collision.gameObject.AddComponent<TilemapCollider2D>();
            var original=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/MapGeneration/RandomMapRuntime.prefab").GetComponentInChildren<MapTilemapRenderer>(true);
            collision.gameObject.layer=original.TerrainCollisionTilemap.gameObject.layer;
            var so=new SerializedObject(terrain);
            Set(so,"waterBaseTilemap",water);Set(so,"sandBaseTilemap",sand);Set(so,"grassOverlayTilemap",grass);Set(so,"elevationTilemap",elevation);Set(so,"terrainCollisionTilemap",collision);Set(so,"terrainV2Palette",assets.SourcePalette);so.ApplyModifiedPropertiesWithoutUndo();
            var demo=new GameObject("MapVNextIsolatedEntry").AddComponent<MapVNextDemo>();demo.Assets=assets;demo.Terrain=terrain;
            var camera=new GameObject("Main Camera").AddComponent<Camera>();camera.tag="MainCamera";camera.orthographic=true;camera.orthographicSize=7.5f;camera.transform.position=new Vector3(12,8.5f,-10);camera.backgroundColor=new Color(.05f,.075f,.10f);camera.clearFlags=CameraClearFlags.SolidColor;demo.View=camera;
            new GameObject("GameInput").AddComponent<GameInput>();
            var canvas=new GameObject("CaseBrowser",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));demo.Overlay=canvas.GetComponent<Canvas>();demo.Overlay.renderMode=RenderMode.ScreenSpaceOverlay;
            var scaler=canvas.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1280,720);scaler.matchWidthOrHeight=.5f;
            var header=Panel(canvas.transform,"Header",new Vector2(0,1),new Vector2(1,1),new Vector2(0,-112),Vector2.zero);
            demo.Caption=Label(header,"CaseMetadata","Map VNext",17,TextAnchor.UpperLeft);var rt=demo.Caption.rectTransform;rt.offsetMin=new Vector2(16,8);rt.offsetMax=new Vector2(-16,-8);
            var controls=Panel(canvas.transform,"Controls",Vector2.zero,new Vector2(1,0),Vector2.zero,new Vector2(0,55));
            string[] labels={"Prev / P","Next / N","Pair +","West / East","Reset / R","Clean / F1","Exit / Esc"};
            demo.Controls=new UnityEngine.UI.Button[7];
            for(int i=0;i<labels.Length;i++)
            {
                var b=new GameObject("Control"+i,typeof(RectTransform),typeof(UnityEngine.UI.Image),typeof(UnityEngine.UI.Button));b.transform.SetParent(controls,false);var br=b.GetComponent<RectTransform>();br.anchorMin=new Vector2(i/7f,0);br.anchorMax=new Vector2((i+1)/7f,1);br.offsetMin=new Vector2(5,7);br.offsetMax=new Vector2(-5,-7);b.GetComponent<UnityEngine.UI.Image>().color=new Color(.17f,.20f,.27f);demo.Controls[i]=b.GetComponent<UnityEngine.UI.Button>();Label(b.transform,"Text",labels[i],16,TextAnchor.MiddleCenter);
            }
            var es=new GameObject("EventSystem",typeof(EventSystem),typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));es.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>().AssignDefaultActions();
            demo.Overlay.gameObject.SetActive(false);
            EditorSceneManager.SaveScene(scene,ScenePath);AssetDatabase.SaveAssets();
            File.WriteAllText(Path.Combine(Root,"prepare.json"),"{\"status\":\"PASS\",\"scene\":\""+ScenePath+"\",\"nativeTiles\":220,\"savedCells\":138}");
            EditorApplication.Exit(0);
        }
        catch(Exception e){Debug.LogException(e);EditorApplication.Exit(2);}
    }

    static Tilemap Layer(GameObject parent,string name,int order)
    {
        var go=new GameObject(name,typeof(Tilemap),typeof(TilemapRenderer));go.transform.SetParent(parent.transform,false);go.GetComponent<TilemapRenderer>().sortingOrder=order;return go.GetComponent<Tilemap>();
    }
    static int CountCells(Tilemap tilemap)
    {int count=0;foreach(var cell in tilemap.cellBounds.allPositionsWithin)if(tilemap.HasTile(cell))count++;return count;}

    public static void RefreshCandidateScene()
    {
        try
        {
            var scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
            var demo=UnityEngine.Object.FindObjectOfType<MapVNextDemo>();
            demo.Overlay.gameObject.SetActive(false);
            EditorSceneManager.SaveScene(scene,ScenePath);AssetDatabase.SaveAssets();EditorApplication.Exit(0);
        }
        catch(Exception e){Debug.LogException(e);EditorApplication.Exit(2);}
    }
    static void Set(SerializedObject so,string field,UnityEngine.Object value){so.FindProperty(field).objectReferenceValue=value;}
    internal static Transform Panel(Transform parent,string name,Vector2 min,Vector2 max,Vector2 offsetMin,Vector2 offsetMax)
    {
        var go=new GameObject(name,typeof(RectTransform),typeof(UnityEngine.UI.Image));go.transform.SetParent(parent,false);var r=go.GetComponent<RectTransform>();r.anchorMin=min;r.anchorMax=max;r.offsetMin=offsetMin;r.offsetMax=offsetMax;go.GetComponent<UnityEngine.UI.Image>().color=new Color(.035f,.045f,.07f,.9f);return go.transform;
    }
    internal static Text Label(Transform parent,string name,string value,int size,TextAnchor alignment)
    {
        var go=new GameObject(name,typeof(RectTransform),typeof(Text));go.transform.SetParent(parent,false);var t=go.GetComponent<Text>();t.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");t.fontSize=size;t.color=Color.white;t.alignment=alignment;t.text=value;var r=t.rectTransform;r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=Vector2.zero;r.offsetMax=Vector2.zero;return t;
    }

    public static void Build() => BuildCandidate(Root,ScenePath,"RPGDemo_MapVNext_Stairs",MapVNextAssets.Identity);

    public static void BuildCandidate(string root,string scenePath,string productName,string identity)
    {
        const string settingsPath="ProjectSettings/ProjectSettings.asset";
        const string fallback="Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Fallback.asset";
        byte[] settingsBytes=File.ReadAllBytes(settingsPath),fontBytes=File.ReadAllBytes(fallback);
        string company=PlayerSettings.companyName,product=PlayerSettings.productName;
        int exit=2;
        try
        {
            // A new fixture identity does not replace, ignore, or rewrite retained manifests.
            var retained=Resources.Load<EcologyRecipeDefinition>(EcologyRecipeDefinition.ResourcePathFor(EcologyRecipeDefinition.MainV1ProfileId));
            retained.ResolveTypes();retained.VerifyRetainedSourceFiles(true);
            PlayerSettings.companyName="CodexMapAcceptance";PlayerSettings.productName=productName;
            string exe=Path.Combine(root,"MVP_Windows_x64",productName=="RPGDemo_MapVNext_Random"?"MapVNextRandom.exe":"MapVNextStairs.exe");Directory.CreateDirectory(Path.GetDirectoryName(exe));
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions {scenes=new[]{scenePath},locationPathName=exe,target=BuildTarget.StandaloneWindows64,options=BuildOptions.DetailedBuildReport});
            var assets=AssetDatabase.LoadAssetAtPath<MapVNextAssets>(AssetRoot+"/MapVNextAssets.asset");
            var result=new BuildEvidence {status=report.summary.result==BuildResult.Succeeded?"PASS":"FAIL",result=report.summary.result.ToString(),executable=exe,scene=scenePath,identity=identity,sourceFingerprint=Environment.GetEnvironmentVariable("RPG_P2_SOURCE_FINGERPRINT"),inputFingerprint=assets.InputFingerprint,bytes=report.summary.totalSize,warnings=(int)report.summary.totalWarnings,errors=(int)report.summary.totalErrors,packedSourceAssets=report.packedAssets.SelectMany(p=>p.contents).Select(p=>p.sourceAssetPath).Distinct().OrderBy(p=>p).ToArray()};
            File.WriteAllText(Path.Combine(root,"BuildReport.json"),JsonUtility.ToJson(result,true));exit=result.status=="PASS"?0:2;
        }
        catch(Exception e){Debug.LogException(e);}
        finally
        {
            PlayerSettings.companyName=company;PlayerSettings.productName=product;AssetDatabase.SaveAssets();
            byte[] generated=File.ReadAllBytes(fallback);File.WriteAllBytes(Path.Combine(root,"TMP-fallback-generated-during-build.asset"),generated);
            File.WriteAllBytes(fallback,fontBytes);File.WriteAllBytes(settingsPath,settingsBytes);
            EditorApplication.Exit(exit);
        }
    }
}

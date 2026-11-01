using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using MapVNext;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class MapVNextP2Build
{
    public const string ScenePath="Assets/Scenes/MapVNext/MapVNextRandom.unity";
    public static string Root=>Path.GetFullPath("Artifacts/MapAcceptance/MapVNextP2Random-20261029");
    public static string BuildRoot=>Path.Combine(Root,"Build-"+MapVNextRandomLayout.Identity.Split('-')[MapVNextRandomLayout.Identity.Split('-').Length-1]);
    public static void Prepare()
    {
        try
        {
            var pilot=new List<LayoutSummary>();
            foreach(int seed in MapVNextRandomLayout.PilotSeeds)
            {
                var layout=MapVNextRandomLayout.Generate(seed);pilot.Add(layout.Summary);
                File.WriteAllText(Path.Combine(Root,"preparation-layouts.json"),"["+string.Join(",",pilot.Select(p=>JsonUtility.ToJson(p,true)))+"]");
                if(layout.Map==null)throw new InvalidOperationException("PilotLayoutRejected:"+seed+":"+string.Join(";",layout.Summary.attempts.Select(p=>p.reason)));
            }
            bool existing=AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath)!=null;
            var scene=EditorSceneManager.OpenScene(existing?ScenePath:MapVNextBuild.ScenePath,OpenSceneMode.Single);
            var demo=UnityEngine.Object.FindObjectOfType<MapVNextDemo>();
            if(demo==null)throw new InvalidOperationException("ExistingVNextDemoMissing");
            demo.EnableRandomLayout=true;demo.Overlay.gameObject.SetActive(false);
            if(demo.RandomControlPanel==null)
            {
                var panel=MapVNextBuild.Panel(demo.Overlay.transform,"RandomControls",Vector2.zero,new Vector2(1,0),Vector2.zero,new Vector2(0,55));
                demo.RandomControlPanel=panel.gameObject;
                string[] names={"Next map / N","Same Seed / G","Overview / Tab","Reset / R","Base samples","Clean / F1","Exit / Esc"};
                demo.RandomControls=new UnityEngine.UI.Button[names.Length];
                for(int i=0;i<names.Length;i++)demo.RandomControls[i]=Button(panel,"RandomControl"+i,names[i],new Vector2(i/7f,0),new Vector2((i+1)/7f,1),new Vector2(4,7),new Vector2(-4,-7));
                var header=demo.Caption.transform.parent;
                var hr=header.GetComponent<RectTransform>();hr.offsetMin=new Vector2(0,-138);
                demo.Caption.rectTransform.offsetMax=new Vector2(-410,-7);demo.Caption.fontSize=16;
                demo.Caption.resizeTextForBestFit=true;demo.Caption.resizeTextMinSize=12;demo.Caption.resizeTextMaxSize=16;
                var inputObject=new GameObject("SeedInput",typeof(RectTransform),typeof(UnityEngine.UI.Image),typeof(UnityEngine.UI.InputField));inputObject.transform.SetParent(header,false);
                var ir=inputObject.GetComponent<RectTransform>();ir.anchorMin=new Vector2(1,1);ir.anchorMax=new Vector2(1,1);ir.pivot=new Vector2(1,1);ir.anchoredPosition=new Vector2(-148,-14);ir.sizeDelta=new Vector2(240,40);
                inputObject.GetComponent<UnityEngine.UI.Image>().color=new Color(.16f,.18f,.25f);
                var input=inputObject.GetComponent<UnityEngine.UI.InputField>();var text=MapVNextBuild.Label(inputObject.transform,"Text","21193",18,TextAnchor.MiddleLeft);
                text.supportRichText=false;text.raycastTarget=false;text.rectTransform.offsetMin=new Vector2(9,3);text.rectTransform.offsetMax=new Vector2(-9,-3);
                input.textComponent=text;input.contentType=UnityEngine.UI.InputField.ContentType.IntegerNumber;input.characterLimit=11;input.text="21193";demo.SeedField=input;
                demo.GenerateSeedButton=Button(header,"GenerateSeed","Generate Seed",new Vector2(1,1),new Vector2(1,1),new Vector2(-138,-54),new Vector2(-12,-14));
                demo.ReturnRandomButton=Button(header,"ReturnRandom","Back to random",new Vector2(1,0),new Vector2(1,0),new Vector2(-205,9),new Vector2(-12,46));
                var hint=MapVNextBuild.Label(header,"SeedHint","Terrain height = H0-H4; not game floor.\nEnter signed Seed, then Generate Seed.",14,TextAnchor.UpperRight);
                hint.raycastTarget=false;hint.rectTransform.anchorMin=new Vector2(1,1);hint.rectTransform.anchorMax=new Vector2(1,1);hint.rectTransform.pivot=new Vector2(1,1);hint.rectTransform.anchoredPosition=new Vector2(-12,-64);hint.rectTransform.sizeDelta=new Vector2(380,48);
            }
            EditorSceneManager.SaveScene(scene,ScenePath);AssetDatabase.SaveAssets();
            File.WriteAllText(Path.Combine(Root,"prepare.json"),"{\"status\":\"PASS\",\"scene\":\""+ScenePath+"\",\"sourceScenePreserved\":true,\"mode\":\"random-default\"}");
            EditorApplication.Exit(0);
        }
        catch(Exception ex){Debug.LogException(ex);EditorApplication.Exit(2);}
    }
    static UnityEngine.UI.Button Button(Transform parent,string name,string label,Vector2 min,Vector2 max,Vector2 low,Vector2 high)
    {
        var go=new GameObject(name,typeof(RectTransform),typeof(UnityEngine.UI.Image),typeof(UnityEngine.UI.Button));go.transform.SetParent(parent,false);
        var rect=go.GetComponent<RectTransform>();rect.anchorMin=min;rect.anchorMax=max;rect.offsetMin=low;rect.offsetMax=high;
        go.GetComponent<UnityEngine.UI.Image>().color=new Color(.18f,.20f,.29f);
        var text=MapVNextBuild.Label(go.transform,"Text",label,16,TextAnchor.MiddleCenter);text.raycastTarget=false;
        return go.GetComponent<UnityEngine.UI.Button>();
    }
    public static void Build()
    {
        if(Directory.Exists(Path.Combine(BuildRoot,"MVP_Windows_x64")))throw new InvalidOperationException("BuildIdentityAlreadyExistsDoNotOverwrite");
        Directory.CreateDirectory(BuildRoot);
        MapVNextBuild.BuildCandidate(BuildRoot,ScenePath,"RPGDemo_MapVNext_Random",MapVNextRandomLayout.Identity);
    }
}

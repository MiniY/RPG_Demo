using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Test-only camera records actual native motion; it never drives bodies.</summary>
public sealed class SurfaceV4NativeVideo : MonoBehaviour
{
    [Serializable] public sealed class Frame
    {
        public string file,actor,sprite;
        public float real,world,minimumGap;
        public Vector2 body,foot;
        public int elevation;
    }
    [Serializable] private sealed class Clip
    {
        public string scope="Controlled original-prefab PlayMode fixture. Actual automatically simulated Rigidbody poses and original movement calls. Test camera; display sorting follows the same MapData elevation formula. No body/collider/scale/physics mutation by recorder; not natural AI or keyboard input.";
        public List<Frame> frames=new List<Frame>();
    }
    private readonly Dictionary<string,Clip> clips=new Dictionary<string,Clip>();
    private readonly Dictionary<int,int> baseOrder=new Dictionary<int,int>();
    private Camera captureCamera;
    private RenderTexture target;
    private Texture2D pixels;
    public static void Record(GameObject owner,MapData map,MapTilemapRenderer terrain,GameObject actor,Collider2D foot,string clip,float minimumGap)
    {
        var recorder=owner.GetComponent<SurfaceV4NativeVideo>()??owner.AddComponent<SurfaceV4NativeVideo>();
        recorder.Capture(owner,map,terrain,actor,foot,Regex.Replace(clip,"[^A-Za-z0-9_-]","_"),minimumGap);
    }
    private void Capture(GameObject owner,MapData map,MapTilemapRenderer terrain,GameObject actor,Collider2D foot,string key,float gap)
    {
        if(!clips.TryGetValue(key,out Clip clip)){clip=new Clip();clips.Add(key,clip);}
        if(clip.frames.Count>0 && Time.realtimeSinceStartup-clip.frames[clip.frames.Count-1].real<.075f)return;
        if(captureCamera==null)
        {
            var cameraObject=new GameObject("Owned native physics evidence camera");cameraObject.transform.SetParent(transform,false);
            captureCamera=cameraObject.AddComponent<Camera>();captureCamera.enabled=false;captureCamera.orthographic=true;
            captureCamera.orthographicSize=6f;captureCamera.clearFlags=CameraClearFlags.SolidColor;captureCamera.backgroundColor=new Color(.12f,.2f,.22f,1f);
            target=new RenderTexture(960,540,24);pixels=new Texture2D(960,540,TextureFormat.RGB24,false);
        }
        var bodies=owner.GetComponentsInChildren<Rigidbody2D>().Where(b=>b.bodyType!=RigidbodyType2D.Static && b.gameObject.activeInHierarchy).ToArray();
        foreach(var body in bodies)
        {
            var cell=terrain.Coordinates.WorldToCell(body.position);int level=map.IsInside(cell)?map.GetElevation(cell):0;
            var group=body.GetComponentInChildren<SortingGroup>(true);
            if(group!=null)
            {
                if(!baseOrder.ContainsKey(group.GetInstanceID()))baseOrder.Add(group.GetInstanceID(),group.sortingOrder);
                group.sortingOrder=baseOrder[group.GetInstanceID()]+level*10;
            }
            else foreach(var renderer in body.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if(!baseOrder.ContainsKey(renderer.GetInstanceID()))baseOrder.Add(renderer.GetInstanceID(),renderer.sortingOrder);
                renderer.sortingOrder=baseOrder[renderer.GetInstanceID()]+level*10;
            }
        }
        Vector2 centre=bodies.Length==0?(Vector2)actor.transform.position:bodies.Select(b=>b.position).Aggregate(Vector2.zero,(a,b)=>a+b)/bodies.Length;
        captureCamera.transform.position=new Vector3(centre.x,centre.y+1f,-20f);
        var old=RenderTexture.active;
        try
        {
            captureCamera.targetTexture=target;captureCamera.Render();RenderTexture.active=target;
            pixels.ReadPixels(new Rect(0,0,960,540),0,0);pixels.Apply();
            string relative="NativeMotion/"+key+"/"+clip.frames.Count.ToString("D4")+".png";
            string absolute=Path.Combine(Environment.GetEnvironmentVariable("RPG_DEMO_REMEDIATION_EVIDENCE"),relative);
            Directory.CreateDirectory(Path.GetDirectoryName(absolute));File.WriteAllBytes(absolute,pixels.EncodeToPNG());
            var body=actor.GetComponent<Rigidbody2D>();var cell=terrain.Coordinates.WorldToCell(body.position);
            var sprite=actor.GetComponentInChildren<SpriteRenderer>(true);
            clip.frames.Add(new Frame{file=relative,actor=actor.name,sprite=sprite!=null && sprite.sprite!=null?sprite.sprite.name:"",
                real=Time.realtimeSinceStartup,world=Time.time,body=body.position,foot=foot.bounds.center,minimumGap=gap,
                elevation=map.IsInside(cell)?map.GetElevation(cell):0});
        }
        finally{captureCamera.targetTexture=null;RenderTexture.active=old;}
    }
    public static void Flush(GameObject owner)
    {if(owner!=null){var r=owner.GetComponent<SurfaceV4NativeVideo>();if(r!=null)r.Save();}}
    private void Save()
    {
        foreach(var entry in clips)
        {
            string folder=Path.Combine(Environment.GetEnvironmentVariable("RPG_DEMO_REMEDIATION_EVIDENCE"),"NativeMotion",entry.Key);
            Directory.CreateDirectory(folder);File.WriteAllText(Path.Combine(folder,"frames.json"),JsonUtility.ToJson(entry.Value,true));
        }
    }
    private void OnDestroy(){Save();if(target!=null)Destroy(target);if(pixels!=null)Destroy(pixels);}
}

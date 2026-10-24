using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>Derived display only. No Collider, terrain edits, registry or gameplay RNG.</summary>
public sealed class MapSurfaceV4Visuals : MonoBehaviour
{
    [Serializable] public sealed class FoamPlacement
    {
        public Vector2Int cell;
        public int cardinalMask;
        public string piece, group;
        public float phase;
    }
    public readonly List<FoamPlacement> Placements = new List<FoamPlacement>();
    public readonly List<string> AssetLimits = new List<string>();
    public int ShoreCells { get; private set; }
    public int WaterRocks { get; private set; }
    public int HighlandShadows { get; private set; }
    public int ObjectShadows { get; private set; }
    private readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
    private readonly Dictionary<string, Tilemap> layers = new Dictionary<string, Tilemap>();
    private readonly List<GameObject> shadows = new List<GameObject>();
    private readonly Dictionary<string, Sprite[]> frames = new Dictionary<string, Sprite[]>();
    private MapTilemapRenderer terrain;
    private MapSurfaceV4Settings settings;
    private Sprite shadowStrip, shadowBlob;
    private MapSimpleDecorationData acceptedDecorationSource;
    private static readonly Vector2Int[] Cardinal = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };

    public static void Project(MapData map, MapTilemapRenderer terrain, MapSurfaceV4Settings settings)
    {
        Transform parent = terrain.GroundTilemap.transform.parent;
        Transform old = parent.Find("GeneratedSurfaceVisuals_R4");
        var state = old != null ? old.GetComponent<MapSurfaceV4Visuals>() : null;
        if (state == null)
        {
            var holder = new GameObject("GeneratedSurfaceVisuals_R4"); holder.transform.SetParent(parent, false);
            state = holder.AddComponent<MapSurfaceV4Visuals>();
        }
        state.ClearOwned(); state.terrain = terrain; state.settings = settings;
        state.Build(map);
    }

    private void Build(MapData map)
    {
        if (settings.shorelineTexture == null || settings.waterRockTexture == null || settings.contactShadowTexture == null)
            throw new InvalidOperationException("SurfaceV4VisualSourceMissing");
        if (settings.shorelineTexture.width % 192 != 0 || settings.shorelineTexture.height != 192 ||
            settings.waterRockTexture.width % 128 != 0 || settings.waterRockTexture.height != 128)
            throw new InvalidOperationException("SurfaceV4UnexpectedNativeAnimationLayout");
        Transform parent = terrain.GroundTilemap.transform.parent;
        foreach (Tilemap layer in parent.GetComponentsInChildren<Tilemap>(true))
        {
            if (layer.name == "GeneratedWaterFoam" || layer.name.StartsWith("GeneratedElevation") && layer.name.EndsWith("Shadow"))
                layer.ClearAllTiles();
        }
        var surface = MapSurfaceV4Policy.Build(map, terrain.TerrainV2Palette, settings);
        var faces = new HashSet<Vector2Int>(surface.Faces.Select(f => f.Cell));
        bool Water(Vector2Int c) => map.IsInside(c) && TerrainTopology.IsWater(map.GetCell(c).terrainType) && !faces.Contains(c);
        bool Bank(Vector2Int c) => map.IsInside(c) && !Water(c);
        float phase = (MapSemanticPlacementPrimitives.StableScore(map.GenerationSeed, "surface.foam.v4", "shore", Vector2Int.zero) % 16UL) / settings.shorelineFramesPerSecond;
        var edgeTiles = new MapSurfaceV4AnimatedTile[4];
        // The native ring's outer pieces point back toward its centre. Opposite
        // pieces therefore place their foam at the touching edge of a water cell.
        int[] columns = { 1, 0, 1, 2 }, rows = { 0, 1, 2, 1 };
        for (int d = 0; d < 4; d++) edgeTiles[d] = Tile(Frames(settings.shorelineTexture, 192, columns[d], rows[d]), settings.shorelineFramesPerSecond, phase);
        var cornerTiles = new[] { Tile(Frames(settings.shorelineTexture,192,0,0),settings.shorelineFramesPerSecond,phase),
            Tile(Frames(settings.shorelineTexture,192,0,2),settings.shorelineFramesPerSecond,phase),
            Tile(Frames(settings.shorelineTexture,192,2,2),settings.shorelineFramesPerSecond,phase),
            Tile(Frames(settings.shorelineTexture,192,2,0),settings.shorelineFramesPerSecond,phase) };
        Vector2Int[] diagonals = { new Vector2Int(1,1), new Vector2Int(1,-1), new Vector2Int(-1,-1), new Vector2Int(-1,1) };
        for (int y = map.Origin.y; y < map.Origin.y + map.Height; y++)
        for (int x = map.Origin.x; x < map.Origin.x + map.Width; x++)
        {
            var cell = new Vector2Int(x,y); if (!Water(cell)) continue;
            int mask = 0;
            for (int d = 0; d < 4; d++) if (Bank(cell + Cardinal[d])) mask |= 1 << d;
            bool hasPiece = false;
            for (int d = 0; d < 4; d++) if ((mask & (1 << d)) != 0)
            {
                Layer("Shore"+d,-14).SetTile(new Vector3Int(x,y,0),edgeTiles[d]); hasPiece = true;
                Placements.Add(new FoamPlacement { cell=cell,cardinalMask=mask,piece="edge"+d,group="shore",phase=phase });
            }
            for (int d = 0; d < 4; d++)
            {
                int first = d, second = (d+1)%4;
                if ((mask & ((1<<first)|(1<<second))) != 0 || !Bank(cell+diagonals[d])) continue;
                Layer("Corner"+d,-13).SetTile(new Vector3Int(x,y,0),cornerTiles[d]); hasPiece=true;
                Placements.Add(new FoamPlacement { cell=cell,cardinalMask=mask,piece="diagonal"+d,group="shore",phase=phase });
            }
            if (hasPiece) ShoreCells++;
        }
        AssetLimits.Add("Native diagonal corner pieces are sparse and can be transparent in some frames; no centre-fill or invented corner art is substituted. Actual corner review required.");
        var candidates = new List<Vector2Int>();
        for (int y = map.Origin.y+1; y < map.Origin.y+map.Height-2; y++)
        for (int x = map.Origin.x+1; x < map.Origin.x+map.Width-2; x++)
        {
            var cell=new Vector2Int(x,y); bool legal=true;
            for(int dy=-1;dy<=2;dy++) for(int dx=-1;dx<=2;dx++) if(!Water(cell+new Vector2Int(dx,dy))) legal=false;
            if(legal)candidates.Add(cell);
        }
        var placed=new List<Vector2Int>(); int budget=Mathf.Clamp(map.Width*map.Height/600,1,6);
        foreach(var cell in candidates.OrderBy(c=>MapSemanticPlacementPrimitives.StableScore(map.GenerationSeed,"surface.rock.v4","visual",c)).ThenBy(c=>c.y).ThenBy(c=>c.x))
        {
            if(placed.Any(p=>(p-cell).sqrMagnitude<36))continue;
            float rockPhase=(MapSemanticPlacementPrimitives.StableScore(map.GenerationSeed,"surface.rock.v4","phase",cell)%8UL)/5f;
            string group="rock@"+cell;
            for(int dy=0;dy<2;dy++)for(int dx=0;dx<2;dx++)
            {
                var at=cell+new Vector2Int(dx,dy);
                Layer("WaterRocks",-12).SetTile(new Vector3Int(at.x,at.y,0),Tile(Frames(settings.waterRockTexture,128,dx,dy),5f,rockPhase));
                Placements.Add(new FoamPlacement{cell=at,piece="rock"+dx+dy,group=group,phase=rockPhase});
            }
            placed.Add(cell); WaterRocks++; if(placed.Count>=budget)break;
        }
        shadowStrip=Sprite.Create(settings.contactShadowTexture,new Rect(72,72,48,32),new Vector2(.5f,.5f),64f);
        shadowBlob=Sprite.Create(settings.contactShadowTexture,new Rect(32,32,128,128),new Vector2(.5f,.5f),64f);
        owned.Add(shadowStrip);owned.Add(shadowBlob);
        foreach(var face in surface.Faces)
        {
            Vector3 centre=terrain.GroundTilemap.GetCellCenterWorld(new Vector3Int(face.Cell.x,face.Cell.y,0));
            float foot=centre.y+face.Tile.sprite.bounds.min.y;
            Shadow("FaceShadow",shadowStrip,new Vector3(centre.x,foot-.08f,0),new Vector2(1f,.16f),
                face.Level==0?1:(face.Level-1)*10+1);
            HighlandShadows++;
        }
    }

    public void AddDecorationShadows(MapData map, MapSimpleDecorationData decorations)
    {
        if (ReferenceEquals(acceptedDecorationSource, decorations)) return;
        if (acceptedDecorationSource != null) throw new InvalidOperationException("SurfaceV4DecorationChangedWithoutProjection");
        acceptedDecorationSource = decorations;
        var eligible = new HashSet<string>(settings.supplementShadowVariantIds ?? Array.Empty<string>(),StringComparer.Ordinal);
        foreach(var placement in decorations.Placements)
        {
            var variant=settings.simpleDecorationPalette.FindVariant(placement.VariantId);
            if(variant==null || !eligible.Contains(variant.VariantId))continue;
            Vector3 centre=terrain.Coordinates.CellToWorld(placement.AnchorCell);
            bool tree=variant.DecorationType==MapSimpleDecorationType.Tree;
            Shadow("ObjectShadow/"+variant.VariantId,shadowBlob,centre+new Vector3(.1f,-.12f,0),tree?new Vector2(1.4f,.45f):new Vector2(.8f,.3f),
                map.GetElevation(placement.AnchorCell)*10+1);
            ObjectShadows++;
        }
    }

    private void Shadow(string name,Sprite sprite,Vector3 position,Vector2 size,int order)
    {
        var holder=new GameObject(name);holder.transform.SetParent(transform,false);holder.transform.position=position;
        holder.transform.localScale=new Vector3(size.x/sprite.bounds.size.x,size.y/sprite.bounds.size.y,1);
        var renderer=holder.AddComponent<SpriteRenderer>();renderer.sprite=sprite;renderer.color=new Color(1,1,1,settings.lightShadowTint);
        renderer.sortingOrder=order;shadows.Add(holder);
    }
    private Sprite[] Frames(Texture2D texture,int stride,int column,int row)
    {
        string key=texture.GetInstanceID()+":"+column+":"+row;
        if(frames.TryGetValue(key,out var cached))return cached;
        var result=new Sprite[texture.width/stride];
        for(int i=0;i<result.Length;i++)
        {
            result[i]=Sprite.Create(texture,new Rect(i*stride+column*64,row*64,64,64),new Vector2(.5f,.5f),64f);
            result[i].name="SurfaceV4/"+texture.name+"/"+column+row+"/"+i;owned.Add(result[i]);
        }
        frames.Add(key,result);return result;
    }
    private MapSurfaceV4AnimatedTile Tile(Sprite[] sprites,float speed,float phase)
    {var tile=ScriptableObject.CreateInstance<MapSurfaceV4AnimatedTile>();tile.Frames=sprites;tile.Speed=speed;tile.Phase=phase;owned.Add(tile);return tile;}
    private Tilemap Layer(string name,int order)
    {
        if(layers.TryGetValue(name,out var map))return map;
        var holder=new GameObject("SurfaceV4"+name);holder.transform.SetParent(transform,false);
        map=holder.AddComponent<Tilemap>();map.animationFrameRate=1f;holder.AddComponent<TilemapRenderer>().sortingOrder=order;layers.Add(name,map);return map;
    }
    public void ClearOwned()
    {
        foreach(var layer in layers.Values)if(layer!=null)layer.ClearAllTiles();
        foreach(var shadow in shadows)if(shadow!=null){shadow.SetActive(false);Release(shadow);}shadows.Clear();
        foreach(var item in owned)if(item!=null)Release(item);owned.Clear();frames.Clear();Placements.Clear();AssetLimits.Clear();
        acceptedDecorationSource=null;
        ShoreCells=WaterRocks=HighlandShadows=ObjectShadows=0;
    }
    private static void Release(UnityEngine.Object item)
    { if(Application.isPlaying)Destroy(item);else DestroyImmediate(item); }
    private void OnDestroy(){ClearOwned();}
}

internal sealed class MapSurfaceV4AnimatedTile : TileBase
{
    internal Sprite[] Frames;
    internal float Speed,Phase;
    public override void GetTileData(Vector3Int position,ITilemap tilemap,ref TileData data)
    {data.sprite=Frames[0];data.color=Color.white;data.transform=Matrix4x4.identity;data.flags=TileFlags.LockTransform|TileFlags.LockColor;data.colliderType=Tile.ColliderType.None;}
    public override bool GetTileAnimationData(Vector3Int position,ITilemap tilemap,ref TileAnimationData data)
    {data.animatedSprites=Frames;data.animationSpeed=Speed;data.animationStartTime=Phase;return true;}
}

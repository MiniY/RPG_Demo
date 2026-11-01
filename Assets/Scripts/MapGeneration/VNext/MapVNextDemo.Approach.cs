using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MapVNext
{
    public sealed partial class MapVNextDemo
    {
        readonly Dictionary<int,EcologyNavigationGraph> approachGraphs=new Dictionary<int,EcologyNavigationGraph>();
        readonly List<string> ApproachPlans=new List<string>{"case,generation,level,startNode,goalNode,pathNodes,projectionIdentity,footprintIdentity"};
        EcologyNavigationGraph ApproachGraph(int level)
        {
            if(approachGraphs.TryGetValue(level,out var cached))return cached;
            // Existing graph/BFS, fed by the current renderer's real physics shapes.
            // The retained MapCandidateCollisionGeometry.Capture reconstructs old cell cliffs;
            // it cannot represent this explicit native-slope fixture without losing the adapter.
            var hard=new HashSet<Collider2D>(Terrain.TerrainCollisionTilemap.GetComponents<Collider2D>());
            hard.Add(Terrain.WaterBaseTilemap.transform.parent.GetComponentsInChildren<PolygonCollider2D>().Single(c=>c.name=="GeneratedCliffEdges"));
            float radius=Foot.radius+Physics2D.defaultContactOffset;
            var surface=Terrain.GetComponent<MapVNextSurfaceQuery>();var footprint=MapVNextGroundFootprint.Circle(radius);
            bool ClearPoint(Vector2 p)=>surface.CanOccupy(footprint,p,level,out _)&&!Physics2D.OverlapCircleAll(p,radius,Physics2D.AllLayers).Any(hard.Contains);
            bool ClearEdge(Vector2 a,Vector2 b)=>surface.CanSweep(footprint,a,b,level,out _)&&!Physics2D.CircleCastAll(a,radius,(b-a).normalized,Vector2.Distance(a,b),Physics2D.AllLayers).Any(hit=>hard.Contains(hit.collider));
            int count=Map.Width*Map.Height;var legal=new bool[count];var masks=new byte[count];
            Vector2Int Cell(int id)=>Map.Origin+new Vector2Int(id%Map.Width,id/Map.Width);
            int Id(Vector2Int c)=>(c.y-Map.Origin.y)*Map.Width+c.x-Map.Origin.x;
            var dirs=new[]{Vector2Int.right,Vector2Int.up,Vector2Int.left,Vector2Int.down};
            for(int id=0;id<count;id++)
            {var c=Cell(id);legal[id]=Map.IsWalkable(c)&&Map.GetElevation(c)==level&&ClearPoint(Center(c));}
            for(int id=0;id<count;id++)if(legal[id])
            for(int d=0;d<4;d++)
            {
                var a=Cell(id);var b=a+dirs[d];
                if(Map.IsInside(b)&&legal[Id(b)]&&Map.CanTraverse(a,b)&&ClearEdge(Center(a),Center(b)))masks[id]|=(byte)(1<<d);
            }
            var graph=new EcologyNavigationGraph(Guid.Parse(Generation),Map.Origin,Map.Width,Map.Height,
                MapVNextFixture.Digest(Map)+"-actual-collider-approach-H"+level,Foot.GetInstanceID()+":circle:"+F(Foot.radius)+":skin:"+F(Physics2D.defaultContactOffset),legal,masks);
            approachGraphs.Add(level,graph);return graph;
        }
        List<Vector2Int> ApproachPath(Vector2 from,Vector2 target,int level)
        {
            var a=Terrain.Coordinates.WorldToCell(from);var b=Terrain.Coordinates.WorldToCell(target);var graph=ApproachGraph(level);
            int Id(Vector2Int p)=>(p.y-Map.Origin.y)*Map.Width+p.x-Map.Origin.x;
            Vector2Int Cell(int id)=>Map.Origin+new Vector2Int(id%Map.Width,id/Map.Width);
            int start=Id(a),goal=Id(b);Require(graph.IsLegal(start)&&graph.IsLegal(goal),"ApproachEndpointHasNoFootClearance:"+Current.Id);
            var field=graph.BuildReturnField(new[]{goal});Require(field.Distances[start]>=0,"PhysicalSameLevelApproachMissing:"+Current.Id);
            var path=new List<Vector2Int>{a};int node=start;
            for(int i=0;node!=goal&&i<graph.NodeCount;i++)
            {int next=field.Next[node];Require(graph.CanMove(node,next),"ApproachGraphEdgeMismatch");path.Add(Cell(next));node=next;}
            Require(node==goal,"ApproachPathDidNotTerminate");
            ApproachPlans.Add(string.Join(",",Current.Id,Generation,level,start,goal,string.Join("|",path.Select(Id)),graph.ProjectionIdentity,graph.FootprintIdentity));
            return path;
        }
        public void ValidateSeedApproachRoutes()
        {
            string before=MapVNextFixture.Digest(Map);Vector2 origin=Center(Map.SpawnCell),cursor=origin;
            foreach(var ramp in ramps){ApproachPath(cursor,ramp.Entry,ramp.LowerElevation);cursor=ramp.Exit;}
            Vector2 goal=Center(Map.ExitCell);ApproachPath(cursor,goal,4);cursor=goal;
            foreach(var ramp in ramps.AsEnumerable().Reverse()){ApproachPath(cursor,ramp.Exit,ramp.UpperElevation);cursor=ramp.Entry;}
            ApproachPath(cursor,origin,0);Require(before==MapVNextFixture.Digest(Map),"ApproachPlanningMutatedMapData");
        }
        IEnumerator OnLevelToWithClearance(Vector2 target,CaseEvidence e)
        {
            int h=ActualElevation;var path=ApproachPath(FootPosition,target,h);
            yield return MoveToR3(Center(path[0]),false,e);
            for(int i=1;i<path.Count;i++)yield return MoveToR3(Center(path[i]),false,e);
            yield return MoveToR3(target,false,e);Require(ActualElevation==h,"ApproachChangedElevation");
        }
    }
}

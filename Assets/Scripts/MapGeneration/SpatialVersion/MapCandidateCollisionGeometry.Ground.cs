using System;
using System.Collections.Generic;
using UnityEngine;

public sealed partial class MapCandidateCollisionGeometry
{
    private readonly struct GroundPoint
    {
        internal readonly double X,Y;
        internal GroundPoint(double x,double y) {X=x;Y=y;}
    }
    private static double GroundCross(GroundPoint a,GroundPoint b,GroundPoint c) => (b.X-a.X)*(c.Y-a.Y)-(b.Y-a.Y)*(c.X-a.X);
    private static double GroundArea(IReadOnlyList<GroundPoint> polygon)
    {
        double area=0;
        for(int i=1;i+1<polygon.Count;i++) area+=GroundCross(polygon[0],polygon[i],polygon[i+1]);
        return area;
    }
    public static void ValidateGroundPolygon(Vector2[] points)
    {
        if(points==null || points.Length<3) throw new ArgumentException("GroundPolygonRequired");
        var work=new List<GroundPoint>();double ox=points[0].x,oy=points[0].y;
        foreach(Vector2 point in points)
        {
            if(!Finite(point)) throw new ArgumentException("FiniteGroundPolygonRequired");
            work.Add(new GroundPoint((double)point.x-ox,(double)point.y-oy));
        }
        double area=GroundArea(work);if(area==0) throw new ArgumentException("DegenerateGroundPolygon");
        double sign=area>0?1:-1;
        for(int i=0;i<work.Count;i++)
            if(sign*GroundCross(work[i],work[(i+1)%work.Count],work[(i+2)%work.Count])<0) throw new ArgumentException("ConvexGroundPolygonRequired");
    }
    // Renderer-only declarations have no Box2D contact skin. Intersect actual
    // interiors against the captured forbidden shapes; shared edges are contact.
    // Work points remain double throughout: float intersection rounding can erase
    // a small but real penetration. Native collider/contact routines are unchanged.
    public static bool GroundInteriorsOverlap(Vector2[] convexGround, Vector2[] obstacle)
    {
        ValidateGroundPolygon(convexGround);
        if(obstacle==null || obstacle.Length<3) throw new ArgumentException("ObstaclePolygonRequired");
        foreach(Vector2 point in obstacle) if(!Finite(point)) throw new ArgumentException("FiniteObstaclePolygonRequired");
        return GroundInteriorsOverlapValidated(convexGround,obstacle);
    }
    private static bool GroundInteriorsOverlapValidated(Vector2[] convexGround,Vector2[] obstacle)
    {
        double ox=convexGround[0].x,oy=convexGround[0].y;
        GroundPoint Point(Vector2 p) => new GroundPoint((double)p.x-ox,(double)p.y-oy);
        var ground=new List<GroundPoint>();foreach(Vector2 p in convexGround) ground.Add(Point(p));
        double sign=GroundArea(ground)>0?1:-1;
        var points=new List<GroundPoint>();foreach(Vector2 p in obstacle) points.Add(Point(p));
        for(int edge=0;edge<ground.Count && points.Count>0;edge++)
        {
            GroundPoint a=ground[edge],b=ground[(edge+1)%ground.Count];
            var next=new List<GroundPoint>();GroundPoint previous=points[points.Count-1];double previousSide=sign*GroundCross(a,b,previous);
            foreach(GroundPoint current in points)
            {
                double side=sign*GroundCross(a,b,current);
                if((side>=0)!=(previousSide>=0))
                {
                    double t=previousSide/(previousSide-side);
                    next.Add(new GroundPoint(previous.X+t*(current.X-previous.X),previous.Y+t*(current.Y-previous.Y)));
                }
                if(side>=0) next.Add(current);
                previous=current;previousSide=side;
            }
            points=next;
        }
        return Math.Abs(GroundArea(points))>0;
    }
}

"""Independent offline measurement of actual fixed-step feet against captured world polygons."""
import csv,math,json
from pathlib import Path

def point_segment(p,a,b):
    x,y=b[0]-a[0],b[1]-a[1];d=x*x+y*y
    t=max(0,min(1,((p[0]-a[0])*x+(p[1]-a[1])*y)/d)) if d else 0
    return math.hypot(p[0]-a[0]-t*x,p[1]-a[1]-t*y)

def inside(p,poly):
    hit=False
    for i,b in enumerate(poly):
        a=poly[i-1]
        if (a[1]>p[1])!=(b[1]>p[1]) and p[0]<(b[0]-a[0])*(p[1]-a[1])/(b[1]-a[1])+a[0]:hit=not hit
    return hit

def cross(a,b,c):return (b[0]-a[0])*(c[1]-a[1])-(b[1]-a[1])*(c[0]-a[0])
def segment_distance(a,b,c,d):
    if cross(a,b,c)*cross(a,b,d)<0 and cross(c,d,a)*cross(c,d,b)<0:return 0
    return min(point_segment(a,c,d),point_segment(b,c,d),point_segment(c,a,b),point_segment(d,a,b))

def measure(player,phase_prefix="CLIFF_"):
    geometries={}
    for path in (player/'CliffGeometry').glob('*.json'):
        data=json.loads(path.read_text('utf-8'))
        regions=[]
        for region in data['regions']:
            p=[(v['x'],v['y']) for v in region['vertices']]
            regions.append((region['id'],p,(min(v[0] for v in p),min(v[1] for v in p),max(v[0] for v in p),max(v[1] for v in p))))
        geometries[data['generation']]=regions
    identity=json.loads((player/"cliff-evidence.json").read_text("utf-8"))["identity"]
    samples=[];violations=[];previous=None;resets=0;checked=0;segments=0;max_gap=0;skipped_generations=0
    with (player/'movement.csv').open(encoding='utf-8-sig',newline='') as f:
        header=next(csv.reader([next(line for line in f if not line.startswith("#"))]))
        for line in f:
            if line.startswith('#'):previous=None;resets+=1;continue
            row=dict(zip(header,next(csv.reader([line]))))
            if row.get('trialPhase')=='DIAGNOSTIC_R1_GEOMETRY_EXPECTED_FAIL' or (phase_prefix is not None and not row.get('trialPhase','').startswith(phase_prefix)):previous=None;continue
            if row.get('generation') not in geometries:previous=None;skipped_generations+=1;continue
            if row['identity']!=identity:violations.append('Build mismatch');continue
            p=(float(row['footX']),float(row['footY']));radius=.30;threshold=radius-.002-0.000002
            a=p
            if previous and previous['generation']==row['generation'] and previous['trialPhase']==row['trialPhase']:
                a=previous['point'];segments+=1
                max_gap=max(max_gap,float(row['physicsTime'])-float(previous['physicsTime']))
            for rid,poly,bounds in geometries[row['generation']]:
                if max(a[0],p[0])+radius<bounds[0] or min(a[0],p[0])-radius>bounds[2] or max(a[1],p[1])+radius<bounds[1] or min(a[1],p[1])-radius>bounds[3]:continue
                checked+=1
                distance=min(segment_distance(a,p,poly[i-1],poly[i]) for i in range(len(poly)))
                if inside(p,poly) or inside(a,poly) or distance<threshold:
                    violations.append(dict(region=rid,phase=row['trialPhase'],position=p,physicsTime=row['physicsTime'],clearance=distance-radius))
            previous={**row,'point':p};samples.append(row)
    return dict(status='PASS' if not violations and samples else 'FAIL',samples=len(samples),sweptSegments=segments,nearRegionChecks=checked,skippedGenerationSamples=skipped_generations,resetMarkers=resets,maxFixedSampleGap=max_gap,contactTolerance=.002,violations=violations[:50],method='Independent Python segment-to-polygon distances on actual CSV feet and runtime polygon readback; .000002 world CSV rounding allowance, not a new gameplay tolerance.')


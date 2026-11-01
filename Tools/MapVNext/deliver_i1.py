"""I1 formal integration report over immutable Build/Run evidence. Does not launch Unity."""
from pathlib import Path
import argparse,csv,json,html,re,shutil,hashlib,zipfile,copy
from urllib.parse import unquote,urlsplit
from html.parser import HTMLParser
from PIL import Image
from deliver_readable import tree,sha,save,read
from cliff_motion import inside,segment_distance
HERE=Path(__file__).resolve().parent

def postbuild_module(build_id):
    if build_id=='map-vnext-i1-integration-20261031-a41':
        import i1_postbuild_a41 as module
        return module
    if build_id=='map-vnext-i1-integration-20261031-a40':
        import i1_postbuild_a40 as module
        return module
    if build_id=='map-vnext-i1-integration-20261031-a39':
        import i1_postbuild_a39 as module
        return module
    if build_id=='map-vnext-i1-diagnostic-20261031-a38':raise RuntimeError('DiagnosticA38NotEligibleForDelivery')
    if build_id=='map-vnext-i1-integration-20261030-a14':
        import i1_postbuild_qualification as module
        return module
    if build_id=='map-vnext-i1-diagnostic-20261030-a16':
        import i1_postbuild_a16 as module
        return module
    if build_id=='map-vnext-i1-integration-20261030-a17':
        import i1_postbuild_a17 as module
        return module
    if build_id=='map-vnext-i1-integration-20261030-a20':
        import i1_postbuild_a20 as module
        return module
    if build_id=='map-vnext-i1-integration-20261030-a21':
        import i1_postbuild_a21 as module
        return module
    if build_id=='map-vnext-i1-integration-20261031-a34':
        import i1_postbuild_a34 as module
        return module
    if build_id=='map-vnext-i1-diagnostic-20261031-a37':
        raise RuntimeError('DiagnosticA37NotEligibleForDelivery')
    return None

REQUIRED=('title','purpose','look','actions','normal','abnormal','verification')
SEEDS=[21193,1,-1,0,42,103,2026,7727,31013,65537,104729,2147483647,314159,271828,8675309,123456789,-2147483648,-2026,-42,17,73,4099,65521,987654321]

def postbuild_export_folders(admission):
    return [Path(admission.QUAL_REL).name,Path(admission.REVIEW_REL).name]

def phase_evidence_names(build_id,phase):
    if build_id not in ('map-vnext-i1-integration-20261030-a17','map-vnext-i1-integration-20261030-a20','map-vnext-i1-integration-20261030-a21','map-vnext-i1-integration-20261031-a34','map-vnext-i1-integration-20261031-a39','map-vnext-i1-integration-20261031-a40','map-vnext-i1-integration-20261031-a41') or phase=='restore':return []
    names=['combat-input.csv','movement.csv']
    if build_id in ('map-vnext-i1-integration-20261030-a20','map-vnext-i1-integration-20261030-a21','map-vnext-i1-integration-20261031-a34','map-vnext-i1-integration-20261031-a39','map-vnext-i1-integration-20261031-a40','map-vnext-i1-integration-20261031-a41') and phase=='pilot':names.append('consumer-lifecycle.jsonl')
    if build_id=='map-vnext-i1-integration-20261030-a21' and phase=='pilot':names.append('consumer-visibility-search.csv')
    if build_id in ('map-vnext-i1-integration-20261031-a34','map-vnext-i1-integration-20261031-a39','map-vnext-i1-integration-20261031-a40','map-vnext-i1-integration-20261031-a41') and phase=='pilot':names+=['consumer-visibility-search.csv','consumer-pursuit-prediction.jsonl','consumer-setup-phase.jsonl','consumer-native-motion.csv']
    return names

def require_battle_evidence(runroot,build_id,phase):
    names=phase_evidence_names(build_id,phase)
    for name in names:
        if not (Path(runroot)/'Player'/name).is_file():raise RuntimeError('ActualPhaseEvidenceRequired:'+phase+':'+name)
    return names

def require_disjoint(*paths):
    resolved=[Path(p).resolve() for p in paths]
    for i,a in enumerate(resolved):
        for b in resolved[i+1:]:
            if a.is_relative_to(b) or b.is_relative_to(a):raise RuntimeError('InputOutputPathsOverlap')

def require_input_status(row):
    if row.get('status')=='READY_SAVED_REOPENED':return True
    if row.get('status')=='BOUNDED_REJECTION_NOT_READY' and any(row.get('reason','').startswith(code+':') for code in ['VNextFormalLayoutRejected','EcologyBudgetExhaustedOnFixedTerrain']):return False
    raise RuntimeError('UnknownOrFailedInputCannotBecomeRejection')

def require_run(runroot,phase,project,executable=None):
    receipt=read(runroot/'receipt.json');sup=read(runroot/'supervisor.json');request=read(runroot/'command.json');after=read(runroot/'guard-after.json');native=read(runroot/'native-trace.json')
    cleanup=sup.get('cleanup',{})
    if receipt.get('status')!='PASS_LIFECYCLE_ONLY' or receipt.get('phase')!=phase or receipt.get('runId')!=sup.get('runId') or receipt.get('naturalExit') is not True or receipt.get('exitCode')!=0 or receipt.get('buildInputsAndTreeMatched') is not True or receipt.get('cleanup')!=cleanup:raise RuntimeError('RunReceiptDoesNotMatchSupervisor')
    if sup.get('operation')!=phase or sup.get('state')!='COMPLETED' or sup.get('launched') is not True or sup.get('naturalExit') is not True or sup.get('exitCode')!=0 or cleanup.get('status')!='PASS' or cleanup.get('forced') or cleanup.get('residualPids') or cleanup.get('errors') or not all(sup.get('locks',{}).get(k,{}).get('released') is True for k in ['project','heavy']):raise RuntimeError('UnqualifiedSupervisorLifecycle')
    process=sup.get('process',{});roots=[r for r in native.get('records',[]) if r.get('pid')==native.get('root')]
    if receipt.get('trace')!='VALID_NATIVE_TIME_LINE' or native.get('status')!=receipt.get('trace') or len(roots)!=1:raise RuntimeError('RunNativeRootIdentityMismatch')
    native_root=roots[0]
    if not all(process.get(k) is not None and native_root.get(k)==process[k] for k in ['pid','creationTime100ns','createdAtUtc','imagePath']) or native.get('root')!=process.get('pid') or native_root.get('command')!=sup.get('command'):raise RuntimeError('RunNativeRootIdentityMismatch')
    if after.get('state')!='FREE' or Path(sup['projectPath']).resolve()!=project.resolve() or request['command']!=sup.get('command') or Path(process['imagePath']).resolve()!=Path(request['command'][0]).resolve():raise RuntimeError('RunProjectProcessOrTimelineMismatch')
    if not Path(request['runtimeRoot']).resolve().is_relative_to(runroot.parents[1]/'RuntimeData'):raise RuntimeError('RunPrivateDataRootMissing')
    if executable is not None and Path(request['command'][0]).resolve()!=executable.resolve():raise RuntimeError('RunExecutableMismatch')
    return receipt,request

def require_build(source,build,build_id,digest):
    report=read(build/'BuildReport.json');validation=read(build/'BuildValidation.json');receipt_path=Path(validation['receiptPath']).resolve()
    if not receipt_path.is_relative_to(source/'VersionedRuns') or receipt_path.name!='receipt.json' or sha(receipt_path)!=validation.get('receiptSha256'):raise RuntimeError('BuildReceiptHashOrPathMismatch')
    admission=postbuild_module(build_id)
    if admission is not None:
        qualification=admission.verified_qualification(source.parents[2],build,build_id,digest)
        return build/'MVP_Windows_x64/RPG_Demo_MapVNext_I1.exe',qualification
    if report.get('status')!='BUILD_SUCCEEDED_PLAYER_NOT_RUN' or report.get('identity')!=build_id or report.get('implementationDigest')!=digest or report.get('errors')!=0 or validation.get('identity')!=build_id or validation.get('status')!='BUILD_INPUTS_AND_LIFECYCLE_VERIFIED' or validation.get('fontDeltaQualified') is not True:raise RuntimeError('UnqualifiedBuild')
    receipt,request=require_run(receipt_path.parent,'build',source.parents[2])
    if Path(request['buildRoot']).resolve()!=build or 'MapVNextI1Build.Build' not in request['command']:raise RuntimeError('BuildCommandMismatch')
    executable=Path(report['executable']).resolve()
    if executable!=build/'MVP_Windows_x64/RPG_Demo_MapVNext_I1.exe' or tree(build/'MVP_Windows_x64')!=read(build/'build-tree.json'):raise RuntimeError('BuildTreeOrExecutableMismatch')
    if read(receipt_path.parent/'build-inputs-after.json')!=read(build/'build-inputs.json'):raise RuntimeError('BuildInputsChanged')
    return executable,None

def require_capture_labels(frames,inputs,phase):
    def exact(image,label,generation):
        rows=[r for r in frames if r['image']==image]
        if len(rows)!=1 or rows[0].get('phase')!=label or (generation is not None and rows[0].get('generation')!=generation):raise RuntimeError('ActualCapturePhaseOrGenerationMismatch:'+image)
        return rows[0]
    exact('Startup.png','Startup','')
    for row in inputs:
        if row['status']=='READY_SAVED_REOPENED':
            exact(row['spawnImage'],'Spawn',row['generation']);exact(row['overviewImage'],'Overview',row['generation'])
    if phase=='restore' and not exact('FreshProcess-Restored.png','FreshProcessRestored',None).get('generation'):raise RuntimeError('FreshRestoreGenerationMissing')

def bind_replay(player,frames,build):
    worlds={read(p)['generation'] for p in (player/'World').glob('*.json') if read(p)['buildId']==build}
    movement=[]
    with (player/'movement.csv').open(encoding='utf-8-sig',newline='') as stream:
        header=next(csv.reader([next(stream)]))
        for line in stream:
            if not line.startswith('#'):movement.append(dict(zip(header,next(csv.reader([line])))))
    spans={(r['generation'],r['phase']) for r in movement if r.get('buildId')==build}
    for frame in frames:
        if frame['image'].startswith(('Motion/','Negatives/')) and (frame.get('buildId')!=build or frame.get('generation') not in worlds or (frame['generation'],frame['phase']) not in spans):raise RuntimeError('ReplayGenerationNotBoundToWorldAndMovement')

def require_owner(current,expected):
    if current!=expected:raise RuntimeError('OutputOwnerOrExactInputsChanged')

def require_phases(phases):
    if set(phases)!={'pilot','full','restore'} or len(set(phases.values()))!=3:raise RuntimeError('ThreeDistinctPlayerPhasesRequired')

def decode_png(path):
    with Image.open(path) as image:
        if image.format!='PNG':raise ValueError('ExpectedPNG')
        image.load();return image.size

def validate(model):
    errors=[];build=model.get('build');files=model.get('files',{})
    if model.get('humanAcceptance')!='PENDING' or model.get('mergeReady')!='NO':errors.append('Human or merge boundary expanded')
    if not build or not model.get('implementationDigest'):errors.append('Build identity missing')
    if model.get('fullRequestedSeeds')!=SEEDS:errors.append('Fixed 24 input denominator changed')
    seen=set()
    for unit in model.get('units',[]):
        if unit['id'] in seen:errors.append('Duplicate case')
        seen.add(unit['id'])
        for field in REQUIRED:
            if not isinstance(unit.get(field),str) or not re.search('[\u4e00-\u9fff]',unit[field]):errors.append(unit['id']+': Chinese '+field+' missing')
        try:
            ready=require_input_status(unit.get('input',{}))
            if ready!=(unit.get('status')=='PASS') or ready!=bool(unit.get('countsForReady')) or unit.get('kind')!=('playable' if ready else 'diagnostic'):errors.append('Input result classification mismatch')
        except RuntimeError:errors.append('Unknown/failed input is not a rejection')
        if unit.get('human')!='PENDING':errors.append('Unsupported case human pass')
        if unit.get('kind')!='playable' and unit.get('countsForTraversal'):errors.append('Diagnostic/material counted as playable')
        if unit.get('status')=='PASS' and (not unit.get('subject') or not unit.get('scope')):errors.append('PASS lacks verifier/scope')
        for medium in unit.get('media',[]):
            record=files.get(medium['path'])
            if not record or record.get('build')!=build or medium.get('build')!=build or record.get('runId')!=medium.get('runId'):errors.append('Build/Run/media mismatch')
    for gif in model.get('gifs',[]):
        if gif.get('identity')!=build or files.get(gif.get('path'),{}).get('sha256')!=gif.get('sha256') or len(gif.get('sourceFrames',[]))<2:errors.append('GIF identity/hash/source missing')
        for frame in gif.get('sourceFrames',[]):
            if files.get(frame.get('path'),{}).get('sha256')!=frame.get('sha256'):errors.append('GIF source changed')
    for frame in model.get('replay',[]):
        record=files.get(frame.get('path'))
        if not record or frame.get('buildId')!=build or record.get('build')!=build or record.get('runId')!=frame.get('runId'):errors.append('Replay Build/Run/frame mismatch')
    if [u.get('input',{}).get('requestedSeed') for u in model.get('units',[])]!=SEEDS:errors.append('Cases do not preserve exact requested inputs')
    if len(model.get('units',[]))!=24:errors.append('Missing attempted case')
    return errors

def independent_motion(player,build):
    geometries={}
    for path in (player/'World').glob('*.json'):
        data=read(path)
        if data['buildId']!=build:raise RuntimeError('WorldBuildMismatch')
        regions=[]
        for region in data['rock']:
            p=[(v['x'],v['y']) for v in region['vertices']]
            regions.append((region['id'],p,(min(x for x,y in p),min(y for x,y in p),max(x for x,y in p),max(y for x,y in p))))
        if data['generation'] in geometries and geometries[data['generation']]!=regions:raise RuntimeError('GenerationGeometryChanged')
        geometries[data['generation']]=regions
    violations=[];samples=segments=near=resets=0;previous=None;phases={};gap=0
    with (player/'movement.csv').open(encoding='utf-8-sig',newline='') as stream:
        header=next(csv.reader([next(stream)]))
        for line in stream:
            if line.startswith('#'):previous=None;resets+=1;continue
            row=dict(zip(header,next(csv.reader([line]))));gen=row['generation'];p=(float(row['footX']),float(row['footY']))
            if row['buildId']!=build or gen not in geometries:raise RuntimeError('UnboundMovementSample:'+gen)
            a=p
            if previous and previous['generation']==gen:
                a=previous['point'];segments+=1;gap=max(gap,float(row['physicsTime'])-float(previous['physicsTime']))
            for rid,poly,bounds in geometries[gen]:
                if max(a[0],p[0])+.30<bounds[0] or min(a[0],p[0])-.30>bounds[2] or max(a[1],p[1])+.30<bounds[1] or min(a[1],p[1])-.30>bounds[3]:continue
                near+=1;distance=min(segment_distance(a,p,poly[i-1],poly[i]) for i in range(len(poly)))
                if inside(a,poly) or inside(p,poly) or distance<.30-.002-.000002:violations.append({'generation':gen,'phase':row['phase'],'time':row['physicsTime'],'region':rid,'clearance':distance-.30})
            key=gen+':'+row['phase'];item=phases.setdefault(key,{'samples':0,'dual':0,'minHeight':int(row['height']),'maxHeight':int(row['height'])});item['samples']+=1
            item['dual']+=abs(float(row['moveX']))>.5 and abs(float(row['moveY']))>.5
            item['minHeight']=min(item['minHeight'],int(row['height']));item['maxHeight']=max(item['maxHeight'],int(row['height']))
            previous={**row,'point':p};samples+=1
    return {'status':'PASS' if samples and not violations else 'FAIL','subject':'Independent Python geometry measurement','scope':'Every captured actual input foot and inter-sample segment vs same-generation runtime rock polygons','samples':samples,'segments':segments,'nearRegionChecks':near,'setupResets':resets,'maximumPhysicsTimeGap':gap,'contactTolerance':.002,'csvRoundingAllowance':.000002,'violations':violations[:50],'phases':phases}

def check_exported_player_evidence(root,model):
    if model.get('build') not in ('map-vnext-i1-integration-20261031-a39','map-vnext-i1-integration-20261031-a40','map-vnext-i1-integration-20261031-a41'):return
    root=Path(root).resolve();runs=model.get('playerRuns',{});require_phases(runs);expected_replay=[]
    def registered(path,run):
        path=Path(path).resolve()
        if not path.is_relative_to(root) or not path.is_file():raise RuntimeError('ExportedOriginalEvidenceMissingOrEscapes')
        record=model['files'].get(path.relative_to(root).as_posix(),{})
        if record.get('sha256')!=sha(path) or record.get('build')!=model['build'] or record.get('runId')!=run:raise RuntimeError('ExportedOriginalEvidenceIndexMismatch')
        return path
    for phase,run in runs.items():
        if not isinstance(run,str) or not re.fullmatch('[A-Za-z0-9_-]+',run):raise RuntimeError('ExportedRunPathInvalid')
        player=root/'Media'/run;evidence=root/'Evidence/Runs'/run
        body=read(registered(player/'i1-player-evidence.json',run))
        if (body.get('buildId'),body.get('implementationDigest'),body.get('phase'),body.get('status'))!=(model['build'],model['implementationDigest'],phase,'AUTOMATED_CHECKS_COMPLETE_HUMAN_PENDING'):raise RuntimeError('ExportedPlayerIdentityOrStatusMismatch')
        with registered(player/'frames.csv',run).open(encoding='utf-8-sig',newline='') as stream:frames=list(csv.DictReader(stream))
        if any(f.get('buildId')!=model['build'] for f in frames):raise RuntimeError('ExportedFrameBuildMismatch')
        require_capture_labels(frames,body['inputs'],phase)
        for name in phase_evidence_names(model['build'],phase):registered(player/name,run)
        for f in frames:registered(player/f['image'],run)
        if phase=='restore':
            # This source path does not export World or movement. Bind the frame
            # to the actual first commit and the raw copied checkpoint instead.
            with registered(player/'movement.csv',run).open(encoding='utf-8-sig',newline='') as stream:
                if list(csv.DictReader(line for line in stream if not line.startswith('#'))):raise RuntimeError('FreshRestoreHasUnexpectedMovementScope')
            if body['inputs'] or any(f['image'].startswith(('Motion/','Negatives/')) for f in frames):raise RuntimeError('FreshRestoreHasUnexpectedTraversalScope')
            info=read(registered(evidence/'restore-input.json',run));copied=registered(evidence/'restore-input-copy.json',run)
            if info.get('originalFormalDataUsed') is not False or sha(copied)!=info.get('sha256'):raise RuntimeError('FreshRestoreCopiedInputMismatch')
            checkpoint=read(copied);log=registered(evidence/'player.log',run).read_bytes().decode('utf-8','replace')
            commits=re.findall(r'Map generation committed: GenerationId=([0-9a-fA-F-]{36}); RequestedSeed=(-?\d+); ActualSeed=(-?\d+);',log)
            restored=next(f for f in frames if f['image']=='FreshProcess-Restored.png')
            if not commits or restored['generation']!=commits[0][0] or checkpoint['generation']['requestedSeed']!=int(commits[0][1]) or 'host='+model['build'] not in log:raise RuntimeError('FreshRestoreLogCheckpointMismatch')
            continue
        worlds=set()
        for path in (player/'World').glob('*.json'):
            data=read(registered(path,run))
            if data.get('buildId')!=model['build']:raise RuntimeError('ExportedWorldBuildMismatch')
            worlds.add(data['generation'])
        for f in frames:
            if f['image']!='Startup.png' and f.get('generation') not in worlds:raise RuntimeError('ExportedFrameGenerationNotInWorld')
        registered(player/'movement.csv',run);bind_replay(player,frames,model['build'])
        if phase=='pilot':
            expected_replay=[{**f,'path':(Path('Media')/run/f['image']).as_posix(),'runId':run} for f in frames if f['image'].startswith(('Motion/','Negatives/'))]
        if phase=='full':
            if [x['requestedSeed'] for x in body['inputs']]!=SEEDS or [u['input'] for u in model['units']]!=body['inputs']:raise RuntimeError('ExportedFullInputsDoNotMatchOriginal')
    if model.get('replay')!=expected_replay:raise RuntimeError('ExportedReplayNotExactOriginalFrames')

class Links(HTMLParser):
    def __init__(self):super().__init__();self.links=[]
    def handle_starttag(self,tag,attrs):
        for k,v in attrs:
            if k in ('src','href') and v:self.links.append(v)

A41_CAPTURE_HELPER_SHA='120f1296df17d18fda03576a8af1f85e6ffe854eae3370769a000ccd17c696e1'
A41_CAPTURE_FFMPEG_SHA='2ce797a0f88d7f067180338fb227f7b1928ea727bd9a4d7a1d022f7c52af71a3'
A41_VIDEO_BASE='Evidence/Reviewed/Authoring/VersionedExtension/FinalVerification-a41/'
A41_WGC_HELPER_SHA='0ec2664aeedf83e4299da01261df1c6a7c9e153945d26c0196d366f6bb2a4268'
A41_WGC_BACKEND_SHA='ab0ba278165183e7af106080de94e112f15da2aa65143e06b706e7a2871bc875'
A41_WGC_SOURCE_SHA='c8ae34d516f9468e42fddb8c53bc3e6cc843f2c94c9a22618949d0df93b4764a'
# Media validation and generation use the same explicit admission export group.
A41_WGC_EXPORT='Evidence/PostBuild/'+postbuild_export_folders(postbuild_module('map-vnext-i1-integration-20261031-a41'))[1]+'/Tools/MapVNext/'
A41_VIDEO_HELPER_EXPORT=A41_WGC_EXPORT+'record_i1_player.py'

def decode_continuous_video(path):
    # Decode the entire file, fail on corrupt packets, and read actual stream metadata.
    import imageio_ffmpeg,subprocess,os
    ff=Path(imageio_ffmpeg.get_ffmpeg_exe())
    if sha(ff)!=A41_CAPTURE_FFMPEG_SHA:raise RuntimeError('ContinuousVideoUnknownDecoder')
    absolute=str(Path(path).resolve())
    if os.name=='nt' and not absolute.startswith('\\\\?\\'):absolute='\\\\?\\'+absolute
    command=[str(ff),'-hide_banner','-xerror','-err_detect','explode','-progress','pipe:1','-nostats','-i',absolute,'-map','0:v:0','-an','-f','null','-']
    try:result=subprocess.run(command,capture_output=True,text=True,encoding='utf-8',errors='replace',timeout=180)
    except (OSError,subprocess.TimeoutExpired) as error:raise RuntimeError('ContinuousVideoActualDecodeFailed:'+str(error)) from error
    if result.returncode!=0 or 'progress=end' not in result.stdout:raise RuntimeError('ContinuousVideoActualDecodeFailed:'+result.stderr[-600:])
    line=next((l for l in result.stderr.splitlines() if 'Stream #' in l and 'Video:' in l),None)
    size=re.search(r'\b(\d{2,5})x(\d{2,5})\b',line or '');rate=re.search(r'\b([\d.]+) fps\b',line or '')
    frames=re.findall(r'(?m)^frame=(\d+)$',result.stdout)
    if not size or not rate or not frames:raise RuntimeError('ContinuousVideoActualStreamMetadataMissing')
    value={'width':int(size[1]),'height':int(size[2]),'fps':float(rate[1]),'decodedFrames':int(frames[-1])}
    if value['decodedFrames']<2 or abs(value['fps']-12)>0.00001:raise RuntimeError('ContinuousVideoActualFrameRateOrLengthRejected')
    return value

def validate_continuous_video_evidence(root,model):
    if model.get('build')!='map-vnext-i1-integration-20261031-a41':return []
    root=Path(root).resolve();base=A41_VIDEO_BASE+'ContinuousVideo/'
    candidates=[p for p in model.get('files',{}) if p.startswith(base) and p.endswith('.mp4')]
    if not candidates:return []
    def original(path):
        p=(root/path).resolve();record=model.get('files',{}).get(path)
        if not p.is_relative_to(root) or not p.is_file() or not record or record.get('sha256')!=sha(p):raise RuntimeError('ContinuousVideoMissingOrChangedOriginal:'+path)
        return p
    def norm(value):return str(value).replace('\\','/').lower()
    def stamp(value):
        import datetime
        try:return datetime.datetime.fromisoformat(value.replace('Z','+00:00')).timestamp()
        except (TypeError,ValueError,AttributeError):raise RuntimeError('ContinuousVideoInvalidTimestamp')
    quality=read(original(A41_VIDEO_BASE+'video-quality-verification.json'))
    if (quality.get('status'),quality.get('buildId'),quality.get('implementationDigest'),quality.get('visualReview'))!=('ACTUAL_OWNED_WINDOW_VIDEO_DECODED_AND_VISUALLY_VERIFIED',model['build'],model['implementationDigest'],'AGENT_FRAME_REVIEW_NOT_USER_ACCEPTANCE'):raise RuntimeError('ContinuousVideoQualityIdentityOrReviewMissing')
    if sha(original(A41_VIDEO_HELPER_EXPORT))!=A41_CAPTURE_HELPER_SHA:raise RuntimeError('ContinuousVideoUnknownCaptureImplementation')
    verified=[]
    for path in candidates:
        video=original(path);record=model['files'][path];run=record.get('runId')
        if record.get('build')!=model['build'] or run not in model.get('playerRuns',{}).values() or path!=base+run+'/owned-player-continuous.mp4':raise RuntimeError('ContinuousVideoBuildOrRunMismatch')
        directory=path.rsplit('/',1)[0]+'/'
        startpath=original(directory+'start.json');start=read(startpath);end=read(original(directory+'capture-result.json'))
        if (start.get('status'),start.get('buildId'),start.get('run'))!=('OWNED_WINDOW_CAPTURE_STARTED_NOT_GAMEPLAY_PASS',model['build'],run):raise RuntimeError('ContinuousVideoCaptureStartIdentityMismatch')
        if (end.get('status'),end.get('buildId'),end.get('run'),end.get('ffmpegExitCode'),end.get('noPlayerControlOrTermination'),end.get('captureOnlyExactWindow'))!=('CAPTURE_CLOSED_CONTENT_AND_RUN_VALIDATION_PENDING',model['build'],run,0,True,True) or end.get('stopReason') not in ['EXACT_PLAYER_EXITED','OWNED_WINDOW_UNAVAILABLE']:raise RuntimeError('ContinuousVideoCaptureDidNotCloseLawfully')
        if end.get('startSha256')!=sha(startpath) or end.get('videoSha256')!=sha(video) or end.get('videoBytes')!=video.stat().st_size:raise RuntimeError('ContinuousVideoCaptureBytesMismatch')
        wgc=start.get('captureMethod')=='WGC_HWND_V1'
        expected_helper=A41_WGC_HELPER_SHA if wgc else A41_CAPTURE_HELPER_SHA
        if start.get('captureMethod') not in [None,'WGC_HWND_V1'] or start.get('captureScriptSha256')!=expected_helper or start.get('ffmpegSha256')!=A41_CAPTURE_FFMPEG_SHA:raise RuntimeError('ContinuousVideoUnknownCaptureDependency')
        if wgc:
            for relative,expected in [('record_i1_wgc.py',A41_WGC_HELPER_SHA),('native/OwnedWindowCapture.exe',A41_WGC_BACKEND_SHA),('native/OwnedWindowCapture.cpp',A41_WGC_SOURCE_SHA)]:
                if sha(original(A41_WGC_EXPORT+relative))!=expected:raise RuntimeError('ContinuousVideoUnknownWGCImplementation')
            if start.get('backendSha256')!=A41_WGC_BACKEND_SHA or start.get('backendSourceSha256')!=A41_WGC_SOURCE_SHA or end.get('captureMethod')!='WGC_HWND_V1' or end.get('backendExitCode')!=0 or end.get('forcedOwnedChildren')!=[] or end.get('secondaryErrors')!=[] or end.get('primaryFailure') is not None or type(end.get('sourceFrames')) is not int or end['sourceFrames']<2 or type(end.get('sourceUpdates')) is not int or end['sourceUpdates']<2:raise RuntimeError('ContinuousVideoWGCCaptureFailed')
        prefix='Evidence/Runs/'+run+'/'
        sup=read(original(prefix+'supervisor.json'));native=read(original(prefix+'native-trace.json'));receipt=read(original(prefix+'receipt.json'));request=read(original(prefix+'command.json'))
        body=read(original('Media/'+run+'/i1-player-evidence.json'));process=sup.get('process',{})
        if (receipt.get('status'),sup.get('runId'),body.get('buildId'),body.get('implementationDigest'),body.get('status'))!=('PASS_LIFECYCLE_ONLY',receipt.get('runId'),model['build'],model['implementationDigest'],'AUTOMATED_CHECKS_COMPLETE_HUMAN_PENDING'):raise RuntimeError('ContinuousVideoActualRunNotQualified')
        roots=[r for r in native.get('records',[]) if r.get('pid')==native.get('root')]
        if native.get('status')!='VALID_NATIVE_TIME_LINE' or len(roots)!=1 or native.get('root')!=process.get('pid') or not process.get('pid'):raise RuntimeError('ContinuousVideoActualNativeRootMissing')
        native_root=roots[0]
        if any(native_root.get(k)!=process.get(k) or process.get(k) is None for k in ['pid','creationTime100ns','createdAtUtc','imagePath']) or native_root.get('command')!=sup.get('command') or request.get('command')!=sup.get('command'):raise RuntimeError('ContinuousVideoActualNativeIdentityMismatch')
        cleanup=sup.get('cleanup',{})
        if sup.get('operation')!='player' or sup.get('naturalExit') is not True or sup.get('exitCode')!=0 or cleanup.get('status')!='PASS' or cleanup.get('forced') or cleanup.get('residualPids') or any(sup.get('locks',{}).get(k,{}).get('released') is not True for k in ['project','heavy']):raise RuntimeError('ContinuousVideoActualPlayerLifecycleRejected')
        owner=start.get('process',{});window=start.get('window',{});hwnd=window.get('hwnd')
        if owner.get('ProcessId')!=process['pid'] or window.get('pid')!=process['pid'] or start.get('creationTime100ns')!=process['creationTime100ns'] or type(hwnd) is not int or hwnd<=0:raise RuntimeError('ContinuousVideoCapturedOwnerMismatch')
        command=request.get('command',[])
        if not command or norm(owner.get('ExecutablePath'))!=norm(process['imagePath']) or norm(start.get('expectedExe'))!=norm(command[0]) or norm(command[0])!=norm(process['imagePath']):raise RuntimeError('ContinuousVideoCapturedExecutableMismatch')
        exe=original('MVP_Windows_x64/RPG_Demo_MapVNext_I1.exe')
        if start.get('exeSha256')!=sha(exe):raise RuntimeError('ContinuousVideoCapturedExecutableBytesMismatch')
        if command.count('--i1-accept')!=1:raise RuntimeError('ContinuousVideoPlayerCommandMissing')
        output=command[command.index('--i1-accept')+1]
        if '--i1-accept' not in owner.get('CommandLine','') or norm(output) not in norm(owner.get('CommandLine','')):raise RuntimeError('ContinuousVideoCapturedCommandMismatch')
        ffcommand=start.get('command',[])
        def option(name):
            if ffcommand.count(name)!=1 or ffcommand.index(name)+1>=len(ffcommand):raise RuntimeError('ContinuousVideoCaptureCommandRejected')
            return ffcommand[ffcommand.index(name)+1]
        if wgc:
            backend=start.get('backendCommand',[]);width=start.get('sourceWidth');height=start.get('sourceHeight')
            if len(backend)!=4 or not norm(backend[0]).endswith('/tools/mapvnext/native/ownedwindowcapture.exe') or backend[1:]!=[str(hwnd),str(process['pid']),process['creationTime100ns']] or type(width) is not int or type(height) is not int or not 640<=width<=4096 or not 360<=height<=2400:raise RuntimeError('ContinuousVideoWGCOwnerOrSurfaceMismatch')
            if option('-i')!='pipe:0' or option('-f')!='rawvideo' or option('-pixel_format')!='bgra' or option('-video_size')!=str(width)+'x'+str(height) or option('-vf')!='pad=ceil(iw/2)*2:ceil(ih/2)*2':raise RuntimeError('ContinuousVideoCaptureCommandRejected')
        elif option('-i')!='hwnd='+str(hwnd) or option('-f')!='gdigrab':raise RuntimeError('ContinuousVideoCaptureCommandRejected')
        if option('-framerate')!='12' or '-an' not in ffcommand or not norm(ffcommand[-1]).endswith('/finalverification-a41/continuousvideo/'+run+'/owned-player-continuous.mp4'):raise RuntimeError('ContinuousVideoCaptureCommandRejected')
        began=stamp(start.get('utc'));ended=stamp(end.get('utc'));born=stamp(process.get('createdAtUtc'));exited=stamp(native_root.get('exitObservedAtUtc'))
        if not born-1<=began<=exited+1 or not began<ended<=exited+30:raise RuntimeError('ContinuousVideoCaptureOutsideActualRun')
        rows=[r for r in quality.get('validVideos',[]) if r.get('relativePath')==path.removeprefix('Evidence/Reviewed/')]
        if len(rows)!=1 or rows[0].get('run')!=run or rows[0].get('runId')!=receipt.get('runId') or rows[0].get('sha256')!=sha(video) or type(rows[0].get('collectorExitCode')) is not int or rows[0]['collectorExitCode']!=0:raise RuntimeError('ContinuousVideoQualityRowMismatch')
        decoded=decode_continuous_video(video)
        if decoded!=rows[0].get('decoded') or decoded['decodedFrames']/decoded['fps']>ended-began+3:raise RuntimeError('ContinuousVideoActualDecodeDoesNotMatchQualifiedClip')
        if wgc and (decoded['decodedFrames']!=end['sourceFrames'] or (decoded['width'],decoded['height'])!=((width+1)//2*2,(height+1)//2*2)):raise RuntimeError('ContinuousVideoWGCSurfaceOrFrameCountMismatch')
        verified.append(path)
    return verified

def check(root):
    root=Path(root);model=read(root/'manifest.json');errors=validate(model);checked=0
    admission=postbuild_module(model.get('build'))
    if admission is None:
        import i1_postbuild_qualification as admission
    try:admission.check_portable_evidence(root,model)
    except (RuntimeError,OSError,ValueError,KeyError) as error:errors.append('Post-build evidence: '+str(error))
    try:check_exported_player_evidence(root,model)
    except (RuntimeError,OSError,ValueError,KeyError) as error:errors.append('Exported original evidence: '+str(error))
    try:validate_continuous_video_evidence(root,model)
    except (RuntimeError,OSError,ValueError,KeyError) as error:errors.append('Continuous original video evidence: '+str(error))
    for path,record in model['files'].items():
        target=root/path
        if not target.resolve().is_relative_to(root.resolve()) or not target.is_file() or sha(target)!=record['sha256']:errors.append('Missing/changed file:'+path)
        elif target.suffix.lower()=='.gif':
            with Image.open(target) as im:
                if im.n_frames<2:errors.append('Invalid dynamic GIF:'+path)
                for index in range(im.n_frames):im.seek(index);im.load()
        elif target.suffix.lower()=='.png':
            try:decode_png(target);checked+=1
            except (OSError,ValueError) as error:errors.append('PNG decode failed:'+path+':'+str(error))
    parser=Links();parser.feed((root/'index.html').read_text('utf-8'))
    for href in parser.links:
        url=urlsplit(href)
        if url.scheme or url.netloc:errors.append('Nonportable external link:'+href);continue
        path=unquote(url.path)
        if path and (not (root/path).resolve().is_relative_to(root.resolve()) or not (root/path).exists()):errors.append('Broken link:'+href)
    if tree(root/'MVP_Windows_x64')!=model['buildTree']:errors.append('Full Player tree mismatch')
    for name in ('RPG_Demo_MapVNext_I1.exe','UnityPlayer.dll','RPG_Demo_MapVNext_I1_Data'):
        if not (root/'MVP_Windows_x64'/name).exists():errors.append('Incomplete Windows Player:'+name)
    result={'status':'PASS_STATIC_PACKAGE_CHECK' if not errors else 'FAIL','errors':errors,'pngDecoded':checked,'fullPlayerFiles':len(model['buildTree']),'browserPreview':'SEPARATE_RECEIPT_REQUIRED','humanAcceptance':'PENDING'}
    save(root/'Checks/package-check.json',result);return result

def render(root,model):
    h=lambda value:html.escape(str(value),quote=True)
    cards=[]
    for unit in model['units']:
        images=''.join('<a href="'+h(m['path'])+'"><img loading="lazy" src="'+h(m['path'])+'" alt="'+h(unit['title'])+'真实运行原图"></a>' for m in unit['media'] if m['path'].endswith('.png'))
        rows=''.join('<p><b>'+label+'：</b>'+h(unit[field])+'</p>' for field,label in [('purpose','用途'),('look','看点'),('actions','操作'),('normal','正常'),('abnormal','异常'),('verification','验证范围')])
        cards.append('<article data-seed="'+h(unit['input']['requestedSeed'])+'" data-status="'+h(unit['status'])+'"><h2>'+h(unit['title'])+'</h2><p>'+h(unit['id'])+' · '+h(unit['status'])+' · 人工待确认</p>'+rows+images+'<details><summary>实际请求与生成身份</summary><pre>'+h(json.dumps(unit['input'],ensure_ascii=False,indent=2))+'</pre></details></article>')
    intro='''<h1>随机高地进入完整游戏后的集成试玩</h1><p>这次检查：怪物、物品、出口和存档接上新地图后是否正常。石壁不能站人，也不能放东西；楼梯需要沿斜坡上下。</p><p class="pending">本包等待你审图和试玩。r7 已验收只覆盖此前候选，本集成版尚未人工通过。MERGE_READY=NO，未合入 main，未切正式默认，未提交或推送。</p><p>先打开 Windows 试玩程序。在启动选择画面左下输入 Seed，再按“Start selected seed”（用此种子开始）。WASD 移动，沿坡面同时按两个方向，靠近已解锁出口按 F。保存与恢复使用独立数据目录；要换 Seed，按 Esc 打开原暂停菜单并返回启动选择，再使用左下控件。</p><p>请依次检查：出生画面与五色高地 → 所有楼梯往返和纯 A/D 阻挡 → 怪物、掉落、出口 → 保存重开与死亡/换层。异常请记录 Build、Seed、按键和现象。</p><p>下面保留固定 24 次输入的全部结果。被严格拒绝的地图没有进入 GameplayReady（可开始玩的状态），不计成功样例。画面是当前同一 Build 的真实 Player 原图；全景临时调整相机且隐藏界面，未移动实体。</p>'''
    replay='<section><h2>楼梯真实输入采样回放</h2><p>这些是实际 Player 的采样原帧，按记录时刻和按键查看；不是完整连续录像。滑动进度或播放逐帧检查，原始 CSV 可以单独审计。</p><button id="play">播放采样帧</button><input type="range" id="frame" min="0" max="0" value="0"><p id="frameInfo"></p><img id="replayImage" style="width:100%" alt="实际运行采样回放"></section>'
    replay+=''.join('<p>原生尺寸采样 GIF（256色，审阅节奏；准确颜色见原 PNG）</p><img style="width:100%" src="'+h(g['path'])+'" alt="真实上下楼采样GIF">' for g in model.get('gifs',[]))
    cache_note='<p>本次 Build 原回执仍为 FAIL／BUILD_BLOCKED：TMP 构建过程清空了动态缓存。独立双轴核实精确差异后，仅一次归还源码原字节，158 个 Player 文件保持原样；本包使用单独受审的 '+h(model['build'])+' 后置追加资格。原运行输入曾变化，原失败没有改写。准入工具是后置审阅依赖，没有回编进此 Build；字体实际显示仍待你审阅。</p>' if model.get('postBuildQualification') else ''
    battle_note='<p>正向行走在需要时通过原鼠标左键自卫，耗体力、攻击延迟和伤害仍由原系统处理。实际按键与战斗回调见 CSV；无来源死亡只作观察，不算玩家伤害。A/D 负例仅用键盘，先让原攻击自然结束。消费者夹具在整条行走完成后另开 Generation，不能把它的恢复算作行走成功。</p>' if model['build'] in ('map-vnext-i1-integration-20261030-a17','map-vnext-i1-integration-20261030-a20','map-vnext-i1-integration-20261030-a21','map-vnext-i1-integration-20261031-a34','map-vnext-i1-integration-20261031-a39','map-vnext-i1-integration-20261031-a40','map-vnext-i1-integration-20261031-a41') else ''
    diagnostics='<details><summary>展开技术验证、证据与历史失败</summary>'+cache_note+battle_note+'<p>自动运行、生命周期、独立审阅与人工验收分别记录。AI/死亡/池复用验证包含明确摆位和直接调用原受伤入口的测试夹具；这不是自然战斗试玩。楼梯正向路线从出生点由真实输入步行完成，负例摆位单列。</p><p>原 MainV1 manifest 的 1220 项中，1197 原字节相同，23 项是闭包批准的源码差异；独立整组兼容检查另列，未重写旧清单。旧内容仍走旧几何，不宣称执行新版禁入规则。没有进行存档迁移。</p><p>原 r7 后代未退出失败和 I1 历史失败保留在源证据目录；新运行成功不改变旧结果。无严格同位置前后图时不冒充严格对比。素材不计入通行分母。</p>'+''.join('<p><a href="'+h(p)+'">'+h(p)+'</a></p>' for p in model['evidenceLinks'])+'</details>'
    if model['build']=='map-vnext-i1-integration-20261031-a34':intro+='<p class="pending">怪物验证范围：独立正向消费者场景固定为 Seed 7727。Seed 21193 在窄通道的居民互挡失败仍保留，未宣称修复，也不以另一张图的通过代替该结论。请试玩时留意怪物是否长时间互挡；本包仍等待人工验收。</p>'
    if model['build'] in ('map-vnext-i1-integration-20261031-a39','map-vnext-i1-integration-20261031-a40','map-vnext-i1-integration-20261031-a41'):intro+='<p class="pending">已知问题：历史 a34／a37 曾在 Seed 8675309 的楼梯接近阶段出现居民导航失效，根因仍未查明。后续运行未复现不能证明修复，本包仍有合入阻塞。请重点观察怪物是否停滞，以及楼梯往返是否异常。怪物追出领地再返回的独立正向消费者场景固定为 Seed 7727；Seed 21193 窄通道互挡的历史失败仍保留。</p>'
    if model['build']=='map-vnext-i1-integration-20261031-a41':intro+='<p class="pending">历史 a40 两次自动试玩的失败继续保留：一次在怪物碰撞处持续阻挡，另一次玩家死亡后验收驱动读取失效绑定。本版增加明确失败保护；怪物、人口、碰撞、生命值、按键与行走路线未改，不宣称这些阻挡已修复。仍需查看本版真实运行结果并人工验收。</p>'
    video_paths=validate_continuous_video_evidence(root,model)
    video_section=''
    if video_paths:
        video_section='<section id="continuous-video"><h2>连续运行录像</h2><p>静音窗口连续采集，12 帧每秒；开始时刻、实际长度、原进程身份和核验范围见同目录记录。原自动流程的截帧暂停仍保留，不将采样回放冒充连续录像。只列出本版实际 Run 的录像，人工验收仍待完成。</p>'+''.join('<p><a href="'+h(p)+'">打开本次 Player 连续录像</a></p><video controls preload="metadata" src="'+h(p)+'"></video>' for p in video_paths)+'</section>'
    doc='<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>I1 正式集成试玩与验收</title><style>body{background:#15151b;color:#eee;font:17px/1.65 system-ui;margin:0}main{max-width:1200px;margin:auto;padding:28px}h1,h2{line-height:1.3}a{color:#c9b5ff}article,section{background:#24232e;border:1px solid #494451;border-radius:9px;padding:22px;margin:22px 0}article img{width:49%;vertical-align:top}video{display:block;width:100%;max-height:75vh;background:#000}pre{white-space:pre-wrap;word-break:break-all;font-size:13px}.pending{border-left:4px solid #e6b45d;padding:10px;background:#342e22}button{padding:10px 20px;margin:8px;cursor:pointer;font:inherit}details{margin-top:12px}nav{position:sticky;top:0;background:#15151bf2;padding:10px}input{font:inherit;width:180px}@media(max-width:700px){article img{width:100%}}</style><main>'+intro+'<p><b>Build：</b>'+h(model['build'])+'</p><p><a href="MVP_Windows_x64/RPG_Demo_MapVNext_I1.exe">打开 Windows 试玩程序</a> · <a href="START-HERE.md">中文说明</a> · <a href="manifest.json">完整清单</a></p><nav><button data-filter="all">全部输入</button><button data-filter="PASS">可玩样例</button><button data-filter="REJECTED">被拒绝的输入</button><label>按 Seed 查找 <input id="seed" placeholder="例如 21193"></label></nav><div id="cases">'+''.join(cards)+'</div>'+replay+video_section+diagnostics+'<script>let kind="all";function filter(){let text=document.querySelector("#seed").value.trim();document.querySelectorAll("article").forEach(a=>a.hidden=(kind!=="all"&&a.dataset.status!==kind)||!a.dataset.seed.includes(text));}document.querySelectorAll("button[data-filter]").forEach(b=>b.onclick=()=>{kind=b.dataset.filter;filter();});document.querySelector("#seed").oninput=filter;const frames=@@REPLAY@@;const slider=document.querySelector("#frame");slider.max=Math.max(0,frames.length-1);function show(){let f=frames[Number(slider.value)];if(!f)return;document.querySelector("#replayImage").src=f.path;document.querySelector("#frameInfo").textContent="采样原帧 "+(Number(slider.value)+1)+"/"+frames.length+" · "+f.phase+" · 按键 "+f.keys+" · 物理时刻 "+f.physicsTime+" · Generation "+f.generation;}slider.oninput=show;let timer=null;document.querySelector("#play").onclick=()=>{if(timer){clearInterval(timer);timer=null;}else timer=setInterval(()=>{slider.value=(Number(slider.value)+1)%frames.length;show();},240);};show();</script></main></html>'
    doc=doc.replace('@@REPLAY@@',json.dumps(model.get('replay',[]),ensure_ascii=False).replace('</','<\\/'))
    (root/'index.html').write_text(doc,'utf-8')
    intro_text=html.unescape(re.sub('<[^>]*>','',intro.replace('</h1>','\n\n').replace('</p>','\n\n'))).strip()
    (root/'START-HERE.md').write_text('# I1 正式集成试玩\n\n'+intro_text+'\n\nBuild：'+model['build']+'\n\n双击 index.html 审阅；运行 MVP_Windows_x64/RPG_Demo_MapVNext_I1.exe 试玩。人工验收待完成，MERGE_READY=NO。\n\n逐 Seed 正常/异常与真实 Run 见图集。技术与历史证据分别保留，未将旧内容迁移到新版规则。\n','utf-8')

def generate(source,release,root):
    source=Path(source).resolve();root=Path(root).resolve();cfg=read(release);build_id=cfg['buildId'];build=source/cfg['buildDirectory']
    if root.parent!=source:raise RuntimeError('NewDirectOwnDeliveryDirectoryRequired')
    runroots=[(source/'VersionedRuns'/name).resolve() for name in cfg['playerRuns'].values()]
    for run in runroots:
        if run.parent!=source/'VersionedRuns':raise RuntimeError('RunDirectoryEscapesSource')
    for item in [build,source/'Authoring',source/'VersionedRuns',Path(release).resolve()]:require_disjoint(root,item)
    require_phases(cfg['playerRuns'])
    evidence_digest=hashlib.sha256(json.dumps({run:tree(source/'VersionedRuns'/run) for run in sorted(cfg['playerRuns'].values())},sort_keys=True).encode()).hexdigest()
    owner={'schema':1,'source':str(source),'build':build_id,'implementationDigest':cfg['implementationDigest'],'releaseSha256':sha(release),'sourceEvidenceDigest':evidence_digest,'buildReportSha256':sha(build/'BuildReport.json'),'buildTreeManifestSha256':sha(build/'build-tree.json')}
    if root.exists():
        if not (root/'.i1-report-owned').is_file():raise RuntimeError('UnownedOutputDirectory')
        require_owner(read(root/'.i1-report-owned'),owner)
    root.mkdir(parents=True,exist_ok=True);save(root/'.i1-report-owned',owner)
    if build.parent!=source or build.name!='Build-'+build_id:raise RuntimeError('BuildDirectoryIdentityMismatch')
    executable,postbuild=require_build(source,build,build_id,cfg['implementationDigest'])
    model={'build':build_id,'sourceRoot':str(source),'implementationDigest':cfg['implementationDigest'],'humanAcceptance':'PENDING','mergeReady':'NO','fullRequestedSeeds':[],'buildTree':read(build/'build-tree.json'),'files':{},'units':[],'evidenceLinks':[],'replay':[],'historicalFailuresRetainedAt':str(source/'VersionedRuns'),'playerRuns':cfg['playerRuns']}
    def adopt(path,dest,run='BUILD',source_build=None):
        path=Path(path);dest=Path(dest);target=root/dest;target.parent.mkdir(parents=True,exist_ok=True)
        if not target.is_file() or sha(target)!=sha(path):shutil.copyfile(path,target)
        model['files'][dest.as_posix()]={'sha256':sha(path),'build':source_build or build_id,'runId':run,'originalSource':str(path)}
    for f in (build/'MVP_Windows_x64').rglob('*'):
        if f.is_file():adopt(f,Path('MVP_Windows_x64')/f.relative_to(build/'MVP_Windows_x64'))
    for name in ['BuildReport.json','BuildValidation.json','build-inputs.json','build-tree.json','source-fingerprint.txt']:adopt(build/name,Path('Evidence/Build')/name)
    if postbuild is not None:
        admission=postbuild_module(build_id)
        name=getattr(admission,'QUALIFICATION_FILE','PostBuildQualification-'+admission.BUILD_ID.rsplit('-',1)[-1]+'.json')
        model['postBuildQualification']={'status':postbuild['status'],'path':'Evidence/Build/'+name}
        adopt(build/name,Path('Evidence/Build')/name)
        model['evidenceLinks'].append('Evidence/Build/'+name)
        folders=postbuild_export_folders(admission)
        for folder in folders:
            for f in (source/'Authoring/VersionedExtension'/folder).rglob('*'):
                if f.is_file():adopt(f,Path('Evidence/PostBuild')/folder/f.relative_to(source/'Authoring/VersionedExtension'/folder))
        original_run=source/({'map-vnext-i1-integration-20261030-a14':'VersionedRuns/i1-windows-build-01','map-vnext-i1-diagnostic-20261030-a16':'VersionedRuns/i1-windows-native-build-01','map-vnext-i1-integration-20261030-a17':'VersionedRuns/i1-windows-integration-build-02','map-vnext-i1-integration-20261030-a20':'VersionedRuns/i1-windows-integrated-build-a20-01','map-vnext-i1-integration-20261030-a21':'VersionedRuns/i1-windows-integrated-build-a21-01','map-vnext-i1-integration-20261031-a34':'VersionedRuns/i1-windows-integrated-build-a34-01','map-vnext-i1-integration-20261031-a39':'VersionedRuns/i1-build39','map-vnext-i1-integration-20261031-a40':'VersionedRuns/i1-build40','map-vnext-i1-integration-20261031-a41':'VersionedRuns/i1-build41'}[build_id])
        for name in postbuild['originalEvidence']:
            adopt(original_run/name,Path('Evidence/BuildRun')/name,postbuild['originalRunId'])
        model['postBuildEvidenceBindings']=admission.portable_evidence_bindings(postbuild)
    players={}
    for label,run in cfg['playerRuns'].items():
        runroot=source/'VersionedRuns'/run;receipt,request=require_run(runroot,'player',source.parents[2],executable);body=read(runroot/'Player/i1-player-evidence.json')
        cmd=request['command']
        if Path(request['buildRoot']).resolve()!=build or '--i1-accept' not in cmd or Path(cmd[cmd.index('--i1-accept')+1]).resolve()!=runroot/'Player' or '--i1-phase' not in cmd or cmd[cmd.index('--i1-phase')+1]!=label:raise RuntimeError('PlayerAcceptanceCommandMismatch')
        if read(runroot/'build-tree-before.json')!=model['buildTree'] or read(runroot/'build-tree-after.json')!=model['buildTree']:raise RuntimeError('PlayerBuildTreeMismatch')
        if body.get('sourceFingerprint')!=read(runroot/'source-fingerprint.txt').get('sourceFingerprint'):raise RuntimeError('PlayerFingerprintMismatch')
        if receipt['status']!='PASS_LIFECYCLE_ONLY' or body['status']!='AUTOMATED_CHECKS_COMPLETE_HUMAN_PENDING' or body.get('phase')!=label or body['buildId']!=build_id or body['implementationDigest']!=model['implementationDigest']:raise RuntimeError('PlayerEvidenceNotQualified:'+run)
        if label=='restore' and not any('Fresh Player process loaded copied VNext checkpoint' in c for c in body.get('checks',[])):raise RuntimeError('ActualCrossProcessRestoreMissing')
        if label=='full':
            model['fullRequestedSeeds']=[x['requestedSeed'] for x in body['inputs']]
            if model['fullRequestedSeeds']!=SEEDS:raise RuntimeError('FullFixedDenominatorChanged')
        with (runroot/'Player/frames.csv').open(encoding='utf-8-sig',newline='') as frame_stream:
            frame_rows=list(csv.DictReader(frame_stream))
        if any(f['buildId']!=build_id for f in frame_rows):raise RuntimeError('FrameMetadataBuildMismatch:'+run)
        bind_replay(runroot/'Player',frame_rows,build_id)
        if build_id in ('map-vnext-i1-integration-20261031-a39','map-vnext-i1-integration-20261031-a40','map-vnext-i1-integration-20261031-a41'):require_capture_labels(frame_rows,body['inputs'],label)
        for input_row in body['inputs']:
            if input_row['status']=='READY_SAVED_REOPENED':
                for field in ['spawnImage','overviewImage']:
                    if not any(f['image']==input_row[field] and f['generation']==input_row['generation'] for f in frame_rows):raise RuntimeError('OriginalImageNotBoundToActualGeneration:'+field)
        for f in (runroot/'Player').rglob('*'):
            if f.is_file():adopt(f,Path('Media')/run/f.relative_to(runroot/'Player'),run)
        for name in ['receipt.json','supervisor.json','native-trace.json','command.json','source-fingerprint.txt','guard-before.json','guard-after.json','observer-offline-selftest.json','build-tree-before.json','build-tree-after.json']:adopt(runroot/name,Path('Evidence/Runs')/run/name,run)
        if build_id in ('map-vnext-i1-integration-20261031-a39','map-vnext-i1-integration-20261031-a40','map-vnext-i1-integration-20261031-a41'):
            adopt(runroot/'player.log',Path('Evidence/Runs')/run/'player.log',run)
            if label=='restore':
                info=read(runroot/'restore-input.json');copied=Path(info['path']).resolve()
                if info.get('originalFormalDataUsed') is not False or not copied.is_relative_to(source/'RuntimeData') or not copied.is_file() or sha(copied)!=info.get('sha256'):raise RuntimeError('OwnedActualRestoreCopyRequiredForExport')
                adopt(runroot/'restore-input.json',Path('Evidence/Runs')/run/'restore-input.json',run)
                adopt(copied,Path('Evidence/Runs')/run/'restore-input-copy.json',run)
        measurement=independent_motion(runroot/'Player',build_id) if label!='restore' else {'status':'NOT_APPLICABLE_NO_MOVEMENT','scope':'Cross-process restore'}
        measurement_path=root/('Evidence/Motion-'+label+'.json');save(measurement_path,measurement)
        model['files'][measurement_path.relative_to(root).as_posix()]={'sha256':sha(measurement_path),'build':build_id,'runId':run,'originalSource':'Generated independent measurement of retained actual CSV/World files'}
        if label!='restore' and measurement['status']!='PASS':raise RuntimeError('IndependentMotionFailed:'+label)
        if label=='pilot':
            with (runroot/'Player/frames.csv').open(encoding='utf-8-sig',newline='') as stream:
                for row in csv.DictReader(stream):
                    if row['image'].startswith(('Motion/','Negatives/')):
                        model['replay'].append({**row,'path':(Path('Media')/run/row['image']).as_posix(),'runId':run})
            if not model['replay']:raise RuntimeError('ActualDynamicMediaMissing')
        for name in require_battle_evidence(runroot,build_id,label):
            model['evidenceLinks'].append(str(Path('Media')/run/name))
        players[label]=(run,body)
        model['evidenceLinks']+=[str(Path('Media')/run/'i1-player-evidence.json'),str(Path('Evidence')/('Motion-'+label+'.json'))]
    run,full=players['full']
    for row in full['inputs']:
        ready=require_input_status(row);seed=row['requestedSeed'];media=[]
        measured=read(root/'Evidence/Motion-full.json');walked=ready and any(k.startswith(row.get('movementGeneration','NO_GENERATION')+':Stair-Up-') for k in measured.get('phases',{}))
        for field in ['spawnImage','overviewImage']:
            if ready:
                path=(Path('Media')/run/row[field]).as_posix();media.append({'path':path,'build':build_id,'runId':run})
        unit={'id':'I1-Seed-'+str(seed),'title':'可玩地图：种子 '+str(seed) if ready else '严格拒绝：种子 '+str(seed),'purpose':'检查完整游戏中的地图与实体' if ready else '保留本次真实生成失败，检查是否诚实拒绝','look':'看高地色序、梯口边界、角色脚部、怪物与静态物件位置' if ready else '看实际失败原因，本例没有可玩截图','actions':'在试玩程序输入此 Seed；沿坡面同时按两个方向上下，再在低地入口只按 A 或 D' if ready else '无需操作；可在程序输入此 Seed 复现拒绝','normal':'斜向通行顺畅；纯水平不能跨层；实体接地不占石壁；保存重开保持相同地图' if ready else '明确报告无法生成，保留请求，不悄悄换成另一张图','abnormal':'横穿石壁、楼梯卡住、物品掉进岩壁、换层残留或重开布局变化' if ready else '把拒绝写为可玩通过，或静默替换 Seed','verification':'程序实际进入可玩、保存重开；有轨迹的楼梯另经独立距离检查。人工审图和试玩待确认' if ready else '程序记录拒绝；未进入可玩，不计成功通行','kind':'playable' if ready else 'diagnostic','countsForTraversal':walked,'countsForReady':ready,'status':'PASS' if ready else 'REJECTED','human':'PENDING','subject':'Same-build actual Windows Player','scope':'Formal Ready, save/reopen exact identity; movement scope follows captured CSV','media':media,'input':row}
        if ready and not walked:unit['verification']='程序实际进入可玩并保存重开；本例未自动走遍楼梯，人工审图和试玩待确认'
        if build_id=='map-vnext-i1-integration-20261031-a34' and seed==21193:unit['verification']+='；本 Seed 的怪物窄通道互挡失败保留，未证明其完整追出领地再返回'
        model['units'].append(unit)
    for item in cfg.get('evidence',[]):
        entry=item if isinstance(item,dict) else {'path':item,'build':'NOT_RECORDED_REFERENCE','runId':'REFERENCE'}
        path=(source/entry['path']).resolve()
        if not path.is_relative_to(source):raise RuntimeError('ReviewEvidenceEscapesSource')
        dest=Path('Evidence/Reviewed')/path.relative_to(source);adopt(path,dest,entry.get('runId','REFERENCE'),entry.get('build','NOT_RECORDED_REFERENCE'));model['evidenceLinks'].append(dest.as_posix())
    model['evidenceLinks']+=['Evidence/Build/BuildReport.json','Evidence/Build/BuildValidation.json']
    from deliver_p2 import generate_gifs
    chosen=[]
    for prefix in ['Stair-Up-','Stair-Down-']:
        first=next((f for f in model['replay'] if f['phase'].startswith(prefix)),None)
        if first is None:raise RuntimeError('ActualStairMotionFramesMissing:'+prefix)
        for frame in [f for f in model['replay'] if f['phase']==first['phase'] and f['generation']==first['generation']]:
            chosen.append({**frame,'case':prefix+'actual','screenshot':frame['image'],'utc':'NOT_RECORDED_USE_PHYSICS_TIME'})
    native_size=decode_png(root/chosen[0]['path'])
    if any(decode_png(root/frame['path'])!=native_size for frame in chosen):raise RuntimeError('ActualFrameResolutionChangedWithinGIF')
    model['gifs']=generate_gifs(root,chosen,build_id,'DerivedI1GIF','I1GIF',False,native_size)
    pilot_run=players['pilot'][0]
    for gif in model['gifs']:model['files'][gif['path']]={'sha256':gif['sha256'],'build':build_id,'runId':pilot_run,'originalSource':'Derived from exact sourceFrames; native dimensions and 256-color GIF; review timing, not continuous recording'}
    evidence=root/'Evidence/DerivedI1GIF/manifest.json';model['files'][evidence.relative_to(root).as_posix()]={'sha256':sha(evidence),'build':build_id,'runId':pilot_run,'originalSource':'Generated GIF provenance'}
    model['readyCount']=sum(u['countsForReady'] for u in model['units']);model['walkedCount']=sum(u['countsForTraversal'] for u in model['units'])
    errors=validate(model)
    if errors:raise RuntimeError(errors)
    render(root,model);save(root/'manifest.json',model);result=check(root);print(json.dumps(result,ensure_ascii=True));return result

def portable(root,archive,extract):
    root=Path(root).resolve();archive=Path(archive).resolve();extract=Path(extract).resolve()
    receipt_path=root.parent/(root.name+'-portable-check.json')
    require_disjoint(root,archive,extract,receipt_path)
    if archive.exists() or extract.exists() or receipt_path.exists():raise RuntimeError('NewArchiveExtractAndReceiptPathsRequired')
    if check(root)['status']!='PASS_STATIC_PACKAGE_CHECK':raise RuntimeError('PackageNotVerified')
    with zipfile.ZipFile(archive,'w',zipfile.ZIP_DEFLATED,compresslevel=6) as z:
        for f in root.rglob('*'):
            if f.is_file():z.write(f,f.relative_to(root))
    with zipfile.ZipFile(archive) as z:
        if z.testzip():raise RuntimeError('ArchiveCRCFailed')
        z.extractall(extract)
    if tree(root)!=tree(extract):raise RuntimeError('PortableTreeMismatch')
    result=check(extract);save(receipt_path,{'status':result['status'],'zip':str(archive.resolve()),'zipSha256':sha(archive),'extract':str(extract.resolve()),'crc':'PASS','exactTree':'PASS','check':result});print(json.dumps(result));return result

def main():
    p=argparse.ArgumentParser();sub=p.add_subparsers(dest='command',required=True)
    g=sub.add_parser('generate');g.add_argument('--source',type=Path,required=True);g.add_argument('--release',type=Path,required=True);g.add_argument('--output',type=Path,required=True)
    c=sub.add_parser('check');c.add_argument('root',type=Path)
    z=sub.add_parser('portable');z.add_argument('root',type=Path);z.add_argument('--zip',type=Path,required=True);z.add_argument('--extract',type=Path,required=True)
    a=p.parse_args()
    result=generate(a.source,a.release,a.output) if a.command=='generate' else check(a.root) if a.command=='check' else portable(a.root,a.zip,a.extract)
    if a.command=='check':print(json.dumps(result,ensure_ascii=True))
    return 0 if result['status']=='PASS_STATIC_PACKAGE_CHECK' else 2
if __name__=='__main__':raise SystemExit(main())
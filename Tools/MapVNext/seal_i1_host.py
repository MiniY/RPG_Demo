"""Explicit offline authoring of a finite reviewed implementation group. Never used for runtime trust enrollment."""
from pathlib import Path
import argparse,json,hashlib,datetime,subprocess
C=Path(__file__).resolve().parents[2]
A=C/'Artifacts/MapAcceptance/MapVNext-Integration-I1-20261029/Authoring/VersionedExtension'
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
def require_review(review,snapshot_sha):
    if review.get('reviewedSnapshotManifestSha256')!=snapshot_sha or not review.get('reviewer') or review.get('reviewPerformed') is not True:raise RuntimeError('Exact independent review binding required')
    if review.get('sealEligible') is not True or review.get('status')!='STATIC_REVIEW_COMPLETE_NO_OPEN_FINDINGS' or type(review.get('openFindings')) is not int or review['openFindings']!=0:raise RuntimeError('Review is not eligible for controlled sealing')

def require_closure(snapshot,body):
    digest=hashlib.sha256(body).hexdigest()
    if snapshot.get('sourceClosureBeforeSha256')!=digest:raise RuntimeError('Reviewed source closure drifted')
    return json.loads(body.decode('utf-8')),digest

def unreviewed_sources(encoded,reviewed):
    return [path for path in encoded.decode('utf-8').split('\0') if path and path.endswith(('.cs','.asmdef','.rsp')) and path not in reviewed]

def require_baseline_delta(baseline,current,allowed):
    changed={p for p in set(baseline)|set(current) if baseline.get(p)!=current.get(p)}
    unexpected=changed-set(allowed)
    if unexpected:raise RuntimeError('Product delta outside captured baseline approval: '+repr(sorted(unexpected)))
    return sorted(changed)

def main():
    ap=argparse.ArgumentParser();ap.add_argument('--build-id',required=True);ap.add_argument('--review-snapshot',type=Path,required=True);ap.add_argument('--review-receipt',type=Path,required=True);ap.add_argument('--source-closure',type=Path,default=A/'source-closure-before.json');ap.add_argument('--baseline-manifest',type=Path);ap.add_argument('--materialization',type=Path);ap.add_argument('--current-content',type=Path,required=True);args=ap.parse_args()
    snap=json.loads(args.review_snapshot.read_text('utf-8'));review=json.loads(args.review_receipt.read_text('utf-8'))
    require_review(review,sha(args.review_snapshot))
    for row in snap['files']:
        if sha(C/row['path'])!=row['sha256']:raise RuntimeError('Review stale: '+row['path'])
    closure,closure_digest=require_closure(snap,args.source_closure.read_bytes());allowed={r['path']:r for r in closure['closedExistingSourcePaths']}
    if args.baseline_manifest:
        if snap.get('sourceBaselineManifestSha256')!=sha(args.baseline_manifest):raise RuntimeError('Captured baseline binding differs')
        from captured_inputs import read_baseline
        if args.materialization and snap.get('sourceMaterializationSha256')!=sha(args.materialization):raise RuntimeError('Reviewed materialization binding differs')
        baseline=read_baseline(C,args.baseline_manifest,args.materialization)
        import sys
        sys.path.insert(0,str(C/'Tools/EngineeringAssurance'))
        from unity_project_snapshot import source_inventory
        source={p:s for p,s in baseline['sourceFiles'].items() if p.startswith(('Assets/','Packages/','ProjectSettings/'))}
        require_baseline_delta(source,source_inventory(C),closure['baselineAllowedChanges'])
    else:
        existing_changes=subprocess.check_output(['git','diff','--name-only','-z','HEAD','--','Assets','Packages','ProjectSettings'],cwd=C).decode('utf-8').split('\0')
        if any(path and path not in allowed for path in existing_changes):raise RuntimeError('Changed existing product path outside approved closure')
    reviewed_paths={r['path'] for r in snap['files']}
    if args.baseline_manifest:
        # A snapshot has no Git index: inventory every actual compiler input instead.
        authored={p.relative_to(C).as_posix() for p in (C/'Assets').rglob('*') if p.is_file() and p.suffix in ('.cs','.asmdef','.rsp')}
        authored.discard('Assets/Scripts/MapGeneration/SpatialVersion/MapHostBuildStamp.cs')
        if authored-reviewed_paths:raise RuntimeError('New compilation input absent from independent review')
    else:
        if args.materialization:raise RuntimeError('MaterializationRequiresCapturedBaseline')
        untracked_sources=subprocess.check_output(['git','ls-files','-z','--others','--exclude-standard','--','Assets'],cwd=C)
        if unreviewed_sources(untracked_sources,reviewed_paths):raise RuntimeError('New compilation input absent from independent review')
    evidence=A/('HostSeal-'+args.build_id)
    if evidence.exists():raise RuntimeError('Build identity already sealed; choose a new identity')
    main=C/'Assets/Resources/Ecology/SurfaceV4MainV1/RetainedContent_MainV1.json';old=json.loads(main.read_text('utf-8-sig'));diffs=[]
    for row in old['files']:
        path=C/row['path']
        if not path.is_file():raise RuntimeError('Missing original pinned dependency '+row['path'])
        actual=sha(path)
        if actual!=row['sha256']:
            if row['path'] not in allowed or allowed[row['path']]['originalSha256']!=row['sha256']:raise RuntimeError('Unapproved frozen difference '+row['path'])
            diffs.append({'path':row['path'],'originalSha256':row['sha256'],'currentSha256':actual})
    stamp=C/'Assets/Scripts/MapGeneration/SpatialVersion/MapHostBuildStamp.cs'
    files=set((C/'Assets').rglob('*.cs'))|set((C/'Assets').rglob('*.asmdef'))|set((C/'Assets').rglob('*.rsp'))
    for p in list(files):
        meta=Path(str(p)+'.meta')
        if meta.is_file():files.add(meta)
    for root in ['Assets/MapGeneration/MapVNext','Assets/Scenes/MapVNext']:
        files.update(p for p in (C/root).rglob('*') if p.is_file())
    for path in closure.get('reviewedResourcePaths',[]):
        resolved=(C/path).resolve()
        if not resolved.is_relative_to((C/'Assets').resolve()) or not resolved.is_file():raise RuntimeError('Invalid reviewed resource path')
        files.add(resolved)
    files.add(C/'Assets/Scenes/SampleScene.unity')
    target=C/'Assets/MapGeneration/MapVNext/I1/Resources/MapVNextI1Host.json'
    # This one generated stamp is cross-bound by its exact SHA and the external complete Build input manifest.
    files.discard(stamp);files.discard(target);files.discard(Path(str(target)+'.meta'))
    rows=[{'path':p.relative_to(C).as_posix(),'sha256':sha(p)} for p in sorted(files,key=lambda p:p.relative_to(C).as_posix())]
    reviewed={r['path']:r['sha256'] for r in snap['files']}
    if any(reviewed.get(r['path'])!=r['sha256'] for r in rows):raise RuntimeError('Whole host contains an input absent from exact independent review')
    body='\n'.join(r['path']+'\t'+r['sha256'] for r in rows)+'\n';digest=hashlib.sha256(body.encode()).hexdigest()
    retained=C/'Assets/MapGeneration/MapVNext/I1/Resources/CandidateContent_I1.json'
    candidate=args.current_content or retained
    oldid='content-r1:'+sha(main);newid='content-r1:'+sha(candidate)
    differences_digest=hashlib.sha256(('\n'.join(r['path']+'\t'+r['originalSha256']+'\t'+r['currentSha256'] for r in sorted(diffs,key=lambda r:r['path']))+'\n').encode()).hexdigest()
    current_diffs=[]
    for row in json.loads(candidate.read_text('utf-8-sig'))['files']:
        actual=sha(C/row['path'])
        if actual!=row['sha256']:
            if row['path'] not in allowed or allowed[row['path']]['originalSha256']!=row['sha256']:raise RuntimeError('Unapproved current-content difference '+row['path'])
            current_diffs.append({'path':row['path'],'originalSha256':row['sha256'],'currentSha256':actual})
    current_digest=hashlib.sha256(('\n'.join(r['path']+'\t'+r['originalSha256']+'\t'+r['currentSha256'] for r in sorted(current_diffs,key=lambda r:r['path']))+'\n').encode()).hexdigest()
    evidence.parent.mkdir(parents=True,exist_ok=True)
    evidence.mkdir(exist_ok=False)
    stamp.write_text('public static class MapHostBuildStamp\n{\n    public const bool IsReviewedCompatibleHost=true;\n#if RPG_I1_ISOLATED_STARTUP && !UNITY_EDITOR\n    public const bool HasExplicitI1StartupCapability=true;\n    public const string StartupTarget="I1_PRIVATE";\n#else\n    public const bool HasExplicitI1StartupCapability=false;\n    public const string StartupTarget="DEFAULT";\n#endif\n    public const string ReviewEvidenceSha256='+json.dumps(sha(args.review_receipt))+';\n    public const string SourceClosureSha256='+json.dumps(closure_digest)+';\n    public const string ApprovedDifferencesDigest='+json.dumps(differences_digest)+';\n    public const string BuildId='+json.dumps(args.build_id)+';\n    public const string ImplementationDigest='+json.dumps(digest)+';\n    public const string OldContentVersion='+json.dumps(oldid)+';\n    public const string NewContentVersion='+json.dumps(newid)+';\n}\n',encoding='utf-8')
    if args.current_content:
        extra='    public const string RetainedContentVersion='+json.dumps('content-r1:'+sha(retained))+';\n    public const string CurrentContentDifferencesDigest='+json.dumps(current_digest)+';\n'
        stamp.write_text(stamp.read_text('utf-8').replace('\n}\n','\n'+extra+'}\n'),encoding='utf-8')
    host={'schema':2,'startupTargets':['DEFAULT','I1_PRIVATE'],'buildId':args.build_id,'implementationDigest':digest,'oldContentVersion':oldid,'newContentVersion':newid,'closureSha256':closure_digest,'reviewEvidenceSha256':sha(args.review_receipt),'stampSha256':sha(stamp),'files':rows,'approvedDifferences':diffs}
    if args.current_content:host.update(retainedContentVersion='content-r1:'+sha(retained),currentContentDifferences=current_diffs)
    # Immutable external seal receipts retain every authoring iteration; only the current candidate resource changes.
    target.write_text(json.dumps(host,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    (evidence/'host.json').write_bytes(target.read_bytes());(evidence/'stamp.cs').write_bytes(stamp.read_bytes());(evidence/'review-receipt.json').write_bytes(args.review_receipt.read_bytes())
    result={'status':'SEALED_FOR_CURRENT_REVIEWED_GROUP_NOT_RUNTIME_PASS','utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'buildId':args.build_id,'strictOriginalDifferenceCount':len(diffs),'originalManifestSha256':sha(main),'currentHostManifestSha256':sha(target),'stampSha256':sha(stamp),'implementationDigest':digest,'reviewSnapshotManifestSha256':sha(args.review_snapshot),'reviewReceiptSha256':sha(args.review_receipt),'files':len(rows),'playerCheck':'Compiled stamp vs embedded group and exact content IDs; no source filesystem in Player','editorAndPrebuildCheck':'All original pins -> exact K differences -> exact whole-host paths/hashes/dependencies/source inventory -> stamp SHA'}
    (evidence/'seal.json').write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8');print(json.dumps(result))
if __name__=='__main__':main()

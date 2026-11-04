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

def main():
    ap=argparse.ArgumentParser();ap.add_argument('--build-id',required=True);ap.add_argument('--review-snapshot',type=Path,required=True);ap.add_argument('--review-receipt',type=Path,required=True);ap.add_argument('--source-closure',type=Path,default=A/'source-closure-before.json');args=ap.parse_args()
    snap=json.loads(args.review_snapshot.read_text('utf-8'));review=json.loads(args.review_receipt.read_text('utf-8'))
    require_review(review,sha(args.review_snapshot))
    for row in snap['files']:
        if sha(C/row['path'])!=row['sha256']:raise RuntimeError('Review stale: '+row['path'])
    closure,closure_digest=require_closure(snap,args.source_closure.read_bytes());allowed={r['path']:r for r in closure['closedExistingSourcePaths']}
    existing_changes=subprocess.check_output(['git','diff','--name-only','-z','HEAD','--','Assets','Packages','ProjectSettings'],cwd=C).decode('utf-8').split('\0')
    if any(path and path not in allowed for path in existing_changes):raise RuntimeError('Changed existing product path outside approved closure')
    reviewed_paths={r['path'] for r in snap['files']}
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
    files.add(C/'Assets/Scenes/SampleScene.unity')
    target=C/'Assets/MapGeneration/MapVNext/I1/Resources/MapVNextI1Host.json'
    # This one generated stamp is cross-bound by its exact SHA and the external complete Build input manifest.
    files.discard(stamp);files.discard(target);files.discard(Path(str(target)+'.meta'))
    rows=[{'path':p.relative_to(C).as_posix(),'sha256':sha(p)} for p in sorted(files,key=lambda p:p.relative_to(C).as_posix())]
    body='\n'.join(r['path']+'\t'+r['sha256'] for r in rows)+'\n';digest=hashlib.sha256(body.encode()).hexdigest()
    candidate=C/'Assets/MapGeneration/MapVNext/I1/Resources/CandidateContent_I1.json'
    oldid='content-r1:'+sha(main);newid='content-r1:'+sha(candidate)
    differences_digest=hashlib.sha256(('\n'.join(r['path']+'\t'+r['originalSha256']+'\t'+r['currentSha256'] for r in sorted(diffs,key=lambda r:r['path']))+'\n').encode()).hexdigest()
    evidence.mkdir(exist_ok=False)
    stamp.write_text('public static class MapHostBuildStamp\n{\n    public const bool IsReviewedCompatibleHost=true;\n#if RPG_I1_ISOLATED_STARTUP && !UNITY_EDITOR\n    public const bool HasExplicitI1StartupCapability=true;\n    public const string StartupTarget="I1_PRIVATE";\n#else\n    public const bool HasExplicitI1StartupCapability=false;\n    public const string StartupTarget="DEFAULT";\n#endif\n    public const string ReviewEvidenceSha256='+json.dumps(sha(args.review_receipt))+';\n    public const string SourceClosureSha256='+json.dumps(closure_digest)+';\n    public const string ApprovedDifferencesDigest='+json.dumps(differences_digest)+';\n    public const string BuildId='+json.dumps(args.build_id)+';\n    public const string ImplementationDigest='+json.dumps(digest)+';\n    public const string OldContentVersion='+json.dumps(oldid)+';\n    public const string NewContentVersion='+json.dumps(newid)+';\n}\n',encoding='utf-8')
    host={'schema':2,'startupTargets':['DEFAULT','I1_PRIVATE'],'buildId':args.build_id,'implementationDigest':digest,'oldContentVersion':oldid,'newContentVersion':newid,'closureSha256':closure_digest,'reviewEvidenceSha256':sha(args.review_receipt),'stampSha256':sha(stamp),'files':rows,'approvedDifferences':diffs}
    # Immutable external seal receipts retain every authoring iteration; only the current candidate resource changes.
    target.write_text(json.dumps(host,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    (evidence/'host.json').write_bytes(target.read_bytes());(evidence/'stamp.cs').write_bytes(stamp.read_bytes());(evidence/'review-receipt.json').write_bytes(args.review_receipt.read_bytes())
    result={'status':'SEALED_FOR_CURRENT_REVIEWED_GROUP_NOT_RUNTIME_PASS','utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'buildId':args.build_id,'strictOriginalDifferenceCount':len(diffs),'originalManifestSha256':sha(main),'currentHostManifestSha256':sha(target),'stampSha256':sha(stamp),'implementationDigest':digest,'reviewSnapshotManifestSha256':sha(args.review_snapshot),'reviewReceiptSha256':sha(args.review_receipt),'files':len(rows),'playerCheck':'Compiled stamp vs embedded group and exact content IDs; no source filesystem in Player','editorAndPrebuildCheck':'All original pins -> exact K differences -> exact whole-host paths/hashes/dependencies/source inventory -> stamp SHA'}
    (evidence/'seal.json').write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8');print(json.dumps(result))
if __name__=='__main__':main()

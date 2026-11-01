"""Read-only exact-a20 admission after independently reviewed one-time cache recovery.

The original failed Run and blocked BuildValidation are never rewritten. This
module cannot restore fonts, bless another Build, or enroll live source hashes.
The tool group is snapshotted before independent review, separate from a20 input.
"""
from pathlib import Path
import argparse, hashlib, json

BUILD_ID = 'map-vnext-i1-integration-20261030-a20'
DIGEST = '53ac6a3ba033f6da2ced8fbbdcff922c245fec313eae6c83f40f9c5e24d21502'
RUN_ID = '0e928ba45b4f498f9f3ca2d1afc45bd0'
PROJECT = Path(r'D:\.codex\worktrees\map-vnext-integration-i1\RPG_Demo')
ROOT_REL = 'Artifacts/MapAcceptance/MapVNext-Integration-I1-20261029'
QUAL_REL = ROOT_REL + '/Authoring/VersionedExtension/FontCacheQualification-a20'
REVIEW_REL = ROOT_REL + '/Authoring/VersionedExtension/PostBuildAdmissionReview-a20-02'
FONT = 'Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Fallback.asset'
BEFORE = '54406f3fac18227d9780201699a619dd60ebf495fe0179684c0ac200ec2c16b5'
GENERATED = '5e72dec23b070c83d6efdba58431d808ad1bfe48a8e563690f39c08ff4134a1d'
STATUS = 'REVIEWED_POSTBUILD_CACHE_RECOVERY_A20'
TOOL_FILES = {
 'Tools/MapVNext/i1_postbuild_qualification.py', 'Tools/MapVNext/test_i1_postbuild.py',
 'Tools/MapVNext/i1_postbuild_a20.py', 'Tools/MapVNext/test_i1_postbuild_a20.py',
 'Tools/MapVNext/i1_postbuild_a17.py', 'Tools/MapVNext/test_i1_postbuild_a17.py',
 'Tools/MapVNext/i1_postbuild_a16.py', 'Tools/MapVNext/test_i1_postbuild_a16.py',
 'Tools/MapVNext/run_i1.py', 'Tools/MapVNext/test_i1_runner.py',
 'Tools/MapVNext/deliver_i1.py', 'Tools/MapVNext/test_i1_delivery.py',
 'Tools/MapVNext/deliver_readable.py', 'Tools/MapVNext/deliver_p2.py',
 'Tools/MapVNext/cliff_motion.py', 'Tools/MapVNext/i1_native_trace.py',
 'Tools/EngineeringAssurance/unity_guard.py', 'Tools/EngineeringAssurance/unity_supervisor.py',
 'Tools/EngineeringAssurance/assure.py'
}
# Every hash here predates this validator and comes from retained real evidence.
BINDINGS = {'receipt.json': '833b00bf10c91d736f3380af98da86ea33536cd1495dace39bc8772d27e079c6', 'supervisor.json': 'b7298537ac66e09e31f7d91146f29da975d1bc7c67550e8d429b0f3f6be966a0', 'native-trace.json': '32dce25ab15de015d39787c29a1592dbb656d4127a28b2254a9041e5de803c82', 'command.json': '6da7d9855a747094381cc898cac38dc7c1f52ed684b2f83011d890c5766d0f4e', 'guard-after.json': 'd17c81b826864cecd99e280fa23097a787f35e6cdfa4fbac3cc4616e90b4982b', 'font-side-effect.json': '4dae60a1c45fe2f72f1fc3110e923acf96be460804638d7e0ba8b6a8c910aa7a', 'build-inputs-after.json': 'd90b7ae6d21299b0c6ee525e02d2266fdf7fcdb3a6dcd1388d77887cc5e5ae3d', 'TMP.before.asset': '54406f3fac18227d9780201699a619dd60ebf495fe0179684c0ac200ec2c16b5', 'TMP.editor-generated.asset': '5e72dec23b070c83d6efdba58431d808ad1bfe48a8e563690f39c08ff4134a1d', 'source-fingerprint.txt': 'a2ed2d83ebebaf9047dea2f8c8f47bbed67e68bc868f77fc0bf69d9e4689e7ca'}
BUILD_BINDINGS = {'BuildValidation.json': 'd6020331ec563e60506afb663eafe5d17a300215ab32dc348a62dc946682e69e', 'BuildReport.json': '6dc6dabcc54f7e15e633a80682e1171df32b540083614aa443980966ac6d5668', 'build-inputs.json': 'e662bc1b37bf4b6539eea62d2d21f7bbd96b34c893e5e34d5ae0f58ce91ca2c9', 'build-tree.json': 'f28313244f3c20516d48454ddc32594c3197e19b0f811e762d1416ccb3c9feac', 'MVP_Windows_x64/RPG_Demo_MapVNext_I1.exe': '059c313352892718258a120b87bdc6fc7c23ce67181084abe14f86f204f5dddb'}
QUAL_BINDINGS = {'qualification.json': '050de60ef8d4750c95c4ee5d2dc42e94bfc482f936851656acb45c567be290c9', 'independent-review.json': 'a24ef09f0bb5a1f28b932cf986546bf02afb4fff3c962596dd9d4f9c57bc93d0', 'recovery-result.json': 'd90960040b86996b160327ce4a89b5fff6fe519695b1e8537eb58f8a2e5e5b1d'}

def read(p): return json.loads(p.read_text('utf-8'))
def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest()
def tree(root): return {p.relative_to(root).as_posix(): sha(p) for p in sorted(root.rglob('*')) if p.is_file()}
def require_equal(actual, expected, reason):
    if actual != expected: raise RuntimeError(reason)
def require_bindings(root, rows):
    for name, expected in rows.items(): require_equal(sha(root/name), expected, 'PostBuildEvidenceChanged:'+name)

def require_identity(build_id, digest):
    require_equal((build_id,digest),(BUILD_ID,DIGEST),'OnlyExactA20PostBuildQualificationAllowed')

def validate_inputs(current, before, after):
    require_equal(set(before), {'Assets','Packages','ProjectSettings'}, 'FullBuildInputRootsRequired')
    require_equal(set(after), set(before), 'FullAfterInputRootsRequired')
    changes = {kind+'/'+p for kind in before for p in set(before[kind]) | set(after[kind]) if before[kind].get(p)!=after[kind].get(p)}
    require_equal(changes, {FONT}, 'OnlyReviewedFontDeltaAllowed')
    require_equal(before['Assets'].get(FONT.removeprefix('Assets/')), BEFORE, 'WrongOriginalFont')
    require_equal(after['Assets'].get(FONT.removeprefix('Assets/')), GENERATED, 'UnknownGeneratedFont')
    require_equal(current, before, 'CurrentInputsMustExactlyEqualRestoredPrebuildIncludingAddedMissing')

def validate_lifecycle(receipt, sup, native, request, after, project, build):
    require_equal((receipt.get('status'),receipt.get('buildInputsAndTreeMatched')),('FAIL',False),'OriginalFailMustRemain')
    require_equal(receipt.get('runId'),RUN_ID,'OriginalBuildRunMismatch')
    cleanup=sup.get('cleanup',{})
    if sup.get('runId')!=RUN_ID or sup.get('operation')!='build' or sup.get('state')!='COMPLETED' or sup.get('launched') is not True or sup.get('naturalExit') is not True or sup.get('exitCode')!=0 or cleanup.get('status')!='PASS' or cleanup.get('forced') or cleanup.get('residualPids') or cleanup.get('errors') or any(sup.get('locks',{}).get(k,{}).get('released') is not True for k in ('project','heavy')):
        raise RuntimeError('OriginalBuildLifecycleNotQualified')
    if receipt.get('cleanup')!=cleanup or receipt.get('naturalExit') is not True or receipt.get('exitCode')!=0 or receipt.get('trace')!='VALID_NATIVE_TIME_LINE' or native.get('status')!=receipt['trace'] or after.get('state')!='FREE': raise RuntimeError('OriginalRunEvidenceInconsistent')
    process=sup.get('process',{}); roots=[r for r in native.get('records',[]) if r.get('pid')==native.get('root')]
    if len(roots)!=1 or native.get('root')!=process.get('pid') or any(process.get(k) is None or roots[0].get(k)!=process[k] for k in ('pid','creationTime100ns','createdAtUtc','imagePath')) or roots[0].get('command')!=sup.get('command'): raise RuntimeError('OriginalNativeRootMismatch')
    if request.get('command')!=sup.get('command') or Path(sup['projectPath']).resolve()!=project or Path(request['buildRoot']).resolve()!=build or Path(process['imagePath']).resolve()!=Path(request['command'][0]).resolve() or 'MapVNextI1Build.Build' not in request['command']: raise RuntimeError('OriginalBuildCommandMismatch')

def validate_tool_review(manifest, review, manifest_sha):
    if review.get('reviewPerformed') is not True or review.get('reviewer')!='/root/i1_contract_review' or review.get('status')!='APPROVED_EXACT_A20_POSTBUILD_ADMISSION_TOOLS' or type(review.get('openFindings')) is not int or review['openFindings']!=0 or review.get('reviewedManifestSha256')!=manifest_sha or set(review.get('axes',{}))!={'Standards','Spec'} or any(v!='NO_OPEN_FINDINGS' for v in review['axes'].values()): raise RuntimeError('IndependentPostBuildToolReviewRequired')
    rows=manifest.get('files',[])
    if len(rows)!=len(TOOL_FILES) or {r['path'] for r in rows}!=TOOL_FILES: raise RuntimeError('CompleteReviewedToolGroupRequired')
    return rows

def reviewed_tools(project):
    folder=project/REVIEW_REL; manifest=read(folder/'manifest.json'); review=read(folder/'review.json')
    rows=validate_tool_review(manifest,review,sha(folder/'manifest.json'))
    require_bindings(project,{r['path']:r['sha256'] for r in rows})
    return {'manifestSha256':sha(folder/'manifest.json'),'reviewSha256':sha(folder/'review.json'),'files':rows}

def core(project, build, build_id, digest):
    project=Path(project).resolve(); build=Path(build).resolve(); require_identity(build_id,digest)
    require_equal(project, PROJECT.resolve(), 'OnlyOriginalI1PhysicalProjectAllowed')
    require_equal(build, project/ROOT_REL/('Build-'+BUILD_ID), 'OnlyOriginalA20BuildDirectoryAllowed')
    run=project/ROOT_REL/'VersionedRuns/i1-windows-integrated-build-a20-01'; q=project/QUAL_REL
    for root, rows in [(run,BINDINGS),(build,BUILD_BINDINGS),(q,QUAL_BINDINGS)]: require_bindings(root,rows)
    recovery=read(q/'recovery-result.json')
    require_equal(recovery.get('status'),'ONE_TIME_A20_EXACT_RAW_BEFORE_RECOVERED','ActualRecoveryRequired')
    for field in ['full3558InputsExactlyEqualOriginalPrebuild','full158PlayerTreeUnchanged','originalFailureAndBlockedAndGeneratedEvidenceUnchanged']: require_equal(recovery.get(field),True,'IncompleteRecovery:'+field)
    require_equal(sha(q/'recovery-start.json'),recovery['startSha256'],'RecoveryStartEvidenceChanged')
    require_equal(sha(q/'recovery-guard-before.json'),read(q/'recovery-start.json')['guardSha256'],'RecoveryGuardEvidenceChanged')
    for row in read(q/'qualification.json')['callbackSource']: require_equal(sha(project/row['path']),row['sha256'],'TMPDependencyChanged')
    before=read(build/'build-inputs.json'); after=read(run/'build-inputs-after.json')
    validate_inputs({kind:tree(project/kind) for kind in before},before,after)
    require_equal(sum(map(len,before.values())),3558,'OriginalFullInputCountChanged')
    output=read(build/'build-tree.json'); require_equal(len(output),158,'OriginalPlayerTreeCountChanged')
    require_equal(tree(build/'MVP_Windows_x64'),output,'CompletePlayerTreeChangedIncludingAddedMissing')
    report=read(build/'BuildReport.json'); require_identity(report.get('identity'),report.get('implementationDigest'))
    require_equal((report.get('status'),report.get('errors'),report.get('warnings')),('BUILD_SUCCEEDED_PLAYER_NOT_RUN',0,0),'OriginalBuildReportChanged')
    exe=build/'MVP_Windows_x64/RPG_Demo_MapVNext_I1.exe'; require_equal(Path(report['executable']).resolve(),exe,'OriginalExecutableChanged')
    validate_lifecycle(read(run/'receipt.json'),read(run/'supervisor.json'),read(run/'native-trace.json'),read(run/'command.json'),read(run/'guard-after.json'),project,build)
    return {'status':STATUS,'buildId':BUILD_ID,'implementationDigest':DIGEST,'originalRunId':RUN_ID,'originalBuildStatus':'BUILD_BLOCKED','originalRunStatus':'FAIL','originalRunInputsUnchangedClaim':False,'sourceBaselineRestored':True,'playerOutputUnchanged':True,'oneTimeRecoveryOnly':True,'originalEvidence':BINDINGS,'originalBuildEvidence':BUILD_BINDINGS,'cacheQualificationEvidence':QUAL_BINDINGS,'reviewedAdmissionToolGroup':reviewed_tools(project),'toolingNotCompiledIntoOriginalBuild':True,'humanAcceptance':'PENDING','mergeReady':'NO'}

def verified_qualification(project, build, build_id=BUILD_ID, digest=DIGEST):
    expected=core(project,build,build_id,digest)
    require_equal(read(Path(build)/'PostBuildQualification-a20.json'),expected,'ExactAppendOnlyPostBuildQualificationRequired')
    return expected

def require_postbuild(project, build, build_id=BUILD_ID, digest=DIGEST):
    verified_qualification(project,build,build_id,digest)
    return Path(build).resolve()/'MVP_Windows_x64/RPG_Demo_MapVNext_I1.exe'


def portable_evidence_bindings(qualification):
    require_identity(qualification.get('buildId'),qualification.get('implementationDigest'))
    require_equal(qualification.get('status'),STATUS,'PortablePostBuildStatusMismatch')
    require_equal(qualification.get('originalRunId'),RUN_ID,'PortableOriginalRunMismatch')
    result={'Evidence/BuildRun/'+name:digest for name,digest in BINDINGS.items()}
    for name,digest in BUILD_BINDINGS.items():
        result[('' if name.startswith('MVP_Windows_x64/') else 'Evidence/Build/')+name]=digest
    result.update({'Evidence/PostBuild/FontCacheQualification-a20/'+name:digest for name,digest in QUAL_BINDINGS.items()})
    tools=qualification['reviewedAdmissionToolGroup'];prefix='Evidence/PostBuild/PostBuildAdmissionReview-a20-02/'
    result[prefix+'manifest.json']=tools['manifestSha256'];result[prefix+'review.json']=tools['reviewSha256']
    if len(tools['files'])!=len(TOOL_FILES) or {row['path'] for row in tools['files']}!=TOOL_FILES:raise RuntimeError('PortableCompleteToolGroupRequired')
    result.update({prefix+row['path']:row['sha256'] for row in tools['files']})
    return result

def check_portable_evidence(root, model):
    marker=model.get('postBuildQualification')
    if model.get('build')!=BUILD_ID:
        if marker is not None:raise RuntimeError('PortableOtherBuildCannotCarryA20Exception')
        validation=root/'Evidence/Build/BuildValidation.json'
        if validation.is_file() and read(validation).get('status')=='BUILD_BLOCKED':raise RuntimeError('PortableBlockedOtherBuildCannotUseA20Exception')
        return
    if marker is None:raise RuntimeError('PortableKnownA20RequiresExplicitQualifiedMarker')
    path='Evidence/Build/PostBuildQualification-a20.json'
    require_equal(marker,{'status':STATUS,'path':path},'PortablePostBuildMarkerMismatch')
    qualification=read(root/path);bindings=portable_evidence_bindings(qualification)
    require_equal(model.get('postBuildEvidenceBindings'),bindings,'PortablePostBuildBindingIndexIncomplete')
    for name,digest in bindings.items():
        require_equal(model['files'].get(name,{}).get('sha256'),digest,'PortablePostBuildManifestBindingMissing:'+name)
        require_equal(sha(root/name),digest,'PortablePostBuildBytesChanged:'+name)
    prefix=root/'Evidence/PostBuild/PostBuildAdmissionReview-a20-02'
    manifest=read(prefix/'manifest.json');review=read(prefix/'review.json')
    rows=validate_tool_review(manifest,review,sha(prefix/'manifest.json'))
    require_equal(rows,qualification['reviewedAdmissionToolGroup']['files'],'PortableReviewedManifestGroupMismatch')
    require_equal(qualification.get('originalRunStatus'),'FAIL','PortableOriginalFailMustRemain')
    require_equal(qualification.get('originalBuildStatus'),'BUILD_BLOCKED','PortableOriginalBlockedMustRemain')
    require_equal(qualification.get('originalRunInputsUnchangedClaim'),False,'PortableOriginalInputChangeMustRemain')
    # Local source restoration was rechecked before export; no D: source tree is
    # accessed here and no Player execution authority follows from this check.

def qualify(project,build):
    body=core(project,build,BUILD_ID,DIGEST); out=Path(build)/'PostBuildQualification-a20.json'
    with out.open('x',encoding='utf-8') as stream: stream.write(json.dumps(body,ensure_ascii=False,indent=2)+'\n')
    return {'status':body['status'],'qualificationSha256':sha(out),'originalFailAndBlockedPreserved':True}

if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('action',choices=['qualify','check']);parser.add_argument('--project',type=Path,default=PROJECT)
    args=parser.parse_args();build=args.project/ROOT_REL/('Build-'+BUILD_ID)
    print(json.dumps(qualify(args.project,build) if args.action=='qualify' else {'executable':str(require_postbuild(args.project,build))}))

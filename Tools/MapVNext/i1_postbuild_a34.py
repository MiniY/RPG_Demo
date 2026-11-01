"""Read-only exact-a34 admission after independently reviewed one-time cache recovery.

The original failed Run and blocked BuildValidation are never rewritten. This
module cannot restore fonts, bless another Build, or enroll live source hashes.
The tool group is snapshotted before independent review, separate from a34 input.
3558 source inputs and 158 output files are literal counts verified against the
retained actual a34 manifests. Six exact cache/guard/recovery evidence pins close
the portable dependency chain. This never enrolls live source hashes at runtime.
"""
from pathlib import Path
import argparse, hashlib, json

BUILD_ID = 'map-vnext-i1-integration-20261031-a34'
DIGEST = '32eee2d37814e3ac9bd4cc6ae712f2b6ae7b54c5b953eb023a8adfb6d12c2a62'
RUN_ID = '7ede2413e337478cb31795bbb8e15140'
PROJECT = Path(r'D:\.codex\worktrees\map-vnext-integration-i1\RPG_Demo')
ROOT_REL = 'Artifacts/MapAcceptance/MapVNext-Integration-I1-20261029'
QUAL_REL = ROOT_REL + '/Authoring/VersionedExtension/FontCacheQualification-a34'
REVIEW_REL = ROOT_REL + '/Authoring/VersionedExtension/PostBuildAdmissionReview-a34-02'
FONT = 'Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Fallback.asset'
BEFORE = '54406f3fac18227d9780201699a619dd60ebf495fe0179684c0ac200ec2c16b5'
GENERATED = '5e72dec23b070c83d6efdba58431d808ad1bfe48a8e563690f39c08ff4134a1d'
STATUS = 'REVIEWED_POSTBUILD_CACHE_RECOVERY_A34'
TOOL_FILES = {
 'Tools/EngineeringAssurance/assure.py',
 'Tools/EngineeringAssurance/unity_guard.py',
 'Tools/EngineeringAssurance/unity_supervisor.py',
 'Tools/MapVNext/cliff_motion.py',
 'Tools/MapVNext/deliver_i1.py',
 'Tools/MapVNext/deliver_p2.py',
 'Tools/MapVNext/deliver_readable.py',
 'Tools/MapVNext/i1_native_trace.py',
 'Tools/MapVNext/i1_postbuild_a16.py',
 'Tools/MapVNext/i1_postbuild_a17.py',
 'Tools/MapVNext/i1_postbuild_a20.py',
 'Tools/MapVNext/i1_postbuild_a21.py',
 'Tools/MapVNext/i1_postbuild_a34.py',
 'Tools/MapVNext/i1_postbuild_qualification.py',
 'Tools/MapVNext/run_i1.py',
 'Tools/MapVNext/test_i1_delivery.py',
 'Tools/MapVNext/test_i1_postbuild.py',
 'Tools/MapVNext/test_i1_postbuild_a16.py',
 'Tools/MapVNext/test_i1_postbuild_a17.py',
 'Tools/MapVNext/test_i1_postbuild_a20.py',
 'Tools/MapVNext/test_i1_postbuild_a21.py',
 'Tools/MapVNext/test_i1_postbuild_a34.py',
 'Tools/MapVNext/test_i1_runner.py',
}
# Every hash here predates this validator and comes from retained real evidence.
BINDINGS = {'receipt.json': 'da54fe725cee97d9d35cef84d5269e93e3b365fbbbe6914677b66f7f8262ad61', 'supervisor.json': '716bd03fe29e01956353c038713e90d584fac76dd1a823b713adf6c2e7cc4f09', 'native-trace.json': 'd8ce2d5e0fc5712681f60a123f498afbe65c561666d1f316e9975a2ec7a636c7', 'command.json': 'f1a4288d0f84be2add34fb30213e0b6cffdb75be2c99676e82298def4b218dcb', 'guard-after.json': '872ecc467557d0f4496796cdfa11944350b872713de21b5f9a2a610f0d4af8bc', 'font-side-effect.json': '4dae60a1c45fe2f72f1fc3110e923acf96be460804638d7e0ba8b6a8c910aa7a', 'build-inputs-after.json': '8777dcf27bd4343751b8186fe9fdb5b4908fe1b05b7004cc912ec446aaf35254', 'TMP.before.asset': '54406f3fac18227d9780201699a619dd60ebf495fe0179684c0ac200ec2c16b5', 'TMP.editor-generated.asset': '5e72dec23b070c83d6efdba58431d808ad1bfe48a8e563690f39c08ff4134a1d', 'source-fingerprint.txt': 'd3b6f91bee7a6675421f5822b856b3e893c2a80baeb8ed335ab3c073ca3b1545'}
BUILD_BINDINGS = {'BuildValidation.json': '0fae5b83d18cff10924903420a01f0268619908a8344f483ec148104523cba63', 'BuildReport.json': '9c591b7c0d4832ba8c5218eafb097f58e19d861235ae23d2a8c223a5e929bb61', 'build-inputs.json': '6df2d64e430ebbab466d4b5367d95f128958b82c1d53670a78408649ea3289d8', 'build-tree.json': '1d62c8aa5ac69e0e07788b10ca5f643ac78163dfb94442a634a7d6629ac66146', 'MVP_Windows_x64/RPG_Demo_MapVNext_I1.exe': '059c313352892718258a120b87bdc6fc7c23ce67181084abe14f86f204f5dddb'}
QUAL_BINDINGS = {'qualification.json': '0f949f7c263753d8547b1034478c200540ef2092e409af375386b60f83e5079a', 'independent-review.json': 'a2414430934bf9231ecc4bcf82020bf12cae79bcd8d843a4f043a6a48a609d0a', 'recovery-result.json': '45ccc93a807c8a6a4aedf7e3bd8d719db9cd75d2bcf6b4e7908eb446b0a855ad', 'recovery-start.json': 'b9742f77b4c8aa246659997b58f7a73c8636db0943678f53446fd44ca03bf419', 'recovery-guard-before.json': '80dd200bd0e2d458073c0b6a8aa3f6dd4fcb7131970f07358b6ce1a53773000a', 'recovery-guard-after.json': '901cad42794479f3d22fa13ce2bf2d8798e9c73c3b99625fdac330db13a18eae'}

def read(p): return json.loads(p.read_text('utf-8'))
def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest()
def tree(root): return {p.relative_to(root).as_posix(): sha(p) for p in sorted(root.rglob('*')) if p.is_file()}
def require_equal(actual, expected, reason):
    if actual != expected: raise RuntimeError(reason)
def require_bindings(root, rows):
    for name, expected in rows.items(): require_equal(sha(root/name), expected, 'PostBuildEvidenceChanged:'+name)

def require_identity(build_id, digest):
    require_equal((build_id,digest),(BUILD_ID,DIGEST),'OnlyExactA34PostBuildQualificationAllowed')

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
    if review.get('reviewPerformed') is not True or review.get('reviewer')!='/root/i1_contract_review' or review.get('status')!='APPROVED_EXACT_A34_POSTBUILD_ADMISSION_TOOLS' or type(review.get('openFindings')) is not int or review['openFindings']!=0 or review.get('reviewedManifestSha256')!=manifest_sha or set(review.get('axes',{}))!={'Standards','Spec'} or any(v!='NO_OPEN_FINDINGS' for v in review['axes'].values()): raise RuntimeError('IndependentPostBuildToolReviewRequired')
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
    require_equal(build, project/ROOT_REL/('Build-'+BUILD_ID), 'OnlyOriginalA34BuildDirectoryAllowed')
    run=project/ROOT_REL/'VersionedRuns/i1-windows-integrated-build-a34-01'; q=project/QUAL_REL
    for root, rows in [(run,BINDINGS),(build,BUILD_BINDINGS),(q,QUAL_BINDINGS)]: require_bindings(root,rows)
    recovery=read(q/'recovery-result.json')
    require_equal(recovery.get('status'),'ONE_TIME_A34_EXACT_RAW_BEFORE_RECOVERED','ActualRecoveryRequired')
    for field in ['full3558InputsExactlyEqualOriginalPrebuild','full158PlayerTreeUnchanged','originalFailureAndBlockedAndGeneratedEvidenceUnchanged']: require_equal(recovery.get(field),True,'IncompleteRecovery:'+field)
    require_equal(sha(q/'recovery-start.json'),recovery['startSha256'],'RecoveryStartEvidenceChanged')
    require_equal(sha(q/'recovery-guard-before.json'),read(q/'recovery-start.json')['guardSha256'],'RecoveryGuardEvidenceChanged')
    require_equal(sha(q/'recovery-guard-after.json'),recovery['guardAfterSha256'],'RecoveryAfterGuardEvidenceChanged')
    require_equal(read(q/'recovery-guard-after.json').get('state'),'FREE','RecoveryAfterGuardNotFree')
    require_equal((recovery.get('onlyBuildId'),recovery.get('onlyImplementationDigest'),recovery.get('originalRunId')),(BUILD_ID,DIGEST,RUN_ID),'RecoveryScopeMismatch')
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
    require_equal(read(Path(build)/'PostBuildQualification-a34.json'),expected,'ExactAppendOnlyPostBuildQualificationRequired')
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
    result.update({'Evidence/PostBuild/FontCacheQualification-a34/'+name:digest for name,digest in QUAL_BINDINGS.items()})
    tools=qualification['reviewedAdmissionToolGroup'];prefix='Evidence/PostBuild/PostBuildAdmissionReview-a34-02/'
    result[prefix+'manifest.json']=tools['manifestSha256'];result[prefix+'review.json']=tools['reviewSha256']
    if len(tools['files'])!=len(TOOL_FILES) or {row['path'] for row in tools['files']}!=TOOL_FILES:raise RuntimeError('PortableCompleteToolGroupRequired')
    result.update({prefix+row['path']:row['sha256'] for row in tools['files']})
    return result

def validate_portable_player_tree(root, model):
    # The original tree is trusted only after its fixed Build evidence SHA has
    # been checked. Editable report indexes cannot replace that retained tree.
    original=read(Path(root)/'Evidence/Build/build-tree.json')
    require_equal(len(original),158,'PortableOriginalPlayerTreeCountChanged')
    require_equal(model.get('buildTree'),original,'PortableModelTreeDoesNotMatchPinnedOriginal')
    require_equal(tree(Path(root)/'MVP_Windows_x64'),original,'PortableActualPlayerTreeDoesNotMatchPinnedOriginal')


def check_portable_evidence(root, model):
    marker=model.get('postBuildQualification')
    if model.get('build')!=BUILD_ID:
        if marker is not None:raise RuntimeError('PortableOtherBuildCannotCarryA34Exception')
        validation=root/'Evidence/Build/BuildValidation.json'
        if validation.is_file() and read(validation).get('status')=='BUILD_BLOCKED':raise RuntimeError('PortableBlockedOtherBuildCannotUseA34Exception')
        return
    if marker is None:raise RuntimeError('PortableKnownA34RequiresExplicitQualifiedMarker')
    path='Evidence/Build/PostBuildQualification-a34.json'
    require_equal(marker,{'status':STATUS,'path':path},'PortablePostBuildMarkerMismatch')
    qualification=read(root/path);bindings=portable_evidence_bindings(qualification)
    require_equal(model.get('postBuildEvidenceBindings'),bindings,'PortablePostBuildBindingIndexIncomplete')
    for name,digest in bindings.items():
        require_equal(model['files'].get(name,{}).get('sha256'),digest,'PortablePostBuildManifestBindingMissing:'+name)
        require_equal(sha(root/name),digest,'PortablePostBuildBytesChanged:'+name)
    validate_portable_player_tree(root,model)
    prefix=root/'Evidence/PostBuild/PostBuildAdmissionReview-a34-02'
    manifest=read(prefix/'manifest.json');review=read(prefix/'review.json')
    rows=validate_tool_review(manifest,review,sha(prefix/'manifest.json'))
    require_equal(rows,qualification['reviewedAdmissionToolGroup']['files'],'PortableReviewedManifestGroupMismatch')
    require_equal(qualification.get('originalRunStatus'),'FAIL','PortableOriginalFailMustRemain')
    require_equal(qualification.get('originalBuildStatus'),'BUILD_BLOCKED','PortableOriginalBlockedMustRemain')
    require_equal(qualification.get('originalRunInputsUnchangedClaim'),False,'PortableOriginalInputChangeMustRemain')
    # Local source restoration was rechecked before export; no D: source tree is
    # accessed here and no Player execution authority follows from this check.

def qualify(project,build):
    body=core(project,build,BUILD_ID,DIGEST); out=Path(build)/'PostBuildQualification-a34.json'
    with out.open('x',encoding='utf-8') as stream: stream.write(json.dumps(body,ensure_ascii=False,indent=2)+'\n')
    return {'status':body['status'],'qualificationSha256':sha(out),'originalFailAndBlockedPreserved':True}

if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('action',choices=['qualify','check']);parser.add_argument('--project',type=Path,default=PROJECT)
    args=parser.parse_args();build=args.project/ROOT_REL/('Build-'+BUILD_ID)
    print(json.dumps(qualify(args.project,build) if args.action=='qualify' else {'executable':str(require_postbuild(args.project,build))}))

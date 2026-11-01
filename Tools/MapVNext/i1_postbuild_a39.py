"""Read-only exact-a39 admission after independently reviewed one-time cache recovery.

The original failed Run and blocked BuildValidation are never rewritten. This
module cannot restore fonts, bless another Build, or enroll live source hashes.
The tool group is snapshotted before independent review, separate from a39 input.
3560 source inputs and 158 output files are literal counts verified against the
retained actual a39 manifests. Six exact cache/guard/recovery evidence pins close
the portable dependency chain. This never enrolls live source hashes at runtime.
"""
from pathlib import Path
import argparse, hashlib, json

BUILD_ID = 'map-vnext-i1-integration-20261031-a39'
DIGEST = '215b60db909d7de99779809db758a07fd8597a136ad41d1aef676f12c7b2e845'
RUN_ID = 'fcaa3e6d94f34b8ba8545fd46fe20429'
PROJECT = Path(r'D:\.codex\worktrees\map-vnext-integration-i1\RPG_Demo')
ROOT_REL = 'Artifacts/MapAcceptance/MapVNext-Integration-I1-20261029'
QUAL_REL = ROOT_REL + '/Authoring/VersionedExtension/FontCacheQualification-a39'
REVIEW_REL = ROOT_REL + '/Authoring/VersionedExtension/PostBuildAdmissionReview-a39-02'
FONT = 'Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Fallback.asset'
BEFORE = '54406f3fac18227d9780201699a619dd60ebf495fe0179684c0ac200ec2c16b5'
GENERATED = '5e72dec23b070c83d6efdba58431d808ad1bfe48a8e563690f39c08ff4134a1d'
STATUS = 'REVIEWED_POSTBUILD_CACHE_RECOVERY_A39'
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
 'Tools/MapVNext/i1_postbuild_a37.py',
 'Tools/MapVNext/i1_postbuild_a38.py',
 'Tools/MapVNext/i1_postbuild_a39.py',
 'Tools/MapVNext/i1_postbuild_qualification.py',
 'Tools/MapVNext/run_i1.py',
 'Tools/MapVNext/test_i1_delivery.py',
 'Tools/MapVNext/test_i1_postbuild.py',
 'Tools/MapVNext/test_i1_postbuild_a16.py',
 'Tools/MapVNext/test_i1_postbuild_a17.py',
 'Tools/MapVNext/test_i1_postbuild_a20.py',
 'Tools/MapVNext/test_i1_postbuild_a21.py',
 'Tools/MapVNext/test_i1_postbuild_a34.py',
 'Tools/MapVNext/test_i1_postbuild_a37.py',
 'Tools/MapVNext/test_i1_postbuild_a38.py',
 'Tools/MapVNext/test_i1_postbuild_a39.py',
 'Tools/MapVNext/test_i1_runner.py',
}
# Every hash here predates this validator and comes from retained real evidence.
BINDINGS = {'receipt.json': '2de736b0e01d52bb4c502b5ac554f2fcf9af0f9cbb2e604551b29cad255659e0', 'supervisor.json': '5913df97d76d7d771015205135038c11f09f34c6864bf2b08c1330f92f6aae74', 'native-trace.json': '0db5134e0604dd45643e8239b48e4eb4a74e2999e0421977e7b124342b6fcaa8', 'command.json': '327a2e32a27aa3a1199e5452e27ebb969e6a600f1c8e7a087d571eedeb7aabc2', 'guard-after.json': '75d56c3e8eb68e8e2c3223b65a1eac382a9d26c85636912fad92d17592598b77', 'font-side-effect.json': '4dae60a1c45fe2f72f1fc3110e923acf96be460804638d7e0ba8b6a8c910aa7a', 'build-inputs-after.json': '22ce7cc567fedff2ab6c8f1f0b51d523e249b16808ec7413eaa535fe2bb1b67c', 'TMP.before.asset': '54406f3fac18227d9780201699a619dd60ebf495fe0179684c0ac200ec2c16b5', 'TMP.editor-generated.asset': '5e72dec23b070c83d6efdba58431d808ad1bfe48a8e563690f39c08ff4134a1d', 'source-fingerprint.txt': '88825f9fcd34dde97dca94b176138686d4b57fd633309293eda3d9b9221bbfa1'}
BUILD_BINDINGS = {'BuildValidation.json': '367052d7be440eade17c3469c8e78610b46f3ea8868c2e4c38efc8817184b103', 'BuildReport.json': 'bbc640a83fb9f36b1b11f66edb3136436d24cb97c2f68ab0bc0226a28698deb4', 'build-inputs.json': 'af7902948426e2669581dd45db72ae9b3a07e32973403943084922109c83c656', 'build-tree.json': '52887e0d238006e20e807c1f55a97be28a6c3afd0c95be329e4dc73eb7eb227a', 'MVP_Windows_x64/RPG_Demo_MapVNext_I1.exe': '059c313352892718258a120b87bdc6fc7c23ce67181084abe14f86f204f5dddb'}
QUAL_BINDINGS = {'qualification.json': '1d6e8cf54f5f1f25df0799c3b3ce1d927bc276fb1528fee3c79b3a52c45d38b4', 'independent-review.json': 'a3d5f2faffdd28c77dcfeaea80d0b3ae82a6c2a2873f6d1dc0a435e01e1130c2', 'recovery-result.json': '0da578df542a65ba30d8ecbb38f53004ca979d92548b63bf8805f11c21f98cb6', 'recovery-start.json': '0c779739ae9b9c7a90091a4ee5a9e2ec3e4d487dd887014f0709394044c95a5f', 'recovery-guard-before.json': '622070b4fe2570ddda94dcf5e93be692500d82f21d0dff517486475fb3e14db4', 'recovery-guard-after.json': '251402df928b2c82dffe9e8810c19590018b52e00e4affa164cc808a92c12933'}

def read(p): return json.loads(p.read_text('utf-8'))
def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest()
def tree(root): return {p.relative_to(root).as_posix(): sha(p) for p in sorted(root.rglob('*')) if p.is_file()}
def require_equal(actual, expected, reason):
    if actual != expected: raise RuntimeError(reason)
def require_bindings(root, rows):
    for name, expected in rows.items(): require_equal(sha(root/name), expected, 'PostBuildEvidenceChanged:'+name)

def require_identity(build_id, digest):
    require_equal((build_id,digest),(BUILD_ID,DIGEST),'OnlyExactA39PostBuildQualificationAllowed')

def validate_inputs(current, before, after):
    require_equal(set(before), {'Assets','Packages','ProjectSettings'}, 'FullBuildInputRootsRequired')
    require_equal(set(after), set(before), 'FullAfterInputRootsRequired')
    changes = {kind+'/'+p for kind in before for p in set(before[kind]) | set(after[kind]) if before[kind].get(p)!=after[kind].get(p)}
    require_equal(changes, {FONT}, 'OnlyReviewedFontDeltaAllowed')
    require_equal(before['Assets'].get(FONT.removeprefix('Assets/')), BEFORE, 'WrongOriginalFont')
    require_equal(after['Assets'].get(FONT.removeprefix('Assets/')), GENERATED, 'UnknownGeneratedFont')
    require_equal(current, before, 'CurrentInputsMustExactlyEqualRestoredPrebuildIncludingAddedMissing')

def validate_recorded_build(report, validation):
    require_equal((report.get('status'),report.get('identity'),report.get('implementationDigest')),
                  ('BUILD_SUCCEEDED_PLAYER_NOT_RUN',BUILD_ID,DIGEST),'PinnedActualBuildIdentityMismatch')
    require_equal((validation.get('status'),validation.get('identity'),validation.get('fontDeltaQualified')),
                  ('BUILD_BLOCKED',BUILD_ID,False),'OriginalBlockedBuildIdentityMismatch')

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
    if review.get('reviewPerformed') is not True or review.get('reviewer')!='/root/i1_contract_review' or review.get('status')!='APPROVED_EXACT_A39_POSTBUILD_ADMISSION_TOOLS' or type(review.get('openFindings')) is not int or review['openFindings']!=0 or review.get('reviewedManifestSha256')!=manifest_sha or set(review.get('axes',{}))!={'Standards','Spec'} or any(v!='NO_OPEN_FINDINGS' for v in review['axes'].values()): raise RuntimeError('IndependentPostBuildToolReviewRequired')
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
    require_equal(build, project/ROOT_REL/('Build-'+BUILD_ID), 'OnlyOriginalA39BuildDirectoryAllowed')
    run=project/ROOT_REL/'VersionedRuns/i1-build39'; q=project/QUAL_REL
    for root, rows in [(run,BINDINGS),(build,BUILD_BINDINGS),(q,QUAL_BINDINGS)]: require_bindings(root,rows)
    validate_recorded_build(read(build/'BuildReport.json'),read(build/'BuildValidation.json'))
    recovery=read(q/'recovery-result.json')
    require_equal(recovery.get('status'),'ONE_TIME_EXACT_RAW_BEFORE_RECOVERY_VERIFIED','ActualRecoveryRequired')
    for field in ['all3560InputsMatchPrebuild','all158PlayerFilesUnchanged','originalFailAndBlockedUnchanged']: require_equal(recovery.get(field),True,'IncompleteRecovery:'+field)
    require_equal(sha(q/'recovery-start.json'),QUAL_BINDINGS['recovery-start.json'],'RecoveryStartEvidenceChanged')
    require_equal(sha(q/'recovery-guard-before.json'),read(q/'recovery-start.json')['guardBeforeSha256'],'RecoveryGuardEvidenceChanged')
    require_equal(sha(q/'recovery-guard-after.json'),recovery['guardAfterSha256'],'RecoveryAfterGuardEvidenceChanged')
    require_equal(read(q/'recovery-guard-after.json').get('state'),'FREE','RecoveryAfterGuardNotFree')
    require_equal((recovery.get('onlyBuildId'),recovery.get('onlyImplementationDigest'),recovery.get('originalRunId')),(BUILD_ID,DIGEST,RUN_ID),'RecoveryScopeMismatch')
    for row in read(q/'qualification.json')['callbackSource']: require_equal(sha(project/row['path']),row['sha256'],'TMPDependencyChanged')
    before=read(build/'build-inputs.json'); after=read(run/'build-inputs-after.json')
    validate_inputs({kind:tree(project/kind) for kind in before},before,after)
    require_equal(sum(map(len,before.values())),3560,'OriginalFullInputCountChanged')
    output=read(build/'build-tree.json'); require_equal(len(output),158,'OriginalPlayerTreeCountChanged')
    require_equal(tree(build/'MVP_Windows_x64'),output,'CompletePlayerTreeChangedIncludingAddedMissing')
    report=read(build/'BuildReport.json'); require_identity(report.get('identity'),report.get('implementationDigest'))
    require_equal((report.get('status'),report.get('errors'),report.get('warnings')),('BUILD_SUCCEEDED_PLAYER_NOT_RUN',0,0),'OriginalBuildReportChanged')
    exe=build/'MVP_Windows_x64/RPG_Demo_MapVNext_I1.exe'; require_equal(Path(report['executable']).resolve(),exe,'OriginalExecutableChanged')
    validate_lifecycle(read(run/'receipt.json'),read(run/'supervisor.json'),read(run/'native-trace.json'),read(run/'command.json'),read(run/'guard-after.json'),project,build)
    return {'status':STATUS,'buildId':BUILD_ID,'implementationDigest':DIGEST,'originalRunId':RUN_ID,'originalBuildStatus':'BUILD_BLOCKED','originalRunStatus':'FAIL','originalRunInputsUnchangedClaim':False,'sourceBaselineRestored':True,'playerOutputUnchanged':True,'oneTimeRecoveryOnly':True,'originalEvidence':BINDINGS,'originalBuildEvidence':BUILD_BINDINGS,'cacheQualificationEvidence':QUAL_BINDINGS,'reviewedAdmissionToolGroup':reviewed_tools(project),'toolingNotCompiledIntoOriginalBuild':True,'humanAcceptance':'PENDING','mergeReady':'NO'}

def verified_qualification(project, build, build_id=BUILD_ID, digest=DIGEST):
    expected=core(project,build,build_id,digest)
    require_equal(read(Path(build)/'PostBuildQualification-a39.json'),expected,'ExactAppendOnlyPostBuildQualificationRequired')
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
    result.update({'Evidence/PostBuild/FontCacheQualification-a39/'+name:digest for name,digest in QUAL_BINDINGS.items()})
    tools=qualification['reviewedAdmissionToolGroup'];prefix='Evidence/PostBuild/PostBuildAdmissionReview-a39-02/'
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
        if marker is not None:raise RuntimeError('PortableOtherBuildCannotCarryA39Exception')
        validation=root/'Evidence/Build/BuildValidation.json'
        if validation.is_file() and read(validation).get('status')=='BUILD_BLOCKED':raise RuntimeError('PortableBlockedOtherBuildCannotUseA39Exception')
        return
    if marker is None:raise RuntimeError('PortableKnownA39RequiresExplicitQualifiedMarker')
    path='Evidence/Build/PostBuildQualification-a39.json'
    require_equal(marker,{'status':STATUS,'path':path},'PortablePostBuildMarkerMismatch')
    qualification=read(root/path);bindings=portable_evidence_bindings(qualification)
    require_equal(model.get('postBuildEvidenceBindings'),bindings,'PortablePostBuildBindingIndexIncomplete')
    for name,digest in bindings.items():
        require_equal(model['files'].get(name,{}).get('sha256'),digest,'PortablePostBuildManifestBindingMissing:'+name)
        require_equal(sha(root/name),digest,'PortablePostBuildBytesChanged:'+name)
    validate_portable_player_tree(root,model)
    prefix=root/'Evidence/PostBuild/PostBuildAdmissionReview-a39-02'
    manifest=read(prefix/'manifest.json');review=read(prefix/'review.json')
    rows=validate_tool_review(manifest,review,sha(prefix/'manifest.json'))
    require_equal(rows,qualification['reviewedAdmissionToolGroup']['files'],'PortableReviewedManifestGroupMismatch')
    require_equal(qualification.get('originalRunStatus'),'FAIL','PortableOriginalFailMustRemain')
    require_equal(qualification.get('originalBuildStatus'),'BUILD_BLOCKED','PortableOriginalBlockedMustRemain')
    require_equal(qualification.get('originalRunInputsUnchangedClaim'),False,'PortableOriginalInputChangeMustRemain')
    # Local source restoration was rechecked before export; no D: source tree is
    # accessed here and no Player execution authority follows from this check.

def qualify(project,build):
    body=core(project,build,BUILD_ID,DIGEST); out=Path(build)/'PostBuildQualification-a39.json'
    with out.open('x',encoding='utf-8') as stream: stream.write(json.dumps(body,ensure_ascii=False,indent=2)+'\n')
    return {'status':body['status'],'qualificationSha256':sha(out),'originalFailAndBlockedPreserved':True}

if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('action',choices=['qualify','check']);parser.add_argument('--project',type=Path,default=PROJECT)
    args=parser.parse_args();build=args.project/ROOT_REL/('Build-'+BUILD_ID)
    print(json.dumps(qualify(args.project,build) if args.action=='qualify' else {'executable':str(require_postbuild(args.project,build))}))

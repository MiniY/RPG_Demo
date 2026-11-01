"""Read-only exact-a38 admission after independently reviewed one-time cache recovery.

The original failed Run and blocked BuildValidation are never rewritten. This
module cannot restore fonts, bless another Build, or enroll live source hashes.
The tool group is snapshotted before independent review, separate from a38 input.
3560 source inputs and 158 output files are literal counts verified against the
retained actual a38 manifests. Six exact cache/guard/recovery evidence pins close
the portable dependency chain. This never enrolls live source hashes at runtime.
"""
from pathlib import Path
import argparse, hashlib, json

BUILD_ID = 'map-vnext-i1-diagnostic-20261031-a38'
DIGEST = 'eafcd3c82d148ec60308c9a056b92af220f0b43cea07e66f39a5ed1b19a9618f'
RUN_ID = 'a4c4b146e04e42c9bc6e96d44a3b0f65'
PROJECT = Path(r'D:\.codex\worktrees\map-vnext-integration-i1\RPG_Demo')
ROOT_REL = 'Artifacts/MapAcceptance/MapVNext-Integration-I1-20261029'
QUAL_REL = ROOT_REL + '/Authoring/VersionedExtension/FontCacheQualification-a38'
REVIEW_REL = ROOT_REL + '/Authoring/VersionedExtension/PostBuildAdmissionReview-a38-02'
FONT = 'Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Fallback.asset'
BEFORE = '54406f3fac18227d9780201699a619dd60ebf495fe0179684c0ac200ec2c16b5'
GENERATED = '5e72dec23b070c83d6efdba58431d808ad1bfe48a8e563690f39c08ff4134a1d'
STATUS = 'REVIEWED_POSTBUILD_CACHE_RECOVERY_A38'
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
 'Tools/MapVNext/test_i1_runner.py',
}
# Every hash here predates this validator and comes from retained real evidence.
BINDINGS = {'receipt.json': '8ec93a036dbac23e70a4405da03ee54d99cd16dbbf0e9cf0194103808065f11e', 'supervisor.json': 'dce39ed71a1bcc6707970531364323bfd681bc02ee5ae8f829b0c9a6d0d40b82', 'native-trace.json': 'd70457b0ccd3fb061ba195de5aa5bc719512f9257314171c3794fbdcde750548', 'command.json': 'a4dce1dfaa673ac49cbd8aa85b14b67ec580a5764b690524dc31b9c33875d90b', 'guard-after.json': 'b43b0ee288f41f253256473159eb9e6db70f69936879c6d2df2e9095b56acce8', 'font-side-effect.json': '4dae60a1c45fe2f72f1fc3110e923acf96be460804638d7e0ba8b6a8c910aa7a', 'build-inputs-after.json': '42022a99252299ca435b68c38e76f14348fd84a98a65379c3519dffda0e0bc18', 'TMP.before.asset': '54406f3fac18227d9780201699a619dd60ebf495fe0179684c0ac200ec2c16b5', 'TMP.editor-generated.asset': '5e72dec23b070c83d6efdba58431d808ad1bfe48a8e563690f39c08ff4134a1d', 'source-fingerprint.txt': '19ab36b8bc5891adfff1c8182a0e757eeb9167b34677fa56595a82e7b0cebd10'}
BUILD_BINDINGS = {'BuildValidation.json': '9250efc6d706e4cb760265067d8069d880672fda04ee4bbae6ca03770b42fe43', 'BuildReport.json': '558a2d53bca92a91a7310b9150d2ce2a8ab185fd33c607092bbfc3f04657a85e', 'build-inputs.json': '16b6f7ba07f9d4ca9bebf52486cc83e5890ff64ed926b5c4f00490c83eed6684', 'build-tree.json': '518af783e2a021c6ea75df393593550c8e57763870bd39d04fcac8b30ae1bcc9', 'MVP_Windows_x64/RPG_Demo_MapVNext_I1.exe': '059c313352892718258a120b87bdc6fc7c23ce67181084abe14f86f204f5dddb'}
QUAL_BINDINGS = {'qualification.json': 'c58bfc335efa0e8536a1bdab4bd050b92385dddbd09bce3bf80c3b78cf8cc94e', 'independent-review.json': '13f48cb516cec23e9b38027974554d4c1a8736bf1d8c266406fde13ed48f3368', 'recovery-result.json': 'c2919baf7d9a4425d47a6926b1e365d5a41a7d94c6303905c60878a4f47a4b20', 'recovery-start.json': 'fb325be841c45f7b9dbd5b7e0db523236876fcb049f7d01960a5f1232d77e9b9', 'recovery-guard-before.json': 'ab3d66de00dd92215f23569f1654af4a3639461780ba3c41f7caaecabd1644be', 'recovery-guard-after.json': 'cb40bcef172102bc6d1bbdcd85ac3f18f5f737d0a8bc99f646b4f977e483c3af'}

def read(p): return json.loads(p.read_text('utf-8'))
def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest()
def tree(root): return {p.relative_to(root).as_posix(): sha(p) for p in sorted(root.rglob('*')) if p.is_file()}
def require_equal(actual, expected, reason):
    if actual != expected: raise RuntimeError(reason)
def require_bindings(root, rows):
    for name, expected in rows.items(): require_equal(sha(root/name), expected, 'PostBuildEvidenceChanged:'+name)

def require_identity(build_id, digest):
    require_equal((build_id,digest),(BUILD_ID,DIGEST),'OnlyExactA38PostBuildQualificationAllowed')

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
    if review.get('reviewPerformed') is not True or review.get('reviewer')!='/root/i1_contract_review' or review.get('status')!='APPROVED_EXACT_A38_POSTBUILD_ADMISSION_TOOLS' or type(review.get('openFindings')) is not int or review['openFindings']!=0 or review.get('reviewedManifestSha256')!=manifest_sha or set(review.get('axes',{}))!={'Standards','Spec'} or any(v!='NO_OPEN_FINDINGS' for v in review['axes'].values()): raise RuntimeError('IndependentPostBuildToolReviewRequired')
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
    require_equal(build, project/ROOT_REL/('Build-'+BUILD_ID), 'OnlyOriginalA38BuildDirectoryAllowed')
    run=project/ROOT_REL/'VersionedRuns/i1-build38'; q=project/QUAL_REL
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
    require_equal(read(Path(build)/'PostBuildQualification-a38.json'),expected,'ExactAppendOnlyPostBuildQualificationRequired')
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
    result.update({'Evidence/PostBuild/FontCacheQualification-a38/'+name:digest for name,digest in QUAL_BINDINGS.items()})
    tools=qualification['reviewedAdmissionToolGroup'];prefix='Evidence/PostBuild/PostBuildAdmissionReview-a38-02/'
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
        if marker is not None:raise RuntimeError('PortableOtherBuildCannotCarryA38Exception')
        validation=root/'Evidence/Build/BuildValidation.json'
        if validation.is_file() and read(validation).get('status')=='BUILD_BLOCKED':raise RuntimeError('PortableBlockedOtherBuildCannotUseA38Exception')
        return
    if marker is None:raise RuntimeError('PortableKnownA38RequiresExplicitQualifiedMarker')
    path='Evidence/Build/PostBuildQualification-a38.json'
    require_equal(marker,{'status':STATUS,'path':path},'PortablePostBuildMarkerMismatch')
    qualification=read(root/path);bindings=portable_evidence_bindings(qualification)
    require_equal(model.get('postBuildEvidenceBindings'),bindings,'PortablePostBuildBindingIndexIncomplete')
    for name,digest in bindings.items():
        require_equal(model['files'].get(name,{}).get('sha256'),digest,'PortablePostBuildManifestBindingMissing:'+name)
        require_equal(sha(root/name),digest,'PortablePostBuildBytesChanged:'+name)
    validate_portable_player_tree(root,model)
    prefix=root/'Evidence/PostBuild/PostBuildAdmissionReview-a38-02'
    manifest=read(prefix/'manifest.json');review=read(prefix/'review.json')
    rows=validate_tool_review(manifest,review,sha(prefix/'manifest.json'))
    require_equal(rows,qualification['reviewedAdmissionToolGroup']['files'],'PortableReviewedManifestGroupMismatch')
    require_equal(qualification.get('originalRunStatus'),'FAIL','PortableOriginalFailMustRemain')
    require_equal(qualification.get('originalBuildStatus'),'BUILD_BLOCKED','PortableOriginalBlockedMustRemain')
    require_equal(qualification.get('originalRunInputsUnchangedClaim'),False,'PortableOriginalInputChangeMustRemain')
    # Local source restoration was rechecked before export; no D: source tree is
    # accessed here and no Player execution authority follows from this check.

def qualify(project,build):
    body=core(project,build,BUILD_ID,DIGEST); out=Path(build)/'PostBuildQualification-a38.json'
    with out.open('x',encoding='utf-8') as stream: stream.write(json.dumps(body,ensure_ascii=False,indent=2)+'\n')
    return {'status':body['status'],'qualificationSha256':sha(out),'originalFailAndBlockedPreserved':True}

if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('action',choices=['qualify','check']);parser.add_argument('--project',type=Path,default=PROJECT)
    args=parser.parse_args();build=args.project/ROOT_REL/('Build-'+BUILD_ID)
    print(json.dumps(qualify(args.project,build) if args.action=='qualify' else {'executable':str(require_postbuild(args.project,build))}))

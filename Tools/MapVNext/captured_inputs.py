"""Explicit identity for a materialized Unity input tree. Never borrows a parent Git checkout."""
from pathlib import Path
import hashlib,json,os,subprocess,sys
from contextlib import contextmanager

def sha(path):return hashlib.sha256(Path(path).read_bytes()).hexdigest()

def require_git_boundary(project):
    project=Path(project).resolve()
    result=subprocess.run(['git','rev-parse','--show-toplevel'],cwd=project,capture_output=True,
                          env=dict(os.environ,GIT_OPTIONAL_LOCKS='0'))
    if result.returncode==0:
        if Path(result.stdout.decode('utf-8').strip()).resolve()!=project:
            raise RuntimeError('InheritedParentGitIsNotCandidateIdentity')
        return 'OWN_GIT_ROOT'
    error=result.stderr.decode('utf-8','replace')
    if result.returncode==128 and 'not a git repository' in error:return 'NO_GIT_SNAPSHOT'
    raise RuntimeError('CandidateGitIdentityUnknown: '+error)

def read_baseline(project,manifest_path,materialization_path=None):
    project=Path(project).resolve();manifest_path=Path(manifest_path).resolve()
    baseline=json.loads(manifest_path.read_text('utf-8-sig'))
    source=Path(baseline['sourceRoot']).resolve()
    if materialization_path is None:
        if source!=project:raise RuntimeError('Captured baseline source root differs; materialization binding required')
    else:
        receipt=json.loads(Path(materialization_path).read_text('utf-8-sig'))
        if (receipt.get('schema')!=1 or receipt.get('kind')!='EXACT_SOURCE_MATERIALIZATION' or
            receipt.get('sourceFilesExactAtMaterialization') is not True or
            Path(receipt['candidateRoot']).resolve()!=project or Path(receipt['sourceRoot']).resolve()!=source or
            Path(receipt['snapshotManifest']).resolve()!=manifest_path or
            receipt.get('snapshotManifestSha256')!=sha(manifest_path) or
            receipt.get('snapshotId')!=baseline.get('snapshotId') or
            receipt.get('sourceFingerprint')!=baseline.get('sourceFingerprint')):
            raise RuntimeError('CandidateMaterializationBindingMismatch')
    for name,value in baseline['sourceFiles'].items():
        rel=Path(name)
        if rel.is_absolute() or '\\' in name or any(p in ('..','.') for p in name.split('/')) or not name.startswith(('Assets/','Packages/','ProjectSettings/','Tools/')) or len(value)!=64:
            raise RuntimeError('InvalidCapturedSourcePath')
    require_git_boundary(project)
    return baseline

def unity_inventory(project):
    sys.path.insert(0,str(Path(__file__).resolve().parents[1]/'EngineeringAssurance'))
    from unity_project_snapshot import source_inventory
    return source_inventory(Path(project))

def fingerprint(project,manifest_path,materialization_path=None):
    project=Path(project).resolve();baseline=read_baseline(project,manifest_path,materialization_path)
    files=unity_inventory(project)
    payload={'kind':'CAPTURED_UNITY_INPUTS_V1','projectRoot':str(project),'snapshotManifestSha256':sha(manifest_path),
             'materializationSha256':sha(materialization_path) if materialization_path else None,'files':files}
    digest=hashlib.sha256(json.dumps(payload,sort_keys=True,separators=(',',':')).encode('utf-8')).hexdigest()
    return {'schemaVersion':1,'identityKind':'EXPLICIT_CAPTURED_BASELINE','projectRoot':str(project),
            'sourceRoot':baseline['sourceRoot'],'sourceHeadProvenance':baseline['sourceGit']['head'],
            'snapshotId':baseline['snapshotId'],'snapshotManifestSha256':sha(manifest_path),
            'materializationSha256':payload['materializationSha256'],'sourceFingerprint':digest,'unityInputCount':len(files),
            'gitBoundary':require_git_boundary(project)}

@contextmanager
def private_editor_settings(project,run_id):
    """A test-only baked namespace also protects preferences before any test SetUp runs."""
    project=Path(project).resolve()
    import unity_guard
    if unity_guard.status(project)['state']!='FREE':raise RuntimeError('PrivateEditorNamespaceRequiresIdleProject')
    path=project/'ProjectSettings/ProjectSettings.asset';before=path.read_bytes();text=before.decode('utf-8-sig')
    import re
    if len(re.findall(r'^  companyName: [^\r\n]+',text,re.M))!=1 or len(re.findall(r'^  productName: [^\r\n]+',text,re.M))!=1:raise RuntimeError('UnknownPlayerSettingsIdentityFormat')
    text=re.sub(r'^  companyName: [^\r\n]+','  companyName: CodexShopEditorAcceptance',text,flags=re.M)
    text=re.sub(r'^  productName: [^\r\n]+','  productName: '+run_id,text,flags=re.M)
    generated=text.encode('utf-8');path.write_bytes(generated)
    out=project/'Artifacts/MapAcceptance/MapVNext-Integration-I1-20261029/VersionedRuns'/run_id
    try:yield
    finally:
        if unity_guard.status(project)['state']!='FREE':raise RuntimeError('EditorNamespaceRestoreBlockedByLiveOrUnknownOwner')
        after=path.read_bytes()
        # The existing runner restores its known runInBackground side effect first.
        if after!=generated:raise RuntimeError('UnexpectedEditorNamespaceSettingsDeltaLeftForDiagnosis')
        path.write_bytes(before)
        out.mkdir(parents=True,exist_ok=True)
        (out/'private-editor-settings.json').write_text(json.dumps({'company':'CodexShopEditorAcceptance','product':run_id,'sourceBeforeSha256':hashlib.sha256(before).hexdigest(),'testSha256':hashlib.sha256(generated).hexdigest(),'sourceRestored':path.read_bytes()==before,'mainFormalDataUsed':False},indent=2)+'\n',encoding='utf-8')

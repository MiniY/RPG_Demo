"""Task commands use existing Guard.run and its shared Supervisor; no lifecycle implementation here."""
import argparse,hashlib,json,os,pathlib,sys,subprocess,re
C=pathlib.Path(__file__).resolve().parents[2]
sys.path.insert(0,str(C/'Tools/EngineeringAssurance'))
import unity_guard as guard
ROOT=C/'Artifacts/MapAcceptance/MapVNextStairs-20261028-r3-ReviewFix'
UNITY=r'D:\UnityHubSetup\Unity\Hub\Editor\2022.3.62f3c1\Editor\Unity.exe'
def save(p,d):p.write_text(json.dumps(d,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
def digest(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def tree(root):return {p.relative_to(root).as_posix():digest(p) for p in sorted(root.rglob('*')) if p.is_file()}

def main():
    p=argparse.ArgumentParser();p.add_argument('phase',choices=['prepare','refresh','edit','play','build','player','p2prepare','p2play','p2build','p2player']);p.add_argument('--id',required=True);p.add_argument('--timeout',type=int,default=900);p.add_argument('--base-only',action='store_true');p.add_argument('--test-filter');p.add_argument('--batch-phase',choices=['pilot','full','determinism','cliff'],default='full');a=p.parse_args()
    p2=a.phase.startswith('p2');phase=a.phase[2:] if p2 else a.phase
    root=C/'Artifacts/MapAcceptance/MapVNextP2Random-20261029' if p2 else ROOT
    identity=re.search(r'public const string Identity="([^"]+)"',(C/'Assets/Scripts/MapGeneration/VNext/MapVNextRandomLayout.cs').read_text('utf-8')).group(1) if p2 else 'map-vnext-p1-stairs-20261028-r3d'
    build_root=root/('Build-'+identity.rsplit('-',1)[-1])/'MVP_Windows_x64' if p2 else root/'MVP_Windows_x64'
    out=root/'Runs'/a.id;out.mkdir(parents=True,exist_ok=False)
    os.environ['RPG_MAP_VNEXT_ISOLATED']='1';os.environ['RPG_MAP_VNEXT_RUNTIME_ROOT']=str(root/'RuntimeData'/a.id)
    fp=subprocess.check_output([sys.executable,str(C/'Tools/EngineeringAssurance/assure.py'),'fingerprint'],cwd=C).decode().strip()
    (out/'source-fingerprint.txt').write_text(fp,encoding='utf-8')
    save(out/'guard-before.json',before:=guard.status(C))
    if before['state']!='FREE':raise RuntimeError('GUARD_NOT_FREE:'+before['state'])
    os.environ['RPG_P2_SOURCE_FINGERPRINT']=json.loads(fp)['sourceFingerprint']
    validator=None;build_tree=None;build_inputs=None
    def runtime_inputs():return {name:tree(C/name) for name in ("Assets","Packages","ProjectSettings")}
    if p2 and phase=="build":
        if build_root.exists():
            save(out/"precondition.json",dict(status="BLOCKED",reason="P2_BUILD_IDENTITY_ALREADY_HAS_PLAYER",launched=False))
            raise RuntimeError("P2_BUILD_EXISTS_USE_NEW_IDENTITY_DO_NOT_OVERWRITE_INPUTS")
        build_inputs=runtime_inputs();build_root.parent.mkdir(parents=True,exist_ok=True);save(build_root.parent/"build-inputs.json",build_inputs)
    if phase=='player':
        report=json.loads(((build_root.parent if p2 else root)/'BuildReport.json').read_text(encoding='utf-8'));exe=pathlib.Path(report['executable']).resolve()
        assert report['status']=='PASS' and report['identity']==identity and exe.name==('MapVNextRandom.exe' if p2 else 'MapVNextStairs.exe')
        assert exe.is_relative_to(build_root.resolve())
        expected=digest(exe);build_tree=tree(build_root);save(out/'build-tree-before.json',build_tree)
        if p2:
            if build_tree!=json.loads((build_root.parent/'build-tree.json').read_text('utf-8')):raise RuntimeError('P2_PLAYER_TREE_DIFFERS_FROM_BUILT_TREE')
            build_inputs=json.loads((build_root.parent/'build-inputs.json').read_text('utf-8'))
            if runtime_inputs()!=build_inputs:raise RuntimeError('P2_BUILD_RUNTIME_INPUTS_CHANGED_REBUILD_WITH_NEW_IDENTITY')
        command=[str(exe),'-screen-width','1280','-screen-height','720','-screen-fullscreen','0','-logFile',str(out/'player.log'),'--map-vnext-p2-accept' if p2 else '--map-vnext-accept',str(out/'Player')]
        if p2:command+=['--p2-phase',a.batch_phase]
        if a.base_only:command+=['--map-vnext-base-only']
        def validator(project,actual):
            if pathlib.Path(project).resolve()!=C or actual!=command or digest(exe)!=expected or tree(build_root)!=build_tree:raise ValueError('PLAYER_BUILD_OR_COMMAND_CHANGED')
    else:
        command=[UNITY,'-batchmode','-projectPath',str(C),'-logFile',str(out/'unity.log')]
        if phase in ('edit','play'):
            command+=['-runTests','-testPlatform','EditMode' if phase=='edit' else 'PlayMode','-testFilter',('MapVNextCliffTests|MapVNextP2ContractTests|InterfaceAndRelativeLowerSeam_AllEightPairs_KeepTrueDownwardBorders|RenderedHeightColor_IsIndependentOfNearbyDistantOrAbsentWater|AllTwelveSeedApproaches_UseActualFootClearanceWithoutChangingMapData') if p2 else ('MapVNextEditTests|MapVNextR3EditTests' if phase=='edit' else 'MapVNextR3RegressionTests|MapVNextPlayTests'),'-testResults',str(out/'results.xml')]
        else:command+=['-executeMethod',({'prepare':'MapVNextP2Build.Prepare','build':'MapVNextP2Build.Build'} if p2 else {'prepare':'MapVNextBuild.Prepare','refresh':'MapVNextBuild.RefreshCandidateScene','build':'MapVNextBuild.Build'})[phase],'-quit']
    if a.test_filter:
        if phase not in ('edit','play'):raise ValueError('TEST_FILTER_ONLY_FOR_TESTS')
        command[command.index('-testFilter')+1]=a.test_filter
    os.environ['RPG_CLIFF_EVIDENCE']=str(out/'Cliff')
    save(out/'command.json',dict(command=command,phase=a.phase,project=str(C),runtimeRoot=os.environ['RPG_MAP_VNEXT_RUNTIME_ROOT']))
    source_settings=C/'ProjectSettings/ProjectSettings.asset';settings_before=source_settings.read_bytes()
    result=guard.run(C,'map-vnext-stairs-'+a.id,'Map VNext '+a.phase,phase,command,a.timeout,command_validator=validator)
    save(out/'supervisor.json',result)
    settings_after=source_settings.read_bytes()
    if settings_after!=settings_before:
        # Application.runInBackground in Editor tests writes this known property back.
        # Preserve generated evidence, accept only this exact single-property side effect,
        # then restore the bytes read before this owned run. Unknown changes are never restored.
        before_text=settings_before.decode('utf-8-sig').replace('\r\n','\n');after_text=settings_after.decode('utf-8-sig').replace('\r\n','\n')
        if before_text.replace('  runInBackground: 0\n','  runInBackground: 1\n')!=after_text:raise RuntimeError('UNEXPECTED_PROJECT_SETTINGS_SIDE_EFFECT')
        (out/'ProjectSettings.editor-generated.asset').write_bytes(settings_after)
        source_settings.write_bytes(settings_before)
        save(out/'settings-side-effect.json',dict(property='runInBackground',beforeSha256=hashlib.sha256(settings_before).hexdigest(),generatedSha256=hashlib.sha256(settings_after).hexdigest(),restoredSha256=digest(source_settings)))
    build_match=True
    if phase=='player':
        build_after=tree(build_root);save(out/'build-tree-after.json',build_after);build_match=build_after==build_tree
    elif phase=='build' and ((build_root.parent if p2 else root)/'BuildReport.json').exists():save((build_root.parent if p2 else root)/'build-tree.json',tree(build_root))
    if p2 and phase=='build':
        save(out/'build-inputs-after.json',runtime_inputs());build_match=runtime_inputs()==build_inputs
    save(out/'guard-after.json',after:=guard.status(C))
    ok=build_match and result.get('naturalExit') and result.get('exitCode')==0 and result.get('cleanup',{}).get('status')=='PASS' and not result.get('cleanup',{}).get('forced') and not result.get('cleanup',{}).get('residualPids') and all(result.get('locks',{}).get(k,{}).get('released') for k in ['project','heavy']) and after['state']=='FREE'
    receipt=dict(buildTreeMatched=build_match,phase=a.phase,runId=result.get('runId'),status='PASS' if ok else 'FAIL',exitCode=result.get('exitCode'),naturalExit=result.get('naturalExit'),timedOut=result.get('state')=='TIMEOUT',cleanup=result.get('cleanup'),guard=after['state'])
    save(out/'receipt.json',receipt);print(json.dumps(receipt));return 0 if ok else 2
if __name__=='__main__':sys.exit(main())

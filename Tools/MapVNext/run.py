"""Task commands use existing Guard.run and its shared Supervisor; no lifecycle implementation here."""
import argparse,hashlib,json,os,pathlib,sys,subprocess
C=pathlib.Path(__file__).resolve().parents[2]
sys.path.insert(0,str(C/'Tools/EngineeringAssurance'))
import unity_guard as guard
ROOT=C/'Artifacts/MapAcceptance/MapVNextStairs-20261028'
UNITY=r'D:\UnityHubSetup\Unity\Hub\Editor\2022.3.62f3c1\Editor\Unity.exe'
def save(p,d):p.write_text(json.dumps(d,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
def digest(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def main():
    p=argparse.ArgumentParser();p.add_argument('phase',choices=['prepare','refresh','edit','play','build','player']);p.add_argument('--id',required=True);p.add_argument('--timeout',type=int,default=900);p.add_argument('--base-only',action='store_true');a=p.parse_args()
    out=ROOT/'Runs'/a.id;out.mkdir(parents=True,exist_ok=False)
    os.environ['RPG_MAP_VNEXT_ISOLATED']='1';os.environ['RPG_MAP_VNEXT_RUNTIME_ROOT']=str(ROOT/'RuntimeData'/a.id)
    fp=subprocess.check_output([sys.executable,str(C/'Tools/EngineeringAssurance/assure.py'),'fingerprint'],cwd=C).decode().strip()
    (out/'source-fingerprint.txt').write_text(fp,encoding='utf-8')
    save(out/'guard-before.json',before:=guard.status(C))
    if before['state']!='FREE':raise RuntimeError('GUARD_NOT_FREE:'+before['state'])
    validator=None
    if a.phase=='player':
        report=json.loads((ROOT/'BuildReport.json').read_text(encoding='utf-8'));exe=pathlib.Path(report['executable']).resolve()
        assert report['status']=='PASS' and report['identity']=='map-vnext-p1-stairs-20261028-r2' and exe.name=='MapVNextStairs.exe'
        assert exe.is_relative_to((ROOT/'MVP_Windows_x64').resolve())
        expected=digest(exe)
        command=[str(exe),'-screen-width','1280','-screen-height','720','-screen-fullscreen','0','-logFile',str(out/'player.log'),'--map-vnext-accept',str(out/'Player')]
        if a.base_only:command+=['--map-vnext-base-only']
        def validator(project,actual):
            if pathlib.Path(project).resolve()!=C or actual!=command or digest(exe)!=expected:raise ValueError('PLAYER_BUILD_OR_COMMAND_CHANGED')
    else:
        command=[UNITY,'-batchmode','-projectPath',str(C),'-logFile',str(out/'unity.log')]
        if a.phase in ('edit','play'):
            command+=['-runTests','-testPlatform','EditMode' if a.phase=='edit' else 'PlayMode','-testFilter','MapVNextEditTests' if a.phase=='edit' else 'MapVNextPlayTests','-testResults',str(out/'results.xml')]
        else:command+=['-executeMethod',{'prepare':'MapVNextBuild.Prepare','refresh':'MapVNextBuild.RefreshCandidateScene','build':'MapVNextBuild.Build'}[a.phase],'-quit']
    save(out/'command.json',dict(command=command,phase=a.phase,project=str(C),runtimeRoot=os.environ['RPG_MAP_VNEXT_RUNTIME_ROOT']))
    source_settings=C/'ProjectSettings/ProjectSettings.asset';settings_before=source_settings.read_bytes()
    result=guard.run(C,'map-vnext-stairs-'+a.id,'Map VNext '+a.phase,a.phase,command,a.timeout,command_validator=validator)
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
    save(out/'guard-after.json',after:=guard.status(C))
    ok=result.get('naturalExit') and result.get('exitCode')==0 and result.get('cleanup',{}).get('status')=='PASS' and not result.get('cleanup',{}).get('forced') and not result.get('cleanup',{}).get('residualPids') and all(result.get('locks',{}).get(k,{}).get('released') for k in ['project','heavy']) and after['state']=='FREE'
    receipt=dict(phase=a.phase,runId=result.get('runId'),status='PASS' if ok else 'FAIL',exitCode=result.get('exitCode'),naturalExit=result.get('naturalExit'),timedOut=result.get('timedOut'),cleanup=result.get('cleanup'),guard=after['state'])
    save(out/'receipt.json',receipt);print(json.dumps(receipt));return 0 if ok else 2
if __name__=='__main__':sys.exit(main())

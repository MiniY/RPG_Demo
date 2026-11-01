"""I1 adapter over unchanged Guard -> Supervisor. Build/Player identity and private-data checks are task-local."""
from pathlib import Path
import argparse,json,os,sys,subprocess,hashlib,re
C=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(C/'Tools/EngineeringAssurance'));import unity_guard as guard
from i1_native_trace import Trace,selftest
UNITY=r'D:\UnityHubSetup\Unity\Hub\Editor\2022.3.62f3c1\Editor\Unity.exe'
ROOT=C/'Artifacts/MapAcceptance/MapVNext-Integration-I1-20261029'
FONT='Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Fallback.asset'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def save(p,d):p.write_text(json.dumps(d,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
def tree(root):return {p.relative_to(root).as_posix():sha(p) for p in sorted(root.rglob('*')) if p.is_file()}
def inputs():return {name:tree(C/name) for name in ('Assets','Packages','ProjectSettings')}
def preserve_font_delta(font,before,out):
    generated=font.read_bytes()
    if generated==before:return True
    (out/'TMP.before.asset').write_bytes(before)
    (out/'TMP.editor-generated.asset').write_bytes(generated)
    save(out/'font-side-effect.json',{'status':'UNKNOWN_UNQUALIFIED_DELTA_BUILD_BLOCKED','path':FONT,'beforeSha256':hashlib.sha256(before).hexdigest(),'generatedSha256':hashlib.sha256(generated).hexdigest(),'currentSha256':sha(font),'restored':False,'cause':'NOT_DETERMINED','requires':'Independent exact cache-delta qualification before any source restoration or Player use'})
    return False

def current_id():
    source=(C/'Assets/Scripts/MapGeneration/SpatialVersion/MapHostBuildStamp.cs').read_text('utf-8')
    match=re.search(r'public const string BuildId="([a-zA-Z0-9_-]+)";',source)
    if not match:raise RuntimeError('SealedBuildIdentityMissing')
    return match.group(1)
def require_player(report,build):
    validation=json.loads((build/'BuildValidation.json').read_text('utf-8'))
    if current_id()=='map-vnext-i1-integration-20261030-a14':
        from i1_postbuild_qualification import require_postbuild
        return require_postbuild(C,build,current_id(),report.get('implementationDigest'))
    if current_id()=='map-vnext-i1-diagnostic-20261030-a16':
        from i1_postbuild_a16 import require_postbuild
        return require_postbuild(C,build,current_id(),report.get('implementationDigest'))
    if current_id()=='map-vnext-i1-integration-20261030-a17':
        from i1_postbuild_a17 import require_postbuild
        return require_postbuild(C,build,current_id(),report.get('implementationDigest'))
    if current_id()=='map-vnext-i1-integration-20261030-a20':
        from i1_postbuild_a20 import require_postbuild
        return require_postbuild(C,build,current_id(),report.get('implementationDigest'))
    if current_id()=='map-vnext-i1-integration-20261030-a21':
        from i1_postbuild_a21 import require_postbuild
        return require_postbuild(C,build,current_id(),report.get('implementationDigest'))
    if current_id()=='map-vnext-i1-integration-20261031-a34':
        from i1_postbuild_a34 import require_postbuild
        return require_postbuild(C,build,current_id(),report.get('implementationDigest'))
    if current_id()=='map-vnext-i1-diagnostic-20261031-a37':
        from i1_postbuild_a37 import require_postbuild
        return require_postbuild(C,build,current_id(),report.get('implementationDigest'))
    if current_id()=='map-vnext-i1-integration-20261031-a41':
        from i1_postbuild_a41 import require_postbuild
        return require_postbuild(C,build,current_id(),report.get('implementationDigest'))
    if current_id()=='map-vnext-i1-integration-20261031-a40':
        from i1_postbuild_a40 import require_postbuild
        return require_postbuild(C,build,current_id(),report.get('implementationDigest'))
    if current_id()=='map-vnext-i1-integration-20261031-a39':
        from i1_postbuild_a39 import require_postbuild
        return require_postbuild(C,build,current_id(),report.get('implementationDigest'))
    if current_id()=='map-vnext-i1-diagnostic-20261031-a38':
        from i1_postbuild_a38 import require_postbuild
        return require_postbuild(C,build,current_id(),report.get('implementationDigest'))
    if validation.get('status')!='BUILD_INPUTS_AND_LIFECYCLE_VERIFIED' or validation.get('identity')!=current_id() or not validation.get('fontDeltaQualified') or sha(Path(validation['receiptPath']))!=validation['receiptSha256']:raise RuntimeError('QualifiedBuildLifecycleReceiptRequired')
    exe=Path(report['executable']).resolve();player=build/'MVP_Windows_x64'
    if report.get('status')!='BUILD_SUCCEEDED_PLAYER_NOT_RUN' or report.get('identity')!=current_id() or exe.name!='RPG_Demo_MapVNext_I1.exe' or not exe.is_relative_to(player.resolve()):raise RuntimeError('ExactI1BuildReceiptRequired')
    return exe

def lifecycle_qualified(result,after,matched,native,expected_exit=0):
    return matched and native.get('status')=='VALID_NATIVE_TIME_LINE' and result.get('naturalExit') and result.get('exitCode')==expected_exit and result.get('cleanup',{}).get('status')=='PASS' and not result.get('cleanup',{}).get('forced') and not result.get('cleanup',{}).get('residualPids') and all(result.get('locks',{}).get(k,{}).get('released') for k in ('project','heavy')) and after.get('state')=='FREE'

def recover_known_font_cache(font,before,out,result,native,before_inputs,build):
    generated=font.read_bytes()
    if generated==before:return True
    (out/'TMP.before.asset').write_bytes(before);(out/'TMP.editor-generated.asset').write_bytes(generated)
    # Reuse the independently reviewed exact byte pair; never enroll a live new hash.
    import i1_postbuild_a41 as rule
    if sha(font)!=rule.GENERATED or hashlib.sha256(before).hexdigest()!=rule.BEFORE:
        save(out/'font-unknown-delta.json',{'status':'UNKNOWN_CACHE_DELTA_NOT_RESTORED','before':hashlib.sha256(before).hexdigest(),'generated':sha(font)});return False
    current_inputs=inputs()
    changes={kind+'/'+name for kind in set(before_inputs)|set(current_inputs) for name in set(before_inputs.get(kind,{}))|set(current_inputs.get(kind,{})) if before_inputs.get(kind,{}).get(name)!=current_inputs.get(kind,{}).get(name)}
    if changes!={FONT}:
        save(out/'font-recovery-blocked.json',{'status':'OTHER_PRODUCT_INPUTS_CHANGED_NOT_RESTORED','changes':sorted(changes)});return False
    if not lifecycle_qualified(result,guard.status(C),True,native):return False
    for name in ('qualification.json','independent-review.json'):
        if sha(C/rule.QUAL_REL/name)!=rule.QUAL_BINDINGS[name]:raise RuntimeError('OriginalCacheRuleEvidenceChanged')
    original_qualification=json.loads((C/rule.QUAL_REL/'qualification.json').read_text('utf-8'))
    callbacks=original_qualification['callbackSource']
    if len(callbacks)!=3 or any(sha(C/row['path'])!=row['sha256'] for row in callbacks):
        save(out/'font-recovery-blocked.json',{'status':'ORIGINAL_TMP_CALLBACK_PINS_MISMATCH_NOT_RESTORED'});return False
    output_tree=tree(build/'MVP_Windows_x64')
    if not output_tree or 'RPG_Demo_MapVNext_I1.exe' not in output_tree or not (build/'BuildReport.json').is_file():raise RuntimeError('CompletedPlayerTreeBeforeCacheRecoveryRequired')
    report_sha=sha(build/'BuildReport.json')
    if sha(font)!=rule.GENERATED:raise RuntimeError('FontChangedDuringQualification')
    font.write_bytes(before)
    if inputs()!=before_inputs or tree(build/'MVP_Windows_x64')!=output_tree:raise RuntimeError('SourceOrPlayerChangedDuringKnownCacheRecovery')
    save(out/'font-reviewed-recovery.json',{'status':'EXISTING_EXACT_CACHE_RULE_REUSED_SOURCE_RESTORED','beforeSha256':hashlib.sha256(before).hexdigest(),'generatedSha256':hashlib.sha256(generated).hexdigest(),'restoredSha256':sha(font),'ruleIndependentReviewSha256':rule.QUAL_BINDINGS['independent-review.json'],'originalNewRunId':result['runId'],'sourceAtUnityExit':'KNOWN_REVIEWED_CACHE_DELTA_NOT_ORIGINAL_BYTES','preRecoveryOnlyFontDiffered':True,'callbackSource':callbacks,'buildReportSha256':report_sha,'playerTree':output_tree,'newRuntimeHashEnrolled':False,'oldFailReceiptRewritten':False})
    return sha(font)==hashlib.sha256(before).hexdigest()

def main():
    p=argparse.ArgumentParser();p.add_argument('phase',choices=['edit','play','prepare','build','player']);p.add_argument('--id',required=True);p.add_argument('--filter',default='MapVNextI1SeamTests');p.add_argument('--timeout',type=int,default=900)
    p.add_argument('--player-phase',choices=['pilot','full','restore','startup'],default='pilot');p.add_argument('--build-dir',type=Path);p.add_argument('--restore-copy',type=Path)
    p.add_argument('--startup-target',choices=['normal','i1']);p.add_argument('--startup-request',choices=['normal','i1']);p.add_argument('--expect-startup-rejection',action='store_true')
    a=p.parse_args();out=ROOT/'VersionedRuns'/a.id;out.mkdir(parents=True,exist_ok=False)
    save(out/'observer-offline-selftest.json',selftest());save(out/'guard-before.json',before:=guard.status(C))
    if before['state']!='FREE':raise RuntimeError('GUARD_NOT_FREE:'+before['state'])
    os.environ['RPG_I1_RUNTIME_ROOT']=str(ROOT/'RuntimeData'/a.id);os.environ['RPG_I1_EVIDENCE']=str(out)
    fingerprint=subprocess.check_output([sys.executable,str(C/'Tools/EngineeringAssurance/assure.py'),'fingerprint'],cwd=C)
    (out/'source-fingerprint.txt').write_bytes(fingerprint);os.environ['RPG_I1_SOURCE_FINGERPRINT']=json.loads(fingerprint)['sourceFingerprint']
    variant={'normal':'DEFAULT','i1':'I1_PRIVATE'}.get(a.startup_target)
    if a.expect_startup_rejection and (a.phase!='player' or variant is None or a.startup_request is None):raise RuntimeError('ExplicitOwnedStartupRejectionRequired')
    if a.phase=='build' and variant is None and json.loads((C/'Assets/MapGeneration/MapVNext/I1/Resources/MapVNextI1Host.json').read_text('utf-8')).get('schema')==2:raise RuntimeError('ExplicitStartupBuildTargetRequired')
    if variant is not None:os.environ['RPG_I1_BUILD_TARGET']=variant
    default_build=ROOT/('Build-'+current_id())
    if variant is not None:default_build=default_build/variant
    build=(a.build_dir or default_build).resolve();validator=None;built_tree=None;build_inputs=None
    if a.phase=='player':
        if not build.is_relative_to(ROOT.resolve()):raise RuntimeError('I1BuildMustBeInsideOwnArtifactRoot')
        report=json.loads((build/'BuildReport.json').read_text('utf-8'));exe=require_player(report,build)
        built_tree=json.loads((build/'build-tree.json').read_text('utf-8'));build_inputs=json.loads((build/'build-inputs.json').read_text('utf-8'))
        if tree(build/'MVP_Windows_x64')!=built_tree or inputs()!=build_inputs:raise RuntimeError('I1BuildTreeOrRuntimeSourcesChanged')
        save(out/'build-tree-before.json',built_tree)
        command=[str(exe),'-screen-width','1920','-screen-height','1080','-screen-fullscreen','0','-logFile',str(out/'player.log'),'--i1-accept',str(out/'Player'),'--i1-phase',a.player_phase]
        if a.player_phase=='restore':
            if a.restore_copy is None or not a.restore_copy.resolve().is_relative_to(ROOT.resolve()) or not a.restore_copy.is_file():raise RuntimeError('OwnedCopiedCheckpointRequired')
            os.environ['RPG_I1_RESTORE_COPY']=str(a.restore_copy.resolve());save(out/'restore-input.json',{'path':str(a.restore_copy.resolve()),'sha256':sha(a.restore_copy),'originalFormalDataUsed':False})
        if variant is not None:
            if report.get('startupTarget')!=variant or report.get('extraScriptingDefines')!=(['RPG_I1_ISOLATED_STARTUP'] if variant=='I1_PRIVATE' else []) or report.get('companyName')!='CodexMapAcceptance' or report.get('productName')!='RPG_M0_'+current_id()+'_'+variant:raise RuntimeError('ExactSealedStartupBuildMetadataRequired')
            command=[str(exe),'-screen-width','1920','-screen-height','1080','-screen-fullscreen','0','-logFile',str(out/'player.log'),'--m0-startup-proof',str(out/'Player'),'--i1-phase','startup']
            if a.startup_request is not None:command+=['--m0-startup-request',a.startup_request]
            if not a.expect_startup_rejection:
                if a.restore_copy is None or not a.restore_copy.resolve().is_relative_to(ROOT.resolve()) or not a.restore_copy.is_file():raise RuntimeError('OwnedActualSavedCopyRequired')
                os.environ['RPG_I1_RESTORE_COPY']=str(a.restore_copy.resolve())
                runtime=Path(os.environ['RPG_I1_RUNTIME_ROOT']) if variant=='I1_PRIVATE' else Path(os.environ['USERPROFILE'])/'AppData/LocalLow'/report['companyName']/report['productName']
                slots=runtime/'tower-run-slots';slots.mkdir(parents=True,exist_ok=True);target=slots/'tower-run-slot-1.json'
                if target.exists():(out/'OwnedPriorShadowInputSlot1.json').write_bytes(target.read_bytes())
                target.write_bytes(a.restore_copy.read_bytes())
                save(out/'restore-input.json',{'path':str(a.restore_copy.resolve()),'sha256':sha(a.restore_copy),'preparedInput':str(target),'preparedInputSha256':sha(target),'startupTarget':variant,'runtimeRoot':str(runtime),'originalFormalDataUsed':False,'normalNamespaceMode':'Baked company/product, original path resolver and actual PlayerPrefs, no ConfigureTestIsolation' if variant=='DEFAULT' else 'Compile-time explicit I1 isolation'})
        expected=sha(exe)
        def validator(project,actual):
            if Path(project).resolve()!=C or actual!=command or sha(exe)!=expected or tree(build/'MVP_Windows_x64')!=built_tree:raise RuntimeError('I1PlayerBuildOrCommandChanged')
            require_player(report,build)
    else:
        command=[UNITY,'-batchmode','-projectPath',str(C),'-logFile',str(out/'unity.log')]
        if a.phase in ('edit','play'):command+=['-runTests','-testPlatform','EditMode' if a.phase=='edit' else 'PlayMode','-testFilter',a.filter,'-testResults',str(out/'results.xml')]
        else:command+=['-executeMethod','MapVNextI1Authoring.Prepare' if a.phase=='prepare' else 'MapVNextI1Build.Build']
        if a.phase=='build':
            if a.build_dir is not None or (build/'MVP_Windows_x64').exists():raise RuntimeError('I1BuildIdentityAlreadyHasPlayerOrCustomPath')
            build.mkdir(parents=True,exist_ok=False);build_inputs=inputs();save(build/'build-inputs.json',build_inputs)
            (build/'source-fingerprint.txt').write_bytes(fingerprint);os.environ['RPG_I1_BUILD_ROOT']=str(build)
    task='map-vnext-i1-'+a.id;trace=Trace(C,task,out);trace.start();save(out/'command.json',{'command':command,'runtimeRoot':os.environ['RPG_I1_RUNTIME_ROOT'],'buildRoot':str(build) if a.phase in ('build','player') else None})
    settings=C/'ProjectSettings/ProjectSettings.asset';settings_before=settings.read_bytes();font_before=(C/FONT).read_bytes() if a.phase=='build' else None
    result=None
    try:result=guard.run(C,task,'Map VNext I1 '+a.phase,a.phase,command,a.timeout,command_validator=validator);save(out/'supervisor.json',result)
    finally:
        extra=trace.finish()
        if result is not None and not result.get('launched'):extra['status']='NOT_LAUNCHED_NO_PROCESS_TO_TRACE';save(out/'native-trace.json',extra)
    settings_after=settings.read_bytes()
    if settings_after!=settings_before:
        first=settings_before.decode('utf-8-sig').replace('\r\n','\n');last=settings_after.decode('utf-8-sig').replace('\r\n','\n')
        if first.replace('  runInBackground: 0\n','  runInBackground: 1\n')!=last:raise RuntimeError('UNEXPECTED_PROJECT_SETTINGS_SIDE_EFFECT')
        (out/'ProjectSettings.editor-generated.asset').write_bytes(settings_after);settings.write_bytes(settings_before)
        save(out/'settings-side-effect.json',{'property':'runInBackground','beforeSha256':hashlib.sha256(settings_before).hexdigest(),'generatedSha256':hashlib.sha256(settings_after).hexdigest(),'restoredSha256':sha(settings)})
    font_qualified=(recover_known_font_cache(C/FONT,font_before,out,result,extra,build_inputs,build) if variant is not None else preserve_font_delta(C/FONT,font_before,out)) if a.phase=='build' else True
    matched=font_qualified
    if a.phase=='build':
        after_inputs=inputs();save(out/'build-inputs-after.json',after_inputs);matched=font_qualified and after_inputs==build_inputs
        if (build/'BuildReport.json').exists():save(build/'build-tree.json',tree(build/'MVP_Windows_x64'))
    if a.phase=='player':
        after_tree=tree(build/'MVP_Windows_x64');save(out/'build-tree-after.json',after_tree);matched=after_tree==built_tree and inputs()==build_inputs
        try:require_player(report,build)
        except (RuntimeError,OSError,ValueError,KeyError) as error:
            matched=False;save(out/'postbuild-admission-after-error.json',{'status':'BLOCKED','error':str(error)})
    if a.phase=='player' and a.expect_startup_rejection:
        early=json.loads((out/'Player/startup-early.json').read_text('utf-8'))['rows']
        rejected=next((row for row in early if row.get('phase')=='RejectedPrivateQuarantine'),{})
        ended=next((row for row in early if row.get('phase')=='ProcessQuitting'),{})
        raw_log=(out/'player.log').read_text('utf-8',errors='replace')
        unexpected_markers=[marker for marker in ['M0_STARTUP_PROOF_READY_OBSERVED','I1_PLAYER_ACCEPTANCE','I1_ISOLATION slots='] if marker in raw_log]
        if not early or early[0].get('phase')!='GateBefore' or early[0].get('sessionExists') is not False or rejected.get('sessionExists') is not False or rejected.get('ready') is not False or not rejected.get('rejected') or rejected.get('preferences')!='TowerRunMemoryPreferenceStore' or 'RejectedStartup' not in rejected.get('slots','') or 'I1StartupCapabilityOrEntryMismatch' not in rejected.get('failure','') or not ended or any(row.get('ready') for row in early) or unexpected_markers:matched=False
        save(out/'rejection-ready-log-check.json',{'unexpectedMarkers':unexpected_markers,'quittingObserved':bool(ended),'allObservedReadyFalse':not any(row.get('ready') for row in early),'note':'Actual early rejection and process quitting state, plus exact startup/Ready markers. No claim about all future inputs.'})
        save(out/'expected-rejection-evidence.json',{'status':'ACTUAL_EARLY_REJECTION_PROVED' if matched else 'REJECTION_PROOF_MISMATCH','rawSupervisorStatePreserved':result.get('state'),'rawExitCode':result.get('exitCode'),'phase':rejected.get('phase'),'sessionExistedAtRejection':rejected.get('sessionExists'),'readyAtRejection':rejected.get('ready'),'rawEarlySha256':sha(out/'Player/startup-early.json')})
    after=guard.status(C);save(out/'guard-after.json',after)
    ok=lifecycle_qualified(result,after,matched,extra,2 if a.expect_startup_rejection else 0)
    receipt={'status':('EXPECTED_REJECTION_LIFECYCLE_ONLY' if a.expect_startup_rejection else 'PASS_LIFECYCLE_ONLY') if ok else 'FAIL','runId':result.get('runId'),'phase':a.phase,'exitCode':result.get('exitCode'),'naturalExit':result.get('naturalExit'),'cleanup':result.get('cleanup'),'state':result.get('state'),'trace':extra['status'],'buildInputsAndTreeMatched':matched,'assertions':'READ_XML_OR_PLAYER_EVIDENCE_SEPARATELY','startupTarget':variant,'expectedExitCode':2 if a.expect_startup_rejection else 0,'sourceRestoredAfterKnownCacheDelta':(out/'font-reviewed-recovery.json').exists()}
    save(out/'receipt.json',receipt)
    if a.phase=='build':save(build/'BuildValidation.json',{'status':'BUILD_INPUTS_AND_LIFECYCLE_VERIFIED' if ok else 'BUILD_BLOCKED','receiptPath':str(out/'receipt.json'),'receiptSha256':sha(out/'receipt.json'),'identity':current_id(),'fontDeltaQualified':font_qualified})
    print(json.dumps(receipt));return 0 if ok else 2
if __name__=='__main__':sys.exit(main())

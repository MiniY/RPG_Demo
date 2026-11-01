"""Read-only supplemental trace. Guard/Supervisor alone owns admission and cleanup."""
from pathlib import Path
import ctypes as ct, json, os, sys, threading, time, subprocess, queue
C=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(C/'Tools/EngineeringAssurance'))
import unity_guard as guard
from unity_supervisor import Native,ProcessHandle

def descendants(rows,root):
    if root not in rows:return set()
    selected={root}
    while True:
        extra={p for p,r in rows.items() if r['parentPid'] in selected}
        if extra.issubset(selected):return selected
        selected.update(extra)

def snapshot():
    n=Native();k=n.k
    k.CreateToolhelp32Snapshot.argtypes=[ct.c_uint32,ct.c_uint32];k.CreateToolhelp32Snapshot.restype=ct.c_void_p
    for name in ['Process32FirstW','Process32NextW']:
        f=getattr(k,name);f.argtypes=[ct.c_void_p,ct.POINTER(guard._ProcessEntry)];f.restype=ct.c_int
    handle=k.CreateToolhelp32Snapshot(2,0)
    if handle in (None,ct.c_void_p(-1).value):raise OSError(ct.get_last_error(),'Process snapshot failed')
    try:
        entry=guard._ProcessEntry();entry.size=ct.sizeof(entry);rows={}
        ok=k.Process32FirstW(handle,ct.byref(entry))
        if not ok:raise OSError(ct.get_last_error(),'First process enumeration failed; never treat as FREE')
        while ok:
            rows[int(entry.pid)]={'parentPid':int(entry.parentPid),'imageName':entry.exe}
            ok=k.Process32NextW(handle,ct.byref(entry))
        if ct.get_last_error()!=18:raise OSError(ct.get_last_error(),'Incomplete process enumeration')
        if os.getpid() not in rows:raise RuntimeError('Snapshot did not contain observer process')
        return rows
    finally:n.close(handle)

def selftest():
    fake={1:{'parentPid':0},2:{'parentPid':1},3:{'parentPid':2},4:{'parentPid':9}}
    assert descendants(fake,1)=={1,2,3}
    assert descendants(fake,999)==set()
    assert descendants({1:{'parentPid':2},2:{'parentPid':1}},1)=={1,2}
    entry=guard._ProcessEntry();entry.size=ct.sizeof(entry)
    assert hasattr(entry,'pid') and not hasattr(entry,'th32ProcessID')
    rows=snapshot();assert rows[os.getpid()]['parentPid']==os.getppid()
    n=Native();h=ProcessHandle(n,os.getpid())
    try:
        identity=h.describe();assert not h.exited() and identity['creationTime100ns']
        def same(expected,actual):
            if expected['pid']!=actual['pid'] or expected['creationTime100ns']!=actual['creationTime100ns']:raise RuntimeError('PID_REUSED_OR_WRONG_PROCESS')
        same(identity,identity)
        wrong={**identity,'creationTime100ns':'0'}
        try:same(identity,wrong)
        except RuntimeError:pass
        else:raise AssertionError('PID reuse negative not rejected')
    finally:h.close()
    return {'status':'PASS_OFFLINE_POSITIVE_NEGATIVE_AND_NATIVE_SELF','checks':['descendant chain','unrelated excluded','unknown root not empty proof','cycle terminates','renamed ctypes fields','complete native snapshot','live identity','PID-reuse rejection'],'processes':len(rows),'unityLaunched':False}

class Trace:
    def __init__(self,project,task,out):
        self.project=project;self.task=task;self.out=out;self.stop=threading.Event();self.records={};self.errors=[];self.root=None;self.events=[];self.requests=queue.Queue()
    def event(self,event,**fields):
        row={'atUtc':guard.utc(),'event':event,**fields};self.events.append(row)
        with (self.out/'native-events.jsonl').open('a',encoding='utf-8') as f:f.write(json.dumps(row)+'\n')
    def start(self):
        self.worker=threading.Thread(target=self.run,daemon=True);self.commands=threading.Thread(target=self.commandlines,daemon=True);self.worker.start();self.commands.start()
    def commandlines(self):
        while not self.stop.is_set() or not self.requests.empty():
            try:pid=self.requests.get(timeout=.1)
            except queue.Empty:continue
            script="$ErrorActionPreference='Stop'; Get-CimInstance Win32_Process -Filter 'ProcessId = "+str(int(pid))+"' | Select-Object ProcessId,ParentProcessId,CreationDate,ExecutablePath,CommandLine | ConvertTo-Json -Compress"
            try:
                r=subprocess.run(['powershell','-NoProfile','-NonInteractive','-Command',script],capture_output=True,text=True,encoding='utf-8',errors='replace',timeout=10)
                self.records[pid]['cimCommandLineObservation']={'exitCode':r.returncode,'data':json.loads(r.stdout) if r.stdout.strip() else None,'stderr':r.stderr}
            except Exception as error:self.records[pid]['cimCommandLineObservation']={'status':'UNKNOWN','reason':str(error)}
    def run(self):
        tracked={};n=Native();_,owner_path=guard.project_paths(self.project)
        try:
            while not self.stop.is_set():
                if self.root is None:
                    try:
                        owner=json.loads(owner_path.read_text('utf-8'));progress=json.loads(Path(owner['resultPath']).read_text('utf-8'))
                        if owner.get('taskId')==self.task and progress.get('taskId')==self.task and progress.get('process'):
                            identity=progress['process'];h=ProcessHandle(n,identity['pid']);actual=h.describe()
                            if identity['creationTime100ns']!=actual['creationTime100ns']:h.close();raise RuntimeError('ROOT_IDENTITY_MISMATCH')
                            self.root=identity['pid'];tracked[self.root]=h
                            self.records[self.root]={**actual,'parentPid':None,'firstSeenAtUtc':guard.utc(),'ownership':'exact Supervisor Run root','command':progress['command']}
                            self.event('ROOT_BOUND',runId=progress['runId'],identity=actual)
                    except (OSError,ValueError,KeyError):pass
                rows=snapshot()
                if self.root is not None:
                    selected=descendants(rows,self.root)
                    # Retain already bound child objects after their parent exits.
                    for old in list(tracked):selected.update(descendants(rows,old))
                    for pid in selected:
                        if pid in self.records:continue
                        try:
                            h=ProcessHandle(n,pid);identity=h.describe();tracked[pid]=h
                            self.records[pid]={**identity,**rows[pid],'firstSeenAtUtc':guard.utc(),'ownership':'observed native parent chain from exact Run root'}
                            self.event('DESCENDANT_BOUND',identity=self.records[pid]);self.requests.put(pid)
                        except OSError as error:self.event('VANISHED_BEFORE_BIND',pid=pid,error=str(error))
                    for pid,h in list(tracked.items()):
                        if h.exited():
                            self.records[pid]['exitObservedAtUtc']=guard.utc();self.records[pid]['exitCode']=h.exit_code();self.event('EXIT_SIGNALLED',pid=pid,exitCode=h.exit_code());h.close();del tracked[pid]
                self.stop.wait(.1)
        except Exception as error:self.errors.append(str(error));self.event('TRACE_ERROR',error=str(error))
        finally:
            for pid,h in tracked.items():
                self.records[pid]['lastState']='EXITED_CONFIRMED' if h.exited() else 'LIVE_CONFIRMED';h.close()
    def finish(self):
        self.stop.set();self.worker.join(5);self.commands.join(15)
        data={'status':'VALID_NATIVE_TIME_LINE' if self.root is not None and not self.errors and not self.worker.is_alive() else 'INVALID','root':self.root,'records':list(self.records.values()),'errors':self.errors,'extraEvidenceOnly':True,'notAdmissionAuthority':True,'samplingIntervalSeconds':.1}
        (self.out/'native-trace.json').write_text(json.dumps(data,indent=2)+'\n','utf-8');return data
if __name__=='__main__':print(json.dumps(selftest()))

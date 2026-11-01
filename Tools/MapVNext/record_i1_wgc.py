"""Record one exact owned I1 HWND through Windows Graphics Capture. No desktop or game control."""
from pathlib import Path
import argparse,ctypes,ctypes.wintypes as W,subprocess,json,time,datetime,hashlib,sys
import imageio_ffmpeg
ap=argparse.ArgumentParser();ap.add_argument('--run',required=True);a=ap.parse_args()
C=Path.cwd();R=C/'Artifacts/MapAcceptance/MapVNext-Integration-I1-20261029';BUILD='map-vnext-i1-integration-20261031-a41';EXE=R/('Build-'+BUILD)/'MVP_Windows_x64/RPG_Demo_MapVNext_I1.exe'
assert a.run == 'i1-p41-full';OUT=R/'Authoring/VersionedExtension/FinalVerification-a41/ContinuousVideo'/a.run;OUT.mkdir(parents=True,exist_ok=False)
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest();utc=lambda:datetime.datetime.now(datetime.timezone.utc).isoformat()
def save(n,o):(OUT/n).write_text(json.dumps(o,indent=2)+'\n','utf-8')
user=ctypes.WinDLL('user32',use_last_error=True);kernel=ctypes.WinDLL('kernel32',use_last_error=True)
user.GetWindowThreadProcessId.argtypes=[W.HWND,ctypes.POINTER(W.DWORD)];user.GetWindowThreadProcessId.restype=W.DWORD
user.GetWindowTextLengthW.argtypes=[W.HWND];user.GetWindowTextLengthW.restype=ctypes.c_int
user.GetWindowTextW.argtypes=[W.HWND,W.LPWSTR,ctypes.c_int];user.GetWindowTextW.restype=ctypes.c_int
user.IsWindowVisible.argtypes=[W.HWND];user.IsWindowVisible.restype=W.BOOL
user.GetClientRect.argtypes=[W.HWND,ctypes.POINTER(W.RECT)];user.GetClientRect.restype=W.BOOL
CALLBACK=ctypes.WINFUNCTYPE(W.BOOL,W.HWND,W.LPARAM);user.EnumWindows.argtypes=[CALLBACK,W.LPARAM]
kernel.OpenProcess.argtypes=[W.DWORD,W.BOOL,W.DWORD];kernel.OpenProcess.restype=W.HANDLE
kernel.WaitForSingleObject.argtypes=[W.HANDLE,W.DWORD];kernel.WaitForSingleObject.restype=W.DWORD
kernel.GetProcessTimes.argtypes=[W.HANDLE,ctypes.POINTER(W.FILETIME),ctypes.POINTER(W.FILETIME),ctypes.POINTER(W.FILETIME),ctypes.POINTER(W.FILETIME)];kernel.GetProcessTimes.restype=W.BOOL
kernel.CloseHandle.argtypes=[W.HANDLE]
def candidates():
 code='Get-CimInstance Win32_Process -Filter "Name=\'RPG_Demo_MapVNext_I1.exe\'" | Select-Object ProcessId,CreationDate,ExecutablePath,CommandLine | ConvertTo-Json -Compress'
 raw=subprocess.check_output(['powershell','-NoProfile','-Command',code],encoding='utf-8-sig',errors='strict').strip()
 if not raw:return []
 rows=json.loads(raw);rows=rows if isinstance(rows,list) else [rows];expected=str(R/'VersionedRuns'/a.run/'Player').replace('\\','/').lower()
 return [r for r in rows if r.get('ExecutablePath') and Path(r['ExecutablePath']).resolve()==EXE.resolve() and expected in (r.get('CommandLine') or '').replace('\\','/').lower() and '--i1-accept' in (r.get('CommandLine') or '')]
def windows(pid):
 found=[]
 @CALLBACK
 def each(hwnd,param):
  owner=W.DWORD();user.GetWindowThreadProcessId(hwnd,ctypes.byref(owner))
  if owner.value==pid and user.IsWindowVisible(hwnd):
   rect=W.RECT()
   if user.GetClientRect(hwnd,ctypes.byref(rect)) and rect.right>=640 and rect.bottom>=360:
    n=user.GetWindowTextLengthW(hwnd);buf=ctypes.create_unicode_buffer(n+1);user.GetWindowTextW(hwnd,buf,n+1);found.append({'hwnd':int(hwnd),'pid':pid,'title':buf.value,'clientWidth':rect.right,'clientHeight':rect.bottom})
  return True
 user.EnumWindows(each,0);return found
owner=None;window=None;deadline=time.monotonic()+90
while time.monotonic()<deadline:
 rows=candidates();assert len(rows)<=1,'AmbiguousOwnedPlayerProcess'
 if rows:
  found=windows(int(rows[0]['ProcessId']));assert len(found)<=1,'AmbiguousOwnedPlayerWindow'
  if found:owner=rows[0];window=found[0];break
 time.sleep(.5)
assert owner is not None and window is not None,'OwnedPlayerWindowNotFound_NoDesktopFallback'
handle=kernel.OpenProcess(0x101000,False,window['pid']);assert handle
created=W.FILETIME();exit=W.FILETIME();kt=W.FILETIME();ut=W.FILETIME();assert kernel.GetProcessTimes(handle,ctypes.byref(created),ctypes.byref(exit),ctypes.byref(kt),ctypes.byref(ut))
creation=str((created.dwHighDateTime<<32)|created.dwLowDateTime);assert kernel.WaitForSingleObject(handle,0)==258

def _bounded_header(backend, readers, timeout=15):
 import queue,threading
 result=queue.Queue(maxsize=1)
 def read():
  try:result.put((True,backend.stdout.readline(128)))
  except BaseException as error:result.put((False,error))
 reader=threading.Thread(target=read,name='I1OwnedWgcHeader',daemon=True);readers.append(reader);reader.start()
 try:okay,value=result.get(timeout=timeout)
 except queue.Empty:raise RuntimeError('WGCHeaderDeadlineExceeded') from None
 if not okay:raise value
 if not value:raise RuntimeError('WGCHeaderEOF')
 if not value.endswith(b'\n') or len(value)>100:raise RuntimeError('WGCMalformedHeader')
 return value.decode('ascii').strip().split()

def _cleanup_owned(children,errors,forced):
 # Every entry is an owned Popen, never a target PID or the game process.
 for name,child in children:
  if child is None:continue
  try:active=child.poll() is None
  except BaseException as error:errors.append(name+'.poll:'+repr(error));active=True
  if not active:continue
  forced.append('OWN_'+name+'_FINALLY_TERMINATED')
  try:child.terminate()
  except BaseException as error:errors.append(name+'.terminate:'+repr(error))
  try:child.wait(timeout=10)
  except BaseException as error:errors.append(name+'.wait:'+repr(error))


def _publish_capture_result(out,result):
 # Only a fully closed, durable file gains the canonical consumer name.
 import os
 target=out/'capture-result.json';staged=out/'capture-result.unpublished.json'
 if target.exists():raise FileExistsError('CaptureResultAlreadyPublished')
 with staged.open('x',encoding='utf-8') as stream:
  stream.write(json.dumps(result,indent=2)+'\n');stream.flush();os.fsync(stream.fileno())
 # This worker is Windows-only: rename fails if the target exists.
 os.rename(staged,target)

bridge=C/'Tools/MapVNext/native/OwnedWindowCapture.exe';source=C/'Tools/MapVNext/native/OwnedWindowCapture.cpp'
assert bridge.is_file() and source.is_file()
backendCommand=[str(bridge),str(window['hwnd']),str(window['pid']),creation]
backendLog=None;log=None;backend=None;proc=None;forced=[];errors=[];readers=[];primary=None;primary_tb=None;video=OUT/'owned-player-continuous.mp4'
try:
 backendLog=(OUT/'wgc.log').open('wb');log=(OUT/'ffmpeg.log').open('wb')
 backend=subprocess.Popen(backendCommand,stdout=subprocess.PIPE,stderr=backendLog,bufsize=0,creationflags=subprocess.CREATE_NO_WINDOW)
 header=_bounded_header(backend,readers)
 assert len(header)==3 and header[0]=='WGC_BGRA','ActualWGCHeaderRequired'
 width,height=map(int,header[1:]);assert 640<=width<=4096 and 360<=height<=2400
 ff=Path(imageio_ffmpeg.get_ffmpeg_exe())
 command=[str(ff),'-hide_banner','-loglevel','info','-n','-f','rawvideo','-pixel_format','bgra','-video_size',str(width)+'x'+str(height),'-framerate','12','-i','pipe:0','-an','-vf','pad=ceil(iw/2)*2:ceil(ih/2)*2','-c:v','libx264','-preset','veryfast','-crf','20','-pix_fmt','yuv420p','-threads','2','-movflags','+faststart',str(video)]
 start={'status':'OWNED_WINDOW_CAPTURE_STARTED_NOT_GAMEPLAY_PASS','utc':utc(),'buildId':BUILD,'run':a.run,'expectedExe':str(EXE),'exeSha256':sha(EXE),'process':owner,'creationTime100ns':creation,'window':window,'command':command,'ffmpegSha256':sha(ff),'captureScriptSha256':sha(Path(__file__)),'captureMethod':'WGC_HWND_V1','backendCommand':backendCommand,'backendSha256':sha(bridge),'backendSourceSha256':sha(source),'sourceWidth':width,'sourceHeight':height,'scope':'Continuous12fps silent WGC surface of exact owned HWND. No monitor/desktop/focus/input/control/game termination. Initial header deadline15s; each owned child cleanup attempted separately, primary and secondary errors retained. Native owner and frame size checked; raw EOF closes only owned encoder. Not user acceptance.'};save('start.json',start)
 proc=subprocess.Popen(command,stdin=backend.stdout,stdout=log,stderr=subprocess.STDOUT,creationflags=subprocess.CREATE_NO_WINDOW);backend.stdout.close()
 backend.wait(timeout=1520);proc.wait(timeout=30)
except BaseException as error:primary=error;primary_tb=error.__traceback__
finally:
 _cleanup_owned([('WGC',backend),('FFMPEG',proc)],errors,forced)
 for reader in readers:
  try:
   reader.join(timeout=2)
   if reader.is_alive():errors.append('HeaderReaderStillAliveAfterOwnedChildCleanup')
  except BaseException as error:errors.append('HeaderReader.join:'+repr(error))
 for name,closer in [('NativeHandle',lambda:kernel.CloseHandle(handle)),('BackendStdout',lambda:backend.stdout.close() if backend is not None and backend.stdout is not None else None),('WgcLog',lambda:backendLog.close() if backendLog is not None else None),('FfmpegLog',lambda:log.close() if log is not None else None)]:
  try:
   value=closer()
   if name=='NativeHandle' and not value:errors.append('NativeHandle.CloseFailed')
  except BaseException as error:errors.append(name+'.close:'+repr(error))
try:text=(OUT/'wgc.log').read_text('utf-8',errors='replace')
except BaseException as error:errors.append('WgcLog.read:'+repr(error));text=''
import re
match=re.search(r'WGC_STOP=(EXACT_PLAYER_EXITED|OWNED_WINDOW_UNAVAILABLE) frames=(\d+) updates=(\d+)',text)
reason=match[1] if match else 'WGC_FAILED_NO_VALID_TERMINAL'
def evidence_sha(path):
 try:return sha(path) if path.is_file() else None
 except BaseException as error:errors.append('Evidence.sha:'+repr(error));return None
startsha=evidence_sha(OUT/'start.json');videosha=evidence_sha(video)
try:videoBytes=video.stat().st_size if video.exists() else 0
except BaseException as error:errors.append('Video.stat:'+repr(error));videoBytes=0
be=backend.returncode if backend is not None else None;en=proc.returncode if proc is not None else None
okay=primary is None and be==0 and en==0 and not forced and not errors and match is not None
result={'status':'CAPTURE_CLOSED_CONTENT_AND_RUN_VALIDATION_PENDING' if okay else 'CAPTURE_FAILED_RETAINED_NOT_QUALIFIED','utc':utc(),'buildId':BUILD,'run':a.run,'captureMethod':'WGC_HWND_V1','stopReason':reason,'ffmpegExitCode':en,'backendExitCode':be,'forcedOwnedChildren':forced,'secondaryErrors':errors,'primaryFailure':repr(primary) if primary is not None else None,'sourceFrames':int(match[2]) if match else 0,'sourceUpdates':int(match[3]) if match else 0,'startSha256':startsha,'videoSha256':videosha,'videoBytes':videoBytes,'noPlayerControlOrTermination':True,'captureOnlyExactWindow':True}
try:_publish_capture_result(OUT,result)
except BaseException as error:
 errors.append('CaptureResult.publish:'+repr(error))
 if primary is None:primary=error;primary_tb=error.__traceback__
 result['status']='CAPTURE_FAILED_RETAINED_NOT_QUALIFIED';result['primaryFailure']=repr(primary)
print(json.dumps(result))
if primary is not None:raise primary.with_traceback(primary_tb)
if not okay:raise RuntimeError('ActualWGCCaptureFailed:'+repr(errors))

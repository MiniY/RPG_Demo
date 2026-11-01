"""Record only the HWND of one exact owned I1 Run. Never captures desktop or controls Player."""
from pathlib import Path
import argparse,ctypes,ctypes.wintypes as W,subprocess,json,time,datetime,hashlib,sys
import imageio_ffmpeg
ap=argparse.ArgumentParser();ap.add_argument('--run',required=True);a=ap.parse_args()
C=Path.cwd();R=C/'Artifacts/MapAcceptance/MapVNext-Integration-I1-20261029';BUILD='map-vnext-i1-integration-20261031-a41';EXE=R/('Build-'+BUILD)/'MVP_Windows_x64/RPG_Demo_MapVNext_I1.exe'
assert a.run in ['i1-p41-pilot','i1-p41-full'];OUT=R/'Authoring/VersionedExtension/FinalVerification-a41/ContinuousVideo'/a.run;OUT.mkdir(parents=True,exist_ok=False)
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
ff=Path(imageio_ffmpeg.get_ffmpeg_exe());video=OUT/'owned-player-continuous.mp4';command=[str(ff),'-hide_banner','-loglevel','info','-n','-f','gdigrab','-draw_mouse','0','-framerate','12','-i','hwnd='+str(window['hwnd']),'-an','-c:v','libx264','-preset','veryfast','-crf','20','-pix_fmt','yuv420p','-threads','2','-movflags','+faststart',str(video)]
start={'status':'OWNED_WINDOW_CAPTURE_STARTED_NOT_GAMEPLAY_PASS','utc':utc(),'buildId':BUILD,'run':a.run,'expectedExe':str(EXE),'exeSha256':sha(EXE),'process':owner,'creationTime100ns':creation,'window':window,'command':command,'ffmpegSha256':sha(ff),'captureScriptSha256':sha(Path(__file__)),'scope':'Continuous12fps silent native window capture; may begin after Player startup. Only exact owned HWND; no desktop/focus/input/control/AI/health/collision changes. Actual duration and content must be decoded and visually checked; not human acceptance.','reference':'https://ffmpeg.org/ffmpeg-devices.html#gdigrab'};save('start.json',start)
log=(OUT/'ffmpeg.log').open('wb');proc=subprocess.Popen(command,stdin=subprocess.PIPE,stdout=log,stderr=subprocess.STDOUT,creationflags=subprocess.CREATE_NO_WINDOW);limit=time.monotonic()+1500;end='UNKNOWN'
try:
 while proc.poll() is None:
  if kernel.WaitForSingleObject(handle,0)==0:end='EXACT_PLAYER_EXITED';break
  if time.monotonic()>limit:end='CAPTURE_TIME_LIMIT';break
  found=windows(window['pid'])
  if not any(w['hwnd']==window['hwnd'] for w in found):end='OWNED_WINDOW_UNAVAILABLE';break
  time.sleep(.3)
 if proc.poll() is None:
  try:proc.stdin.write(b'q\n');proc.stdin.flush()
  except (BrokenPipeError,OSError):pass
  try:proc.wait(timeout=15)
  except subprocess.TimeoutExpired:proc.terminate();proc.wait(timeout=10);end+=';OWN_FFMPEG_FORCE_TERMINATED'
 else:end='FFMPEG_EXITED_BEFORE_PLAYER'
finally:
 if proc.poll() is None:
  try:proc.stdin.write(b'q\n');proc.stdin.flush()
  except (BrokenPipeError,OSError):pass
  try:proc.wait(timeout=10)
  except subprocess.TimeoutExpired:proc.terminate();proc.wait(timeout=10)
 kernel.CloseHandle(handle);log.close()
result={'status':'CAPTURE_CLOSED_CONTENT_AND_RUN_VALIDATION_PENDING','utc':utc(),'buildId':BUILD,'run':a.run,'stopReason':end,'ffmpegExitCode':proc.returncode,'startSha256':sha(OUT/'start.json'),'videoSha256':sha(video) if video.exists() else None,'videoBytes':video.stat().st_size if video.exists() else 0,'noPlayerControlOrTermination':True,'captureOnlyExactWindow':True};save('capture-result.json',result);print(json.dumps(result))

[CmdletBinding()]
param(
    [ValidateSet('Smoke','Regression','Performance','Memory','Stress','Soak')]
    [string]$Profile = 'Smoke',
    [string]$Filter = '',
    [string]$PlatformOverride = '',
    [int]$Seed = 20260929,
    [int]$Loops = 0,
    [int]$TimeoutMinutes = 0,
    [int]$MaxMemoryMB = 0,
    [switch]$SkipSync,
    [int]$AllowStaleUnityPid = 0
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$config = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'config.json') -Raw | ConvertFrom-Json
$project = [IO.Path]::GetFullPath($config.projectPath)
$output = [IO.Path]::GetFullPath($config.outputRoot)
$stateDir = Join-Path $output '01_环境与同步'
$reportsDir = Join-Path $output '03_测试运行报告'
$rawDir = Join-Path $output '07_测试证据与原始日志'
foreach ($dir in @($stateDir,$reportsDir,$rawDir)) { [IO.Directory]::CreateDirectory($dir) | Out-Null }
$lockFile = Join-Path $stateDir 'run.lock'
$activeFile = Join-Path $stateDir 'active-run.json'
$runId = [DateTimeOffset]::Now.ToString('yyyyMMdd-HHmmss') + '-' + ([guid]::NewGuid().ToString('N').Substring(0,8))
$runDir = Join-Path $rawDir $runId
$runGate = $null
$proc = $null
$started = [DateTimeOffset]::Now
$runState = 'BLOCKED'
$reason = ''
$mainSha = ''
$dataSha = ''
$postMainSha = ''
$counts = [ordered]@{total=0;passed=0;failed=0;skipped=0;inconclusive=0}
$legs = @()
$exitCode = 2
$testFilter = ''
$runLoops = 0
$isStale = $false
$verifiedStaleUnityPids = @()
$nunitStatus = 'NOT_RUN'
$unityExitStatus = 'NOT_RUN'
$dataStatus = 'NOT_REQUIRED'
$mainFreshnessStatus = 'UNKNOWN'
$worktreeStatus = 'UNKNOWN'
$settingsPath = Join-Path $project 'ProjectSettings\ProjectSettings.asset'
$settingsBefore = $null
$settingsRecovery = $null
function Invoke-Git([string[]]$GitArgs) {
    $lines = & git -C $project @GitArgs 2>&1
    if ($LASTEXITCODE -ne 0) { throw "git $($GitArgs -join ' ') failed ($LASTEXITCODE): $($lines -join ' ')" }
    return ($lines | Out-String).Trim()
}
function Quote-Arg([string]$Value) { return '"' + $Value.Replace('"','\"') + '"' }
function Assert-LockfileFree([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) { return }
    try {
        $handle = [IO.File]::Open($Path,[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::None)
        $handle.Dispose()
    } catch { throw "Unity lockfile is held: $Path" }
}
function Restore-KnownSettingsPollution {
    if ($null -eq $settingsBefore) { return }
    $dirtyPaths = @(Invoke-Git -GitArgs @('status','--porcelain=v1','--untracked-files=all') | Where-Object { $_ })
    if ($dirtyPaths.Count -eq 0) { $script:worktreeStatus = 'CLEAN'; return }
    if ($dirtyPaths.Count -ne 1 -or $dirtyPaths[0] -ne 'M ProjectSettings/ProjectSettings.asset') {
        $script:worktreeStatus = 'DIRTY_UNEXPECTED'
        return
    }
    $after = [IO.File]::ReadAllBytes($settingsPath)
    $beforeText = [Text.Encoding]::UTF8.GetString($settingsBefore)
    $afterText = [Text.Encoding]::UTF8.GetString($after)
    if ($beforeText -notmatch '(?m)^  runInBackground: 0(?=\r?$)' -or $afterText -notmatch '(?m)^  runInBackground: 1(?=\r?$)') {
        $script:worktreeStatus = 'DIRTY_UNEXPECTED'
        return
    }
    $candidate = [Text.Encoding]::UTF8.GetBytes(($afterText -creplace '(?m)^  runInBackground: 1(?=\r?$)', '  runInBackground: 0'))
    if ([Convert]::ToBase64String($settingsBefore) -cne [Convert]::ToBase64String($candidate)) {
        $script:worktreeStatus = 'DIRTY_UNEXPECTED'
        return
    }
    [IO.File]::WriteAllBytes((Join-Path $runDir 'ProjectSettings.polluted.asset'),$after)
    $diff = & git -C $project diff -- ProjectSettings/ProjectSettings.asset 2>&1
    $diff | Set-Content -LiteralPath (Join-Path $runDir 'ProjectSettings.pollution.diff') -Encoding UTF8
    [IO.File]::WriteAllBytes($settingsPath,$settingsBefore)
    if (Invoke-Git -GitArgs @('status','--porcelain=v1','--untracked-files=all')) {
        $script:worktreeStatus = 'DIRTY_RESTORE_FAILED'
        return
    }
    $script:worktreeStatus = 'RESTORED_KNOWN_UTF_CHANGE'
    $script:settingsRecovery = 'Only runInBackground: 0 to 1 was observed; polluted bytes and diff saved; original bytes restored and clean Git verified.'
}
try {
    $runGate = [IO.File]::Open($lockFile,[IO.FileMode]::OpenOrCreate,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)
    [IO.Directory]::CreateDirectory($runDir) | Out-Null
    $active = [ordered]@{runId=$runId;startedAt=$started.ToString('o');profile=$Profile;project=$project;runnerPid=$PID;unityPid=$null}
    $active | ConvertTo-Json | Set-Content -LiteralPath $activeFile -Encoding UTF8
    if (-not $SkipSync) {
        & (Join-Path $PSScriptRoot 'Sync-DataTest.ps1') -FromRunner | Out-Null
        if ($LASTEXITCODE -ne 0) { throw 'Pre-run synchronization did not pass; see sync-status.json.' }
    }
    $syncState = Get-Content -LiteralPath (Join-Path $stateDir 'sync-status.json') -Raw | ConvertFrom-Json
    if ($syncState.result -notin @('MERGED','UNCHANGED')) { throw "Sync state is $($syncState.result)." }
    $mainSha = [string]$syncState.mainSha
    $dataSha = Invoke-Git -GitArgs @('rev-parse','HEAD')
    & git -C $project merge-base --is-ancestor $mainSha HEAD 2>$null
    if ($LASTEXITCODE -ne 0) { throw 'DataTest does not contain checked origin/main SHA.' }
    if ((Invoke-Git -GitArgs @('status','--porcelain=v1','--untracked-files=all'))) { throw 'DataTest contains uncommitted files.' }
    $settingsBefore = [IO.File]::ReadAllBytes($settingsPath)
    $worktreeStatus = 'CLEAN'
    if (-not (Test-Path -LiteralPath $config.editorPath)) { throw 'Configured Unity Editor executable is missing.' }
    try { $cimUnity = @(Get-CimInstance Win32_Process -Filter "name = 'Unity.exe'" -ErrorAction Stop) }
    catch { throw "Cannot verify Unity process inventory: $($_.Exception.Message)" }
    $others = @(Get-Process -Name Unity -ErrorAction SilentlyContinue)
    if ($AllowStaleUnityPid -gt 0) {
        $stale = @($cimUnity | Where-Object { $_.ProcessId -eq $AllowStaleUnityPid })
        if ($stale.Count -ne 1 -or [int]$stale[0].HandleCount -ne 0) { throw 'Stale Unity PID exception could not be verified.' }
        try { $opened = [Diagnostics.Process]::GetProcessById($AllowStaleUnityPid); $opened.Dispose(); throw 'Unity PID is still openable; no exception allowed.' }
        catch [ArgumentException] {}
        catch [System.Management.Automation.MethodInvocationException] {
            if ($_.Exception.InnerException -isnot [ArgumentException]) { throw }
        }
        Assert-LockfileFree (Join-Path $project 'Temp\UnityLockfile')
        Assert-LockfileFree 'D:\UnityProjects\RPG_Demo\Temp\UnityLockfile'
        $cimUnity = @($cimUnity | Where-Object { $_.ProcessId -ne $AllowStaleUnityPid })
        $others = @($others | Where-Object { $_.Id -ne $AllowStaleUnityPid })
    }
    $verifiedStaleUnityPids = @()
    foreach ($candidate in $cimUnity) {
        if ([int]$candidate.HandleCount -ne 0) { continue }
        $missing = $false
        try {
            $opened = [Diagnostics.Process]::GetProcessById([int]$candidate.ProcessId)
            $opened.Dispose()
        } catch [ArgumentException] { $missing = $true }
        catch [System.Management.Automation.MethodInvocationException] {
            if ($_.Exception.InnerException -is [ArgumentException]) { $missing = $true }
            else { throw }
        }
        if ($missing) { $verifiedStaleUnityPids += [int]$candidate.ProcessId }
    }
    if ($verifiedStaleUnityPids.Count -gt 0) {
        Assert-LockfileFree (Join-Path $project 'Temp\UnityLockfile')
        Assert-LockfileFree 'D:\UnityProjects\RPG_Demo\Temp\UnityLockfile'
        $cimUnity = @($cimUnity | Where-Object { $_.ProcessId -notin $verifiedStaleUnityPids })
        $others = @($others | Where-Object { $_.Id -notin $verifiedStaleUnityPids })
    }
    if ($cimUnity.Count -gt 0 -or $others.Count -gt 0) {
        throw "Unity process inventory is nonempty (CIM=$($cimUnity.Count), Process=$($others.Count)); project ownership is uncertain."
    }
    Assert-LockfileFree (Join-Path $project 'Temp\UnityLockfile')
    $spec = $config.profiles.$Profile
    $runLoops = if ($Loops -gt 0) { $Loops } else { [int]$spec.loops }
    $limitMB = if ($MaxMemoryMB -gt 0) { $MaxMemoryMB } else { [int]$config.memoryLimitMB }
    $limitMinutes = if ($TimeoutMinutes -gt 0) { $TimeoutMinutes } else { [int]$config.unityTimeoutMinutes }
    if ($runLoops -lt 1 -or $runLoops -gt 10000 -or $limitMB -lt 512 -or $limitMinutes -lt 1 -or $limitMinutes -gt 1440) { throw 'Requested resource limits are invalid.' }
    if ($PlatformOverride -and $PlatformOverride -notin @('EditMode','PlayMode')) { throw 'PlatformOverride must be EditMode or PlayMode.' }
    $platforms = if ($PlatformOverride) { @($PlatformOverride) } elseif ($Profile -eq 'Regression') { @('EditMode','PlayMode') } else { @([string]$spec.platform) }
    $testFilter = if ($Filter) { $Filter } else { [string]$spec.filter }
    if ($Profile -in @('Performance','Memory','Stress','Soak','Smoke') -and $testFilter -eq 'DataTestMapProbeTests') { $dataStatus = 'PENDING' }
    $runState = 'PASS'
    foreach ($platform in $platforms) {
        $xmlPath = Join-Path $runDir "$platform.xml"
        $logPath = Join-Path $runDir "$platform.log"
        $metricPath = Join-Path $runDir "$platform-metrics.json"
        $arguments = @(
            '-batchmode',
            '-projectPath',(Quote-Arg $project),
            '-runTests','-testPlatform',$platform,
            '-testResults',(Quote-Arg $xmlPath),
            '-logFile',(Quote-Arg $logPath)
        )
        if ($testFilter) { $arguments += @('-testFilter',(Quote-Arg $testFilter)) }
        $psi = New-Object Diagnostics.ProcessStartInfo
        $psi.FileName = [string]$config.editorPath
        $psi.Arguments = $arguments -join ' '
        $psi.UseShellExecute = $false
        $psi.CreateNoWindow = $true
        $psi.EnvironmentVariables['DATATEST_PROBE_MODE'] = $Profile
        $psi.EnvironmentVariables['DATATEST_PROBE_SEED'] = [string]$Seed
        $psi.EnvironmentVariables['DATATEST_PROBE_LOOPS'] = [string]$runLoops
        $psi.EnvironmentVariables['DATATEST_METRICS_PATH'] = $metricPath
        $proc = [Diagnostics.Process]::Start($psi)
        $active.unityPid = $proc.Id
        $active | ConvertTo-Json | Set-Content -LiteralPath $activeFile -Encoding UTF8
        $legStarted = [DateTimeOffset]::Now
        $stopReason = ''
        $xmlAt = $null
        while (-not $proc.WaitForExit(1000)) {
            $proc.Refresh()
            if (Test-Path -LiteralPath (Join-Path $runDir 'cancel.request')) { $stopReason = 'CANCELLED'; break }
            if (([DateTimeOffset]::Now - $legStarted).TotalMinutes -gt $limitMinutes) { $stopReason = 'TIMEOUT'; break }
            if ($proc.WorkingSet64 -gt ($limitMB * 1MB)) { $stopReason = 'MEMORY_LIMIT'; break }
            if (Test-Path -LiteralPath $xmlPath) {
                if ($null -eq $xmlAt) { $xmlAt = [IO.File]::GetLastWriteTime($xmlPath).ToString('o') }
                $xmlAge = ([DateTime]::UtcNow - [IO.File]::GetLastWriteTimeUtc($xmlPath)).TotalSeconds
                if ($xmlAge -gt [int]$config.editorShutdownGraceSeconds) { $stopReason = 'EDITOR_EXIT_TIMEOUT_AFTER_XML'; break }
            }
        }
        if ($stopReason) {
            $proc.Refresh()
            if (-not $proc.HasExited) { $proc.Kill(); $proc.WaitForExit(30000) | Out-Null }
            $runState = if ($stopReason -in @('TIMEOUT','EDITOR_EXIT_TIMEOUT_AFTER_XML')) { 'TIMEOUT' } else { 'BLOCKED' }
            $reason = $stopReason
        }
        if ($null -eq $xmlAt -and (Test-Path -LiteralPath $xmlPath)) { $xmlAt = [IO.File]::GetLastWriteTime($xmlPath).ToString('o') }
        $processExitAt = [DateTimeOffset]::Now
        $unityExitStatus = if ($stopReason -in @('TIMEOUT','EDITOR_EXIT_TIMEOUT_AFTER_XML')) { 'TIMEOUT' } elseif ($stopReason) { $stopReason } elseif ($proc.ExitCode -eq 0) { 'EXIT_0' } else { "EXIT_$($proc.ExitCode)" }
        $leg = [ordered]@{platform=$platform;arguments=$psi.Arguments;startedAt=$legStarted.ToString('o');xmlAt=$xmlAt;processExitAt=$processExitAt.ToString('o');durationSeconds=[math]::Round(($processExitAt-$legStarted).TotalSeconds,3);exitCode=$proc.ExitCode;xml=$xmlPath;log=$logPath;metrics=$metricPath;stopReason=$stopReason}
        $legs += $leg
        $active.unityPid = $null
        $active | ConvertTo-Json | Set-Content -LiteralPath $activeFile -Encoding UTF8
        if (Test-Path -LiteralPath $xmlPath) {
            try {
                [xml]$doc = Get-Content -LiteralPath $xmlPath -Raw
                $node = $doc.'test-run'
                if (-not $node) { throw 'NUnit test-run node missing.' }
                foreach ($name in @('total','passed','failed','skipped','inconclusive')) {
                    $counts[$name] += [int]$node.GetAttribute($name)
                }
                $nunitStatus = if ([int]$node.GetAttribute('failed') -gt 0) { 'FAIL' } elseif ([int]$node.GetAttribute('passed') -gt 0) { 'PASS' } else { 'INCONCLUSIVE' }
            } catch { $nunitStatus = 'INVALID_XML'; if ($runState -eq 'PASS') { $runState = 'INVALID'; $reason = "Invalid NUnit XML: $($_.Exception.Message)" } }
        } else { $nunitStatus = 'MISSING_XML'; if ($runState -eq 'PASS') { $runState = 'INVALID'; $reason = "NUnit XML missing for $platform." } }
        if ($dataStatus -eq 'PENDING') {
            try {
                if (-not (Test-Path -LiteralPath $metricPath)) { throw 'Metrics file missing.' }
                $metric = Get-Content -LiteralPath $metricPath -Raw | ConvertFrom-Json
                if ($metric.mode -ne $Profile -or [int]$metric.baseSeed -ne $Seed -or [int]$metric.loops -ne $runLoops -or @($metric.generationMilliseconds).Count -ne $runLoops -or [int]$metric.warmupCount -ne 3 -or -not $metric.settingsJson) { throw 'Metrics configuration or sample count mismatch.' }
                foreach ($sample in $metric.generationMilliseconds) { if ([double]$sample -le 0 -or [double]::IsNaN([double]$sample) -or [double]::IsInfinity([double]$sample)) { throw 'Invalid sample duration.' } }
                $dataStatus = 'COMPLETE'
            } catch { $dataStatus = 'INVALID'; if ($runState -eq 'PASS') { $runState = 'INVALID'; $reason = $_.Exception.Message } }
        }
        if ($counts.failed -gt 0) {
            if ($runState -eq 'PASS') { $runState = 'FAIL' }
            if (-not $reason) { $reason = "NUnit failed $($counts.failed)." }
        } elseif ($runState -eq 'PASS' -and ($counts.total -eq 0 -or $counts.passed -eq 0)) {
            $runState = 'BLOCKED'
            $reason = 'No tests passed the requested filter.'
        } elseif ($runState -eq 'PASS' -and $proc.ExitCode -ne 0) {
            $runState = 'BLOCKED'
            $reason = "NUnit XML exists but Unity exited $($proc.ExitCode)."
        }
        if ($runState -ne 'PASS') { break }
    }
    $remote = & git -C $project ls-remote --heads origin main 2>&1
    $remoteText = ($remote | Out-String).Trim()
    if ($LASTEXITCODE -eq 0 -and $remoteText -match '^([0-9a-f]{40})') {
        $postMainSha = $Matches[1]
        $mainFreshnessStatus = if ($postMainSha -eq $mainSha) { 'CURRENT' } else { 'STALE' }
        if ($postMainSha -ne $mainSha) {
            $isStale = $true
            if ($runState -eq 'PASS') { $runState = 'STALE'; $reason = 'origin/main changed during the run.' }
        }
    } else { $mainFreshnessStatus = 'UNKNOWN'; if ($runState -eq 'PASS') { $runState = 'INCONCLUSIVE'; $reason = 'Could not verify post-run origin/main.' } }
    if ($runState -eq 'PASS') { $exitCode = 0 } else { $exitCode = 1 }
} catch {
    $runState = 'BLOCKED'
    $reason = $_.Exception.Message
    $exitCode = 2
} finally {
    $ended = [DateTimeOffset]::Now
    if (-not (Test-Path -LiteralPath $runDir)) { [IO.Directory]::CreateDirectory($runDir) | Out-Null }
    try { Restore-KnownSettingsPollution }
    catch { $worktreeStatus = 'RECOVERY_ERROR'; $settingsRecovery = $_.Exception.Message }
    if ($worktreeStatus -in @('DIRTY_UNEXPECTED','DIRTY_RESTORE_FAILED','RECOVERY_ERROR')) {
        if ($runState -eq 'PASS') { $runState = 'INVALID'; $reason = 'Unexpected DataTest worktree modification after test.' }
        $exitCode = 1
    }
    if ($dataStatus -eq 'PENDING') { $dataStatus = 'NOT_COLLECTED' }
    if ($runState -ne 'PASS' -and $exitCode -eq 0) { $exitCode = 1 }
    $summary = [ordered]@{
        runId=$runId;startedAt=$started.ToString('o');endedAt=$ended.ToString('o')
        timezone=[TimeZoneInfo]::Local.Id;profile=$Profile;filter=$testFilter;seed=$Seed
        requestedLoops=$runLoops;isStale=$isStale;allowedStaleUnityPid=$AllowStaleUnityPid;verifiedStaleUnityPids=$verifiedStaleUnityPids;mainSha=$mainSha;dataTestSha=$dataSha;postMainSha=$postMainSha
        status=$runState;reason=$reason;counts=$counts;legs=$legs
        quality=[ordered]@{nunit=$nunitStatus;unityExit=$unityExitStatus;data=$dataStatus;mainFreshness=$mainFreshnessStatus;worktree=$worktreeStatus;overall=$runState}
        settingsRecovery=$settingsRecovery
        unityVersion='2022.3.62f3c1';platform='Windows Editor';machine=$env:COMPUTERNAME
        outputDirectory=$runDir
    }
    $summaryPath = Join-Path $runDir 'summary.json'
    $summary | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $summaryPath -Encoding UTF8
    $reportPath = Join-Path $reportsDir "$runId.md"
    $lines = @(
        "# DataTest 运行 $runId",'',
        "- 状态：**$runState**",
        "- 原因：$reason",
        "- 分项门禁：NUnit=$nunitStatus；Unity 退出=$unityExitStatus；数据=$dataStatus；main 时效=$mainFreshnessStatus；工作树=$worktreeStatus",
        "- 设置恢复：$settingsRecovery",
        "- 时间：$($started.ToString('o')) → $($ended.ToString('o'))（$([TimeZoneInfo]::Local.Id)）",
        "- main SHA：$mainSha",
        "- DataTest SHA：$dataSha",
        "- 结束时远端 main SHA：$postMainSha；是否过期：$isStale",
        "- 测试：$Profile；过滤器：$testFilter；循环：$runLoops；Seed：$Seed",
        "- 平台：Windows Editor；Unity：2022.3.62f3c1；机器：$env:COMPUTERNAME",
        "- NUnit：总数 $($counts.total)，通过 $($counts.passed)，失败 $($counts.failed)，跳过 $($counts.skipped)，不确定 $($counts.inconclusive)",'',
        '## 原始证据','',
        "- [summary.json](../07_测试证据与原始日志/$runId/summary.json)"
    )
    foreach ($leg in $legs) {
        $lines += "- [$($leg.platform) XML](../07_测试证据与原始日志/$runId/$($leg.platform).xml)；[Unity 日志](../07_测试证据与原始日志/$runId/$($leg.platform).log)"
        if (Test-Path -LiteralPath $leg.metrics) {
            $lines += "- [$($leg.platform) 指标](../07_测试证据与原始日志/$runId/$($leg.platform)-metrics.json)"
            try {
                $m = Get-Content -LiteralPath $leg.metrics -Raw | ConvertFrom-Json
                $lines += "- 地图生成 Editor 插桩：平均 $([math]::Round($m.meanMilliseconds,2)) ms，P95 $([math]::Round($m.p95Milliseconds,2)) ms，P99 $([math]::Round($m.p99Milliseconds,2)) ms；循环 $($m.loops) 次，地图 $($m.width)×$($m.height)。"
                $lines += "- 稳定态托管内存：$([math]::Round($m.managedBeforeBytes/1MB,2)) → $([math]::Round($m.managedAfterBytes/1MB,2)) MiB；Unity Reserved：$([math]::Round($m.unityReservedBeforeBytes/1MB,2)) → $([math]::Round($m.unityReservedAfterBytes/1MB,2)) MiB。单轮变化不构成泄漏结论。"
            } catch { $lines += '- 指标 JSON 解析失败；保留原始文件供复核。' }
        }
    }
    $lines += @('','## 结论边界','','本运行只覆盖报告列出的 Editor 测试。Player 功能、真实输入黑盒、长期内存泄漏和性能回归未由本运行证明。')
    $lines | Set-Content -LiteralPath $reportPath -Encoding UTF8
    if (Test-Path -LiteralPath $activeFile) { Remove-Item -LiteralPath $activeFile -Force }
    if ($runGate) { $runGate.Dispose() }
    try { & (Join-Path $PSScriptRoot 'Update-DataTestDashboard.ps1') | Out-Null }
    catch { $_.Exception.Message | Add-Content -LiteralPath (Join-Path $stateDir 'dashboard-errors.log') -Encoding UTF8 }
    Write-Output ($summary | ConvertTo-Json -Depth 8)
}
exit $exitCode

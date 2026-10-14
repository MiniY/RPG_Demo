[CmdletBinding()]
param(
    [switch]$FromRunner,
    [switch]$AutoSmoke
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$config = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'config.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$project = [IO.Path]::GetFullPath($config.projectPath)
$output = [IO.Path]::GetFullPath($config.outputRoot)
$stateDir = Join-Path $output '01_环境与同步'
[IO.Directory]::CreateDirectory($stateDir) | Out-Null
$stateFile = Join-Path $stateDir 'sync-status.json'
$logFile = Join-Path $stateDir 'sync.log'
$lockFile = Join-Path $stateDir 'sync.lock'
$runLock = Join-Path $stateDir 'run.lock'
$state = [ordered]@{
    checkedAt = [DateTimeOffset]::Now.ToString('o')
    lastSuccessfulSyncAt = $null
    result = 'RUNNING'
    mainSha = $null
    dataTestSha = $null
    pendingCommits = $null
    conflict = $false
    error = $null
}
if (Test-Path -LiteralPath $stateFile) {
    try { $previous = Get-Content -LiteralPath $stateFile -Raw -Encoding UTF8 | ConvertFrom-Json; $state.lastSuccessfulSyncAt = $previous.lastSuccessfulSyncAt } catch {}
}
function Invoke-Git([string[]]$GitArgs) {
    $lines = & git -C $project @GitArgs 2>&1
    if ($LASTEXITCODE -ne 0) { throw "git $($GitArgs -join ' ') failed ($LASTEXITCODE): $($lines -join ' ')" }
    return ($lines | Out-String).Trim()
}
function Test-FileLocked([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) { return $false }
    try {
        $probe = [IO.File]::Open($Path, [IO.FileMode]::Open, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
        $probe.Dispose()
        return $false
    } catch [IO.IOException] { return $true }
}
$gate = $null
$changed = $false
$exitCode = 0
try {
    $gate = [IO.File]::Open($lockFile, [IO.FileMode]::OpenOrCreate, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
    if (-not $FromRunner -and (Test-FileLocked $runLock)) { throw 'A DataTest run is active.' }
    if (-not (Test-Path -LiteralPath $project)) { throw 'DataTest worktree is missing.' }
    if (Test-FileLocked (Join-Path $project 'Temp\UnityLockfile')) { throw 'DataTest Unity project is occupied; sync deferred.' }
    if ((Invoke-Git -GitArgs @('branch','--show-current')) -ne 'DataTest') { throw 'Worktree is not on DataTest.' }
    $state.dataTestSha = Invoke-Git -GitArgs @('rev-parse','HEAD')
    $oldMain = Invoke-Git -GitArgs @('rev-parse','refs/remotes/origin/main')
    $fetch = & git -C $project fetch --no-tags origin refs/heads/main:refs/remotes/origin/main 2>&1
    if ($LASTEXITCODE -ne 0) { throw "Fetch failed ($LASTEXITCODE): $($fetch -join ' ')" }
    $state.mainSha = Invoke-Git -GitArgs @('rev-parse','refs/remotes/origin/main')
    & git -C $project merge-base --is-ancestor $oldMain $state.mainSha 2>$null
    if ($LASTEXITCODE -ne 0) { throw 'origin/main moved non-fast-forward; manual review required.' }
    $dirty = Invoke-Git -GitArgs @('status','--porcelain=v1','--untracked-files=all')
    if ($dirty) { throw 'DataTest has uncommitted or untracked files; sync deferred.' }
    $mergeHead = & git -C $project rev-parse -q --verify MERGE_HEAD 2>$null
    if ($LASTEXITCODE -eq 0 -and $mergeHead) { throw 'An unfinished merge already exists.' }
    $state.pendingCommits = [int](Invoke-Git -GitArgs @('rev-list','--count',"HEAD..$($state.mainSha)"))
    & git -C $project merge-base --is-ancestor $state.mainSha HEAD 2>$null
    if ($LASTEXITCODE -eq 0) {
        $state.result = 'UNCHANGED'
    } else {
        $merge = & git -C $project merge --no-edit --no-ff $state.mainSha 2>&1
        if ($LASTEXITCODE -ne 0) { throw "Merge failed ($LASTEXITCODE): $($merge -join ' ')" }
        $state.dataTestSha = Invoke-Git -GitArgs @('rev-parse','HEAD')
        & git -C $project merge-base --is-ancestor $state.mainSha HEAD 2>$null
        if ($LASTEXITCODE -ne 0) { throw 'Post-merge ancestry verification failed.' }
        $state.pendingCommits = 0
        $state.result = 'MERGED'
        $changed = $true
    }
    $state.lastSuccessfulSyncAt = [DateTimeOffset]::Now.ToString('o')
} catch {
    $state.result = 'BLOCKED'
    $state.error = $_.Exception.Message
    $exitCode = 2
    try {
        $unmerged = & git -C $project diff --name-only --diff-filter=U 2>$null
        $state.conflict = [bool]$unmerged
        $state.dataTestSha = (& git -C $project rev-parse HEAD 2>$null | Out-String).Trim()
        if ($state.mainSha) { $state.pendingCommits = [int]((& git -C $project rev-list --count "HEAD..$($state.mainSha)" 2>$null | Out-String).Trim()) }
    } catch {}
} finally {
    $state.checkedAt = [DateTimeOffset]::Now.ToString('o')
    $tmp = "$stateFile.$PID.tmp"
    $state | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $tmp -Encoding UTF8
    Move-Item -LiteralPath $tmp -Destination $stateFile -Force
    "$($state.checkedAt) $($state.result) main=$($state.mainSha) DataTest=$($state.dataTestSha) pending=$($state.pendingCommits) error=$($state.error)" | Add-Content -LiteralPath $logFile -Encoding UTF8
    if ($gate) { $gate.Dispose() }
}
$state | ConvertTo-Json -Depth 5
$dashboardScript = Join-Path $PSScriptRoot 'Update-DataTestDashboard.ps1'
if (Test-Path -LiteralPath $dashboardScript) {
    try { & $dashboardScript | Out-Null }
    catch { $_.Exception.Message | Add-Content -LiteralPath (Join-Path $stateDir 'dashboard-errors.log') -Encoding UTF8 }
}
if ($exitCode -eq 0 -and $changed -and $AutoSmoke) {
    & (Join-Path $PSScriptRoot 'Invoke-DataTest.ps1') -Profile Smoke -SkipSync
    exit $LASTEXITCODE
}
exit $exitCode

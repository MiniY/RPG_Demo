$ErrorActionPreference = 'Stop'
$root = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\..')).Path
Push-Location $root
try {
    foreach ($name in @('post-commit', 'post-merge', 'post-checkout', 'post-rewrite')) {
        if (-not (Test-Path -LiteralPath (Join-Path $root ".githooks/$name") -PathType Leaf)) {
            throw "Missing versioned hook: $name"
        }
    }
    & python --version | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Python is required by the architecture update hook.' }
    $existingPath = (& git config --local --get core.hooksPath)
    if ($LASTEXITCODE -eq 0 -and $existingPath -ne '.githooks') {
        throw "Existing core.hooksPath is '$existingPath'. No configuration changed; integrate hooks manually."
    }
    if (-not $existingPath) {
        $hooksDir = (& git rev-parse --git-path hooks).Trim()
        if ($LASTEXITCODE -ne 0) { throw 'Cannot locate current Git hooks directory.' }
        if (-not [IO.Path]::IsPathRooted($hooksDir)) { $hooksDir = Join-Path $root $hooksDir }
        $active = @(Get-ChildItem -LiteralPath $hooksDir -File -ErrorAction SilentlyContinue |
            Where-Object { $_.Name -notlike '*.sample' })
        if ($active.Count -gt 0) {
            throw "Existing active hooks found: $($active.Name -join ', '). No configuration changed."
        }
        & git config --local core.hooksPath .githooks
        if ($LASTEXITCODE -ne 0) { throw 'git config failed.' }
    }
    $now = (& git config --local --get core.hooksPath)
    if ($now -ne '.githooks') { throw "Installation did not persist: $now" }
    Write-Output "Installed versioned hooks for this repository: $now"
} finally {
    Pop-Location
}

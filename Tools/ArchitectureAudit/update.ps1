param([switch]$Check)

$ErrorActionPreference = 'Stop'
$scriptPath = Join-Path $PSScriptRoot 'update.py'
if (-not (Test-Path -LiteralPath $scriptPath -PathType Leaf)) {
    throw "Missing update.py: $scriptPath"
}
$arguments = @($scriptPath)
if ($Check) { $arguments += '--check' }
& python @arguments
exit $LASTEXITCODE

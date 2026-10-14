[CmdletBinding()]
param()
$path='D:\Obsidian-1.9.14\MyObjects\Unity\RPG_Demo文档\测试文档\01_环境与同步\active-run.json'
if (-not (Test-Path -LiteralPath $path)) { Write-Output 'No DataTest run is active.'; exit 1 }
$active = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
$runDir = Join-Path 'D:\Obsidian-1.9.14\MyObjects\Unity\RPG_Demo文档\测试文档\07_测试证据与原始日志' $active.runId
if (-not (Test-Path -LiteralPath $runDir)) { throw 'Active run directory is missing.' }
New-Item -ItemType File -Path (Join-Path $runDir 'cancel.request') -ErrorAction SilentlyContinue | Out-Null
Write-Output "Cancellation requested for $($active.runId). The runner will stop only its own Unity process."

[CmdletBinding()]
param(
 [Parameter(Mandatory)][string]$Key,
 [Parameter(Mandatory)][string]$Title,
 [Parameter(Mandatory)][string]$Module,
 [ValidateSet('P0','P1','P2','P3')][string]$Priority='P2',
 [Parameter(Mandatory)][string]$Expected,
 [Parameter(Mandatory)][string]$Actual,
 [Parameter(Mandatory)][string]$ReproSteps,
 [string]$RunId='',
 [string]$Seed='',
 [string]$EvidencePath='',
 [string]$CodeLocation=''
)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$config=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'config.json') -Raw|ConvertFrom-Json
$dir=Join-Path $config.outputRoot '06_缺陷追踪'
[IO.Directory]::CreateDirectory($dir)|Out-Null
$normalizedKey=$Key.Trim().ToLowerInvariant()
$bytes=[Text.Encoding]::UTF8.GetBytes($normalizedKey)
$hash=[Security.Cryptography.SHA256]::Create().ComputeHash($bytes)
$id='BUG-'+(($hash[0..5]|ForEach-Object {$_.ToString('X2')}) -join '')
$path=Join-Path $dir "$id.md"
$time=[DateTimeOffset]::Now.ToString('o')
$mainSha='UNVERIFIED'
$dataTestSha='UNVERIFIED'
$environment='UNVERIFIED'
if($RunId){
 $summaryPath=Join-Path $config.outputRoot "07_测试证据与原始日志\$RunId\summary.json"
 if(-not(Test-Path -LiteralPath $summaryPath)){throw 'Run ID summary.json not found; refuse unverified association.'}
 $run=Get-Content -LiteralPath $summaryPath -Raw|ConvertFrom-Json
 $mainSha=[string]$run.mainSha
 $dataTestSha=[string]$run.dataTestSha
 $environment="$($run.platform); Unity $($run.unityVersion); $($run.machine)"
 if(-not $mainSha){$mainSha='UNVERIFIED'}
}
$event="- $time；Run ID：$RunId；Seed：$Seed；实际：$Actual"
if(Test-Path -LiteralPath $path){
 $text=Get-Content -LiteralPath $path -Raw
 if($text -notmatch [regex]::Escape("- 归一化键：$normalizedKey")){throw 'Bug ID collision or key mismatch; refuse edit.'}
 if($RunId -and $text.Contains("Run ID：$RunId")){Write-Output "$id ALREADY_RECORDED $path";exit 0}
 Add-Content -LiteralPath $path -Value $event -Encoding UTF8
 Write-Output "$id UPDATED $path"
}else{
 @(
 "# $id $Title",'',
 "- 归一化键：$normalizedKey",
 "- 模块：$Module",
 "- 优先级：$Priority",
 '- 状态：OPEN',
 "- 首次发现：$time",
 "- 最近复现：$time",
 "- 预期：$Expected",
 "- 实际：$Actual",
 "- 复现步骤：$ReproSteps",
 "- Seed：$Seed",
 "- Run ID：$RunId",
 "- main SHA：$mainSha",
 "- DataTest SHA：$dataTestSha",
 "- 测试环境：$environment",
 "- 日志或截图：$EvidencePath",
 "- 相关代码位置：$CodeLocation",
 "- 回归验证：待执行",'',
 '## 复现历史','',
 $event,'',
 '## 根因与修复建议','','待证据确认。DataTest 不直接修改 main 业务逻辑。'
 )|Set-Content -LiteralPath $path -Encoding UTF8
 Write-Output "$id CREATED $path"
}

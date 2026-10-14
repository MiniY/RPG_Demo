[CmdletBinding()]
param()
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$base = $PSScriptRoot
$command = 'powershell.exe -NoProfile -NonInteractive -ExecutionPolicy Bypass -File "' + (Join-Path $base 'Sync-DataTest.ps1') + '" -AutoSmoke'
$tasks = @(
    @{Name='RPG_Demo_DataTest_Sync_5min';Args=@('/SC','MINUTE','/MO','5')},
    @{Name='RPG_Demo_DataTest_Sync_Logon';Args=@('/SC','ONLOGON')}
)
foreach ($task in $tasks) {
    & schtasks.exe /Query /TN $task.Name /FO LIST 2>$null | Out-Null
    if ($LASTEXITCODE -eq 0) {
        Write-Output "$($task.Name): already exists; inspect it instead of overwriting."
        continue
    }
    $args = @('/Create','/TN',$task.Name,'/TR',$command,'/F') + $task.Args
    $result = & schtasks.exe @args 2>&1
    if ($LASTEXITCODE -ne 0) { throw "Task creation failed for $($task.Name): $($result -join ' ')" }
    $check = & schtasks.exe /Query /TN $task.Name /V /FO LIST 2>&1
    if ($LASTEXITCODE -ne 0) { throw "Task creation was reported but verification failed for $($task.Name)." }
    Write-Output "$($task.Name): INSTALLED"
    Write-Output ($check | Select-String -Pattern 'TaskName|Task To Run|Schedule Type|Last Result|TaskName|上次运行结果|计划任务' | Out-String)
}

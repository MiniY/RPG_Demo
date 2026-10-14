[CmdletBinding()]
param([ValidateRange(1,60)][int]$IntervalMinutes = 5)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$name = 'RPG_Demo_DataTest_Sync_5min'
$script = Join-Path $PSScriptRoot 'Sync-DataTest.ps1'
$command = 'powershell.exe -NoProfile -NonInteractive -ExecutionPolicy Bypass -File "' + $script + '" -AutoSmoke'
$task = Get-ScheduledTask -TaskName $name -ErrorAction SilentlyContinue
if (-not $task) {
    $result = & schtasks.exe /Create /TN $name /TR $command /SC MINUTE /MO $IntervalMinutes 2>&1
    if ($LASTEXITCODE -ne 0) { throw "Task creation failed: $($result -join ' ')" }
    $task = Get-ScheduledTask -TaskName $name -ErrorAction Stop
}
$minuteTriggers = @($task.Triggers | Where-Object { $_.CimClass.CimClassName -eq 'MSFT_TaskTimeTrigger' })
if ($minuteTriggers.Count -ne 1 -or $minuteTriggers[0].Repetition.Interval -ne "PT$($IntervalMinutes)M") {
    throw 'Existing task has an unexpected minute trigger; inspect it manually rather than overwriting.'
}
$triggers = @($task.Triggers)
if (-not @($triggers | Where-Object { $_.CimClass.CimClassName -eq 'MSFT_TaskLogonTrigger' }).Count) {
    $account = "$env:USERDOMAIN\$env:USERNAME"
    $triggers += New-ScheduledTaskTrigger -AtLogOn -User $account
}
$settings = $task.Settings
$settings.StartWhenAvailable = $true
$settings.DisallowStartIfOnBatteries = $false
$settings.StopIfGoingOnBatteries = $false
Set-ScheduledTask -TaskName $name -Trigger $triggers -Settings $settings -ErrorAction Stop | Out-Null
$check = Get-ScheduledTask -TaskName $name -ErrorAction Stop
$logonCount = @($check.Triggers | Where-Object { $_.CimClass.CimClassName -eq 'MSFT_TaskLogonTrigger' }).Count
$minuteCount = @($check.Triggers | Where-Object { $_.CimClass.CimClassName -eq 'MSFT_TaskTimeTrigger' -and $_.Repetition.Interval -eq "PT$($IntervalMinutes)M" }).Count
if ($logonCount -ne 1 -or $minuteCount -ne 1 -or -not $check.Settings.StartWhenAvailable) {
    throw 'Task verification failed after installation.'
}
Write-Output "$name INSTALLED_AND_VERIFIED interval=$IntervalMinutes minutes logon=yes catchup=yes"
Get-ScheduledTaskInfo -TaskName $name | Select-Object LastRunTime,LastTaskResult,NextRunTime

<#
.SYNOPSIS
    Registers (or replaces) a Windows scheduled task that runs Update-Ytdlp.ps1 every day.

.DESCRIPTION
    The task runs as the current user, only while they are logged on (Docker Desktop runs in the user session), with
    Windows PowerShell. Pass the same -ComposeFile / -EnvFile you would give Update-Ytdlp.ps1 if they differ from the
    Dev defaults. -Unregister removes the task.

.EXAMPLE
    ./scripts/Register-YtdlpUpdateTask.ps1                  # daily at 04:00
.EXAMPLE
    ./scripts/Register-YtdlpUpdateTask.ps1 -At 06:30
.EXAMPLE
    ./scripts/Register-YtdlpUpdateTask.ps1 -Unregister
#>
[CmdletBinding(SupportsShouldProcess)]
param(
    [string] $TaskName = 'ms-vid-dldr Update yt-dlp',
    [string] $At = '04:00',
    [string[]] $ComposeFile = @('docker-compose.Dev.yaml'),
    [string] $EnvFile = '.env.dev',
    [switch] $Unregister
)

$ErrorActionPreference = 'Stop'

if ($Unregister) {
    if (Get-ScheduledTask -TaskName $TaskName -ErrorAction SilentlyContinue) {
        if ($PSCmdlet.ShouldProcess($TaskName, 'Unregister scheduled task')) {
            Unregister-ScheduledTask -TaskName $TaskName -Confirm:$false
            Write-Host "Removed scheduled task '$TaskName'."
        }
    }
    else { Write-Host "No scheduled task named '$TaskName'." }
    return
}

$script = Join-Path $PSScriptRoot 'Update-Ytdlp.ps1'
$files = ($ComposeFile | ForEach-Object { "'$($_.Replace("'", "''"))'" }) -join ','
$command = "& '$($script.Replace("'", "''"))' -ComposeFile $files -EnvFile '$($EnvFile.Replace("'", "''"))'"

$action = New-ScheduledTaskAction -Execute 'powershell.exe' `
    -Argument "-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command `"$command`"" `
    -WorkingDirectory (Split-Path -Parent $PSScriptRoot)
$trigger = New-ScheduledTaskTrigger -Daily -At ([datetime]::ParseExact($At, 'HH:mm', $null))
$settings = New-ScheduledTaskSettingsSet -StartWhenAvailable -ExecutionTimeLimit (New-TimeSpan -Minutes 30) `
    -MultipleInstances IgnoreNew
$principal = New-ScheduledTaskPrincipal -UserId "$env:USERDOMAIN\$env:USERNAME" -LogonType Interactive -RunLevel Limited

if ($PSCmdlet.ShouldProcess($TaskName, "Register daily task at $At")) {
    Register-ScheduledTask -TaskName $TaskName -Action $action -Trigger $trigger -Settings $settings `
        -Principal $principal -Description 'Rebuilds the ms-vid-dldr Downloader when PyPI has a newer yt-dlp.' -Force | Out-Null
    Write-Host "Registered '$TaskName': daily at $At (runs late if the PC was off). Log: Logs\Update-Ytdlp.log"
}

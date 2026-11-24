<#
.SYNOPSIS
    Installs the Distributor so it runs in the background whenever you log on (Windows Task Scheduler).

.DESCRIPTION
    1. Publishes MS.Vid-dldr.Distributor to -InstallPath (default %LOCALAPPDATA%\ms-vid-dldr\Distributor) as a
       windowless app (OutputType WinExe), so rebuilding the repo never locks or breaks the running copy. Re-run this
       script after changing the Distributor's code.
    2. Copies your git-ignored appsettings.Local.json next to it (share folder, DistributorUsername).
    3. Registers the task: at logon (1 minute later, so Docker Desktop and the Manager can start), as you, no time
       limit, restarted if it fails, one copy at a time. Then starts it.

    The build is Debug by default: a Release build also loads appsettings.Release.json, which points at the original
    production servers. Logs go to Grafana (Loki, service ms-vid-dldr-distributor); there is no console window.

.EXAMPLE
    ./scripts/Install-DistributorTask.ps1              # install / update, and start it
.EXAMPLE
    ./scripts/Install-DistributorTask.ps1 -Unregister  # stop it and remove the task (files are left in place)
#>
[CmdletBinding(SupportsShouldProcess)]
param(
    [string] $InstallPath = (Join-Path $env:LOCALAPPDATA 'ms-vid-dldr\Distributor'),
    [string] $TaskName = 'ms-vid-dldr Distributor',
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Debug',
    [switch] $Unregister
)

$ErrorActionPreference = 'Stop'
$project = Join-Path (Split-Path -Parent $PSScriptRoot) 'MS.Vid-dldr.Distributor'
$exeName = 'MS.Vid-dldr.Distributor.exe'

function Stop-Distributor {
    if (Get-ScheduledTask -TaskName $TaskName -ErrorAction SilentlyContinue) {
        Stop-ScheduledTask -TaskName $TaskName -ErrorAction SilentlyContinue
    }
    # The Distributor allows one instance (named mutex): stop any copy, including one started with `dotnet run`.
    Get-Process -Name ($exeName -replace '\.exe$') -ErrorAction SilentlyContinue | ForEach-Object {
        if ($PSCmdlet.ShouldProcess("$($_.Id) $($_.Path)", 'Stop running Distributor')) {
            Stop-Process -Id $_.Id -Force
            Write-Host "Stopped running Distributor (PID $($_.Id))."
        }
    }
}

if ($Unregister) {
    Stop-Distributor
    if (Get-ScheduledTask -TaskName $TaskName -ErrorAction SilentlyContinue) {
        if ($PSCmdlet.ShouldProcess($TaskName, 'Unregister scheduled task')) {
            Unregister-ScheduledTask -TaskName $TaskName -Confirm:$false
            Write-Host "Removed scheduled task '$TaskName'. Files in $InstallPath were left in place."
        }
    }
    else { Write-Host "No scheduled task named '$TaskName'." }
    return
}

Stop-Distributor

if ($PSCmdlet.ShouldProcess($InstallPath, "Publish the Distributor ($Configuration)")) {
    & dotnet publish (Join-Path $project 'MS.Vid-dldr.Distributor.csproj') -c $Configuration -o $InstallPath -p:OutputType=WinExe --nologo -v quiet
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit code $LASTEXITCODE" }

    $local = Join-Path $project 'appsettings.Local.json'
    if (Test-Path $local) {
        Copy-Item $local (Join-Path $InstallPath 'appsettings.Local.json') -Force
        Write-Host 'Copied appsettings.Local.json.'
    }
    else {
        Write-Warning "No appsettings.Local.json in ${project}: the Distributor will use appsettings.json's share folder and your Windows user name."
    }
}

$action = New-ScheduledTaskAction -Execute (Join-Path $InstallPath $exeName) -WorkingDirectory $InstallPath
$trigger = New-ScheduledTaskTrigger -AtLogOn -User "$env:USERDOMAIN\$env:USERNAME"
$trigger.Delay = 'PT1M'
$settings = New-ScheduledTaskSettingsSet -ExecutionTimeLimit ([TimeSpan]::Zero) -RestartCount 5 `
    -RestartInterval (New-TimeSpan -Minutes 1) -MultipleInstances IgnoreNew -StartWhenAvailable `
    -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries
$principal = New-ScheduledTaskPrincipal -UserId "$env:USERDOMAIN\$env:USERNAME" -LogonType Interactive -RunLevel Limited

if ($PSCmdlet.ShouldProcess($TaskName, 'Register and start the logon task')) {
    Register-ScheduledTask -TaskName $TaskName -Action $action -Trigger $trigger -Settings $settings -Principal $principal `
        -Description 'Moves finished ms-vid-dldr downloads from the share folder to their requested folder.' -Force | Out-Null
    Start-ScheduledTask -TaskName $TaskName
    Write-Host "Registered and started '$TaskName' ($InstallPath\$exeName). It starts 1 minute after you log on."
}

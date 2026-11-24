<#
.SYNOPSIS
    Rebuilds and restarts the Downloader container when PyPI has a newer yt-dlp than the one it runs.

.DESCRIPTION
    1. Reads the latest yt-dlp release from PyPI (or uses -Version).
    2. Reads the version installed in the running Downloader container.
    3. If the latest is newer, checks the Manager for active downloads. The Downloader acks each message as soon as it
       receives it, so restarting it mid-download would lose that download: when one is active the script skips this
       run and the next scheduled run tries again.
    4. Rebuilds only the Downloader image with --build-arg YTDLP_VERSION=<latest> (only the pip layer is rebuilt),
       restarts only that container (--no-deps) and checks the new version.

    Every run is appended to Logs/Update-Ytdlp.log. Exit code 0 = up to date, updated or skipped; 1 = failed.
    Runs on Windows PowerShell 5.1 and PowerShell 7.

.EXAMPLE
    ./scripts/Update-Ytdlp.ps1 -WhatIf          # report what would happen
.EXAMPLE
    ./scripts/Update-Ytdlp.ps1                  # update the Dev stack if a newer yt-dlp exists
.EXAMPLE
    ./scripts/Update-Ytdlp.ps1 -Version 2025.10.22 -Force   # install a specific release
#>
[CmdletBinding(SupportsShouldProcess)]
param(
    # Compose files, relative to the repo root, in the order docker compose -f takes them.
    [string[]] $ComposeFile = @('docker-compose.Dev.yaml'),
    [string] $EnvFile = '.env.dev',
    [string] $Service = 'downloader',
    # Install this release instead of the latest one on PyPI.
    [string] $Version,
    # Rebuild even when the installed version already matches.
    [switch] $Force,
    # Manager REST address used to look for active downloads.
    [string] $ManagerUrl = 'http://localhost:8000',
    # ANALYSIS/DOWNLOADING downloads with activity in this many minutes count as active (older ones are stuck).
    [int] $BusyWindowMinutes = 30,
    # Restart even when downloads are active, or when the Manager cannot be reached.
    [switch] $SkipBusyCheck,
    [string] $LogPath
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
if (-not $LogPath) { $LogPath = Join-Path $repoRoot 'Logs\Update-Ytdlp.log' }

function Write-Log([string] $Message) {
    $line = '{0:yyyy-MM-dd HH:mm:ss} {1}' -f (Get-Date), $Message
    Write-Host $line
    $dir = Split-Path -Parent $LogPath
    if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force -WhatIf:$false | Out-Null }
    Add-Content -Path $LogPath -Value $line -Encoding UTF8 -WhatIf:$false
}

function Get-ComposeArgs {
    $composeArgs = @('compose', '--env-file', $EnvFile)
    # Machine-specific overrides (git-ignored), e.g. .env.dev.local after .env.dev.
    if (Test-Path "$EnvFile.local") { $composeArgs += @('--env-file', "$EnvFile.local") }
    foreach ($file in $ComposeFile) { $composeArgs += @('-f', $file) }
    return , $composeArgs
}

# Runs docker compose; stderr (progress output) goes to the console. Throws on a non-zero exit code.
function Invoke-Compose([string[]] $Arguments) {
    # Compose writes progress to stderr. If the caller redirects it, Windows PowerShell turns each line into an error,
    # which 'Stop' would throw; rely on the exit code instead.
    $ErrorActionPreference = 'Continue'
    $all = (Get-ComposeArgs) + $Arguments
    & docker @all
    if ($LASTEXITCODE -ne 0) { throw "docker $($all -join ' ') failed with exit code $LASTEXITCODE" }
}

# yt-dlp versions are dates (2025.01.15, nightlies add .HHMMSS). PyPI drops leading zeros; [version] compares both.
function ConvertTo-YtdlpVersion([string] $Text) {
    $parsed = $null
    if ($Text -and [version]::TryParse($Text.Trim(), [ref] $parsed)) { return $parsed }
    return $null
}

function Get-LatestYtdlpVersion {
    $info = Invoke-RestMethod -Uri 'https://pypi.org/pypi/yt-dlp/json' -TimeoutSec 30
    return [string] $info.info.version
}

# Version installed in the running container, or $null when it is not running.
function Get-InstalledYtdlpVersion {
    # Windows PowerShell turns redirected native stderr into errors, which 'Stop' would throw.
    $ErrorActionPreference = 'Continue'
    $all = (Get-ComposeArgs) + @('exec', '-T', $Service, 'python', '-c', 'import yt_dlp.version as v; print(v.__version__)')
    $output = & docker @all 2>$null
    if ($LASTEXITCODE -ne 0) { return $null }
    return ([string] ($output | Select-Object -Last 1)).Trim()
}

function Get-ActiveDownloadCount {
    $response = Invoke-RestMethod -Uri "$($ManagerUrl.TrimEnd('/'))/v1/configure/downloads/take/200" -TimeoutSec 30
    $since = (Get-Date).ToUniversalTime().AddMinutes(-$BusyWindowMinutes)
    $active = @($response.downloads | Where-Object {
            # PowerShell 7 turns ISO dates in JSON into [datetime]; Windows PowerShell leaves strings.
            $last = if ($_.lastActivity -is [datetime]) { $_.lastActivity } elseif ($_.lastActivity) { [datetime]::Parse($_.lastActivity) }
            $_.status -in @('ANALYSIS', 'DOWNLOADING') -and $last -and $last.ToUniversalTime() -gt $since
        })
    return $active.Count
}

function Invoke-YtdlpUpdate {
    $target = if ($Version) { $Version } else { Get-LatestYtdlpVersion }
    $targetVersion = ConvertTo-YtdlpVersion $target
    if (-not $targetVersion) { throw "Cannot read '$target' as a yt-dlp version." }

    $installed = Get-InstalledYtdlpVersion
    $installedVersion = ConvertTo-YtdlpVersion $installed
    $running = [bool] $installed

    if ($running) {
        Write-Log "yt-dlp: installed $installed, target $target$(if (-not $Version) { ' (latest on PyPI)' })."
        if (-not $Force -and $installedVersion -and $installedVersion -ge $targetVersion) {
            Write-Log 'Up to date; nothing to do.'
            return
        }
    }
    else {
        Write-Log "yt-dlp: '$Service' is not running; target $target. The image will be rebuilt but not started."
    }

    if ($running -and -not $SkipBusyCheck) {
        try { $activeCount = Get-ActiveDownloadCount }
        catch {
            Write-Log "Skipped: cannot check the Manager for active downloads ($($_.Exception.Message)). Use -SkipBusyCheck to restart anyway."
            return
        }
        if ($activeCount -gt 0) {
            Write-Log "Skipped: $activeCount download(s) active; restarting the Downloader would lose them. The next run will try again."
            return
        }
    }

    if (-not $PSCmdlet.ShouldProcess("$Service (yt-dlp $installed -> $target)", 'Rebuild image and restart container')) { return }

    $previousArg = $env:YTDLP_VERSION
    $env:YTDLP_VERSION = $target
    try {
        Write-Log "Building '$Service' with YTDLP_VERSION=$target..."
        Invoke-Compose @('build', $Service)
        if ($running) {
            Write-Log "Restarting '$Service'..."
            Invoke-Compose @('up', '-d', '--no-deps', $Service)
        }
    }
    finally { $env:YTDLP_VERSION = $previousArg }

    if ($running) {
        Start-Sleep -Seconds 3
        $now = Get-InstalledYtdlpVersion
        $nowVersion = ConvertTo-YtdlpVersion $now
        if (-not $nowVersion -or $nowVersion -ne $targetVersion) { throw "After the restart '$Service' reports yt-dlp '$now', expected $target." }
        Write-Log "Updated: '$Service' now runs yt-dlp $now."
    }
    else {
        Write-Log "Image rebuilt with yt-dlp $target; it is used the next time '$Service' starts."
    }
}

$exitCode = 0
Push-Location $repoRoot
try { Invoke-YtdlpUpdate }
catch {
    Write-Log "FAILED: $($_.Exception.Message)"
    $exitCode = 1
}
finally { Pop-Location }
exit $exitCode

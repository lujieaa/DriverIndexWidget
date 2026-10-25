
# Upgrade-DesktopGuardPolicy.ps1
# Windows PowerShell 5.1
# Detect new non-shortcut desktop items and move them to another folder.

$ErrorActionPreference = 'Stop'

$appDir = Join-Path $env:LOCALAPPDATA 'DesktopClickGuard'
$monitorPath = Join-Path $appDir 'DesktopClickGuard.ps1'
$startupDir = [Environment]::GetFolderPath('Startup')
$shortcutPath = Join-Path $startupDir 'DesktopClickGuard.lnk'
$psExe = Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe'

New-Item -ItemType Directory -Path $appDir -Force | Out-Null
New-Item -ItemType Directory -Path $startupDir -Force | Out-Null

# Back up the existing monitor script, if present.
if (Test-Path $monitorPath) {
    $backup = "$monitorPath.backup-$(Get-Date -Format 'yyyyMMdd-HHmmss')"
    Copy-Item -LiteralPath $monitorPath -Destination $backup -Force
}

# Stop existing monitor processes for this script before replacing it.
Get-CimInstance Win32_Process |
    Where-Object {
        $_.ProcessId -ne $PID -and
        $_.Name -match '^powershell(\.exe)?$' -and
        $_.CommandLine -and
        $_.CommandLine.Contains($monitorPath)
    } |
    ForEach-Object {
        Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue
    }

Start-Sleep -Seconds 1

$monitorCode = @'
$ErrorActionPreference = 'SilentlyContinue'
Add-Type -AssemblyName System.Windows.Forms

$desktop = [Environment]::GetFolderPath('Desktop')
$documents = [Environment]::GetFolderPath('MyDocuments')
$fallback = Join-Path $documents 'Desktop-BlockedItems'

# If Documents resolves to the Desktop itself, use a private app folder.
if ([IO.Path]::GetFullPath($fallback).TrimEnd('\') -eq
    [IO.Path]::GetFullPath($desktop).TrimEnd('\')) {
    $fallback = Join-Path $env:LOCALAPPDATA 'DesktopClickGuard\BlockedItems'
}

New-Item -ItemType Directory -Path $fallback -Force | Out-Null

function Test-IsShortcut {
    param([string]$Path)
    $ext = [IO.Path]::GetExtension($Path).ToLowerInvariant()
    return ($ext -eq '.lnk' -or $ext -eq '.url')
}

function Get-ItemSignature {
    param($Item)
    try {
        $length = 0
        $modified = $Item.LastWriteTimeUtc.Ticks
        if (-not $Item.PSIsContainer) {
            $length = $Item.Length
        }
        return "$($Item.FullName)|$length|$modified"
    } catch {
        return ''
    }
}

function Get-FreeDestination {
    param(
        [string]$Folder,
        [string]$Name
    )

    $candidate = Join-Path $Folder $Name
    if (-not (Test-Path -LiteralPath $candidate)) {
        return $candidate
    }

    $base = [IO.Path]::GetFileNameWithoutExtension($Name)
    $ext = [IO.Path]::GetExtension($Name)
    $i = 1

    do {
        $newName = '{0} ({1}){2}' -f $base, $i, $ext
        $candidate = Join-Path $Folder $newName
        $i++
    } while (Test-Path -LiteralPath $candidate)

    return $candidate
}

function Handle-NewDesktopItem {
    param($Item)

    if (-not (Test-Path -LiteralPath $Item.FullName)) {
        return
    }

    if (Test-IsShortcut -Path $Item.FullName) {
        return
    }

    $message = "The Desktop is not a file storage location.`r`n`r`nNew item detected: $($Item.Name)`r`n`r`nPlease choose another folder. Clicking Cancel moves it automatically to:`r`n$fallback"

    [System.Windows.Forms.MessageBox]::Show(
        $message,
        'Desktop Storage Notice',
        [System.Windows.Forms.MessageBoxButtons]::OK,
        [System.Windows.Forms.MessageBoxIcon]::Warning
    ) | Out-Null

    $targetFolder = $fallback
    $dialog = New-Object System.Windows.Forms.FolderBrowserDialog
    $dialog.Description = 'Select a destination folder for this file or folder'
    $dialog.ShowNewFolderButton = $true
    $dialog.SelectedPath = [Environment]::GetFolderPath('MyDocuments')

    try {
        $result = $dialog.ShowDialog()
        if ($result -eq [System.Windows.Forms.DialogResult]::OK -and
            -not [string]::IsNullOrWhiteSpace($dialog.SelectedPath)) {

            $chosen = [IO.Path]::GetFullPath($dialog.SelectedPath).TrimEnd('\')
            $desk = [IO.Path]::GetFullPath($desktop).TrimEnd('\')

            # Do not allow selecting Desktop itself as the destination.
            if ($chosen -ne $desk) {
                $targetFolder = $chosen
            }
        }
    } catch {
        $targetFolder = $fallback
    } finally {
        $dialog.Dispose()
    }

    New-Item -ItemType Directory -Path $targetFolder -Force | Out-Null
    $destination = Get-FreeDestination -Folder $targetFolder -Name $Item.Name

    try {
        Move-Item -LiteralPath $Item.FullName -Destination $destination -Force -ErrorAction Stop
    } catch {
        [System.Windows.Forms.MessageBox]::Show(
            "Automatic move failed. The file may still be writing or in use by another program.`r`nClose the related program and move it manually.`r`n`r`nItem: $($Item.FullName)`r`nReason: $($_.Exception.Message)",
            'Failed To Move Desktop Item',
            [System.Windows.Forms.MessageBoxButtons]::OK,
            [System.Windows.Forms.MessageBoxIcon]::Error
        ) | Out-Null
    }
}

# Record existing Desktop entries so they are not moved at startup.
$known = @{}
$pending = @{}

Get-ChildItem -LiteralPath $desktop -Force |
    ForEach-Object {
        $known[$_.FullName.ToLowerInvariant()] = $true
    }

while ($true) {
    Start-Sleep -Seconds 1

    $items = @(Get-ChildItem -LiteralPath $desktop -Force -ErrorAction SilentlyContinue)
    $current = @{}

    foreach ($item in $items) {
        $key = $item.FullName.ToLowerInvariant()
        $current[$key] = $true

        if (-not $known.ContainsKey($key)) {
            if (-not (Test-IsShortcut -Path $item.FullName)) {
                if (-not $pending.ContainsKey($key)) {
                    $pending[$key] = @{
                        FirstSeen = Get-Date
                        Signature = Get-ItemSignature -Item $item
                        StableCount = 0
                    }
                } else {
                    $signature = Get-ItemSignature -Item $item
                    if ($signature -eq $pending[$key].Signature) {
                        $pending[$key].StableCount++
                    } else {
                        $pending[$key].Signature = $signature
                        $pending[$key].StableCount = 0
                    }
                }
            } else {
                $known[$key] = $true
            }
        }
    }

    # Forget paths that have disappeared; this also handles renamed items.
    foreach ($key in @($known.Keys)) {
        if (-not $current.ContainsKey($key)) {
            $known.Remove($key)
        }
    }

    foreach ($key in @($pending.Keys)) {
        if (-not $current.ContainsKey($key)) {
            $pending.Remove($key)
            continue
        }

        $entry = $pending[$key]
        $age = ((Get-Date) - $entry.FirstSeen).TotalSeconds

        # Wait for a download/copy to stop changing before moving it.
        if ($entry.StableCount -ge 3 -or $age -ge 30) {
            $item = Get-Item -LiteralPath $key -Force -ErrorAction SilentlyContinue

            if ($null -ne $item) {
                Handle-NewDesktopItem -Item $item
            }

            $known[$key] = $true
            $pending.Remove($key)
        }
    }
}
'@

# Write the monitor script.
Set-Content -LiteralPath $monitorPath -Value $monitorCode -Encoding UTF8

# Create/update the current-user Startup shortcut.
$shell = New-Object -ComObject WScript.Shell
$link = $shell.CreateShortcut($shortcutPath)
$link.TargetPath = $psExe
$link.Arguments = "-NoProfile -STA -ExecutionPolicy Bypass -WindowStyle Hidden -File `"$monitorPath`""
$link.WorkingDirectory = $appDir
$link.WindowStyle = 7
$link.Description = 'Warn and move new non-shortcut Desktop items'
$link.Save()

# Start the new monitor now.
Start-Process -FilePath $psExe -ArgumentList @(
    '-NoProfile',
    '-STA',
    '-ExecutionPolicy', 'Bypass',
    '-WindowStyle', 'Hidden',
    '-File', "`"$monitorPath`""
) -WindowStyle Hidden

# Mirror the monitor's own default holding-folder logic for the summary.
$defaultHolding = Join-Path ([Environment]::GetFolderPath('MyDocuments')) 'Desktop-BlockedItems'
if ([IO.Path]::GetFullPath($defaultHolding).TrimEnd('\') -eq
    [IO.Path]::GetFullPath([Environment]::GetFolderPath('Desktop')).TrimEnd('\')) {
    $defaultHolding = Join-Path $env:LOCALAPPDATA 'DesktopClickGuard\BlockedItems'
}

Write-Host ''
Write-Host 'Upgrade completed.' -ForegroundColor Green
Write-Host "Monitor script  : $monitorPath"
Write-Host "Startup shortcut: $shortcutPath"
Write-Host "Default holding folder (when Cancel is selected): $defaultHolding"
if ($backup) {
    Write-Host "Backup          : $backup"
}
Write-Host ''
Write-Host 'Note: this detects and then moves new items; it is not a hard file-system block.'

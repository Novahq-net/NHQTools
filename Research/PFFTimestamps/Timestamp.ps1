# Look for any *.pff in the current folder
# Applies timestamps based on update.dmp
# update.dmp Format: ENTRY=FILENAME,TIMESTAMP,SIZE

$PackExe = "pack.exe"
$PackCmd = Join-Path $PSScriptRoot "pack.exe"
$PffFile = Get-ChildItem -File -Filter *.pff -ErrorAction SilentlyContinue
$DumpDir = ".\dump"
$UpdateDmp = ".\update.dmp"

if (!(Test-Path $PackCmd)) {
    Write-Error "$($PackExe) not found."
    exit 1
}

if (!(Test-Path $PffFile)) {
    Write-Error "No pff file found in current directory"
    exit 1
}

if (!(Test-Path $DumpDir)) {
    New-Item -ItemType Directory -Path $DumpDir | Out-Null
    Write-Host "Created dump directory"
}

if (!(Test-Path $DumpDir)) {
    Write-Error "$($DumpDir) not found."
    exit 1
}

Write-Host "Executing $($PackExe) $($PffFile.Name) /DUMP ..."

Push-Location $PSScriptRoot
& $PackCmd $PffFile.Name "/DUMP"
Pop-Location
  
if (!(Test-Path $UpdateDmp)) {
    Write-Error "update.dmp not found."
    exit 1
}

$lines = Get-Content $UpdateDmp

foreach ($line in $lines) {

    if (-not $line.Trim() -or $line.Trim().StartsWith('[')) {
        continue
    }

    # Parse: ENTRY=NAME,TIMESTAMP,SIZE
    if ($line -notmatch '^ENTRY=(?<name>[^,]+),(?<timestamp>\d+),(?<size>\d+)$') {
        Write-Warning "Skipping invalid line: $line"
        continue
    }

    $name       = $matches['name']
    $timestamp  = [int64]$matches['timestamp']
    $sizeExpect = [int64]$matches['size']

    $filePath = Join-Path $DumpDir $name

    if (!(Test-Path $filePath)) {
        Write-Warning "File not found: $filePath"
        continue
    }

    $file = Get-Item $filePath

    # Convert Unix epoch to UTC DateTime
    $dt = [DateTimeOffset]::FromUnixTimeSeconds($timestamp).UtcDateTime

    try {
        $file.CreationTimeUtc   = $dt
        $file.LastWriteTimeUtc  = $dt
        $file.LastAccessTimeUtc = $dt

        Write-Host "Updated $name to $dt (UTC)"
    }
    catch {
        Write-Warning "Failed to update ${name}: $($_.Exception.Message)"
    }
}

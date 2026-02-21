Push-Location $PSScriptRoot

$RootPath = "."

$TargetDate = Get-Date -Date "2000-01-01 12:00:00"

$Directories = Get-ChildItem -Path $RootPath -Directory | Where-Object { $_.Name -match "^PFF\d-\d+$" }

if ($Directories.Count -eq 0) {
    Write-Host "No directories starting with 'PFF' found in $RootPath." -ForegroundColor Yellow
    exit
}

foreach ($Dir in $Directories) {
    Write-Host "Directory: $($Dir.Name)" -ForegroundColor Cyan

    $Files = Get-ChildItem -Path $Dir.FullName -File

    foreach ($File in $Files) {
        try {

            $File.CreationTime = $TargetDate
            $File.LastWriteTime = $TargetDate

            Write-Host " - Updated: $($File.Name)" -ForegroundColor Gray
        }
        catch {
            Write-Error "Failed to update $($File.Name). Error: $_"
        }
    }
}

Write-Host ""
Write-Host "Done..." -ForegroundColor Green
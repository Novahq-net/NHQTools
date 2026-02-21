Push-Location $PSScriptRoot

$RootPath = "."

$Pack = ".\pack.exe"

if (-not (Test-Path $Pack)) {
    Write-Error "Could not find $Pack in the current directory."
    exit
}

Write-Host "Select Packing Mode:" -ForegroundColor Yellow
Write-Host "[P] PlainText"
Write-Host "[E] Encoded"

do {
    $Selection = Read-Host "Please enter 'P' or 'E'"
    $Selection = $Selection.ToUpper()
} until ($Selection -eq "P" -or $Selection -eq "E")

if ($Selection -eq "E") {
    $Suffix = "-Encoded"
    $IsEncoded = $true
}
else {
    $Suffix = "-PlainText"
    $IsEncoded = $false
}

$Directories = Get-ChildItem -Path $RootPath -Directory | Where-Object { $_.Name -match "^PFF\d-\d+$" }

if ($Directories.Count -eq 0) {
    Write-Host "No directories matching the pattern PFF[n]-[n] found in $RootPath." -ForegroundColor Yellow
    exit
}

foreach ($Dir in $Directories) {
    Write-Host "Directory: $($Dir.Name)" -ForegroundColor Cyan
    
    $PffName = "$($Dir.Name)$Suffix.pff"

    $Files = Get-ChildItem -Path $Dir.FullName -File | Sort-Object `
        @{Expression={if ($_.Name.ToLower().StartsWith("_anchor")) {0} else {1}}}, `
        @{Expression={$_.Name}}

    foreach ($File in $Files) {
        $Arguments = @($PffName, $File.FullName)

        if ($IsEncoded) {
            $Arguments += "/ENCODE"
        }

        Write-Host "  Packing: $($File.Name) -> $PffName" -ForegroundColor Gray

        & $Pack $Arguments

    }
}

Write-Host ""
Write-Host "Done..." -ForegroundColor Green
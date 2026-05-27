param(
    [string]$RimWorldModsPath = "${env:ProgramFiles(x86)}\Steam\steamapps\common\RimWorld\Mods",
    [string]$ModFolderName = "WalkTheWorld",
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$solutionPath = Join-Path $repoRoot "CellByCell.sln"
$targetRoot = Join-Path $RimWorldModsPath $ModFolderName

dotnet build $solutionPath -c $Configuration

New-Item -ItemType Directory -Path $targetRoot -Force | Out-Null

foreach ($folder in @("About", "Defs", "Languages", "Textures", "1.6")) {
    $target = Join-Path $targetRoot $folder
    if (Test-Path $target) {
        Remove-Item -Path $target -Recurse -Force
    }

    $source = Join-Path $repoRoot $folder
    if (Test-Path $source) {
        Copy-Item -Path $source -Destination $targetRoot -Recurse -Force
    }
}

Write-Host "Walk the World deployed to: $targetRoot"
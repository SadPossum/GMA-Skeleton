[CmdletBinding()]
param()

. (Join-Path $PSScriptRoot 'common.ps1')

$frameworkSync = Join-GmaPath 'gma\framework\eng\sync-solution.ps1'
if (-not (Test-Path -LiteralPath $frameworkSync -PathType Leaf)) {
    throw 'GMA Framework solution tooling is not mounted. Run eng/gma-update.ps1 -Init first.'
}

$sourceRoots = [System.Collections.Generic.List[string]]::new()
$sourceRoots.Add((Join-GmaPath 'gma\framework'))
$sourceRoots.Add((Join-GmaPath 'gma\extensions'))

$modulesRoot = Join-GmaPath 'gma\modules'
if (-not (Test-Path -LiteralPath $modulesRoot -PathType Container)) {
    throw "GMA modules root is not mounted: '$modulesRoot'."
}

foreach ($moduleRoot in Get-ChildItem -LiteralPath $modulesRoot -Directory |
    Sort-Object -Property Name) {
    $sourceRoots.Add($moduleRoot.FullName)
}

foreach ($sourceRoot in $sourceRoots) {
    if (-not (Test-Path -LiteralPath $sourceRoot -PathType Container)) {
        throw "GMA source repository is not mounted: '$sourceRoot'."
    }

    $solutions = @(Get-ChildItem -LiteralPath $sourceRoot -Filter '*.slnx' -File)
    if ($solutions.Count -ne 1) {
        throw "Expected exactly one root .slnx in '$sourceRoot', found $($solutions.Count)."
    }

    & $frameworkSync `
        -RepositoryRoot $sourceRoot `
        -Solution $solutions[0].Name `
        -Check
}

Write-Host "All $($sourceRoots.Count) mounted GMA source solutions are synchronized."

param([string] $Branch = 'dev')

. (Join-Path $PSScriptRoot 'common.ps1')

$repositoryRoot = Get-GmaRepositoryRoot
$implementation = Join-GmaPath 'gma/framework/eng/check-submodule-heads.ps1'
if (-not (Test-Path -LiteralPath $implementation -PathType Leaf)) {
    throw 'GMA framework tooling is not mounted. Run eng/gma-update.ps1 -Init first.'
}

& $implementation -RepositoryRoot $repositoryRoot -ExpectedBranch $Branch

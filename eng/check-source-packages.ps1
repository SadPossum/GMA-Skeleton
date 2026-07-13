param(
    [switch] $SkipRestore,
    [switch] $SkipBuild
)

. (Join-Path $PSScriptRoot 'common.ps1')

$repositoryRoot = Get-GmaRepositoryRoot
$implementation = Join-GmaPath 'gma/framework/eng/check-source-packages.ps1'
if (-not (Test-Path -LiteralPath $implementation -PathType Leaf)) {
    throw 'GMA framework tooling is not mounted. Run eng/gma-update.ps1 -Init first.'
}

& $implementation @PSBoundParameters `
    -RepositoryRoot $repositoryRoot `
    -PathPrefix @('gma/framework', 'gma/extensions', 'gma/modules/')

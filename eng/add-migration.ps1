param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[A-Za-z][A-Za-z0-9_.-]*$')]
    [string] $Module,

    [Parameter(Mandatory = $true)]
    [ValidateSet('SqlServer', 'PostgreSql')]
    [string] $Provider,

    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[A-Za-z][A-Za-z0-9_]*$')]
    [string] $Name,

    [string] $Connection,

    [ValidatePattern('^[A-Za-z][A-Za-z0-9_]*DbContext$')]
    [string] $Context
)

. (Join-Path $PSScriptRoot 'common.ps1')

$repositoryRoot = Get-GmaRepositoryRoot
$implementation = Join-GmaPath 'gma/framework/eng/add-migration.ps1'
if (-not (Test-Path -LiteralPath $implementation -PathType Leaf)) {
    throw 'GMA framework tooling is not mounted. Run eng/gma-update.ps1 -Init first.'
}

& $implementation @PSBoundParameters -RepositoryRoot $repositoryRoot

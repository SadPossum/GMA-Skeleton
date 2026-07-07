param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[A-Z][A-Za-z0-9]*$')]
    [string] $Name,

    [switch] $Persistence,
    [switch] $SqlServerMigrations,
    [switch] $PostgreSqlMigrations,
    [switch] $AdminCli,
    [switch] $AdminApi,
    [switch] $Inbox,
    [switch] $Outbox,
    [switch] $Cache,
    [switch] $RegisterInHost
)

. (Join-Path $PSScriptRoot 'common.ps1')

$repositoryRoot = Get-GmaRepositoryRoot
$implementation = Join-Path $repositoryRoot 'src\Framework\eng\new-module.ps1'

& $implementation @PSBoundParameters -RepositoryRoot $repositoryRoot -CompositionSolution 'GenericModularApi.slnx'
exit $LASTEXITCODE

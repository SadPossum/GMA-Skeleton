param(
    [switch] $Init,
    [switch] $Remote
)

. (Join-Path $PSScriptRoot 'common.ps1')

$gitmodules = Join-GmaPath '.gitmodules'
if (-not (Test-Path -LiteralPath $gitmodules)) {
    Write-Host 'No .gitmodules file found. Nothing to update in this monorepo checkout.'
    return
}

$arguments = @('submodule', 'update', '--recursive')
if ($Init) {
    $arguments += '--init'
}

if ($Remote) {
    $arguments += '--remote'
}

git -C (Get-GmaRepositoryRoot) @arguments
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

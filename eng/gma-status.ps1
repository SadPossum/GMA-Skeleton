. (Join-Path $PSScriptRoot 'common.ps1')

Write-Host "Repository: $(Get-GmaRepositoryRoot)"

$sourceRoots = Join-GmaPath 'Gma.SourceRoots.props'
if (Test-Path -LiteralPath $sourceRoots) {
    Write-Host "Source roots: configured by Gma.SourceRoots.props"
}
else {
    Write-Host "Source roots: using checked-in defaults"
}

Write-Host ''
Write-Host 'Git status:'
git -C (Get-GmaRepositoryRoot) status --short --branch
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

$gitmodules = Join-GmaPath '.gitmodules'
if (Test-Path -LiteralPath $gitmodules) {
    Write-Host ''
    Write-Host 'Submodules:'
    git -C (Get-GmaRepositoryRoot) submodule status --recursive
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}
else {
    Write-Host ''
    Write-Host 'Submodules: none configured in this checkout.'
}

. (Join-Path $PSScriptRoot 'common.ps1')

Write-Host "Repository: $(Get-GmaRepositoryRoot)"

$sourceRoots = Join-GmaPath 'Gma.SourceRoots.props'
if (Test-Path -LiteralPath $sourceRoots) {
    Write-Host "Source roots: configured by Gma.SourceRoots.props"
}
else {
    Write-Host "Source roots: using checked-in defaults"
}

$gmaSourceRootChecks = @(
    @{ Name = 'framework'; Path = 'gma\framework\Gma.SourceRoots.props' },
    @{ Name = 'administration'; Path = 'gma\modules\administration\Gma.SourceRoots.props' },
    @{ Name = 'auth'; Path = 'gma\modules\auth\Gma.SourceRoots.props' },
    @{ Name = 'files'; Path = 'gma\modules\files\Gma.SourceRoots.props' },
    @{ Name = 'notifications'; Path = 'gma\modules\notifications\Gma.SourceRoots.props' },
    @{ Name = 'task-runtime'; Path = 'gma\modules\task-runtime\Gma.SourceRoots.props' },
    @{ Name = 'tenancy'; Path = 'gma\modules\tenancy\Gma.SourceRoots.props' }
)

if (Test-Path -LiteralPath (Join-GmaPath 'gma') -PathType Container) {
    Write-Host ''
    Write-Host 'GMA source package roots:'
    foreach ($check in $gmaSourceRootChecks) {
        $sourceRootPath = Join-GmaPath $check.Path
        $checkoutPath = Split-Path -Parent $sourceRootPath
        if (-not (Test-Path -LiteralPath $checkoutPath -PathType Container)) {
            Write-Host "- $($check.Name): checkout missing"
        }
        elseif (Test-Path -LiteralPath $sourceRootPath -PathType Leaf) {
            Write-Host "- $($check.Name): source roots configured"
        }
        else {
            Write-Host "- $($check.Name): source roots missing; run eng/gma-bootstrap.ps1 -SourceLayout GmaSubmodules"
        }
    }
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

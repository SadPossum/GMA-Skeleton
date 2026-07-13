param(
    [switch] $SkipRestore,
    [switch] $SkipBuild,
    [switch] $SkipMigrationCheck
)

. (Join-Path $PSScriptRoot 'common.ps1')

& (Join-Path $PSScriptRoot 'sync-solution.ps1') -Check

if (-not $SkipRestore) {
    & (Join-Path $PSScriptRoot 'restore.ps1')

    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}

if (-not $SkipBuild) {
    & (Join-Path $PSScriptRoot 'build.ps1') -NoRestore

    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}

if (-not $SkipMigrationCheck) {
    & (Join-Path $PSScriptRoot 'check-migrations.ps1') -NoBuild

    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}

& (Join-Path $PSScriptRoot 'test-fast.ps1') -NoBuild

if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

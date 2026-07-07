param(
    [switch] $SkipRestore,
    [switch] $SkipBuild,
    [switch] $SkipTests,
    [switch] $FocusedSolutions
)

. (Join-Path $PSScriptRoot 'common.ps1')

$solutions = @('GenericModularApi.slnx')

if ($FocusedSolutions) {
    $solutions += @(
        'Gma.Framework.slnx',
        'Gma.Modules.Administration.slnx',
        'Gma.Modules.Auth.slnx',
        'Gma.Modules.Catalog.slnx',
        'Gma.Modules.Files.slnx',
        'Gma.Modules.Notifications.slnx',
        'Gma.Modules.Ordering.slnx',
        'Gma.Modules.TaskSamples.slnx',
        'Gma.Modules.TaskRuntime.slnx',
        'Gma.Modules.Tenancy.slnx'
    )
}

foreach ($solution in $solutions) {
    $solutionPath = Join-GmaPath $solution
    if (-not (Test-Path -LiteralPath $solutionPath)) {
        throw "Missing solution '$solutionPath'."
    }

    if (-not $SkipRestore) {
        Invoke-GmaDotNet -Arguments @('restore', $solutionPath)
    }

    if (-not $SkipBuild) {
        $buildArguments = @('build', $solutionPath, '--no-restore', '-m:1')
        Invoke-GmaDotNet -Arguments $buildArguments
    }

    if (-not $SkipTests) {
        $testArguments = @('test', $solutionPath, '--no-build', '--logger', 'console;verbosity=minimal')
        Invoke-GmaDotNet -Arguments $testArguments
    }
}

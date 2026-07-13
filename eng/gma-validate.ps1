param(
    [switch] $SkipRestore,
    [switch] $SkipBuild,
    [switch] $SkipTests,
    [switch] $FocusedSolutions
)

. (Join-Path $PSScriptRoot 'common.ps1')

$solutions = @('GMA-Skeleton.slnx')

if ($FocusedSolutions) {
    $solutions += @(
        'gma\framework\Gma.Framework.slnx',
        'gma\extensions\Gma.Extensions.slnx',
        'gma\modules\access-control\Gma.Modules.AccessControl.slnx',
        'gma\modules\administration\Gma.Modules.Administration.slnx',
        'gma\modules\auth\Gma.Modules.Auth.slnx',
        'gma\modules\files\Gma.Modules.Files.slnx',
        'gma\modules\notifications\Gma.Modules.Notifications.slnx',
        'gma\modules\task-runtime\Gma.Modules.TaskRuntime.slnx',
        'gma\modules\tenancy\Gma.Modules.Tenancy.slnx'
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

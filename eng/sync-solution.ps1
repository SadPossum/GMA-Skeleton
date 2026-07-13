param([switch] $Check)

. (Join-Path $PSScriptRoot 'common.ps1')

$repositoryRoot = Get-GmaRepositoryRoot
$implementation = Join-GmaPath 'gma/framework/eng/sync-solution.ps1'
if (-not (Test-Path -LiteralPath $implementation -PathType Leaf)) {
    throw 'GMA framework tooling is not mounted. Run eng/gma-update.ps1 -Init first.'
}

$arguments = @{
    RepositoryRoot = $repositoryRoot
    Solution = 'GMA-Skeleton.slnx'
    SolutionItems = @(
        '.config/dotnet-tools.json',
        '.editorconfig',
        '.gitattributes',
        '.gitignore',
        '.gitmodules',
        '.github/dependabot.yml',
        'Directory.Build.props',
        'Directory.Packages.props',
        'global.json',
        'gma/README.md',
        'gma/modules/README.md',
        'Gma.SourceRoots.props.example',
        'LICENSE',
        'nuget.config',
        'README.md'
    )
}
if ($Check) {
    $arguments.Check = $true
}

& $implementation @arguments

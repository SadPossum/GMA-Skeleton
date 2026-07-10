param(
    [switch] $NoBuild
)

. (Join-Path $PSScriptRoot 'common.ps1')

Invoke-GmaDotNet -Arguments @('tool', 'restore')

$repositoryRoot = Get-GmaRepositoryRoot
$migrationSearchRoots = @(
    'src\Modules',
    'gma\modules'
) |
    ForEach-Object { Join-GmaPath $_ } |
    Where-Object { Test-Path -LiteralPath $_ -PathType Container }

$migrationProjects = $migrationSearchRoots |
    ForEach-Object {
        Get-ChildItem -LiteralPath $_ -Recurse -Filter *.csproj -File
    } |
    Where-Object {
        $_.BaseName.EndsWith('.Persistence.SqlServerMigrations', [System.StringComparison]::Ordinal) -or
        $_.BaseName.EndsWith('.Persistence.PostgreSqlMigrations', [System.StringComparison]::Ordinal)
    } |
    Sort-Object FullName

if ($migrationProjects.Count -eq 0) {
    throw 'No provider migration projects were found under src\Modules or gma\modules.'
}

foreach ($project in $migrationProjects) {
    $relativeProject = $project.FullName.Substring($repositoryRoot.Length).TrimStart('\', '/')
    Write-Host "Checking migration drift for $relativeProject"

    if (-not $NoBuild) {
        Invoke-GmaDotNet -Arguments @('build', $project.FullName, '--no-restore')
    }

    $arguments = @(
        'tool',
        'run',
        'dotnet-ef',
        'migrations',
        'has-pending-model-changes',
        '--project',
        $project.FullName,
        '--startup-project',
        $project.FullName
    )

    if ($NoBuild) {
        $arguments += '--no-build'
    }

    Invoke-GmaDotNet -Arguments $arguments
}

Write-Host 'Migration drift checks passed.'

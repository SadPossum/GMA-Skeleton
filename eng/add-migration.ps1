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

function Assert-GmaPathExists {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Path,

        [Parameter(Mandatory = $true)]
        [string] $Description
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        throw "$Description was not found at '$Path'."
    }
}

function Resolve-GmaDbContextName {
    param(
        [Parameter(Mandatory = $true)]
        [string] $PersistenceRoot,

        [Parameter(Mandatory = $true)]
        [string] $ModuleName,

        [string] $RequestedContext
    )

    if (-not [string]::IsNullOrWhiteSpace($RequestedContext)) {
        return $RequestedContext
    }

    $contextNames = @()
    $sourceFiles = Get-ChildItem -LiteralPath $PersistenceRoot -Filter *.cs -Recurse -File |
        Where-Object { $_.FullName -notmatch '\\Migrations\\' }

    foreach ($sourceFile in $sourceFiles) {
        $source = Get-Content -LiteralPath $sourceFile.FullName -Raw
        $matches = [regex]::Matches($source, '\bclass\s+(?<name>[A-Za-z][A-Za-z0-9_]*DbContext)\b')

        foreach ($match in $matches) {
            $contextNames += $match.Groups['name'].Value
        }
    }

    $contextNames = @($contextNames | Sort-Object -Unique)

    if ($contextNames.Count -eq 1) {
        return $contextNames[0]
    }

    $conventionalName = "${ModuleName}DbContext"
    if ($contextNames -contains $conventionalName) {
        return $conventionalName
    }

    throw "Could not determine DbContext for module '$ModuleName'. Found: $($contextNames -join ', '). Pass -Context explicitly."
}

function ConvertTo-GmaModulePascalName {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Name
    )

    $normalizedName = $Name
    if ($normalizedName.StartsWith('Gma.Modules.', [System.StringComparison]::OrdinalIgnoreCase)) {
        $normalizedName = $normalizedName.Substring('Gma.Modules.'.Length)
    }

    if ($normalizedName.Contains('-') -or $normalizedName.Contains('_') -or $normalizedName.Contains('.')) {
        return (($normalizedName -split '[-_.]') |
            Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
            ForEach-Object {
                $part = $_.ToLowerInvariant()
                $part.Substring(0, 1).ToUpperInvariant() + $part.Substring(1)
            }) -join ''
    }

    return $normalizedName
}

function ConvertTo-GmaModuleAlias {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Name
    )

    $pascalName = ConvertTo-GmaModulePascalName -Name $Name
    $withWordBoundaries = [regex]::Replace($pascalName, '([A-Z]+)([A-Z][a-z])', '$1-$2')
    $withNumberBoundaries = [regex]::Replace($withWordBoundaries, '([a-z0-9])([A-Z])', '$1-$2')
    return $withNumberBoundaries.ToLowerInvariant()
}

function Resolve-GmaMigrationTarget {
    param(
        [Parameter(Mandatory = $true)]
        [string] $ModuleName,

        [Parameter(Mandatory = $true)]
        [string] $ProviderName
    )

    $pascalName = ConvertTo-GmaModulePascalName -Name $ModuleName
    $moduleAlias = ConvertTo-GmaModuleAlias -Name $ModuleName

    $candidates = @(
        [pscustomobject] @{
            ModuleRoot = Join-GmaPath "src\Modules\$pascalName"
            SourceRoot = Join-GmaPath "src\Modules\$pascalName"
            ProjectPrefix = $pascalName
            ContextModuleName = $pascalName
        },
        [pscustomobject] @{
            ModuleRoot = Join-GmaPath "gma\modules\$moduleAlias"
            SourceRoot = Join-Path (Join-GmaPath "gma\modules\$moduleAlias") 'src'
            ProjectPrefix = "Gma.Modules.$pascalName"
            ContextModuleName = $pascalName
        }
    )

    $matches = @()
    foreach ($candidate in $candidates) {
        $persistenceRoot = Join-Path $candidate.SourceRoot "$($candidate.ProjectPrefix).Persistence"
        $migrationRoot = Join-Path $candidate.SourceRoot "$($candidate.ProjectPrefix).Persistence.${ProviderName}Migrations"
        $persistenceProject = Join-Path $persistenceRoot "$($candidate.ProjectPrefix).Persistence.csproj"
        $migrationProject = Join-Path $migrationRoot "$($candidate.ProjectPrefix).Persistence.${ProviderName}Migrations.csproj"

        if ((Test-Path -LiteralPath $persistenceProject -PathType Leaf) -and
            (Test-Path -LiteralPath $migrationProject -PathType Leaf)) {
            $matches += [pscustomobject] @{
                ModuleRoot = $candidate.ModuleRoot
                PersistenceRoot = $persistenceRoot
                MigrationRoot = $migrationRoot
                PersistenceProject = $persistenceProject
                MigrationProject = $migrationProject
                ContextModuleName = $candidate.ContextModuleName
            }
        }
    }

    if ($matches.Count -eq 1) {
        return $matches[0]
    }

    if ($matches.Count -gt 1) {
        throw "Module '$ModuleName' matched multiple migration targets. Use a more specific module name."
    }

    throw "Module '$ModuleName' with $ProviderName migrations was not found under 'src\Modules\$pascalName' or 'gma\modules\$moduleAlias\src'."
}

Invoke-GmaDotNet -Arguments @('tool', 'restore')

$target = Resolve-GmaMigrationTarget -ModuleName $Module -ProviderName $Provider
$moduleRoot = $target.ModuleRoot
$persistenceRoot = $target.PersistenceRoot
$persistenceProject = $target.PersistenceProject
$migrationProject = $target.MigrationProject
$startupProject = $migrationProject

Assert-GmaPathExists -Path $moduleRoot -Description "Module '$Module'"
Assert-GmaPathExists -Path $persistenceProject -Description "Persistence project for module '$Module'"
Assert-GmaPathExists -Path $migrationProject -Description "$Provider migration project for module '$Module'"

$contextName = Resolve-GmaDbContextName -PersistenceRoot $persistenceRoot -ModuleName $target.ContextModuleName -RequestedContext $Context

Invoke-GmaDotNet -Arguments @('build', $migrationProject, '--no-restore')

$arguments = @(
    'ef',
    'migrations',
    'add',
    $Name,
    '--no-build',
    '--project',
    $migrationProject,
    '--startup-project',
    $startupProject,
    '--context',
    $contextName,
    '--output-dir',
    'Migrations',
    '--',
    '--provider',
    $Provider
)

if (-not [string]::IsNullOrWhiteSpace($Connection)) {
    $arguments += @('--connection', $Connection)
}

Invoke-GmaDotNet -Arguments $arguments

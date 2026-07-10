[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[A-Z][A-Za-z0-9]*$')]
    [string] $Name,

    [Parameter(Mandatory = $true)]
    [string] $OutputPath,

    [switch] $Force,

    [string[]] $Modules = @()
)

. (Join-Path $PSScriptRoot 'common.ps1')

$script:GmaKnownModuleSpecs = @(
    [pscustomobject] @{
        Alias = 'access-control'
        Repository = 'GMA-Module-Access-Control'
        SourceRootProperty = 'GmaModuleAccessControlRoot'
        PublicApiProject = $null
        PublicApiNamespace = $null
        PublicApiModuleType = $null
        MigrationProjectPrefix = 'Gma.Modules.AccessControl.Persistence'
        DbContextType = $null
    },
    [pscustomobject] @{
        Alias = 'administration'
        Repository = 'GMA-Module-Administration'
        SourceRootProperty = 'GmaModuleAdministrationRoot'
        PublicApiProject = $null
        PublicApiNamespace = $null
        PublicApiModuleType = $null
        MigrationProjectPrefix = 'Gma.Modules.Administration.Persistence'
        DbContextType = $null
    },
    [pscustomobject] @{
        Alias = 'auth'
        Repository = 'GMA-Module-Auth'
        SourceRootProperty = 'GmaModuleAuthRoot'
        PublicApiProject = 'Gma.Modules.Auth.Api'
        PublicApiNamespace = 'Gma.Modules.Auth.Api'
        PublicApiModuleType = 'AuthModule'
        MigrationProjectPrefix = 'Gma.Modules.Auth.Persistence'
        DbContextType = 'Gma.Modules.Auth.Persistence.AuthDbContext'
    },
    [pscustomobject] @{
        Alias = 'files'
        Repository = 'GMA-Module-Files'
        SourceRootProperty = 'GmaModuleFilesRoot'
        PublicApiProject = 'Gma.Modules.Files.Api'
        PublicApiNamespace = 'Gma.Modules.Files.Api'
        PublicApiModuleType = 'FilesModule'
        MigrationProjectPrefix = $null
        DbContextType = $null
    },
    [pscustomobject] @{
        Alias = 'notifications'
        Repository = 'GMA-Module-Notifications'
        SourceRootProperty = 'GmaModuleNotificationsRoot'
        PublicApiProject = 'Gma.Modules.Notifications.Api'
        PublicApiNamespace = 'Gma.Modules.Notifications.Api'
        PublicApiModuleType = 'NotificationsModule'
        MigrationProjectPrefix = 'Gma.Modules.Notifications.Persistence'
        DbContextType = 'Gma.Modules.Notifications.Persistence.NotificationsDbContext'
    },
    [pscustomobject] @{
        Alias = 'task-runtime'
        Repository = 'GMA-Module-Task-Runtime'
        SourceRootProperty = 'GmaModuleTaskRuntimeRoot'
        PublicApiProject = $null
        PublicApiNamespace = $null
        PublicApiModuleType = $null
        MigrationProjectPrefix = 'Gma.Modules.TaskRuntime.Persistence'
        DbContextType = $null
    },
    [pscustomobject] @{
        Alias = 'tenancy'
        Repository = 'GMA-Module-Tenancy'
        SourceRootProperty = 'GmaModuleTenancyRoot'
        PublicApiProject = 'Gma.Modules.Tenancy.Api'
        PublicApiNamespace = 'Gma.Modules.Tenancy.Api'
        PublicApiModuleType = 'TenancyModule'
        MigrationProjectPrefix = $null
        DbContextType = $null
    }
)

function Resolve-GmaTemplatePath {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Path
    )

    if ([System.IO.Path]::IsPathRooted($Path)) {
        return [System.IO.Path]::GetFullPath($Path)
    }

    return [System.IO.Path]::GetFullPath((Join-GmaPath $Path))
}

function ConvertTo-GmaKebabCase {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Value
    )

    $withAcronymBoundaries = [regex]::Replace($Value, '([A-Z]+)([A-Z][a-z])', '$1-$2')
    $withWordBoundaries = [regex]::Replace($withAcronymBoundaries, '([a-z0-9])([A-Z])', '$1-$2')
    return $withWordBoundaries.ToLowerInvariant()
}

function Get-GmaSelectedModuleSpecs {
    param([string[]] $ModuleAliases)

    $moduleAliasArray = @($ModuleAliases |
        ForEach-Object { $_ -split ',' } |
        ForEach-Object { $_.Trim().ToLowerInvariant() } |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    if ($moduleAliasArray.Count -eq 0) {
        return @()
    }

    $normalizedAliases = @($moduleAliasArray | Select-Object -Unique)
    $knownAliases = @('all') + @($script:GmaKnownModuleSpecs | ForEach-Object { $_.Alias })
    $unknownAliases = @($normalizedAliases | Where-Object { $knownAliases -notcontains $_ })
    if ($unknownAliases.Count -gt 0) {
        throw "Unknown GMA module alias '$($unknownAliases -join "', '")'. Allowed values: $($knownAliases -join ', ')."
    }

    if ($normalizedAliases -contains 'all') {
        return @($script:GmaKnownModuleSpecs)
    }

    return @($script:GmaKnownModuleSpecs | Where-Object { $normalizedAliases -contains $_.Alias })
}

function Write-GmaTemplateFile {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Path,

        [AllowEmptyString()]
        [Parameter(Mandatory = $true)]
        [string[]] $Lines
    )

    $directory = Split-Path -Parent $Path
    if (-not (Test-Path -LiteralPath $directory -PathType Container)) {
        New-Item -ItemType Directory -Force -Path $directory | Out-Null
    }

    if ((Test-Path -LiteralPath $Path) -and -not $Force) {
        throw "Refusing to overwrite existing file '$Path'. Rerun with -Force to refresh generated files."
    }

    $action = if (Test-Path -LiteralPath $Path) { 'Overwrite' } else { 'Create' }
    if ($PSCmdlet.ShouldProcess($Path, "$action generated app file")) {
        [System.IO.File]::WriteAllLines($Path, $Lines, [System.Text.UTF8Encoding]::new($false))
    }
}

function Write-GmaGeneratedBootstrap {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Root,

        [object[]] $ModuleSpecs = @()
    )

    $selectedModuleSpecArray = @($ModuleSpecs)
    $selectedModuleAliasesLiteral = if ($selectedModuleSpecArray.Count -eq 0) {
        '@()'
    }
    else {
        '@(' + (($selectedModuleSpecArray | ForEach-Object { "'$($_.Alias)'" }) -join ', ') + ')'
    }

    $moduleRootPropertyLines = @($selectedModuleSpecArray | ForEach-Object {
        "    '$($_.Alias)' = '$($_.SourceRootProperty)'"
    })

    Write-GmaTemplateFile (Join-Path $Root 'eng\common.ps1') @(
        'Set-StrictMode -Version Latest',
        '$ErrorActionPreference = ''Stop''',
        '',
        '$script:RepositoryRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot ''..'')).Path',
        '',
        'function Get-GmaRepositoryRoot {',
        '    return $script:RepositoryRoot',
        '}',
        '',
        'function Join-GmaPath {',
        '    param([Parameter(Mandatory = $true)][string] $Path)',
        '    $normalizedPath = $Path.Replace(''\'', [System.IO.Path]::DirectorySeparatorChar)',
        '    $normalizedPath = $normalizedPath.Replace(''/'', [System.IO.Path]::DirectorySeparatorChar)',
        '    return Join-Path $script:RepositoryRoot $normalizedPath',
        '}',
        '',
        'function Resolve-GmaDotNet {',
        '    if (-not [string]::IsNullOrWhiteSpace($env:GMA_DOTNET)) {',
        '        return $env:GMA_DOTNET',
        '    }',
        '',
        '    return ''dotnet''',
        '}',
        '',
        'function Invoke-GmaDotNet {',
        '    param(',
        '        [Parameter(Mandatory = $true)][string[]] $Arguments,',
        '        [string] $WorkingDirectory = $script:RepositoryRoot',
        '    )',
        '',
        '    Push-Location -LiteralPath $WorkingDirectory',
        '    try {',
        '        & (Resolve-GmaDotNet) @Arguments',
        '        if ($LASTEXITCODE -ne 0) {',
        '            throw "dotnet $($Arguments -join '' '') failed with exit code $LASTEXITCODE."',
        '        }',
        '    }',
        '    finally {',
        '        Pop-Location',
        '    }',
        '}'
    )

    $bootstrapLines = @(
        '[CmdletBinding(SupportsShouldProcess = $true)]',
        'param([switch] $Force)',
        '',
        '. (Join-Path $PSScriptRoot ''common.ps1'')',
        '',
        'function Write-GmaSourceRootsFile {',
        '    param(',
        '        [Parameter(Mandatory = $true)][string] $Path,',
        '        [Parameter(Mandatory = $true)][string[]] $Lines,',
        '        [Parameter(Mandatory = $true)][string] $Description',
        '    )',
        '',
        '    if (Test-Path -LiteralPath $Path) {',
        '        if (-not $Force) {',
        '            $existingLines = [System.IO.File]::ReadAllLines($Path)',
        '            if ([string]::Join("`n", $existingLines) -eq [string]::Join("`n", $Lines)) {',
        '                Write-Host "$Description already exists. Use -Force to refresh it: $Path"',
        '                return',
        '            }',
        '',
        '            throw "$Description already exists with different contents. Use -Force to refresh it: $Path"',
        '        }',
        '    }',
        '',
        '    $directory = Split-Path -Parent $Path',
        '    if (-not (Test-Path -LiteralPath $directory -PathType Container)) {',
        '        throw "Cannot write $Description because ''$directory'' does not exist. Initialize submodules or local source mounts first."',
        '    }',
        '',
        '    $action = if (Test-Path -LiteralPath $Path) { ''Overwrite'' } else { ''Create'' }',
        '    if ($PSCmdlet.ShouldProcess($Path, "$action $Description")) {',
        '        [System.IO.File]::WriteAllLines($Path, $Lines, [System.Text.UTF8Encoding]::new($false))',
        '        Write-Host "$action ${Description}: $Path"',
        '    }',
        '}',
        '',
        "`$selectedModuleAliases = $selectedModuleAliasesLiteral",
        '$moduleRootProperties = @{'
    )

    $bootstrapLines += $moduleRootPropertyLines
    $bootstrapLines += @(
        '}',
        '',
        '$rootLinesBuilder = New-Object ''System.Collections.Generic.List[string]''',
        '$rootLinesBuilder.Add(''<Project>'')',
        '$rootLinesBuilder.Add(''  <PropertyGroup>'')',
        '$rootLinesBuilder.Add(''    <GmaFrameworkRoot>$(MSBuildThisFileDirectory)gma\framework\src\</GmaFrameworkRoot>'')',
        '$rootLinesBuilder.Add(''    <GmaModulesRoot>$(MSBuildThisFileDirectory)gma\modules\</GmaModulesRoot>'')',
        'foreach ($moduleAlias in $selectedModuleAliases) {',
        '    $propertyName = $moduleRootProperties[$moduleAlias]',
        '    $rootLinesBuilder.Add("    <$propertyName>`$(GmaModulesRoot)$moduleAlias\src\</$propertyName>")',
        '}',
        '$rootLinesBuilder.Add(''  </PropertyGroup>'')',
        '$rootLinesBuilder.Add(''</Project>'')',
        '$rootLines = $rootLinesBuilder.ToArray()',
        '',
        '$frameworkLines = @(',
        '    ''<Project>'',',
        '    ''  <PropertyGroup>'',',
        '    ''    <GmaFrameworkRoot>$(MSBuildThisFileDirectory)src\</GmaFrameworkRoot>'',',
        '    ''  </PropertyGroup>'',',
        '    ''</Project>''',
        ')',
        '',
        '$moduleLinesBuilder = New-Object ''System.Collections.Generic.List[string]''',
        '$moduleLinesBuilder.Add(''<Project>'')',
        '$moduleLinesBuilder.Add(''  <PropertyGroup>'')',
        '$moduleLinesBuilder.Add(''    <GmaFrameworkRoot>$(MSBuildThisFileDirectory)..\..\framework\src\</GmaFrameworkRoot>'')',
        '$moduleLinesBuilder.Add(''    <GmaModulesRoot>$(MSBuildThisFileDirectory)..\</GmaModulesRoot>'')',
        'foreach ($moduleAlias in $selectedModuleAliases) {',
        '    $propertyName = $moduleRootProperties[$moduleAlias]',
        '    $moduleLinesBuilder.Add("    <$propertyName>`$(GmaModulesRoot)$moduleAlias\src\</$propertyName>")',
        '}',
        '$moduleLinesBuilder.Add(''  </PropertyGroup>'')',
        '$moduleLinesBuilder.Add(''</Project>'')',
        '$moduleLines = $moduleLinesBuilder.ToArray()',
        '',
        'Write-GmaSourceRootsFile -Path (Join-GmaPath ''Gma.SourceRoots.props'') -Lines $rootLines -Description ''root source-root configuration''',
        'Write-GmaSourceRootsFile -Path (Join-GmaPath ''gma/framework/Gma.SourceRoots.props'') -Lines $frameworkLines -Description ''framework source-root configuration''',
        '',
        'foreach ($moduleAlias in $selectedModuleAliases) {',
        '    $moduleRoot = Join-GmaPath "gma/modules/$moduleAlias"',
        '    if (Test-Path -LiteralPath $moduleRoot -PathType Container) {',
        '        Write-GmaSourceRootsFile -Path (Join-Path $moduleRoot ''Gma.SourceRoots.props'') -Lines $moduleLines -Description "$moduleAlias module source-root configuration"',
        '    }',
        '}'
    )

    Write-GmaTemplateFile (Join-Path $Root 'eng\gma-bootstrap.ps1') $bootstrapLines

    Write-GmaTemplateFile (Join-Path $Root 'eng\gma-status.ps1') @(
        '. (Join-Path $PSScriptRoot ''common.ps1'')',
        '',
        'Write-Host "Repository: $(Get-GmaRepositoryRoot)"',
        'if (Test-Path -LiteralPath (Join-GmaPath ''Gma.SourceRoots.props'')) {',
        '    Write-Host ''Source roots: configured''',
        '}',
        'else {',
        '    Write-Host ''Source roots: using checked-in defaults''',
        '}',
        'Write-Host ''''',
        '$repositoryRoot = Get-GmaRepositoryRoot',
        '$gitRoot = git -C $repositoryRoot rev-parse --show-toplevel 2>$null',
        'if ($LASTEXITCODE -eq 0 -and [string]::Equals([System.IO.Path]::GetFullPath($gitRoot).TrimEnd(''\'', ''/''), [System.IO.Path]::GetFullPath($repositoryRoot).TrimEnd(''\'', ''/''), [System.StringComparison]::OrdinalIgnoreCase)) {',
        '    Write-Host ''Git status:''',
        '    git -C $repositoryRoot status --short --branch',
        '    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }',
        '}',
        'else {',
        '    Write-Host ''Git status: app repository not initialized''',
        '}',
        '',
        'if (Test-Path -LiteralPath (Join-GmaPath ''.gitmodules'')) {',
        '    Write-Host ''''',
        '    Write-Host ''Submodules:''',
        '    git -C (Get-GmaRepositoryRoot) submodule status --recursive',
        '    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }',
        '}'
    )

    Write-GmaTemplateFile (Join-Path $Root 'eng\gma-update.ps1') @(
        'param(',
        '    [switch] $Init,',
        '    [switch] $Remote',
        ')',
        '',
        '. (Join-Path $PSScriptRoot ''common.ps1'')',
        '',
        '$gitmodules = Join-GmaPath ''.gitmodules''',
        'if (-not (Test-Path -LiteralPath $gitmodules)) {',
        '    Write-Host ''No .gitmodules file found. Nothing to update until GMA source repositories are added as submodules.''',
        '    return',
        '}',
        '',
        '$arguments = @(''submodule'', ''update'', ''--recursive'')',
        'if ($Init) {',
        '    $arguments += ''--init''',
        '}',
        '',
        'if ($Remote) {',
        '    $arguments += ''--remote''',
        '}',
        '',
        'git -C (Get-GmaRepositoryRoot) @arguments',
        'if ($LASTEXITCODE -ne 0) {',
        '    exit $LASTEXITCODE',
        '}'
    )

    Write-GmaTemplateFile (Join-Path $Root 'eng\gma-validate.ps1') @(
        'param(',
        '    [switch] $SkipRestore,',
        '    [switch] $SkipBuild',
        ')',
        '',
        '. (Join-Path $PSScriptRoot ''common.ps1'')',
        '',
        "`$solutionPath = Join-GmaPath '$Name.slnx'",
        'if (-not $SkipRestore) {',
        '    Invoke-GmaDotNet -Arguments @(''tool'', ''restore'')',
        '    Invoke-GmaDotNet -Arguments @(''restore'', $solutionPath)',
        '}',
        '',
        'if (-not $SkipBuild) {',
        '    Invoke-GmaDotNet -Arguments @(''build'', $solutionPath, ''--no-restore'', ''-m:1'')',
        '}',
        '',
        'Invoke-GmaDotNet -Arguments @(''test'', $solutionPath, ''--no-build'', ''--logger'', ''console;verbosity=minimal'', ''-m:1'', ''-nr:false'')'
    )
}

function Write-GmaGeneratedWorkflow {
    param([Parameter(Mandatory = $true)][string] $Root)

    Write-GmaTemplateFile (Join-Path $Root '.github\workflows\validate.yml') @(
        'name: validate',
        '',
        'on:',
        '  pull_request:',
        '  push:',
        '    branches:',
        '      - main',
        '      - dev',
        '  workflow_dispatch:',
        '',
        'permissions:',
        '  contents: read',
        '',
        'jobs:',
        '  validate:',
        '    strategy:',
        '      matrix:',
        '        os: [windows-latest, ubuntu-latest]',
        '    runs-on: ${{ matrix.os }}',
        '    steps:',
        '      - name: Checkout app',
        '        uses: actions/checkout@9c091bb21b7c1c1d1991bb908d89e4e9dddfe3e0 # v7.0.0',
        '        with:',
        '          fetch-depth: 0',
        '          submodules: recursive',
        '          token: ${{ secrets.GMA_CI_TOKEN || github.token }}',
        '          persist-credentials: false',
        '',
        '      - name: Setup .NET',
        '        uses: actions/setup-dotnet@26b0ec14cb23fa6904739307f278c14f94c95bf1 # v5.4.0',
        '        with:',
        '          dotnet-version: 10.0.x',
        '',
        '      - name: Bootstrap source roots',
        '        shell: pwsh',
        '        run: ./eng/gma-bootstrap.ps1 -Force',
        '',
        '      - name: Restore',
        "        run: dotnet restore $Name.slnx",
        '',
        '      - name: Build',
        "        run: dotnet build $Name.slnx --no-restore -m:1",
        '',
        '      - name: Test',
        "        run: dotnet test $Name.slnx --no-build --logger 'console;verbosity=minimal' -m:1 -nr:false"
    )
}

function Write-GmaGeneratedDeveloperTools {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Root,

        [Parameter(Mandatory = $true)]
        [string] $ApplicationName,

        [object[]] $ModuleSpecs = @()
    )

    Write-GmaTemplateFile (Join-Path $Root 'eng\new-module.ps1') @(
        'param(',
        '    [Parameter(Mandatory = $true)]',
        '    [ValidatePattern(''^[A-Z][A-Za-z0-9]*$'')]',
        '    [string] $Name,',
        '',
        '    [switch] $Persistence,',
        '    [switch] $SqlServerMigrations,',
        '    [switch] $PostgreSqlMigrations,',
        '    [switch] $AdminCli,',
        '    [switch] $AdminApi,',
        '    [switch] $Inbox,',
        '    [switch] $Outbox,',
        '    [switch] $Cache,',
        '    [switch] $RegisterInHost',
        ')',
        '',
        '. (Join-Path $PSScriptRoot ''common.ps1'')',
        '',
        '$repositoryRoot = Get-GmaRepositoryRoot',
        '$implementation = Join-GmaPath ''gma/framework/eng/new-module.ps1''',
        'if (-not (Test-Path -LiteralPath $implementation -PathType Leaf)) {',
        '    throw ''GMA framework tooling is not mounted. Run eng/gma-update.ps1 -Init first.''',
        '}',
        '',
        "& `$implementation @PSBoundParameters -RepositoryRoot `$repositoryRoot -CompositionSolution '$ApplicationName.slnx'",
        'exit $LASTEXITCODE'
    )

    $migrationSpecs = @($ModuleSpecs | Where-Object { -not [string]::IsNullOrWhiteSpace($_.MigrationProjectPrefix) })
    $migrationMapLines = @()
    foreach ($moduleSpec in $migrationSpecs) {
        $projectPattern = "gma\modules\$($moduleSpec.Alias)\src\$($moduleSpec.MigrationProjectPrefix).{0}Migrations\$($moduleSpec.MigrationProjectPrefix).{0}Migrations.csproj"
        $migrationMapLines += "    '$($moduleSpec.Alias)' = '$projectPattern'"
    }

    $migrationLines = @(
        '[CmdletBinding()]',
        'param(',
        '    [Parameter(Mandatory = $true)]',
        '    [ValidateSet(''SqlServer'', ''PostgreSql'')]',
        '    [string] $Provider,',
        '',
        '    [Parameter(Mandatory = $true)]',
        '    [string[]] $Module,',
        '',
        '    [string] $ConnectionString',
        ')',
        '',
        '. (Join-Path $PSScriptRoot ''common.ps1'')',
        '',
        '$moduleProjects = [ordered]@{'
    )
    $migrationLines += $migrationMapLines
    $migrationLines += @(
        '}',
        '',
        '$aliases = @($Module | ForEach-Object { $_ -split '','' } | ForEach-Object { $_.Trim().ToLowerInvariant() } | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -Unique)',
        'if ($aliases.Count -eq 0) {',
        '    throw ''Provide at least one persisted module alias.''',
        '}',
        '$unknown = @($aliases | Where-Object { -not $moduleProjects.ContainsKey($_) })',
        'if ($unknown.Count -gt 0) {',
        '    throw "Unknown or migration-free module alias: $($unknown -join '', ''). Available persisted modules: $($moduleProjects.Keys -join '', '')."',
        '}',
        '',
        'if ([string]::IsNullOrWhiteSpace($ConnectionString)) {',
        '    $ConnectionString = [Environment]::GetEnvironmentVariable("ConnectionStrings__$Provider")',
        '}',
        'if ([string]::IsNullOrWhiteSpace($ConnectionString)) {',
        '    throw "Provide -ConnectionString or set ConnectionStrings__$Provider. Migrations never use checked-in production credentials."',
        '}',
        '',
        'Invoke-GmaDotNet -Arguments @(''tool'', ''restore'')',
        'foreach ($alias in $aliases) {',
        '    $relativeProject = [string]::Format($moduleProjects[$alias], $Provider)',
        '    $project = Join-GmaPath $relativeProject',
        '    if (-not (Test-Path -LiteralPath $project -PathType Leaf)) {',
        '        throw "Migration project for module ''$alias'' is not mounted at ''$relativeProject''."',
        '    }',
        '',
        '    Write-Host "Applying $Provider migrations for $alias..."',
        '    Invoke-GmaDotNet -Arguments @(''ef'', ''database'', ''update'', ''--project'', $project, ''--'', ''--connection'', $ConnectionString)',
        '}',
        '',
        'Write-Host ''Selected module migrations applied successfully.'''
    )

    Write-GmaTemplateFile (Join-Path $Root 'eng\migrate.ps1') $migrationLines
}

$resolvedOutputPath = Resolve-GmaTemplatePath $OutputPath
if ((Test-Path -LiteralPath $resolvedOutputPath) -and
    @(Get-ChildItem -LiteralPath $resolvedOutputPath -Force).Count -gt 0 -and
    -not $Force) {
    throw "Output path '$resolvedOutputPath' already exists and is not empty. Choose a new path or rerun with -Force."
}

New-Item -ItemType Directory -Force -Path $resolvedOutputPath | Out-Null

Write-GmaTemplateFile (Join-Path $resolvedOutputPath '.gitignore') @(
    'bin/',
    'obj/',
    '.vs/',
    '.idea/',
    '.tmp/',
    'Gma.SourceRoots.props',
    '*.user',
    '*.suo'
)

Write-GmaTemplateFile (Join-Path $resolvedOutputPath 'global.json') @(
    '{',
    '  "sdk": {',
    '    "version": "10.0.300",',
    '    "rollForward": "latestFeature"',
    '  }',
    '}'
)

Write-GmaTemplateFile (Join-Path $resolvedOutputPath '.config\dotnet-tools.json') @(
    '{',
    '  "version": 1,',
    '  "isRoot": true,',
    '  "tools": {',
    '    "dotnet-ef": {',
    '      "version": "10.0.8",',
    '      "commands": [ "dotnet-ef" ]',
    '    }',
    '  }',
    '}'
)

$packageVersionTemplate = Get-Content -LiteralPath (Join-GmaPath 'Directory.Packages.props')
Write-GmaTemplateFile (Join-Path $resolvedOutputPath 'Directory.Packages.props') $packageVersionTemplate

Write-GmaTemplateFile (Join-Path $resolvedOutputPath 'Directory.Build.props') @(
    '<Project>',
    '  <PropertyGroup>',
    '    <GmaRepositoryRoot Condition="''$(GmaRepositoryRoot)'' == ''''">$(MSBuildThisFileDirectory)</GmaRepositoryRoot>',
    '  </PropertyGroup>',
    '',
    '  <Import Project="$(MSBuildThisFileDirectory)Gma.SourceRoots.props"',
    '          Condition="Exists(''$(MSBuildThisFileDirectory)Gma.SourceRoots.props'')" />',
    '',
    '  <PropertyGroup>',
    '    <GmaFrameworkRoot Condition="''$(GmaFrameworkRoot)'' == ''''">$(GmaRepositoryRoot)gma\framework\src\</GmaFrameworkRoot>',
    '    <GmaModulesRoot Condition="''$(GmaModulesRoot)'' == ''''">$(GmaRepositoryRoot)gma\modules\</GmaModulesRoot>',
    '    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>',
    '    <TargetFramework>net10.0</TargetFramework>',
    '    <ImplicitUsings>enable</ImplicitUsings>',
    '    <Nullable>enable</Nullable>',
    '    <AnalysisLevel>latest</AnalysisLevel>',
    '    <AnalysisMode>Recommended</AnalysisMode>',
    '    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>',
    '    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>',
    '    <Deterministic>true</Deterministic>',
    '    <LangVersion>latest</LangVersion>',
    '    <GenerateDocumentationFile>true</GenerateDocumentationFile>',
    '    <NoWarn>$(NoWarn);CS1591;IDE0005;IDE0008</NoWarn>',
    '  </PropertyGroup>',
    '</Project>'
)

$selectedModuleSpecs = Get-GmaSelectedModuleSpecs $Modules
$selectedModuleSpecArray = @($selectedModuleSpecs)
$selectedModuleText = if ($selectedModuleSpecArray.Count -eq 0) {
    'none'
}
else {
    ($selectedModuleSpecArray | ForEach-Object { $_.Alias }) -join ', '
}

$publicApiModuleSpecs = @($selectedModuleSpecArray | Where-Object { -not [string]::IsNullOrWhiteSpace($_.PublicApiProject) })
$persistedPublicApiModuleSpecs = @($publicApiModuleSpecs | Where-Object { -not [string]::IsNullOrWhiteSpace($_.MigrationProjectPrefix) })
$readinessModuleSpecs = @($publicApiModuleSpecs | Where-Object { -not [string]::IsNullOrWhiteSpace($_.DbContextType) })
$selectedModuleAliases = @($selectedModuleSpecArray | ForEach-Object { $_.Alias })
$hasAuth = $selectedModuleAliases -contains 'auth'
$hasFiles = $selectedModuleAliases -contains 'files'
$hasNotifications = $selectedModuleAliases -contains 'notifications'
$hasTenancy = $selectedModuleAliases -contains 'tenancy'
$publicApiModuleText = if ($publicApiModuleSpecs.Count -eq 0) {
    'none'
}
else {
    ($publicApiModuleSpecs | ForEach-Object { $_.Alias }) -join ', '
}

$submoduleCommandLines = @(
    'git submodule add https://github.com/SadPossum/GMA-Framework.git gma/framework'
)

foreach ($moduleSpec in $selectedModuleSpecArray) {
    $submoduleCommandLines += "git submodule add https://github.com/SadPossum/$($moduleSpec.Repository).git gma/modules/$($moduleSpec.Alias)"
}

$sourceRootExampleLines = @(
    '<Project>',
    '  <PropertyGroup>',
    '    <!-- Copy this file to Gma.SourceRoots.props and keep the copy untracked. -->',
    '    <GmaFrameworkRoot>$(MSBuildThisFileDirectory)gma\framework\src\</GmaFrameworkRoot>',
    '    <GmaModulesRoot>$(MSBuildThisFileDirectory)gma\modules\</GmaModulesRoot>'
)

foreach ($moduleSpec in $selectedModuleSpecArray) {
    $sourceRootExampleLines += "    <$($moduleSpec.SourceRootProperty)>`$(GmaModulesRoot)$($moduleSpec.Alias)\src\</$($moduleSpec.SourceRootProperty)>"
}

$sourceRootExampleLines += @(
    '  </PropertyGroup>',
    '</Project>'
)

Write-GmaTemplateFile (Join-Path $resolvedOutputPath 'Gma.SourceRoots.props.example') $sourceRootExampleLines

Write-GmaTemplateFile (Join-Path $resolvedOutputPath "$Name.slnx") @(
    '<Solution>',
    '  <Folder Name="/eng/">',
    '    <File Path="eng/common.ps1" />',
    '    <File Path="eng/gma-bootstrap.ps1" />',
    '    <File Path="eng/gma-status.ps1" />',
    '    <File Path="eng/gma-update.ps1" />',
    '    <File Path="eng/gma-validate.ps1" />',
    '    <File Path="eng/migrate.ps1" />',
    '    <File Path="eng/new-module.ps1" />',
    '  </Folder>',
    '  <Folder Name="/.github/workflows/">',
    '    <File Path=".github/workflows/validate.yml" />',
    '  </Folder>',
    '  <Folder Name="/docs/">',
    '    <File Path="docs/gma-source.md" />',
    '  </Folder>',
    '  <Folder Name="/Solution Items/">',
    '    <File Path=".gitignore" />',
    '    <File Path=".config/dotnet-tools.json" />',
    '    <File Path="Directory.Build.props" />',
    '    <File Path="Directory.Packages.props" />',
    '    <File Path="global.json" />',
    '    <File Path="Gma.SourceRoots.props.example" />',
    '    <File Path="README.md" />',
    '  </Folder>',
    '  <Folder Name="/src/Hosts/">',
    '    <File Path="src/Hosts/README.md" />',
    "    <Project Path=`"src/Hosts/$Name.Host.Api/$Name.Host.Api.csproj`" />",
    '  </Folder>',
    '  <Folder Name="/src/Modules/">',
    '    <File Path="src/Modules/README.md" />',
    '  </Folder>',
    '  <Folder Name="/src/Shared/">',
    '    <File Path="src/Shared/README.md" />',
    "    <Project Path=`"src/Shared/$Name.SharedKernel/$Name.SharedKernel.csproj`" />",
    '  </Folder>',
    '  <Folder Name="/tests/">',
    "    <Project Path=`"tests/$Name.Architecture.Tests/$Name.Architecture.Tests.csproj`" />",
    '  </Folder>',
    '</Solution>'
)

Write-GmaTemplateFile (Join-Path $resolvedOutputPath 'README.md') @(
    "# $Name",
    '',
    "$Name is an application shell built with GMA source packages.",
    '',
    '## Quick Start',
    '',
    'After adding or mounting the GMA source packages described in [docs/gma-source.md](docs/gma-source.md):',
    '',
    '```powershell',
    '.\eng\gma-update.ps1 -Init',
    '.\eng\gma-bootstrap.ps1 -Force',
    '.\eng\gma-validate.ps1',
    "dotnet run --project .\src\Hosts\$Name.Host.Api\$Name.Host.Api.csproj",
    '```',
    '',
    '## Layout',
    '',
    '- Runtime hosts live under `src/Hosts/`.',
    '- App-owned modules live under `src/Modules/`.',
    '- App-owned shared code lives under `src/Shared/`.',
    '- Reusable GMA source packages are mounted under `gma/`.',
    '- GMA source-package workflow notes live in [docs/gma-source.md](docs/gma-source.md).'
)

$gmaSourceDocsLines = @(
    '# GMA Source Packages',
    '',
    'This app consumes GMA as editable source so product work can improve the framework and reusable modules without waiting on package publishing.',
    '',
    '## Selected Sources',
    '',
    '- GMA framework mount: `gma/framework`.',
    '- Reusable GMA module mounts: `gma/modules/<alias>`.',
    "- Selected reusable GMA modules for this shell: $selectedModuleText.",
    "- Public API modules composed by ``src/Hosts/$Name.Host.Api``: $publicApiModuleText.",
    '- Admin CLI/API and worker-only module surfaces stay app-specific; add those hosts explicitly when the app needs them.',
    '- Runtime provider choices such as authentication schemes, storage adapters, messaging, and production connection strings remain app-owned composition work.',
    '- `Directory.Packages.props` is seeded from the skeleton catalog so app-owned generated modules can restore immediately; prune or add versions as the product evolves.',
    '',
    '## Add Source Repositories',
    '',
    'If the app does not already have GMA source packages mounted, add them as submodules:',
    '',
    '```powershell'
)

$gmaSourceDocsLines += $submoduleCommandLines
$gmaSourceDocsLines += @(
    '```',
    '',
    'Use equivalent SSH or fork URLs when the app should track private forks instead of the public GMA repositories.',
    '',
    '## Local Bootstrap',
    '',
    'Run these commands after cloning the app, adding submodules, or mounting local source checkouts:',
    '',
    '```powershell',
    '.\eng\gma-update.ps1 -Init',
    '.\eng\gma-bootstrap.ps1 -Force',
    '.\eng\gma-status.ps1',
    '.\eng\gma-validate.ps1',
    '```',
    '',
    '`eng/gma-bootstrap.ps1` writes ignored `Gma.SourceRoots.props` files for the app and mounted GMA repositories. Use `-Force` after moving source mounts or changing the selected module set.',
    '',
    '## Updating GMA',
    '',
    'GMA source packages are app dependencies. Update them deliberately, validate the app, and commit changed submodule pointers only when the new dependency state is intended.',
    '',
    '```powershell',
    'git -C gma/framework fetch origin',
    'git -C gma/framework switch dev',
    'git -C gma/framework pull --ff-only origin dev',
    '.\eng\gma-bootstrap.ps1 -Force',
    '.\eng\gma-validate.ps1',
    'git status --short',
    '```',
    '',
    '## Fixing GMA From This App',
    '',
    '- Work on GMA fixes in the source checkout, push a GMA branch/PR, then update the app submodule pointer deliberately.',
    '- Avoid editing a submodule while it is in detached `HEAD`; switch to a branch inside that source checkout first.',
    '- Upstream fixes when they improve reusable framework or module behavior; keep product-specific policies and workflows in the app.',
    '',
    '## CI',
    '',
    'Generated app shells include `.github/workflows/validate.yml` with recursive submodule checkout, source-root bootstrap, restore, build, and test steps.',
    '',
    'Set `GMA_CI_TOKEN` in GitHub Actions only when private GMA submodules need cross-repository read access. Public GMA repositories can use the default GitHub token.'
)

Write-GmaTemplateFile (Join-Path $resolvedOutputPath 'docs\gma-source.md') $gmaSourceDocsLines

Write-GmaGeneratedBootstrap -Root $resolvedOutputPath -ModuleSpecs $selectedModuleSpecArray
Write-GmaGeneratedWorkflow $resolvedOutputPath
Write-GmaGeneratedDeveloperTools -Root $resolvedOutputPath -ApplicationName $Name -ModuleSpecs $selectedModuleSpecArray

Write-GmaTemplateFile (Join-Path $resolvedOutputPath 'src\Hosts\README.md') @(
    '# Hosts',
    '',
    'Hosts are process entrypoints. Keep composition here and keep domain behavior in modules.',
    '',
    '- `*.Host.Api` composes public HTTP modules.',
    '- Add admin API, admin CLI, worker, or migration hosts only when the product needs them.'
)

Write-GmaTemplateFile (Join-Path $resolvedOutputPath 'src\Modules\README.md') @(
    '# Modules',
    '',
    'Place app-owned domain modules here.',
    '',
    'A module should own its domain model, application use cases, persistence, front doors, tests, and docs. Reusable GMA modules stay mounted under `gma/modules/<alias>` instead of this folder.'
)

Write-GmaTemplateFile (Join-Path $resolvedOutputPath 'src\Shared\README.md') @(
    '# Shared',
    '',
    'Place app-owned shared contracts and small cross-module primitives here.',
    '',
    'Keep this layer small. Prefer module-owned behavior unless a concept is genuinely shared across app modules.'
)

Write-GmaTemplateFile (Join-Path $resolvedOutputPath "src\Shared\$Name.SharedKernel\$Name.SharedKernel.csproj") @(
    '<Project Sdk="Microsoft.NET.Sdk" />'
)

Write-GmaTemplateFile (Join-Path $resolvedOutputPath "src\Shared\$Name.SharedKernel\ApplicationAssemblyMarker.cs") @(
    "namespace $Name.SharedKernel;",
    '',
    'public static class ApplicationAssemblyMarker',
    '{',
    '}'
)

$hostApiProjectLines = @(
    '<Project Sdk="Microsoft.NET.Sdk.Web">',
    '  <ItemGroup>',
    "    <ProjectReference Include=`"..\..\Shared\$Name.SharedKernel\$Name.SharedKernel.csproj`" />",
    '    <ProjectReference Include="$(GmaFrameworkRoot)Api\Gma.Framework.Api\Gma.Framework.Api.csproj" />',
    '    <ProjectReference Include="$(GmaFrameworkRoot)Api\Gma.Framework.Api.Production\Gma.Framework.Api.Production.csproj" />',
    '    <ProjectReference Include="$(GmaFrameworkRoot)Infrastructure\Gma.Framework.Infrastructure\Gma.Framework.Infrastructure.csproj" />',
    '    <ProjectReference Include="$(GmaFrameworkRoot)Modules\Gma.Framework.ModuleComposition\Gma.Framework.ModuleComposition.csproj" />'
)

if ($readinessModuleSpecs.Count -gt 0) {
    $hostApiProjectLines += '    <ProjectReference Include="$(GmaFrameworkRoot)Api\Gma.Framework.Api.Production.EntityFrameworkCore\Gma.Framework.Api.Production.EntityFrameworkCore.csproj" />'
}

if ($hasAuth) {
    $hostApiProjectLines += '    <ProjectReference Include="$(GmaFrameworkRoot)Messaging\Gma.Framework.Messaging.Infrastructure\Gma.Framework.Messaging.Infrastructure.csproj" />'
}

foreach ($moduleSpec in $publicApiModuleSpecs) {
    $sourceRootPropertyReference = '$(' + $moduleSpec.SourceRootProperty + ')'
    $hostApiProjectLines += "    <ProjectReference Include=`"$sourceRootPropertyReference$($moduleSpec.PublicApiProject)\$($moduleSpec.PublicApiProject).csproj`" />"
}

if ($hasFiles) {
    $hostApiProjectLines += '    <ProjectReference Include="$(GmaFrameworkRoot)FileManagement\Gma.Framework.FileManagement.LocalStorage\Gma.Framework.FileManagement.LocalStorage.csproj" />'
}

foreach ($moduleSpec in $persistedPublicApiModuleSpecs) {
    $sourceRootPropertyReference = '$(' + $moduleSpec.SourceRootProperty + ')'
    $hostApiProjectLines += "    <ProjectReference Include=`"$sourceRootPropertyReference$($moduleSpec.MigrationProjectPrefix).SqlServerMigrations\$($moduleSpec.MigrationProjectPrefix).SqlServerMigrations.csproj`" />"
    $hostApiProjectLines += "    <ProjectReference Include=`"$sourceRootPropertyReference$($moduleSpec.MigrationProjectPrefix).PostgreSqlMigrations\$($moduleSpec.MigrationProjectPrefix).PostgreSqlMigrations.csproj`" />"
}

$hostApiProjectLines += @(
    '  </ItemGroup>',
    '</Project>'
)

Write-GmaTemplateFile (Join-Path $resolvedOutputPath "src\Hosts\$Name.Host.Api\$Name.Host.Api.csproj") $hostApiProjectLines

$programUsingLines = @(
    'using Gma.Framework.Api.Modules;',
    'using Gma.Framework.Api.Production;',
    'using Gma.Framework.Api.Security;',
    'using Gma.Framework.Infrastructure;',
    'using Gma.Framework.ModuleComposition;'
)

if ($readinessModuleSpecs.Count -gt 0) {
    $programUsingLines += 'using Gma.Framework.Api.Production.EntityFrameworkCore;'
}

if ($hasAuth) {
    $programUsingLines += 'using Gma.Framework.Messaging.Infrastructure;'
    $programUsingLines += 'using Gma.Modules.Auth.Contracts;'
}

if ($hasFiles) {
    $programUsingLines += 'using Gma.Framework.FileManagement.LocalStorage;'
}

foreach ($moduleSpec in $publicApiModuleSpecs) {
    $programUsingLines += "using $($moduleSpec.PublicApiNamespace);"
}

$programUsingLines = @($programUsingLines | Select-Object -Unique)

$moduleRegistrationLines = New-Object 'System.Collections.Generic.List[string]'
if ($hasTenancy) {
    $moduleRegistrationLines.Add('builder.AddModule<TenancyModule>();')
}

if ($hasAuth) {
    if ($hasTenancy) {
        $moduleRegistrationLines.Add('builder.AddAuthModule(AuthProfile.ScopeAware());')
    }
    else {
        $moduleRegistrationLines.Add('builder.AddAuthModule(AuthProfile.Global());')
    }
}

foreach ($moduleSpec in $publicApiModuleSpecs | Where-Object { $_.Alias -notin @('auth', 'tenancy') }) {
    $moduleRegistrationLines.Add("builder.AddModule<$($moduleSpec.PublicApiModuleType)>();")
}

$programLines = @($programUsingLines)
$programLines += @(
    '',
    'WebApplicationBuilder builder = WebApplication.CreateBuilder(args);',
    '',
    'builder.AddGmaInfrastructure();',
    'builder.Services.AddApiSecurityDefaults();',
    'builder.AddGmaProductionHttp();'
)

if ($hasAuth) {
    $programLines += 'builder.AddMessagingInfrastructure();'
}

if ($hasFiles) {
    $programLines += 'builder.AddLocalFileStorage();'
}

if ($moduleRegistrationLines.Count -gt 0) {
    $programLines += ''
    $programLines += $moduleRegistrationLines.ToArray()
}

foreach ($moduleSpec in $readinessModuleSpecs) {
    $databaseName = ConvertTo-GmaKebabCase $moduleSpec.Alias
    $programLines += "builder.Services.AddGmaEntityFrameworkReadinessCheck<$($moduleSpec.DbContextType)>(`"$databaseName-database`");"
}

$programLines += @(
    '',
    'builder.ValidateModuleComposition();',
    '',
    'WebApplication app = builder.Build();',
    '',
    'app.UseGmaProductionHttp();',
    'app.UseAuthentication();',
    'app.UseAuthorization();',
    '',
    'app.MapGet("/", () => Results.Ok(new',
    '{',
    "    Application = `"$Name`"",
    '}));',
    '',
    'app.MapModules();',
    'app.MapGmaHealthEndpoints();',
    '',
    'app.Run();'
)

Write-GmaTemplateFile (Join-Path $resolvedOutputPath "src\Hosts\$Name.Host.Api\Program.cs") $programLines

$baseSettings = [ordered]@{
    ApplicationIdentity = [ordered]@{
        DisplayName = $Name
        Namespace = (ConvertTo-GmaKebabCase -Value $Name)
    }
    Http = [ordered]@{
        AllowAnyHost = $false
        HttpsRedirectionEnabled = $true
        HstsEnabled = $true
        SecurityHeadersEnabled = $true
        ForwardedHeaders = [ordered]@{
            Enabled = $false
            AllowUnknownProxies = $false
            ForwardLimit = 1
            KnownProxies = @()
        }
        Cors = [ordered]@{
            Enabled = $false
            AllowCredentials = $false
            AllowedOrigins = @()
        }
        RequestTimeouts = [ordered]@{
            Enabled = $true
            DefaultTimeoutSeconds = 30
        }
        RateLimiting = [ordered]@{
            Enabled = $true
            GlobalPermitLimit = 300
            SensitivePermitLimit = 10
            WindowSeconds = 60
            SensitivePathPrefixes = @('/api/auth/register', '/api/auth/login', '/api/auth/refresh')
        }
        PrivateNetwork = [ordered]@{
            Enabled = $false
            AllowedNetworks = @()
        }
    }
    AllowedHosts = 'CHANGE-ME.invalid'
}

$developmentSettings = [ordered]@{
    Http = [ordered]@{
        HttpsRedirectionEnabled = $false
        RateLimiting = [ordered]@{
            Enabled = $false
        }
        PrivateNetwork = [ordered]@{
            Enabled = $false
        }
    }
    AllowedHosts = '*'
}

$hasPersistedPublicModule = $persistedPublicApiModuleSpecs.Count -gt 0
if ($hasPersistedPublicModule) {
    $baseSettings.Persistence = [ordered]@{
        Provider = 'SqlServer'
    }
    $baseSettings.ConnectionStrings = [ordered]@{
        SqlServer = ''
        PostgreSql = ''
    }
    $developmentSettings.Persistence = [ordered]@{
        Provider = 'SqlServer'
    }
    $developmentSettings.ConnectionStrings = [ordered]@{
        SqlServer = "Server=localhost,1433;Database=$Name;User Id=sa;Password=Pass@word1;TrustServerCertificate=True"
        PostgreSql = "Host=localhost;Port=5432;Database=$($Name.ToLowerInvariant());Username=postgres;Password=postgres"
    }
}

if ($hasAuth) {
    $baseSettings.Auth = [ordered]@{
        RefreshTokenLifetimeDays = 30
        FailedLoginLimit = 5
        FailedLoginWindowMinutes = 15
        RefreshTokens = [ordered]@{
            Pepper = ''
        }
        Jwt = [ordered]@{
            SigningKey = ''
            AccessTokenLifetimeMinutes = 15
        }
    }
    $developmentSettings.Auth = [ordered]@{
        RefreshTokens = [ordered]@{
            Pepper = 'local-development-refresh-token-pepper-change-me-00000000000000000000'
        }
        Jwt = [ordered]@{
            SigningKey = 'local-development-signing-key-change-me-00000000000000000000'
        }
    }
}

if ($hasTenancy) {
    $baseSettings.Tenancy = [ordered]@{
        Enabled = $true
        HeaderName = 'X-Tenant-Id'
        LocalDefaultTenantId = 'default'
    }
}

if ($hasFiles) {
    $baseSettings.FileManagement = [ordered]@{
        Enabled = $false
        Provider = 'Unknown'
        MaximumObjectBytes = 10485760
        RequireContentInspection = $true
        AllowedContentTypes = @('image/jpeg', 'image/png', 'application/pdf')
        LocalStorage = [ordered]@{
            RootPath = 'data/files'
        }
    }
    $developmentSettings.FileManagement = [ordered]@{
        Enabled = $true
        Provider = 'LocalStorage'
        MaximumObjectBytes = 10485760
        RequireContentInspection = $false
        AllowedContentTypes = @('image/jpeg', 'image/png', 'application/pdf')
        LocalStorage = [ordered]@{
            RootPath = 'data/files'
        }
    }
}

if ($hasNotifications) {
    $baseSettings.Notifications = [ordered]@{
        Retention = [ordered]@{
            Enabled = $false
            ReadHistoryDays = 90
            UnreadHistoryDays = 365
            BroadcastDays = 365
            BatchSize = 500
            IntervalMinutes = 60
        }
    }
}

$baseSettingsJson = $baseSettings | ConvertTo-Json -Depth 10
$developmentSettingsJson = $developmentSettings | ConvertTo-Json -Depth 10
Write-GmaTemplateFile (Join-Path $resolvedOutputPath "src\Hosts\$Name.Host.Api\appsettings.json") ($baseSettingsJson -split "`r?`n")
Write-GmaTemplateFile (Join-Path $resolvedOutputPath "src\Hosts\$Name.Host.Api\appsettings.Development.json") ($developmentSettingsJson -split "`r?`n")
Write-GmaTemplateFile (Join-Path $resolvedOutputPath "src\Hosts\$Name.Host.Api\Properties\launchSettings.json") @(
    '{',
    '  "$schema": "http://json.schemastore.org/launchsettings.json",',
    '  "profiles": {',
    "    `"$Name.Host.Api`": {",
    '      "commandName": "Project",',
    '      "dotnetRunMessages": true,',
    '      "launchBrowser": false,',
    '      "applicationUrl": "http://localhost:5080",',
    '      "environmentVariables": {',
    '        "ASPNETCORE_ENVIRONMENT": "Development"',
    '      }',
    '    }',
    '  }',
    '}'
)

Write-GmaTemplateFile (Join-Path $resolvedOutputPath "tests\$Name.Architecture.Tests\$Name.Architecture.Tests.csproj") @(
    '<Project Sdk="Microsoft.NET.Sdk">',
    '  <PropertyGroup>',
    '    <IsTestProject>true</IsTestProject>',
    '    <IsPackable>false</IsPackable>',
    '  </PropertyGroup>',
    '  <ItemGroup>',
    '    <PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" />',
    '    <PackageReference Include="Microsoft.NET.Test.Sdk" />',
    '    <PackageReference Include="xunit" />',
    '    <PackageReference Include="xunit.runner.visualstudio">',
    '      <PrivateAssets>all</PrivateAssets>',
    '    </PackageReference>',
    '  </ItemGroup>',
    '  <ItemGroup>',
    "    <ProjectReference Include=`"..\..\src\Hosts\$Name.Host.Api\$Name.Host.Api.csproj`" />",
    '  </ItemGroup>',
    '</Project>'
)

Write-GmaTemplateFile (Join-Path $resolvedOutputPath "tests\$Name.Architecture.Tests\HostStartupTests.cs") @(
    "namespace $Name.Architecture.Tests;",
    '',
    'using System.Net;',
    'using Microsoft.AspNetCore.Mvc.Testing;',
    'using Xunit;',
    '',
    '[Trait("Category", "Architecture")]',
    'public sealed class HostStartupTests',
    '{',
    '    [Fact]',
    '    public async Task GeneratedHostBuildsAndMapsLiveness()',
    '    {',
    '        await using WebApplicationFactory<global::Program> factory = new();',
    '        using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions',
    '        {',
    '            AllowAutoRedirect = false',
    '        });',
    '',
    '        using HttpResponseMessage response = await client.GetAsync("/alive");',
    '',
    '        Assert.Equal(HttpStatusCode.OK, response.StatusCode);',
    '    }',
    '}'
)

Write-GmaTemplateFile (Join-Path $resolvedOutputPath "tests\$Name.Architecture.Tests\ModuleBoundaryTests.cs") @(
    "namespace $Name.Architecture.Tests;",
    '',
    'using System.Xml.Linq;',
    'using Xunit;',
    '',
    '[Trait("Category", "Architecture")]',
    'public sealed class ModuleBoundaryTests',
    '{',
    '    [Fact]',
    '    public void AppModulesKeepDomainApplicationAndCrossModuleBoundaries()',
    '    {',
    '        string repositoryRoot = FindRepositoryRoot();',
    '        string modulesRoot = Path.Combine(repositoryRoot, "src", "Modules");',
    '        if (!Directory.Exists(modulesRoot))',
    '        {',
    '            return;',
    '        }',
    '',
    '        List<string> offenders = [];',
    '        foreach (string projectPath in Directory.EnumerateFiles(modulesRoot, "*.csproj", SearchOption.AllDirectories))',
    '        {',
    '            XDocument project = XDocument.Load(projectPath);',
    '            string projectName = Path.GetFileNameWithoutExtension(projectPath);',
    '            string owner = Path.GetRelativePath(modulesRoot, projectPath)',
    '                .Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries)[0];',
    '            string[] references = [.. project.Descendants("ProjectReference")',
    '                .Select(reference => (string?)reference.Attribute("Include"))',
    '                .Where(reference => !string.IsNullOrWhiteSpace(reference))',
    '                .Select(reference => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(projectPath)!, reference!)))];',
    '',
    '            if (projectName.EndsWith(".Domain", StringComparison.Ordinal))',
    '            {',
    '                offenders.AddRange(project.Descendants("PackageReference")',
    '                    .Select(_ => $"{projectName}: Domain projects cannot reference NuGet packages."));',
    '                offenders.AddRange(references',
    '                    .Where(reference => !reference.StartsWith(Path.Combine(repositoryRoot, "src", "Shared"), StringComparison.OrdinalIgnoreCase))',
    '                    .Select(reference => $"{projectName}: Domain references {Path.GetFileNameWithoutExtension(reference)}."));',
    '            }',
    '',
    '            if (projectName.EndsWith(".Application", StringComparison.Ordinal))',
    '            {',
    '                string[] forbidden = [".Persistence", ".Infrastructure", ".Api", ".AdminApi", ".AdminCli"];',
    '                offenders.AddRange(references',
    '                    .Where(reference => forbidden.Any(token => Path.GetFileNameWithoutExtension(reference).Contains(token, StringComparison.Ordinal)))',
    '                    .Select(reference => $"{projectName}: Application references adapter {Path.GetFileNameWithoutExtension(reference)}."));',
    '            }',
    '',
    '            foreach (string reference in references.Where(reference => reference.StartsWith(modulesRoot, StringComparison.OrdinalIgnoreCase)))',
    '            {',
    '                string referencedOwner = Path.GetRelativePath(modulesRoot, reference)',
    '                    .Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries)[0];',
    '                string referencedProject = Path.GetFileNameWithoutExtension(reference);',
    '                if (!string.Equals(owner, referencedOwner, StringComparison.OrdinalIgnoreCase) &&',
    '                    !referencedProject.EndsWith(".Contracts", StringComparison.Ordinal) &&',
    '                    !referencedProject.EndsWith(".Admin.Contracts", StringComparison.Ordinal))',
    '                {',
    '                    offenders.Add($"{projectName}: cross-module reference to {referencedProject} bypasses contracts.");',
    '                }',
    '            }',
    '        }',
    '',
    '        Assert.Empty(offenders.Order(StringComparer.Ordinal));',
    '    }',
    '',
    '    private static string FindRepositoryRoot()',
    '    {',
    '        DirectoryInfo? directory = new(AppContext.BaseDirectory);',
    '        while (directory is not null)',
    '        {',
    "            if (File.Exists(Path.Combine(directory.FullName, `"$Name.slnx`")))",
    '            {',
    '                return directory.FullName;',
    '            }',
    '',
    '            directory = directory.Parent;',
    '        }',
    '',
    '        throw new InvalidOperationException("Could not locate application repository root.");',
    '    }',
    '}'
)

New-Item -ItemType Directory -Force -Path (Join-Path $resolvedOutputPath 'gma\modules') | Out-Null
New-Item -ItemType File -Force -Path (Join-Path $resolvedOutputPath 'gma\.gitkeep') | Out-Null
New-Item -ItemType File -Force -Path (Join-Path $resolvedOutputPath 'gma\modules\.gitkeep') | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $resolvedOutputPath 'gma\framework') | Out-Null

Write-Host "Created GMA app shell: $resolvedOutputPath"
Write-Host 'Next: add or mount GMA source packages, run eng/gma-update.ps1 -Init, then eng/gma-bootstrap.ps1 -Force and eng/gma-validate.ps1.'

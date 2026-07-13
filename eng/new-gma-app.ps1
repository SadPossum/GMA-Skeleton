[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[A-Z][A-Za-z0-9]*$')]
    [string] $Name,

    [Parameter(Mandatory = $true)]
    [string] $OutputPath,

    [switch] $Force,

    [string[]] $Modules = @(),

    [ValidateSet('Api', 'AdminApi', 'AdminCli', 'Worker', 'Aspire')]
    [string[]] $Hosts = @('Api'),

    [switch] $ServiceDefaults,

    [switch] $DockerValidation
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
    $includeAuthNotificationsExtension =
        ($selectedModuleSpecArray.Alias -contains 'auth') -and
        ($selectedModuleSpecArray.Alias -contains 'notifications')
    $includeAuthNotificationsExtensionLiteral = if ($includeAuthNotificationsExtension) { '$true' } else { '$false' }
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
        "`$includeAuthNotificationsExtension = $includeAuthNotificationsExtensionLiteral",
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
        'if ($includeAuthNotificationsExtension) {',
        '    $rootLinesBuilder.Add(''    <GmaExtensionsRoot>$(MSBuildThisFileDirectory)gma\extensions\src\</GmaExtensionsRoot>'')',
        '}',
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
        '$extensionLines = @(',
        '    ''<Project>'',',
        '    ''  <PropertyGroup>'',',
        '    ''    <GmaExtensionsRoot>$(MSBuildThisFileDirectory)src\</GmaExtensionsRoot>'',',
        '    ''    <GmaFrameworkRoot>$(MSBuildThisFileDirectory)..\framework\src\</GmaFrameworkRoot>'',',
        '    ''    <GmaModuleAuthRoot>$(MSBuildThisFileDirectory)..\modules\auth\src\</GmaModuleAuthRoot>'',',
        '    ''    <GmaModuleNotificationsRoot>$(MSBuildThisFileDirectory)..\modules\notifications\src\</GmaModuleNotificationsRoot>'',',
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
        'if ($includeAuthNotificationsExtension) {',
        '    Write-GmaSourceRootsFile -Path (Join-GmaPath ''gma/extensions/Gma.SourceRoots.props'') -Lines $extensionLines -Description ''extensions source-root configuration''',
        '}',
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
        '    [switch] $Remote,',
        '    [string] $EditableBranch = ''''',
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
        '}',
        '',
        'if (-not [string]::IsNullOrWhiteSpace($EditableBranch)) {',
        '    $paths = @(git -C (Get-GmaRepositoryRoot) config --file .gitmodules --get-regexp ''^submodule\..*\.path$'' | ForEach-Object { ($_ -split ''\s+'', 2)[1] })',
        '    foreach ($path in $paths) {',
        '        $submoduleRoot = Join-GmaPath $path',
        '        git -C $submoduleRoot switch $EditableBranch',
        '        if ($LASTEXITCODE -ne 0) {',
        '            git -C $submoduleRoot switch --track -c $EditableBranch "origin/$EditableBranch"',
        '            if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }',
        '        }',
        '    }',
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
        '& (Join-GmaPath ''eng/sync-solution.ps1'') -Check',
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
    param(
        [Parameter(Mandatory = $true)][string] $Root,
        [switch] $IncludeDocker
    )

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
        'concurrency:',
        '  group: validate-${{ github.ref }}',
        '  cancel-in-progress: true',
        '',
        'env:',
        '  DOTNET_CLI_TELEMETRY_OPTOUT: ''1''',
        '  DOTNET_NOLOGO: ''true''',
        '  DOTNET_SKIP_FIRST_TIME_EXPERIENCE: ''1''',
        '',
        'jobs:',
        '  validate:',
        '    timeout-minutes: 30',
        '    strategy:',
        '      fail-fast: false',
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
        '      - name: Check source dependency heads',
        '        shell: pwsh',
        '        run: ./eng/check-submodule-heads.ps1',
        '',
        '      - name: Check solution graph',
        '        shell: pwsh',
        '        run: ./eng/sync-solution.ps1 -Check',
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

    Write-GmaTemplateFile (Join-Path $Root '.github\dependabot.yml') @(
        'version: 2',
        'updates:',
        '  - package-ecosystem: github-actions',
        '    directory: /',
        '    schedule:',
        '      interval: weekly',
        '    open-pull-requests-limit: 5',
        '',
        '  - package-ecosystem: nuget',
        '    directory: /',
        '    schedule:',
        '      interval: weekly',
        '    open-pull-requests-limit: 10'
    )

    Write-GmaTemplateFile (Join-Path $Root '.github\workflows\release-source-set.yml') @(
        'name: Release Source Set',
        '',
        'on:',
        '  push:',
        '    tags:',
        '      - ''v*''',
        '  workflow_dispatch:',
        '',
        'permissions:',
        '  contents: read',
        '',
        'jobs:',
        '  manifest:',
        '    runs-on: ubuntu-latest',
        '    timeout-minutes: 10',
        '    steps:',
        '      - name: Checkout compatible source set',
        '        uses: actions/checkout@9c091bb21b7c1c1d1991bb908d89e4e9dddfe3e0 # v7.0.0',
        '        with:',
        '          fetch-depth: 0',
        '          submodules: recursive',
        '          token: ${{ secrets.GMA_CI_TOKEN || github.token }}',
        '          persist-credentials: false',
        '',
        '      - name: Export source bill of materials',
        '        shell: pwsh',
        '        run: ./eng/export-source-set.ps1 -RequireClean',
        '',
        '      - name: Publish source bill of materials',
        '        uses: actions/upload-artifact@043fb46d1a93c77aae656e7c1c64a875d1fc6a0a # v7.0.1',
        '        with:',
        '          name: gma-source-set-${{ github.ref_name }}',
        '          path: artifacts/gma-source-set.json',
        '          if-no-files-found: error',
        '          retention-days: 90'
    )

    if ($IncludeDocker) {
        Write-GmaTemplateFile (Join-Path $Root '.github\workflows\docker-tests.yml') @(
            'name: Docker Tests',
            '',
            'on:',
            '  pull_request:',
            '  schedule:',
            '    - cron: ''17 3 * * 1''',
            '  workflow_dispatch:',
            '',
            'permissions:',
            '  contents: read',
            '',
            'jobs:',
            '  docker:',
            '    runs-on: ubuntu-latest',
            '    timeout-minutes: 45',
            '    steps:',
            '      - uses: actions/checkout@9c091bb21b7c1c1d1991bb908d89e4e9dddfe3e0 # v7.0.0',
            '        with:',
            '          submodules: recursive',
            '          token: ${{ secrets.GMA_CI_TOKEN || github.token }}',
            '          persist-credentials: false',
            '      - uses: actions/setup-dotnet@26b0ec14cb23fa6904739307f278c14f94c95bf1 # v5.4.0',
            '        with:',
            '          dotnet-version: 10.0.x',
            '      - shell: pwsh',
            '        run: ./eng/gma-bootstrap.ps1 -Force',
            '      - run: dotnet restore ' + $Name + '.slnx',
            '      - run: dotnet build ' + $Name + '.slnx --no-restore -m:1',
            '      - run: dotnet test ' + $Name + '.slnx --no-build --filter Category=Docker --logger ''console;verbosity=minimal'' -m:1 -nr:false'
        )
    }
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
        "& `$implementation @PSBoundParameters -RepositoryRoot `$repositoryRoot -CompositionSolution '$ApplicationName.slnx' -ProjectPrefix '$ApplicationName.Modules' -PublicApiHostProject 'src\Hosts\$ApplicationName.Host.Api\$ApplicationName.Host.Api.csproj' -PublicApiHostProgram 'src\Hosts\$ApplicationName.Host.Api\Program.cs'"
    )

    Write-GmaTemplateFile (Join-Path $Root 'eng\add-migration.ps1') @(
        'param(',
        '    [Parameter(Mandatory = $true)][ValidatePattern(''^[A-Za-z][A-Za-z0-9_.-]*$'')][string] $Module,',
        '    [Parameter(Mandatory = $true)][ValidateSet(''SqlServer'', ''PostgreSql'')][string] $Provider,',
        '    [Parameter(Mandatory = $true)][ValidatePattern(''^[A-Za-z][A-Za-z0-9_]*$'')][string] $Name,',
        '    [string] $Connection,',
        '    [ValidatePattern(''^[A-Za-z][A-Za-z0-9_]*DbContext$'')][string] $Context',
        ')',
        '',
        '. (Join-Path $PSScriptRoot ''common.ps1'')',
        '$implementation = Join-GmaPath ''gma/framework/eng/add-migration.ps1''',
        'if (-not (Test-Path -LiteralPath $implementation -PathType Leaf)) { throw ''GMA framework tooling is not mounted.'' }',
        '& $implementation @PSBoundParameters -RepositoryRoot (Get-GmaRepositoryRoot) -PathPrefix @(''gma/framework'', ''gma/extensions'', ''gma/modules/'')'
    )

    Write-GmaTemplateFile (Join-Path $Root 'eng\check-migrations.ps1') @(
        'param([switch] $NoBuild)',
        '',
        '. (Join-Path $PSScriptRoot ''common.ps1'')',
        '$implementation = Join-GmaPath ''gma/framework/eng/check-migrations.ps1''',
        'if (-not (Test-Path -LiteralPath $implementation -PathType Leaf)) { throw ''GMA framework tooling is not mounted.'' }',
        '& $implementation @PSBoundParameters -RepositoryRoot (Get-GmaRepositoryRoot)'
    )

    Write-GmaTemplateFile (Join-Path $Root 'eng\check-source-packages.ps1') @(
        'param([switch] $SkipRestore, [switch] $SkipBuild)',
        '',
        '. (Join-Path $PSScriptRoot ''common.ps1'')',
        '$implementation = Join-GmaPath ''gma/framework/eng/check-source-packages.ps1''',
        'if (-not (Test-Path -LiteralPath $implementation -PathType Leaf)) { throw ''GMA framework tooling is not mounted.'' }',
        '& $implementation @PSBoundParameters -RepositoryRoot (Get-GmaRepositoryRoot)'
    )

    Write-GmaTemplateFile (Join-Path $Root 'eng\check-submodule-heads.ps1') @(
        'param([string] $Branch = ''dev'')',
        '',
        '. (Join-Path $PSScriptRoot ''common.ps1'')',
        '$implementation = Join-GmaPath ''gma/framework/eng/check-submodule-heads.ps1''',
        'if (-not (Test-Path -LiteralPath $implementation -PathType Leaf)) { throw ''GMA framework tooling is not mounted.'' }',
        '& $implementation -RepositoryRoot (Get-GmaRepositoryRoot) -ExpectedBranch $Branch'
    )

    Write-GmaTemplateFile (Join-Path $Root 'eng\export-source-set.ps1') @(
        'param([string] $OutputPath = ''artifacts/gma-source-set.json'', [switch] $RequireClean)',
        '',
        '. (Join-Path $PSScriptRoot ''common.ps1'')',
        '$implementation = Join-GmaPath ''gma/framework/eng/export-source-set.ps1''',
        'if (-not (Test-Path -LiteralPath $implementation -PathType Leaf)) { throw ''GMA framework tooling is not mounted.'' }',
        '& $implementation @PSBoundParameters -RepositoryRoot (Get-GmaRepositoryRoot)'
    )

    Write-GmaTemplateFile (Join-Path $Root 'eng\sync-solution.ps1') @(
        'param([switch] $Check)',
        '',
        '. (Join-Path $PSScriptRoot ''common.ps1'')',
        '$implementation = Join-GmaPath ''gma/framework/eng/sync-solution.ps1''',
        'if (-not (Test-Path -LiteralPath $implementation -PathType Leaf)) { throw ''GMA framework tooling is not mounted.'' }',
        "`$arguments = @{ RepositoryRoot = Get-GmaRepositoryRoot; Solution = '$ApplicationName.slnx' }",
        'if ($Check) { $arguments.Check = $true }',
        '& $implementation @arguments'
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
    'artifacts/',
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

Write-GmaTemplateFile (Join-Path $resolvedOutputPath 'nuget.config') @(
    '<?xml version="1.0" encoding="utf-8"?>',
    '<configuration>',
    '  <packageSources>',
    '    <clear />',
    '    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />',
    '  </packageSources>',
    '</configuration>'
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
    '    <GmaExtensionsRoot Condition="''$(GmaExtensionsRoot)'' == ''''">$(GmaRepositoryRoot)gma\extensions\src\</GmaExtensionsRoot>',
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
$selectedHosts = @($Hosts | ForEach-Object { $_.Trim() } | Select-Object -Unique)
if ($selectedHosts -notcontains 'Api') {
    throw 'The generated app requires the Api host. Add Api to -Hosts; other host surfaces are optional.'
}

$hasAdminApiHost = $selectedHosts -contains 'AdminApi'
$hasAdminCliHost = $selectedHosts -contains 'AdminCli'
$hasWorkerHost = $selectedHosts -contains 'Worker'
$hasAspireHost = $selectedHosts -contains 'Aspire'
$selectedHostText = $selectedHosts -join ', '
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
    'git submodule add -b dev https://github.com/SadPossum/GMA-Framework.git gma/framework'
)

foreach ($moduleSpec in $selectedModuleSpecArray) {
    $submoduleCommandLines += "git submodule add -b dev https://github.com/SadPossum/$($moduleSpec.Repository).git gma/modules/$($moduleSpec.Alias)"
}

if ($hasAuth -and $hasNotifications) {
    $submoduleCommandLines += 'git submodule add -b dev https://github.com/SadPossum/GMA-Extensions.git gma/extensions'
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

if ($hasAuth -and $hasNotifications) {
    $sourceRootExampleLines += '    <GmaExtensionsRoot>$(MSBuildThisFileDirectory)gma\extensions\src\</GmaExtensionsRoot>'
}

$sourceRootExampleLines += @(
    '  </PropertyGroup>',
    '</Project>'
)

Write-GmaTemplateFile (Join-Path $resolvedOutputPath 'Gma.SourceRoots.props.example') $sourceRootExampleLines

Write-GmaTemplateFile (Join-Path $resolvedOutputPath "$Name.slnx") @(
    '<Solution>',
    '  <Folder Name="/eng/">',
    '    <File Path="eng/add-migration.ps1" />',
    '    <File Path="eng/check-migrations.ps1" />',
    '    <File Path="eng/check-source-packages.ps1" />',
    '    <File Path="eng/check-submodule-heads.ps1" />',
    '    <File Path="eng/common.ps1" />',
    '    <File Path="eng/export-source-set.ps1" />',
    '    <File Path="eng/gma-bootstrap.ps1" />',
    '    <File Path="eng/gma-status.ps1" />',
    '    <File Path="eng/gma-update.ps1" />',
    '    <File Path="eng/gma-validate.ps1" />',
    '    <File Path="eng/migrate.ps1" />',
    '    <File Path="eng/new-module.ps1" />',
    '    <File Path="eng/sync-solution.ps1" />',
    '  </Folder>',
    '  <Folder Name="/.github/workflows/">',
    '    <File Path=".github/workflows/release-source-set.yml" />',
    '    <File Path=".github/workflows/validate.yml" />',
    '  </Folder>',
    '  <Folder Name="/docs/">',
    '    <File Path="docs/gma-source.md" />',
    '  </Folder>',
    '  <Folder Name="/Solution Items/">',
    '    <File Path=".gitignore" />',
    '    <File Path=".github/dependabot.yml" />',
    '    <File Path=".config/dotnet-tools.json" />',
    '    <File Path="Directory.Build.props" />',
    '    <File Path="Directory.Packages.props" />',
    '    <File Path="global.json" />',
    '    <File Path="Gma.SourceRoots.props.example" />',
    '    <File Path="nuget.config" />',
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
    '.\eng\sync-solution.ps1',
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
    "- Generated host surfaces: $selectedHostText.",
    "- Service defaults generated: $([bool]$ServiceDefaults).",
    '- Generated admin and worker hosts are composition shells; add only the reusable or product modules each process owns.',
    '- Selected Auth and Notifications adapters are composed explicitly by the API host; credentials, enabled providers, shared Data Protection storage for multi-replica OIDC callbacks, email transport, messaging, and production connection strings remain app-owned deployment choices.',
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
    '.\eng\sync-solution.ps1',
    '.\eng\gma-validate.ps1',
    '```',
    '',
    '`eng/gma-bootstrap.ps1` writes ignored `Gma.SourceRoots.props` files for the app and mounted GMA repositories. Use `-Force` after moving source mounts or changing the selected module set.',
    '`eng/sync-solution.ps1 -Check` verifies the deterministic project and operational-file graph. Run it without `-Check` after adding projects or development files.',
    '`eng/new-module.ps1` creates app-owned projects with the `<Application>.Modules.<Module>` prefix.',
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
Write-GmaGeneratedWorkflow -Root $resolvedOutputPath -IncludeDocker:$DockerValidation
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

if ($ServiceDefaults) {
    Write-GmaTemplateFile (Join-Path $resolvedOutputPath "src\ServiceDefaults\$Name.ServiceDefaults\$Name.ServiceDefaults.csproj") @(
        '<Project Sdk="Microsoft.NET.Sdk">',
        '  <ItemGroup>',
        '    <FrameworkReference Include="Microsoft.AspNetCore.App" />',
        '  </ItemGroup>',
        '</Project>'
    )

    Write-GmaTemplateFile (Join-Path $resolvedOutputPath "src\ServiceDefaults\$Name.ServiceDefaults\Extensions.cs") @(
        "namespace $Name.ServiceDefaults;",
        '',
        'using Microsoft.AspNetCore.Builder;',
        'using Microsoft.AspNetCore.Diagnostics.HealthChecks;',
        'using Microsoft.AspNetCore.Routing;',
        'using Microsoft.Extensions.DependencyInjection;',
        'using Microsoft.Extensions.Hosting;',
        '',
        'public static class Extensions',
        '{',
        '    public static IHostApplicationBuilder AddServiceDefaults(this IHostApplicationBuilder builder)',
        '    {',
        '        ArgumentNullException.ThrowIfNull(builder);',
        '        builder.Services.AddHealthChecks();',
        '        return builder;',
        '    }',
        '',
        '    public static IEndpointRouteBuilder MapDefaultEndpoints(this IEndpointRouteBuilder endpoints)',
        '    {',
        '        ArgumentNullException.ThrowIfNull(endpoints);',
        '        endpoints.MapHealthChecks("/alive", new HealthCheckOptions { Predicate = _ => false });',
        '        endpoints.MapHealthChecks("/health");',
        '        return endpoints;',
        '    }',
        '}'
    )
}

$hostApiProjectLines = @(
    '<Project Sdk="Microsoft.NET.Sdk.Web">',
    '  <ItemGroup>',
    "    <ProjectReference Include=`"..\..\Shared\$Name.SharedKernel\$Name.SharedKernel.csproj`" />",
    '    <ProjectReference Include="$(GmaFrameworkRoot)Api\Gma.Framework.Api\Gma.Framework.Api.csproj" />',
    '    <ProjectReference Include="$(GmaFrameworkRoot)Api\Gma.Framework.Api.Production\Gma.Framework.Api.Production.csproj" />',
    '    <ProjectReference Include="$(GmaFrameworkRoot)Infrastructure\Gma.Framework.Infrastructure\Gma.Framework.Infrastructure.csproj" />',
    '    <ProjectReference Include="$(GmaFrameworkRoot)Modules\Gma.Framework.ModuleComposition\Gma.Framework.ModuleComposition.csproj" />'
)

if ($ServiceDefaults) {
    $hostApiProjectLines += "    <ProjectReference Include=`"..\..\ServiceDefaults\$Name.ServiceDefaults\$Name.ServiceDefaults.csproj`" />"
}

if ($readinessModuleSpecs.Count -gt 0) {
    $hostApiProjectLines += '    <ProjectReference Include="$(GmaFrameworkRoot)Api\Gma.Framework.Api.Production.EntityFrameworkCore\Gma.Framework.Api.Production.EntityFrameworkCore.csproj" />'
}

if ($hasAuth) {
    $hostApiProjectLines += '    <ProjectReference Include="$(GmaFrameworkRoot)Messaging\Gma.Framework.Messaging.Infrastructure\Gma.Framework.Messaging.Infrastructure.csproj" />'
    $hostApiProjectLines += '    <ProjectReference Include="$(GmaModuleAuthRoot)Gma.Modules.Auth.Providers.OpenIdConnect\Gma.Modules.Auth.Providers.OpenIdConnect.csproj" />'
}

if ($hasNotifications) {
    $hostApiProjectLines += '    <ProjectReference Include="$(GmaModuleNotificationsRoot)Gma.Modules.Notifications.Adapters.Email\Gma.Modules.Notifications.Adapters.Email.csproj" />'
}

if ($hasAuth -and $hasNotifications) {
    $hostApiProjectLines += '    <ProjectReference Include="$(GmaExtensionsRoot)Gma.Extensions.Auth.Notifications\Gma.Extensions.Auth.Notifications.csproj" />'
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

if ($ServiceDefaults) {
    $programUsingLines += "using $Name.ServiceDefaults;"
}

if ($readinessModuleSpecs.Count -gt 0) {
    $programUsingLines += 'using Gma.Framework.Api.Production.EntityFrameworkCore;'
}

if ($hasAuth) {
    $programUsingLines += 'using Gma.Framework.Messaging.Infrastructure;'
    $programUsingLines += 'using Gma.Modules.Auth.Contracts;'
    $programUsingLines += 'using Gma.Modules.Auth.Providers.OpenIdConnect;'
}

if ($hasNotifications) {
    $programUsingLines += 'using Gma.Modules.Notifications.Adapters.Email;'
}

if ($hasAuth -and $hasNotifications) {
    $programUsingLines += 'using Gma.Extensions.Auth.Notifications;'
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

if ($ServiceDefaults) {
    $programLines += 'builder.AddServiceDefaults();'
}

$programLines += @('', '// module-scaffold:public-api-modules')

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

if ($hasAuth) {
    $programLines += 'builder.AddAuthOpenIdConnectProviders();'
}

if ($hasAuth -and $hasNotifications) {
    $programLines += 'builder.Services.AddAuthNotificationsExtension();'
}

if ($hasNotifications) {
    $programLines += 'builder.Services.AddNotificationEmailAdapter(builder.Configuration);'
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
    'app.MapModules();'
)

if ($ServiceDefaults) {
    $programLines += 'app.MapDefaultEndpoints();'
}
else {
    $programLines += 'app.MapGmaHealthEndpoints();'
}

$programLines += @('', 'app.Run();')

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
            SensitivePathPrefixes = @(
                '/api/auth/register',
                '/api/auth/login',
                '/api/auth/refresh',
                '/api/auth/browser',
                '/api/auth/password',
                '/api/auth/external',
                '/api/auth/email-verification'
            )
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
        ExternalExchangeLifetimeMinutes = 5
        ExternalLinkSessionFreshnessMinutes = 10
        EmailVerificationLifetimeMinutes = 1440
        EmailVerificationRequestCooldownSeconds = 60
        Retention = [ordered]@{
            Enabled = $false
            ExpiredExchangeHistoryHours = 24
            SessionHistoryDays = 365
            BatchSize = 500
            MaxBatchesPerCategoryPerCycle = 4
            IntervalMinutes = 60
        }
        RefreshTokens = [ordered]@{
            Pepper = ''
        }
        Jwt = [ordered]@{
            SigningKey = ''
            AccessTokenLifetimeMinutes = 15
        }
        OpenIdConnect = [ordered]@{
            Enabled = $false
            AllowedReturnUrls = @()
            Providers = [ordered]@{
                google = [ordered]@{
                    Enabled = $false
                    Authority = 'https://accounts.google.com'
                    ClientId = ''
                    ClientSecret = ''
                    Scopes = @('openid', 'email', 'profile')
                    EmailClaim = 'email'
                    EmailVerifiedClaim = 'email_verified'
                    TreatEmailAsVerified = $false
                }
                microsoft = [ordered]@{
                    Enabled = $false
                    Authority = 'https://login.microsoftonline.com/common/v2.0'
                    ClientId = ''
                    ClientSecret = ''
                    Scopes = @('openid', 'email', 'profile')
                    EmailClaim = 'email'
                    EmailVerifiedClaim = 'email_verified'
                    TreatEmailAsVerified = $false
                }
            }
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
        Delivery = [ordered]@{
            Enabled = $true
            BatchSize = 50
            MaxConcurrency = 8
            PollIntervalSeconds = 5
            LeaseSeconds = 60
            MaxAttempts = 8
            RetryBaseSeconds = 5
            RetryMaxMinutes = 30
            AttemptRetentionDays = 90
        }
        Retention = [ordered]@{
            Enabled = $false
            ReadHistoryDays = 90
            UnreadHistoryDays = 365
            BroadcastDays = 365
            BatchSize = 500
            MaxBatchesPerCategoryPerCycle = 4
            IntervalMinutes = 60
        }
        Adapters = [ordered]@{
            Email = [ordered]@{
                Enabled = $false
                ProviderName = 'email'
                SenderAddress = $null
                SenderName = $null
                SubjectPrefix = $null
            }
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

if ($hasAdminApiHost) {
    $adminApiProjectLines = @(
        '<Project Sdk="Microsoft.NET.Sdk.Web">',
        '  <ItemGroup>',
        '    <ProjectReference Include="$(GmaFrameworkRoot)Administration\Gma.Framework.Administration.Api\Gma.Framework.Administration.Api.csproj" />',
        '    <ProjectReference Include="$(GmaFrameworkRoot)Api\Gma.Framework.Api\Gma.Framework.Api.csproj" />',
        '    <ProjectReference Include="$(GmaFrameworkRoot)Api\Gma.Framework.Api.Production\Gma.Framework.Api.Production.csproj" />',
        '    <ProjectReference Include="$(GmaFrameworkRoot)Infrastructure\Gma.Framework.Infrastructure\Gma.Framework.Infrastructure.csproj" />',
        '    <ProjectReference Include="$(GmaFrameworkRoot)Modules\Gma.Framework.ModuleComposition\Gma.Framework.ModuleComposition.csproj" />'
    )
    if ($ServiceDefaults) {
        $adminApiProjectLines += "    <ProjectReference Include=`"..\..\ServiceDefaults\$Name.ServiceDefaults\$Name.ServiceDefaults.csproj`" />"
    }
    $adminApiProjectLines += @('  </ItemGroup>', '</Project>')

    Write-GmaTemplateFile (Join-Path $resolvedOutputPath "src\Hosts\$Name.Host.AdminApi\$Name.Host.AdminApi.csproj") $adminApiProjectLines

    $adminApiProgramLines = @(
        'using Gma.Framework.Administration.Api;',
        'using Gma.Framework.Api.Production;',
        'using Gma.Framework.Api.Security;',
        'using Gma.Framework.Infrastructure;',
        'using Gma.Framework.ModuleComposition;'
    )
    if ($ServiceDefaults) {
        $adminApiProgramLines += "using $Name.ServiceDefaults;"
    }
    $adminApiProgramLines += @(
        '',
        'WebApplicationBuilder builder = WebApplication.CreateBuilder(args);',
        '',
        'builder.Services.AddGmaAdministrationApi(builder.Configuration);',
        'builder.AddGmaInfrastructure();',
        'builder.Services.AddApiSecurityDefaults();',
        'builder.AddGmaProductionHttp();'
    )
    if ($ServiceDefaults) {
        $adminApiProgramLines += 'builder.AddServiceDefaults();'
    }
    $adminApiProgramLines += @(
        'builder.ValidateModuleComposition();',
        '',
        'WebApplication app = builder.Build();',
        'app.UseGmaProductionHttp();',
        'app.UseAuthentication();',
        'app.UseAuthorization();',
        'app.MapAdminApiModules();'
    )
    if ($ServiceDefaults) {
        $adminApiProgramLines += 'app.MapDefaultEndpoints();'
    }
    else {
        $adminApiProgramLines += 'app.MapGmaHealthEndpoints();'
    }
    $adminApiProgramLines += @('', 'app.Run();')

    Write-GmaTemplateFile (Join-Path $resolvedOutputPath "src\Hosts\$Name.Host.AdminApi\Program.cs") $adminApiProgramLines
    Write-GmaTemplateFile (Join-Path $resolvedOutputPath "src\Hosts\$Name.Host.AdminApi\appsettings.json") ($baseSettingsJson -split "`r?`n")
    Write-GmaTemplateFile (Join-Path $resolvedOutputPath "src\Hosts\$Name.Host.AdminApi\appsettings.Development.json") ($developmentSettingsJson -split "`r?`n")
}

if ($hasAdminCliHost) {
    Write-GmaTemplateFile (Join-Path $resolvedOutputPath "src\Hosts\$Name.Host.AdminCli\$Name.Host.AdminCli.csproj") @(
        '<Project Sdk="Microsoft.NET.Sdk">',
        '  <PropertyGroup>',
        '    <OutputType>Exe</OutputType>',
        '    <PackAsTool>true</PackAsTool>',
        "    <ToolCommandName>$((ConvertTo-GmaKebabCase -Value $Name))-admin</ToolCommandName>",
        "    <PackageId>$Name.AdminCli</PackageId>",
        '  </PropertyGroup>',
        '  <ItemGroup>',
        '    <PackageReference Include="Microsoft.Extensions.Hosting" />',
        '    <PackageReference Include="System.CommandLine" />',
        '    <ProjectReference Include="$(GmaFrameworkRoot)Administration\Gma.Framework.Administration.Cli\Gma.Framework.Administration.Cli.csproj" />',
        '    <ProjectReference Include="$(GmaFrameworkRoot)Infrastructure\Gma.Framework.Infrastructure\Gma.Framework.Infrastructure.csproj" />',
        '    <ProjectReference Include="$(GmaFrameworkRoot)Modules\Gma.Framework.ModuleComposition\Gma.Framework.ModuleComposition.csproj" />',
        '  </ItemGroup>',
        '</Project>'
    )

    Write-GmaTemplateFile (Join-Path $resolvedOutputPath "src\Hosts\$Name.Host.AdminCli\Program.cs") @(
        'using Gma.Framework.Administration.Cli;',
        'using Gma.Framework.Infrastructure;',
        'using Gma.Framework.ModuleComposition;',
        'using Microsoft.Extensions.DependencyInjection;',
        'using Microsoft.Extensions.Hosting;',
        'using System.CommandLine;',
        'using System.CommandLine.Parsing;',
        '',
        'HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);',
        'builder.Services.AddGmaAdministrationCli();',
        'builder.AddGmaInfrastructure();',
        'builder.ValidateModuleComposition();',
        '',
        'using IHost host = builder.Build();',
        'host.Services.ValidateAdminCliStartup();',
        'RootCommand rootCommand = host.Services.CreateAdminRootCommand();',
        'ParseResult parseResult = rootCommand.Parse(args);',
        'InvocationConfiguration invocation = new() { EnableDefaultExceptionHandler = false };',
        'return await parseResult.InvokeAsync(invocation, CancellationToken.None).ConfigureAwait(false);'
    )
}

if ($hasWorkerHost) {
    $workerProjectLines = @(
        '<Project Sdk="Microsoft.NET.Sdk">',
        '  <PropertyGroup>',
        '    <OutputType>Exe</OutputType>',
        '  </PropertyGroup>',
        '  <ItemGroup>',
        '    <PackageReference Include="Microsoft.Extensions.Hosting" />',
        '    <ProjectReference Include="$(GmaFrameworkRoot)Infrastructure\Gma.Framework.Infrastructure\Gma.Framework.Infrastructure.csproj" />',
        '    <ProjectReference Include="$(GmaFrameworkRoot)Modules\Gma.Framework.ModuleComposition\Gma.Framework.ModuleComposition.csproj" />'
    )
    if ($ServiceDefaults) {
        $workerProjectLines += "    <ProjectReference Include=`"..\..\ServiceDefaults\$Name.ServiceDefaults\$Name.ServiceDefaults.csproj`" />"
    }
    $workerProjectLines += @('  </ItemGroup>', '</Project>')

    Write-GmaTemplateFile (Join-Path $resolvedOutputPath "src\Hosts\$Name.Host.Worker\$Name.Host.Worker.csproj") $workerProjectLines
    $workerProgramLines = @(
        'using Gma.Framework.Infrastructure;',
        'using Gma.Framework.ModuleComposition;',
        'using Microsoft.Extensions.Hosting;'
    )
    if ($ServiceDefaults) {
        $workerProgramLines += "using $Name.ServiceDefaults;"
    }
    $workerProgramLines += @(
        '',
        'HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);',
        'builder.AddGmaInfrastructure();'
    )
    if ($ServiceDefaults) {
        $workerProgramLines += 'builder.AddServiceDefaults();'
    }
    $workerProgramLines += @(
        'builder.ValidateModuleComposition();',
        'using IHost host = builder.Build();',
        'await host.RunAsync().ConfigureAwait(false);'
    )

    Write-GmaTemplateFile (Join-Path $resolvedOutputPath "src\Hosts\$Name.Host.Worker\Program.cs") $workerProgramLines
    Write-GmaTemplateFile (Join-Path $resolvedOutputPath "src\Hosts\$Name.Host.Worker\appsettings.json") ($baseSettingsJson -split "`r?`n")
    Write-GmaTemplateFile (Join-Path $resolvedOutputPath "src\Hosts\$Name.Host.Worker\appsettings.Development.json") ($developmentSettingsJson -split "`r?`n")
}

if ($hasAspireHost) {
    $aspireProjectLines = @(
        '<Project Sdk="Aspire.AppHost.Sdk/13.4.2">',
        '  <PropertyGroup>',
        '    <OutputType>Exe</OutputType>',
        '    <IsAspireHost>true</IsAspireHost>',
        '  </PropertyGroup>',
        '  <ItemGroup>',
        '    <PackageReference Include="Aspire.Hosting.AppHost" />',
        '    <PackageReference Include="MessagePack" />',
        "    <ProjectReference Include=`"..\$Name.Host.Api\$Name.Host.Api.csproj`" />"
    )
    if ($hasAdminApiHost) {
        $aspireProjectLines += "    <ProjectReference Include=`"..\$Name.Host.AdminApi\$Name.Host.AdminApi.csproj`" />"
    }
    if ($hasWorkerHost) {
        $aspireProjectLines += "    <ProjectReference Include=`"..\$Name.Host.Worker\$Name.Host.Worker.csproj`" />"
    }
    $aspireProjectLines += @('  </ItemGroup>', '</Project>')

    Write-GmaTemplateFile (Join-Path $resolvedOutputPath "src\Hosts\$Name.AppHost\$Name.AppHost.csproj") $aspireProjectLines
    $aspireProgramLines = @(
        'var builder = DistributedApplication.CreateBuilder(args);',
        "builder.AddProject<Projects.$($Name.Replace('.', '_'))_Host_Api>(`"api`");"
    )
    if ($hasAdminApiHost) {
        $aspireProgramLines += "builder.AddProject<Projects.$($Name.Replace('.', '_'))_Host_AdminApi>(`"admin-api`");"
    }
    if ($hasWorkerHost) {
        $aspireProgramLines += "builder.AddProject<Projects.$($Name.Replace('.', '_'))_Host_Worker>(`"worker`");"
    }
    $aspireProgramLines += 'builder.Build().Run();'
    Write-GmaTemplateFile (Join-Path $resolvedOutputPath "src\Hosts\$Name.AppHost\Program.cs") $aspireProgramLines
}

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
    'using Microsoft.AspNetCore.Hosting;',
    'using Microsoft.AspNetCore.Mvc.Testing;',
    'using Xunit;',
    '',
    '[Trait("Category", "Architecture")]',
    'public sealed class HostStartupTests',
    '{',
    '    [Fact]',
    '    public async Task GeneratedHostBuildsAndMapsLiveness()',
    '    {',
    '        await using WebApplicationFactory<global::Program> factory = new WebApplicationFactory<global::Program>()',
    '            .WithWebHostBuilder(builder => builder',
    '                .UseSetting("ConnectionStrings:SqlServer", "Server=localhost;Database=generated;User Id=sa;Password=Generated_test_123!;TrustServerCertificate=true")',
    '                .UseSetting("ConnectionStrings:PostgreSql", "Host=localhost;Database=generated;Username=generated;Password=Generated_test_123!"));',
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
    '        string[] allowedDomainDependencies = ["Gma.Framework.Domain", "Gma.Framework.Results"];',
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
    '                    .Where(reference =>',
    '                        !reference.StartsWith(Path.Combine(repositoryRoot, "src", "Shared"), StringComparison.OrdinalIgnoreCase) &&',
    '                        !allowedDomainDependencies.Contains(Path.GetFileNameWithoutExtension(reference), StringComparer.Ordinal))',
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
    '    [Fact]',
    '    public void AppModuleProjectsUseApplicationPrefix()',
    '    {',
    '        string repositoryRoot = FindRepositoryRoot();',
    '        string modulesRoot = Path.Combine(repositoryRoot, "src", "Modules");',
    '        if (!Directory.Exists(modulesRoot))',
    '        {',
    '            return;',
    '        }',
    '',
    "        string expectedPrefix = `"$Name.Modules.`";",
    '        string[] offenders = [.. Directory.EnumerateFiles(modulesRoot, "*.csproj", SearchOption.AllDirectories)',
    '            .Select(Path.GetFileNameWithoutExtension)',
    '            .OfType<string>()',
    '            .Where(projectName => !projectName!.StartsWith(expectedPrefix, StringComparison.Ordinal))];',
    '',
    '        Assert.Empty(offenders);',
    '    }',
    '',
    '    [Fact]',
    '    public void SolutionListsEveryApplicationProject()',
    '    {',
    '        string repositoryRoot = FindRepositoryRoot();',
    "        XDocument solution = XDocument.Load(Path.Combine(repositoryRoot, `"$Name.slnx`"));",
    '        string[] listed = [.. solution.Descendants("Project")',
    '            .Select(project => ((string)project.Attribute("Path")!).Replace(''/'', Path.DirectorySeparatorChar))',
    '            .Where(path => path.StartsWith("src" + Path.DirectorySeparatorChar, StringComparison.Ordinal) ||',
    '                path.StartsWith("tests" + Path.DirectorySeparatorChar, StringComparison.Ordinal))',
    '            .Order(StringComparer.OrdinalIgnoreCase)];',
    '        string[] roots = ["src", "tests"];',
    '        string[] actual = [.. roots',
    '            .Select(root => Path.Combine(repositoryRoot, root))',
    '            .Where(Directory.Exists)',
    '            .SelectMany(root => Directory.EnumerateFiles(root, "*.csproj", SearchOption.AllDirectories))',
    '            .Where(path => !path.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))',
    '            .Select(path => Path.GetRelativePath(repositoryRoot, path))',
    '            .Order(StringComparer.OrdinalIgnoreCase)];',
    '',
    '        Assert.Equal(actual, listed);',
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

$solutionTool = Join-GmaPath 'gma/framework/eng/sync-solution.ps1'
if (-not (Test-Path -LiteralPath $solutionTool -PathType Leaf)) {
    throw 'The mounted GMA framework does not provide eng/sync-solution.ps1.'
}

& $solutionTool -RepositoryRoot $resolvedOutputPath -Solution "$Name.slnx"

Write-Host "Created GMA app shell: $resolvedOutputPath"
Write-Host 'Next: add or mount GMA source packages, initialize them, bootstrap source roots, sync the solution, and run eng/gma-validate.ps1.'

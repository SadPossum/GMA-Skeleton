[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[A-Z][A-Za-z0-9]*$')]
    [string] $Name,

    [Parameter(Mandatory = $true)]
    [string] $OutputPath,

    [switch] $Force,

    [switch] $UseLocalStage8Candidates,

    [string[]] $Modules = @(),

    [string] $StageRoot = '.agents\stage8d\2'
)

. (Join-Path $PSScriptRoot 'common.ps1')

$script:GmaKnownModuleSpecs = @(
    [pscustomobject] @{
        Alias = 'administration'
        Repository = 'GMA-Module-Administration'
        SourceRootProperty = 'GmaModuleAdministrationRoot'
        PublicApiProject = $null
        PublicApiNamespace = $null
        PublicApiModuleType = $null
    },
    [pscustomobject] @{
        Alias = 'auth'
        Repository = 'GMA-Module-Auth'
        SourceRootProperty = 'GmaModuleAuthRoot'
        PublicApiProject = 'Gma.Modules.Auth.Api'
        PublicApiNamespace = 'Gma.Modules.Auth.Api'
        PublicApiModuleType = 'AuthModule'
    },
    [pscustomobject] @{
        Alias = 'files'
        Repository = 'GMA-Module-Files'
        SourceRootProperty = 'GmaModuleFilesRoot'
        PublicApiProject = 'Gma.Modules.Files.Api'
        PublicApiNamespace = 'Gma.Modules.Files.Api'
        PublicApiModuleType = 'FilesModule'
    },
    [pscustomobject] @{
        Alias = 'notifications'
        Repository = 'GMA-Module-Notifications'
        SourceRootProperty = 'GmaModuleNotificationsRoot'
        PublicApiProject = 'Gma.Modules.Notifications.Api'
        PublicApiNamespace = 'Gma.Modules.Notifications.Api'
        PublicApiModuleType = 'NotificationsModule'
    },
    [pscustomobject] @{
        Alias = 'task-runtime'
        Repository = 'GMA-Module-Task-Runtime'
        SourceRootProperty = 'GmaModuleTaskRuntimeRoot'
        PublicApiProject = $null
        PublicApiNamespace = $null
        PublicApiModuleType = $null
    },
    [pscustomobject] @{
        Alias = 'tenancy'
        Repository = 'GMA-Module-Tenancy'
        SourceRootProperty = 'GmaModuleTenancyRoot'
        PublicApiProject = 'Gma.Modules.Tenancy.Api'
        PublicApiNamespace = 'Gma.Modules.Tenancy.Api'
        PublicApiModuleType = 'TenancyModule'
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

function Add-GmaTemplateJunction {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Path,

        [Parameter(Mandatory = $true)]
        [string] $Target
    )

    if (-not (Test-Path -LiteralPath $Target -PathType Container)) {
        throw "Cannot mount local Stage 8 candidate because target does not exist: $Target"
    }

    if (Test-Path -LiteralPath $Path) {
        if (-not $Force) {
            throw "Refusing to replace existing source mount '$Path'. Rerun with -Force after checking the path."
        }

        $item = Get-Item -LiteralPath $Path -Force
        if (-not ($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint)) {
            throw "Refusing to replace non-link path '$Path'. Remove it manually if it should become a local source mount."
        }

        if ($PSCmdlet.ShouldProcess($Path, 'Remove existing source mount')) {
            Remove-Item -LiteralPath $Path -Force
        }
    }

    $parent = Split-Path -Parent $Path
    if (-not (Test-Path -LiteralPath $parent -PathType Container)) {
        New-Item -ItemType Directory -Force -Path $parent | Out-Null
    }

    if ($PSCmdlet.ShouldProcess($Path, "Create junction to $Target")) {
        New-Item -ItemType Junction -Path $Path -Target $Target | Out-Null
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
        '    return Join-Path $script:RepositoryRoot $Path',
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
        'Write-GmaSourceRootsFile -Path (Join-GmaPath ''gma\framework\Gma.SourceRoots.props'') -Lines $frameworkLines -Description ''framework source-root configuration''',
        '',
        'foreach ($moduleAlias in $selectedModuleAliases) {',
        '    $moduleRoot = Join-GmaPath "gma\modules\$moduleAlias"',
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
        '    Invoke-GmaDotNet -Arguments @(''restore'', $solutionPath)',
        '}',
        '',
        'if (-not $SkipBuild) {',
        '    Invoke-GmaDotNet -Arguments @(''build'', $solutionPath, ''--no-restore'', ''-m:1'')',
        '}'
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
        '    runs-on: windows-latest',
        '    steps:',
        '      - name: Checkout app',
        '        uses: actions/checkout@v4',
        '        with:',
        '          fetch-depth: 0',
        '          submodules: recursive',
        '          token: ${{ secrets.GMA_CI_TOKEN || github.token }}',
        '',
        '      - name: Setup .NET',
        '        uses: actions/setup-dotnet@v4',
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
        "        run: dotnet build $Name.slnx --no-restore -m:1"
    )
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

Write-GmaTemplateFile (Join-Path $resolvedOutputPath 'Directory.Packages.props') @(
    '<Project>',
    '  <ItemGroup />',
    '</Project>'
)

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
$publicApiModuleText = if ($publicApiModuleSpecs.Count -eq 0) {
    'none'
}
else {
    ($publicApiModuleSpecs | ForEach-Object { $_.Alias }) -join ', '
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
    '  </Folder>',
    '  <Folder Name="/.github/workflows/">',
    '    <File Path=".github/workflows/validate.yml" />',
    '  </Folder>',
    '  <Folder Name="/Solution Items/">',
    '    <File Path=".gitignore" />',
    '    <File Path="Directory.Build.props" />',
    '    <File Path="Directory.Packages.props" />',
    '    <File Path="global.json" />',
    '    <File Path="Gma.SourceRoots.props.example" />',
    '    <File Path="README.md" />',
    '  </Folder>',
    '  <Folder Name="/src/">',
    "    <Project Path=`"src/$Name.Host.Api/$Name.Host.Api.csproj`" />",
    "    <Project Path=`"src/$Name.SharedKernel/$Name.SharedKernel.csproj`" />",
    '  </Folder>',
    '</Solution>'
)

Write-GmaTemplateFile (Join-Path $resolvedOutputPath 'README.md') @(
    "# $Name",
    '',
    'This app is generated as a source-first GMA composition shell.',
    '',
    "- Keep app-owned shared code under ``src/$Name.SharedKernel`` or a similar app namespace.",
    '- Mount GMA framework at `gma/framework` and reusable GMA modules under `gma/modules/<alias>`.',
    "- Selected reusable GMA modules for this shell: $selectedModuleText.",
    "- Public API modules composed by `src/$Name.Host.Api`: $publicApiModuleText.",
    '- Admin CLI/API and worker-only module surfaces stay app-specific; add those hosts explicitly when the app needs them.',
    '- Runtime provider choices such as authentication schemes, storage adapters, messaging, and production connection strings remain app-owned composition work.',
    '- Run `eng/gma-bootstrap.ps1` after cloning submodules or mounting local source checkouts.',
    '- Run `eng/gma-update.ps1 -Init` after adding real GMA submodules.',
    '- Work on GMA fixes in the source checkout, push a GMA branch/PR, then update the app submodule pointer deliberately.',
    '- Set `GMA_CI_TOKEN` in GitHub Actions when private GMA submodules need cross-repository read access.'
)

Write-GmaGeneratedBootstrap -Root $resolvedOutputPath -ModuleSpecs $selectedModuleSpecArray
Write-GmaGeneratedWorkflow $resolvedOutputPath

Write-GmaTemplateFile (Join-Path $resolvedOutputPath "src\$Name.SharedKernel\$Name.SharedKernel.csproj") @(
    '<Project Sdk="Microsoft.NET.Sdk" />'
)

Write-GmaTemplateFile (Join-Path $resolvedOutputPath "src\$Name.SharedKernel\ApplicationAssemblyMarker.cs") @(
    "namespace $Name.SharedKernel;",
    '',
    'public static class ApplicationAssemblyMarker',
    '{',
    '}'
)

$hostApiProjectLines = @(
    '<Project Sdk="Microsoft.NET.Sdk.Web">',
    '  <ItemGroup>',
    "    <ProjectReference Include=`"..\$Name.SharedKernel\$Name.SharedKernel.csproj`" />",
    '    <ProjectReference Include="$(GmaFrameworkRoot)Api\Gma.Framework.Api\Gma.Framework.Api.csproj" />',
    '    <ProjectReference Include="$(GmaFrameworkRoot)Infrastructure\Gma.Framework.Infrastructure\Gma.Framework.Infrastructure.csproj" />',
    '    <ProjectReference Include="$(GmaFrameworkRoot)Modules\Gma.Framework.ModuleComposition\Gma.Framework.ModuleComposition.csproj" />'
)

foreach ($moduleSpec in $publicApiModuleSpecs) {
    $sourceRootPropertyReference = '$(' + $moduleSpec.SourceRootProperty + ')'
    $hostApiProjectLines += "    <ProjectReference Include=`"$sourceRootPropertyReference$($moduleSpec.PublicApiProject)\$($moduleSpec.PublicApiProject).csproj`" />"
}

$hostApiProjectLines += @(
    '  </ItemGroup>',
    '</Project>'
)

Write-GmaTemplateFile (Join-Path $resolvedOutputPath "src\$Name.Host.Api\$Name.Host.Api.csproj") $hostApiProjectLines

$programUsingLines = @(
    'using Gma.Framework.Api.Modules;',
    'using Gma.Framework.Api.Security;',
    'using Gma.Framework.Infrastructure;',
    'using Gma.Framework.ModuleComposition;'
)

if ($selectedModuleSpecArray | Where-Object { $_.Alias -eq 'auth' }) {
    $programUsingLines += 'using Gma.Modules.Auth.Contracts;'
}

foreach ($moduleSpec in $publicApiModuleSpecs) {
    $programUsingLines += "using $($moduleSpec.PublicApiNamespace);"
}

$programUsingLines = @($programUsingLines | Select-Object -Unique)

$moduleRegistrationLines = New-Object 'System.Collections.Generic.List[string]'
if ($selectedModuleSpecArray | Where-Object { $_.Alias -eq 'tenancy' }) {
    $moduleRegistrationLines.Add('builder.AddModule<TenancyModule>();')
}

if ($selectedModuleSpecArray | Where-Object { $_.Alias -eq 'auth' }) {
    if ($selectedModuleSpecArray | Where-Object { $_.Alias -eq 'tenancy' }) {
        $moduleRegistrationLines.Add('builder.AddAuthModule(AuthProfile.TenantScoped());')
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
    'builder.Services.AddApiSecurityDefaults();'
)

if ($moduleRegistrationLines.Count -gt 0) {
    $programLines += ''
    $programLines += $moduleRegistrationLines.ToArray()
}

$programLines += @(
    '',
    'builder.ValidateModuleComposition();',
    '',
    'WebApplication app = builder.Build();',
    '',
    'app.UseAuthentication();',
    'app.UseAuthorization();',
    '',
    'app.MapGet("/", () => Results.Ok(new',
    '{',
    "    Application = `"$Name`"",
    '}));',
    '',
    'app.MapModules();',
    '',
    'app.Run();'
)

Write-GmaTemplateFile (Join-Path $resolvedOutputPath "src\$Name.Host.Api\Program.cs") $programLines

New-Item -ItemType Directory -Force -Path (Join-Path $resolvedOutputPath 'gma\modules') | Out-Null
New-Item -ItemType File -Force -Path (Join-Path $resolvedOutputPath 'gma\.gitkeep') | Out-Null
New-Item -ItemType File -Force -Path (Join-Path $resolvedOutputPath 'gma\modules\.gitkeep') | Out-Null

if (-not $UseLocalStage8Candidates) {
    New-Item -ItemType Directory -Force -Path (Join-Path $resolvedOutputPath 'gma\framework') | Out-Null
}

if ($UseLocalStage8Candidates) {
    $resolvedStageRoot = Resolve-GmaTemplatePath $StageRoot
    Add-GmaTemplateJunction `
        -Path (Join-Path $resolvedOutputPath 'gma\framework') `
        -Target (Join-Path $resolvedStageRoot 'repos\GMA-Framework')

    foreach ($moduleSpec in $selectedModuleSpecArray) {
        Add-GmaTemplateJunction `
            -Path (Join-Path $resolvedOutputPath "gma\modules\$($moduleSpec.Alias)") `
            -Target (Join-Path $resolvedStageRoot "repos\$($moduleSpec.Repository)")
    }
}

Write-Host "Created GMA app shell: $resolvedOutputPath"
Write-Host 'Next: run eng/gma-bootstrap.ps1, then eng/gma-validate.ps1.'

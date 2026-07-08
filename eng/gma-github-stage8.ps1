[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [string] $Owner = 'SadPossum',

    [ValidateSet('private', 'public', 'internal')]
    [string] $Visibility = 'private',

    [string] $RemoteHostAlias = 'github.com-private',

    [string] $StageRoot = '.agents\stage8d\2',

    [string] $DefaultBranch = 'dev',

    [string] $GitUserName = 'SadPossum',

    [string] $GitUserEmail = '258739@bk.ru',

    [string[]] $RequiredStatusChecks = @(),

    [switch] $InitializeCandidates,

    [switch] $AuditRepositories,

    [switch] $CreateRepositories,

    [switch] $PushCandidates,

    [switch] $ConfigureRepositories,

    [switch] $ProtectBranches,

    [switch] $SkipSkeleton,

    [switch] $AllowDirtyCandidates,

    [switch] $AllowUnconvertedSkeletonPush,

    [switch] $SkipDivergedMain
)

. (Join-Path $PSScriptRoot 'common.ps1')

function Resolve-GmaLocalPath {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Path
    )

    if ([System.IO.Path]::IsPathRooted($Path)) {
        return $Path
    }

    return Join-GmaPath $Path
}

function Get-GmaStage8RepositoryPlans {
    $stageRootPath = Resolve-GmaLocalPath $StageRoot

    return @(
        [pscustomobject] @{
            Name = 'gma-framework'
            LocalPath = Join-Path $stageRootPath 'repos\gma-framework'
            Solution = 'Gma.Framework.slnx'
            Description = 'Reusable GMA framework packages for modular monolith applications.'
            Topics = @('gma', 'dotnet', 'framework', 'modular-monolith')
        },
        [pscustomobject] @{
            Name = 'gma-module-administration'
            LocalPath = Join-Path $stageRootPath 'repos\gma-module-administration'
            Solution = 'Gma.Modules.Administration.slnx'
            Description = 'Reusable GMA Administration module with RBAC, audit, CLI, and admin API surfaces.'
            Topics = @('gma', 'dotnet', 'module', 'administration')
        },
        [pscustomobject] @{
            Name = 'gma-module-auth'
            LocalPath = Join-Path $stageRootPath 'repos\gma-module-auth'
            Solution = 'Gma.Modules.Auth.slnx'
            Description = 'Reusable GMA Auth module for first-party member authentication and administration.'
            Topics = @('gma', 'dotnet', 'module', 'auth')
        },
        [pscustomobject] @{
            Name = 'gma-module-files'
            LocalPath = Join-Path $stageRootPath 'repos\gma-module-files'
            Solution = 'Gma.Modules.Files.slnx'
            Description = 'Reusable GMA Files module front door for optional file-management workflows.'
            Topics = @('gma', 'dotnet', 'module', 'files')
        },
        [pscustomobject] @{
            Name = 'gma-module-notifications'
            LocalPath = Join-Path $stageRootPath 'repos\gma-module-notifications'
            Solution = 'Gma.Modules.Notifications.slnx'
            Description = 'Reusable GMA Notifications module for persisted notifications and streaming front doors.'
            Topics = @('gma', 'dotnet', 'module', 'notifications')
        },
        [pscustomobject] @{
            Name = 'gma-module-task-runtime'
            LocalPath = Join-Path $stageRootPath 'repos\gma-module-task-runtime'
            Solution = 'Gma.Modules.TaskRuntime.slnx'
            Description = 'Reusable GMA TaskRuntime module for persisted task runs and operator control.'
            Topics = @('gma', 'dotnet', 'module', 'tasks')
        },
        [pscustomobject] @{
            Name = 'gma-module-tenancy'
            LocalPath = Join-Path $stageRootPath 'repos\gma-module-tenancy'
            Solution = 'Gma.Modules.Tenancy.slnx'
            Description = 'Reusable GMA Tenancy module for optional tenant front doors and contracts.'
            Topics = @('gma', 'dotnet', 'module', 'tenancy')
        },
        [pscustomobject] @{
            Name = 'gma-skeleton'
            LocalPath = Join-Path $stageRootPath 'skeleton'
            Solution = 'GenericModularApi.slnx'
            Description = 'GMA skeleton and composition repository for source-first modular monolith applications.'
            Topics = @('gma', 'dotnet', 'template', 'modular-monolith')
        }
    )
}

function Get-GmaSelectedStage8RepositoryPlans {
    $plans = @(Get-GmaStage8RepositoryPlans)
    if ($SkipSkeleton) {
        $plans = @($plans | Where-Object { $_.Name -ne 'gma-skeleton' })
    }

    return $plans
}

function Get-GmaStage8ModuleSourceRootSpecs {
    return @(
        [pscustomobject] @{
            Alias = 'administration'
            Repository = 'gma-module-administration'
            Property = 'GmaModuleAdministrationRoot'
        },
        [pscustomobject] @{
            Alias = 'auth'
            Repository = 'gma-module-auth'
            Property = 'GmaModuleAuthRoot'
        },
        [pscustomobject] @{
            Alias = 'files'
            Repository = 'gma-module-files'
            Property = 'GmaModuleFilesRoot'
        },
        [pscustomobject] @{
            Alias = 'notifications'
            Repository = 'gma-module-notifications'
            Property = 'GmaModuleNotificationsRoot'
        },
        [pscustomobject] @{
            Alias = 'task-runtime'
            Repository = 'gma-module-task-runtime'
            Property = 'GmaModuleTaskRuntimeRoot'
        },
        [pscustomobject] @{
            Alias = 'tenancy'
            Repository = 'gma-module-tenancy'
            Property = 'GmaModuleTenancyRoot'
        }
    )
}

function Get-GmaStage8RootPackageSolutionFiles {
    return @(
        'Gma.Framework.slnx',
        'Gma.Modules.Administration.slnx',
        'Gma.Modules.Auth.slnx',
        'Gma.Modules.Files.slnx',
        'Gma.Modules.Notifications.slnx',
        'Gma.Modules.TaskRuntime.slnx',
        'Gma.Modules.Tenancy.slnx'
    )
}

function Get-GmaStage8SkeletonSubmoduleMountPaths {
    $paths = @('gma/framework')
    foreach ($moduleSpec in Get-GmaStage8ModuleSourceRootSpecs) {
        $paths += "gma/modules/$($moduleSpec.Alias)"
    }

    return $paths
}

function Set-GmaUtf8FileLines {
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

    if (Test-Path -LiteralPath $Path) {
        $existingLines = [System.IO.File]::ReadAllLines($Path)
        if ([string]::Join("`n", $existingLines) -eq [string]::Join("`n", $Lines)) {
            return
        }
    }

    [System.IO.File]::WriteAllLines($Path, $Lines, [System.Text.UTF8Encoding]::new($false))
}

function Get-GmaCandidateSourceRootExampleLines {
    param(
        [Parameter(Mandatory = $true)]
        [object] $RepositoryPlan
    )

    if ($RepositoryPlan.Name -eq 'gma-framework') {
        return @(
            '<Project>',
            '  <PropertyGroup>',
            '    <GmaFrameworkRoot>$(MSBuildThisFileDirectory)src\</GmaFrameworkRoot>',
            '  </PropertyGroup>',
            '</Project>'
        )
    }

    if ($RepositoryPlan.Name -eq 'gma-skeleton') {
        $lines = @(
            '<Project>',
            '  <PropertyGroup>',
            '    <GmaFrameworkRoot>$(MSBuildThisFileDirectory)gma\framework\src\</GmaFrameworkRoot>',
            '    <GmaModulesRoot>$(MSBuildThisFileDirectory)gma\modules\</GmaModulesRoot>'
        )

        foreach ($moduleSpec in Get-GmaStage8ModuleSourceRootSpecs) {
            $lines += "    <$($moduleSpec.Property)>`$(GmaModulesRoot)$($moduleSpec.Alias)\src\</$($moduleSpec.Property)>"
        }

        $lines += @(
            '    <GmaModuleCatalogRoot>$(GmaRepositoryRoot)src\Modules\Catalog\</GmaModuleCatalogRoot>',
            '    <GmaModuleOrderingRoot>$(GmaRepositoryRoot)src\Modules\Ordering\</GmaModuleOrderingRoot>',
            '    <GmaModuleTaskSamplesRoot>$(GmaRepositoryRoot)src\Modules\TaskSamples\</GmaModuleTaskSamplesRoot>',
            '  </PropertyGroup>',
            '</Project>'
        )

        return $lines
    }

    $moduleLines = @(
        '<Project>',
        '  <PropertyGroup>',
        '    <GmaFrameworkRoot>$(MSBuildThisFileDirectory)..\gma-framework\src\</GmaFrameworkRoot>',
        '    <GmaModulesRoot>$(MSBuildThisFileDirectory)..\</GmaModulesRoot>'
    )

    foreach ($moduleSpec in Get-GmaStage8ModuleSourceRootSpecs) {
        $moduleLines += "    <$($moduleSpec.Property)>`$(GmaModulesRoot)$($moduleSpec.Repository)\src\</$($moduleSpec.Property)>"
    }

    $moduleLines += @(
        '  </PropertyGroup>',
        '</Project>'
    )

    return $moduleLines
}

function Get-GmaCandidateSourceRootBootstrapLines {
    return @(
        '[CmdletBinding(SupportsShouldProcess = $true)]',
        'param([switch] $Force)',
        '',
        'Set-StrictMode -Version Latest',
        '$ErrorActionPreference = ''Stop''',
        '',
        '$repositoryRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot ''..'')).Path',
        '$source = Join-Path $repositoryRoot ''Gma.SourceRoots.props.example''',
        '$target = Join-Path $repositoryRoot ''Gma.SourceRoots.props''',
        '',
        'if (-not (Test-Path -LiteralPath $source)) {',
        '    throw "Missing source-root example file: $source"',
        '}',
        '',
        '$sourceLines = [System.IO.File]::ReadAllLines($source)',
        'if (Test-Path -LiteralPath $target) {',
        '    $targetLines = [System.IO.File]::ReadAllLines($target)',
        '    if ([string]::Join("`n", $sourceLines) -eq [string]::Join("`n", $targetLines)) {',
        '        Write-Host "Gma.SourceRoots.props already matches the example."',
        '        return',
        '    }',
        '',
        '    if (-not $Force) {',
        '        throw "Gma.SourceRoots.props already exists with different contents. Use -Force to refresh it from the example."',
        '    }',
        '}',
        '',
        '$action = if (Test-Path -LiteralPath $target) { ''Overwrite'' } else { ''Create'' }',
        'if ($PSCmdlet.ShouldProcess($target, "$action local source-root configuration")) {',
        '    [System.IO.File]::WriteAllLines($target, $sourceLines, [System.Text.UTF8Encoding]::new($false))',
        '    Write-Host "$action local source-root configuration: $target"',
        '}'
    )
}

function Get-GmaCandidateValidationWorkflowLines {
    param(
        [Parameter(Mandatory = $true)]
        [object] $RepositoryPlan
    )

    $checkoutToken = '${{ secrets.GMA_CI_TOKEN || github.token }}'

    if ($RepositoryPlan.Name -eq 'gma-framework') {
        return @(
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
            '      - name: Checkout framework',
            '        uses: actions/checkout@v4',
            '        with:',
            '          fetch-depth: 0',
            '',
            '      - name: Setup .NET',
            '        uses: actions/setup-dotnet@v4',
            '        with:',
            '          dotnet-version: 10.0.x',
            '',
            '      - name: Bootstrap source roots',
            '        shell: pwsh',
            '        run: ./eng/bootstrap-source-roots.ps1 -Force',
            '',
            '      - name: Restore',
            ('        run: dotnet restore ' + $RepositoryPlan.Solution),
            '',
            '      - name: Build',
            ('        run: dotnet build ' + $RepositoryPlan.Solution + ' --no-restore -m:1'),
            '',
            '      - name: Test',
            ('        run: dotnet test ' + $RepositoryPlan.Solution + ' --no-build --logger "console;verbosity=minimal"')
        )
    }

    if ($RepositoryPlan.Name -eq 'gma-skeleton') {
        return @(
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
            '      - name: Checkout skeleton',
            '        uses: actions/checkout@v4',
            '        with:',
            '          fetch-depth: 0',
            '          submodules: recursive',
            "          token: $checkoutToken",
            '',
            '      - name: Setup .NET',
            '        uses: actions/setup-dotnet@v4',
            '        with:',
            '          dotnet-version: 10.0.x',
            '',
            '      - name: Bootstrap source roots',
            '        shell: pwsh',
            '        run: ./eng/bootstrap-source-roots.ps1 -Force',
            '',
            '      - name: Restore',
            ('        run: dotnet restore ' + $RepositoryPlan.Solution),
            '',
            '      - name: Build',
            ('        run: dotnet build ' + $RepositoryPlan.Solution + ' --no-restore -m:1'),
            '',
            '      - name: Test',
            ('        run: dotnet test ' + $RepositoryPlan.Solution + ' --no-build --logger "console;verbosity=minimal"')
        )
    }

    return @(
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
        '      - name: Checkout module',
        '        uses: actions/checkout@v4',
        '        with:',
        "          path: $($RepositoryPlan.Name)",
        '          fetch-depth: 0',
        '',
        '      - name: Checkout framework',
        '        uses: actions/checkout@v4',
        '        with:',
        "          repository: $Owner/gma-framework",
        '          path: gma-framework',
        "          token: $checkoutToken",
        '          fetch-depth: 0',
        '',
        '      - name: Setup .NET',
        '        uses: actions/setup-dotnet@v4',
        '        with:',
        '          dotnet-version: 10.0.x',
        '',
        '      - name: Bootstrap source roots',
        '        shell: pwsh',
        "        working-directory: $($RepositoryPlan.Name)",
        '        run: ./eng/bootstrap-source-roots.ps1 -Force',
        '',
        '      - name: Restore',
        "        working-directory: $($RepositoryPlan.Name)",
        ('        run: dotnet restore ' + $RepositoryPlan.Solution),
        '',
        '      - name: Build',
        "        working-directory: $($RepositoryPlan.Name)",
        ('        run: dotnet build ' + $RepositoryPlan.Solution + ' --no-restore -m:1'),
        '',
        '      - name: Test',
        "        working-directory: $($RepositoryPlan.Name)",
        ('        run: dotnet test ' + $RepositoryPlan.Solution + ' --no-build --logger "console;verbosity=minimal"')
    )
}

function Assert-GmaGithubCliReady {
    if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
        throw 'GitHub CLI is required for Stage 8 repository setup. Install gh, run gh auth login, then rerun this script.'
    }

    & gh auth status
    if ($LASTEXITCODE -ne 0) {
        throw 'GitHub CLI is installed but not authenticated. Run gh auth login or set GH_TOKEN/GITHUB_TOKEN.'
    }
}

function Invoke-GmaGithubCli {
    param(
        [Parameter(Mandatory = $true)]
        [string[]] $Arguments,

        [Parameter(Mandatory = $true)]
        [string] $Target,

        [Parameter(Mandatory = $true)]
        [string] $Action
    )

    if ($PSCmdlet.ShouldProcess($Target, $Action)) {
        & gh @Arguments
        if ($LASTEXITCODE -ne 0) {
            throw "GitHub CLI command failed while running '$Action' for '$Target'."
        }
    }
}

function Invoke-GmaQuietNativeCommand {
    param(
        [Parameter(Mandatory = $true)]
        [string] $FilePath,

        [Parameter(Mandatory = $true)]
        [string[]] $Arguments
    )

    $previousErrorActionPreference = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        & $FilePath @Arguments *> $null
        return $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $previousErrorActionPreference
    }
}

function Invoke-GmaNativeOutput {
    param(
        [Parameter(Mandatory = $true)]
        [string] $FilePath,

        [Parameter(Mandatory = $true)]
        [string[]] $Arguments
    )

    $previousErrorActionPreference = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        $output = & $FilePath @Arguments 2>$null
        return [pscustomobject] @{
            ExitCode = $LASTEXITCODE
            Output = @($output)
        }
    }
    finally {
        $ErrorActionPreference = $previousErrorActionPreference
    }
}

function Get-GmaStage8SshUrl {
    param(
        [Parameter(Mandatory = $true)]
        [object] $RepositoryPlan
    )

    return "git@${RemoteHostAlias}:$Owner/$($RepositoryPlan.Name).git"
}

function Get-GmaRemoteRepositoryAudit {
    param(
        [Parameter(Mandatory = $true)]
        [object] $RepositoryPlan
    )

    $sshUrl = Get-GmaStage8SshUrl $RepositoryPlan
    $heads = Invoke-GmaNativeOutput `
        -FilePath 'git' `
        -Arguments @('ls-remote', '--heads', $sshUrl)

    if ($heads.ExitCode -ne 0) {
        return [pscustomobject] @{
            Repository = "$Owner/$($RepositoryPlan.Name)"
            Reachable = $false
            Branches = ''
            CandidateReady = Test-GmaCandidateOwnsGitRepository $RepositoryPlan.LocalPath
            SkeletonSubmodulesReady = if ($RepositoryPlan.Name -eq 'gma-skeleton') { Test-GmaSkeletonCandidateHasSubmoduleGitlinks $RepositoryPlan.LocalPath } else { $null }
            Note = 'not found or no SSH access'
        }
    }

    $branches = @($heads.Output |
        ForEach-Object { ($_ -split '\s+')[-1] } |
        Where-Object { $_ -like 'refs/heads/*' } |
        ForEach-Object { $_.Substring('refs/heads/'.Length) } |
        Sort-Object)

    return [pscustomobject] @{
        Repository = "$Owner/$($RepositoryPlan.Name)"
        Reachable = $true
        Branches = $branches -join ','
        CandidateReady = Test-GmaCandidateOwnsGitRepository $RepositoryPlan.LocalPath
        SkeletonSubmodulesReady = if ($RepositoryPlan.Name -eq 'gma-skeleton') { Test-GmaSkeletonCandidateHasSubmoduleGitlinks $RepositoryPlan.LocalPath } else { $null }
        Note = if (@($branches).Count -eq 0) { 'empty repository' } else { '' }
    }
}

function Write-GmaRemoteRepositoryAudit {
    param(
        [Parameter(Mandatory = $true)]
        [object[]] $RepositoryPlans
    )

    $RepositoryPlans |
        ForEach-Object { Get-GmaRemoteRepositoryAudit $_ } |
        Format-Table -AutoSize
}

function Test-GmaCandidateOwnsGitRepository {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Path
    )

    if (-not (Test-Path -LiteralPath $Path -PathType Container)) {
        return $false
    }

    $previousErrorActionPreference = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        $repositoryRoot = git -C $Path rev-parse --show-toplevel 2>$null
        $exitCode = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $previousErrorActionPreference
    }

    if ($exitCode -ne 0 -or [string]::IsNullOrWhiteSpace($repositoryRoot)) {
        return $false
    }

    $expectedRoot = [System.IO.Path]::GetFullPath((Resolve-Path -LiteralPath $Path).Path).TrimEnd('\', '/')
    $actualRoot = [System.IO.Path]::GetFullPath($repositoryRoot).TrimEnd('\', '/')

    return [string]::Equals($expectedRoot, $actualRoot, [System.StringComparison]::OrdinalIgnoreCase)
}

function Test-GmaSkeletonCandidateHasSubmoduleGitlinks {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Path
    )

    if (-not (Test-GmaCandidateOwnsGitRepository $Path)) {
        return $false
    }

    if (-not (Test-Path -LiteralPath (Join-Path $Path '.gitmodules') -PathType Leaf)) {
        return $false
    }

    foreach ($mountPath in Get-GmaStage8SkeletonSubmoduleMountPaths) {
        $indexEntry = git -C $Path ls-files --stage -- $mountPath
        if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($indexEntry)) {
            return $false
        }

        if (-not ($indexEntry -match '^160000\s')) {
            return $false
        }
    }

    return $true
}

function Assert-GmaSkeletonCandidateCanBePushed {
    param(
        [Parameter(Mandatory = $true)]
        [object] $RepositoryPlan
    )

    if ($RepositoryPlan.Name -ne 'gma-skeleton' -or $AllowUnconvertedSkeletonPush -or $WhatIfPreference) {
        return
    }

    if (-not (Test-GmaSkeletonCandidateHasSubmoduleGitlinks $RepositoryPlan.LocalPath)) {
        throw "Refusing to push '$Owner/$($RepositoryPlan.Name)' before Stage 9 has added real .gitmodules entries and submodule gitlinks. Run the Stage 9 conversion first, or rerun with -AllowUnconvertedSkeletonPush only for a deliberate placeholder push."
    }
}

function Assert-GmaCandidatePushPreconditions {
    param(
        [Parameter(Mandatory = $true)]
        [object] $RepositoryPlan
    )

    if (-not (Test-Path -LiteralPath $RepositoryPlan.LocalPath -PathType Container)) {
        throw "Candidate repository path is missing: $($RepositoryPlan.LocalPath)"
    }

    Assert-GmaCandidateOwnsGitRepository $RepositoryPlan
    Assert-GmaSkeletonCandidateCanBePushed $RepositoryPlan

    $status = git -C $RepositoryPlan.LocalPath status --short
    if ($status -and -not $AllowDirtyCandidates) {
        throw "Candidate repository has uncommitted changes: $($RepositoryPlan.LocalPath). Commit, clean, or rerun with -AllowDirtyCandidates."
    }
}

function Assert-GmaCandidateOwnsGitRepository {
    param(
        [Parameter(Mandatory = $true)]
        [object] $RepositoryPlan
    )

    if (-not (Test-GmaCandidateOwnsGitRepository $RepositoryPlan.LocalPath)) {
        throw "Candidate path '$($RepositoryPlan.LocalPath)' is not a standalone Git repository. Run this script with -InitializeCandidates after generating Stage 8 candidates."
    }
}

function Ensure-GmaSkeletonCandidateMountIgnores {
    param(
        [Parameter(Mandatory = $true)]
        [object] $RepositoryPlan
    )

    if ($RepositoryPlan.Name -ne 'gma-skeleton') {
        return
    }

    $gmaPath = Join-Path $RepositoryPlan.LocalPath 'gma'
    $moduleMountPath = Join-Path $gmaPath 'modules'
    New-Item -ItemType Directory -Force -Path $moduleMountPath | Out-Null
    New-Item -ItemType File -Force -Path (Join-Path $gmaPath '.gitkeep') | Out-Null
    New-Item -ItemType File -Force -Path (Join-Path $moduleMountPath '.gitkeep') | Out-Null

    $gitIgnorePath = Join-Path $RepositoryPlan.LocalPath '.gitignore'
    $gitIgnore = if (Test-Path -LiteralPath $gitIgnorePath) {
        Get-Content -LiteralPath $gitIgnorePath
    }
    else {
        @()
    }

    if ($gitIgnore -contains '# GMA source repository mount points') {
        return
    }

    Add-Content -LiteralPath $gitIgnorePath -Encoding utf8 -Value @(
        '',
        '# GMA source repository mount points',
        '/gma/framework/',
        '/gma/modules/*/'
    )
}

function Ensure-GmaCandidateSourceRootOverridesAreLocalOnly {
    param(
        [Parameter(Mandatory = $true)]
        [object] $RepositoryPlan
    )

    $gitIgnorePath = Join-Path $RepositoryPlan.LocalPath '.gitignore'
    $gitIgnore = if (Test-Path -LiteralPath $gitIgnorePath) {
        @(Get-Content -LiteralPath $gitIgnorePath)
    }
    else {
        @()
    }

    if ($gitIgnore -notcontains 'Gma.SourceRoots.props') {
        $linesToAppend = @()
        if ($gitIgnore.Count -gt 0 -and -not [string]::IsNullOrWhiteSpace($gitIgnore[-1])) {
            $linesToAppend += ''
        }

        $linesToAppend += '# GMA local source-root overrides'
        $linesToAppend += 'Gma.SourceRoots.props'
        Add-Content -LiteralPath $gitIgnorePath -Encoding utf8 -Value $linesToAppend
    }

    $trackedSourceRoots = (Invoke-GmaQuietNativeCommand `
        -FilePath 'git' `
        -Arguments @('-C', $RepositoryPlan.LocalPath, 'ls-files', '--error-unmatch', 'Gma.SourceRoots.props')) -eq 0
    if ($trackedSourceRoots) {
        git -C $RepositoryPlan.LocalPath rm --cached --quiet -- Gma.SourceRoots.props
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to untrack local source-root override in '$($RepositoryPlan.LocalPath)'."
        }
    }
}

function Ensure-GmaCandidateSourceRootBootstrap {
    param(
        [Parameter(Mandatory = $true)]
        [object] $RepositoryPlan
    )

    Set-GmaUtf8FileLines `
        -Path (Join-Path $RepositoryPlan.LocalPath 'Gma.SourceRoots.props.example') `
        -Lines (Get-GmaCandidateSourceRootExampleLines $RepositoryPlan)

    Set-GmaUtf8FileLines `
        -Path (Join-Path $RepositoryPlan.LocalPath 'eng\bootstrap-source-roots.ps1') `
        -Lines (Get-GmaCandidateSourceRootBootstrapLines)
}

function Ensure-GmaCandidateValidationWorkflow {
    param(
        [Parameter(Mandatory = $true)]
        [object] $RepositoryPlan
    )

    Set-GmaUtf8FileLines `
        -Path (Join-Path $RepositoryPlan.LocalPath '.github\workflows\validate.yml') `
        -Lines (Get-GmaCandidateValidationWorkflowLines $RepositoryPlan)
}

function Copy-GmaStage8Directory {
    param(
        [Parameter(Mandatory = $true)]
        [string] $SourcePath,

        [Parameter(Mandatory = $true)]
        [string] $DestinationPath
    )

    if (Test-Path -LiteralPath $DestinationPath -PathType Container) {
        Remove-Item -LiteralPath $DestinationPath -Recurse -Force
    }

    if (Test-Path -LiteralPath $SourcePath -PathType Container) {
        $parent = Split-Path -Parent $DestinationPath
        if (-not (Test-Path -LiteralPath $parent -PathType Container)) {
            New-Item -ItemType Directory -Force -Path $parent | Out-Null
        }

        Copy-Item -LiteralPath $SourcePath -Destination $DestinationPath -Recurse -Force
    }
}

function Copy-GmaStage8FileIfExists {
    param(
        [Parameter(Mandatory = $true)]
        [string] $SourcePath,

        [Parameter(Mandatory = $true)]
        [string] $DestinationPath
    )

    if (-not (Test-Path -LiteralPath $SourcePath -PathType Leaf)) {
        return
    }

    $parent = Split-Path -Parent $DestinationPath
    if (-not (Test-Path -LiteralPath $parent -PathType Container)) {
        New-Item -ItemType Directory -Force -Path $parent | Out-Null
    }

    Copy-Item -LiteralPath $SourcePath -Destination $DestinationPath -Force
}

function Sync-GmaSkeletonCandidateFromCurrentWorkspace {
    param(
        [Parameter(Mandatory = $true)]
        [object] $RepositoryPlan
    )

    if ($RepositoryPlan.Name -ne 'gma-skeleton') {
        return
    }

    $repositoryRoot = Get-GmaRepositoryRoot

    Copy-GmaStage8Directory `
        -SourcePath (Join-Path $repositoryRoot 'docs') `
        -DestinationPath (Join-Path $RepositoryPlan.LocalPath 'docs')
    Copy-GmaStage8Directory `
        -SourcePath (Join-Path $repositoryRoot 'requests') `
        -DestinationPath (Join-Path $RepositoryPlan.LocalPath 'requests')

    $engDestinationPath = Join-Path $RepositoryPlan.LocalPath 'eng'
    if (-not (Test-Path -LiteralPath $engDestinationPath -PathType Container)) {
        New-Item -ItemType Directory -Force -Path $engDestinationPath | Out-Null
    }

    Get-ChildItem -LiteralPath (Join-Path $repositoryRoot 'eng') -Filter '*.ps1' -File |
        ForEach-Object {
            Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $engDestinationPath $_.Name) -Force
        }

    foreach ($fileName in @(
        '.editorconfig',
        '.gitattributes',
        '.gitignore',
        'Directory.Build.props',
        'Directory.Packages.props',
        'global.json',
        'LICENSE',
        'nuget.config',
        'README.md')) {
        Copy-GmaStage8FileIfExists `
            -SourcePath (Join-Path $repositoryRoot $fileName) `
            -DestinationPath (Join-Path $RepositoryPlan.LocalPath $fileName)
    }

    $stage9Script = Join-Path $repositoryRoot 'eng\gma-stage9.ps1'
    if (-not (Test-Path -LiteralPath $stage9Script -PathType Leaf)) {
        throw "Cannot synchronize skeleton candidate because Stage 9 helper is missing: $stage9Script"
    }

    & powershell -NoProfile -ExecutionPolicy Bypass -File $stage9Script `
        -WriteConvertedSolution `
        -ConvertedSolutionPath (Join-Path $RepositoryPlan.LocalPath 'GenericModularApi.slnx') `
        -Force
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to write converted skeleton candidate solution."
    }

    & powershell -NoProfile -ExecutionPolicy Bypass -File $stage9Script `
        -RewriteSkeletonDocsForSubmoduleLayout `
        -SkeletonRootPath $RepositoryPlan.LocalPath `
        -Force
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to rewrite skeleton candidate docs for submodule layout."
    }

    foreach ($solutionFile in Get-GmaStage8RootPackageSolutionFiles) {
        $solutionPath = Join-Path $RepositoryPlan.LocalPath $solutionFile
        if (Test-Path -LiteralPath $solutionPath -PathType Leaf) {
            Remove-Item -LiteralPath $solutionPath -Force
        }
    }
}

function Initialize-GmaCandidateRepository {
    param(
        [Parameter(Mandatory = $true)]
        [object] $RepositoryPlan
    )

    if (-not (Test-Path -LiteralPath $RepositoryPlan.LocalPath -PathType Container)) {
        throw "Candidate repository path is missing: $($RepositoryPlan.LocalPath)"
    }

    if ($WhatIfPreference) {
        $null = $PSCmdlet.ShouldProcess(
            $RepositoryPlan.LocalPath,
            "Initialize local candidate repository, commit contents as $GitUserName <$GitUserEmail>, and ensure main/dev branches")
        return
    }

    if (-not (Test-GmaCandidateOwnsGitRepository $RepositoryPlan.LocalPath)) {
        git -C $RepositoryPlan.LocalPath init --quiet
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to initialize Git repository at '$($RepositoryPlan.LocalPath)'."
        }
    }

    Sync-GmaSkeletonCandidateFromCurrentWorkspace $RepositoryPlan
    Ensure-GmaSkeletonCandidateMountIgnores $RepositoryPlan
    Ensure-GmaCandidateSourceRootOverridesAreLocalOnly $RepositoryPlan
    Ensure-GmaCandidateSourceRootBootstrap $RepositoryPlan
    Ensure-GmaCandidateValidationWorkflow $RepositoryPlan

    git -C $RepositoryPlan.LocalPath config user.name $GitUserName
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to set local Git user.name for '$($RepositoryPlan.LocalPath)'."
    }

    git -C $RepositoryPlan.LocalPath config user.email $GitUserEmail
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to set local Git user.email for '$($RepositoryPlan.LocalPath)'."
    }

    git -C $RepositoryPlan.LocalPath config core.autocrlf false
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to set local Git core.autocrlf for '$($RepositoryPlan.LocalPath)'."
    }

    git -C $RepositoryPlan.LocalPath config core.longpaths true
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to set local Git core.longpaths for '$($RepositoryPlan.LocalPath)'."
    }

    $hasCommit = (Invoke-GmaQuietNativeCommand `
        -FilePath 'git' `
        -Arguments @('-C', $RepositoryPlan.LocalPath, 'rev-parse', '--verify', 'HEAD')) -eq 0
    $status = git -C $RepositoryPlan.LocalPath status --porcelain
    if (-not $hasCommit -or $status) {
        git -C $RepositoryPlan.LocalPath add -A
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to stage candidate repository '$($RepositoryPlan.LocalPath)'."
        }

        $cachedDiffExitCode = Invoke-GmaQuietNativeCommand `
            -FilePath 'git' `
            -Arguments @('-C', $RepositoryPlan.LocalPath, 'diff', '--cached', '--quiet')
        if ($cachedDiffExitCode -eq 1) {
            git -C $RepositoryPlan.LocalPath commit --quiet -m "chore: initialize $($RepositoryPlan.Name) candidate"
            if ($LASTEXITCODE -ne 0) {
                throw "Failed to commit candidate repository '$($RepositoryPlan.LocalPath)'."
            }
        }
        elseif ($cachedDiffExitCode -ne 0) {
            throw "Failed to inspect staged candidate changes for '$($RepositoryPlan.LocalPath)'."
        }
        elseif (-not $hasCommit) {
            throw "Candidate repository '$($RepositoryPlan.LocalPath)' has no commit and no staged content."
        }
    }

    $currentBranch = git -C $RepositoryPlan.LocalPath branch --show-current
    $hasMain = (Invoke-GmaQuietNativeCommand `
        -FilePath 'git' `
        -Arguments @('-C', $RepositoryPlan.LocalPath, 'show-ref', '--verify', '--quiet', 'refs/heads/main')) -eq 0
    if (-not $hasMain) {
        if ($currentBranch -and $currentBranch -ne 'dev') {
            git -C $RepositoryPlan.LocalPath branch -M main
        }
        else {
            git -C $RepositoryPlan.LocalPath branch main
        }

        if ($LASTEXITCODE -ne 0) {
            throw "Failed to ensure main branch for '$($RepositoryPlan.LocalPath)'."
        }
    }

    $hasDev = (Invoke-GmaQuietNativeCommand `
        -FilePath 'git' `
        -Arguments @('-C', $RepositoryPlan.LocalPath, 'show-ref', '--verify', '--quiet', 'refs/heads/dev')) -eq 0
    if (-not $hasDev) {
        git -C $RepositoryPlan.LocalPath branch dev main
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to ensure dev branch for '$($RepositoryPlan.LocalPath)'."
        }
    }

    $checkoutExitCode = Invoke-GmaQuietNativeCommand `
        -FilePath 'git' `
        -Arguments @('-C', $RepositoryPlan.LocalPath, 'checkout', 'dev')
    if ($checkoutExitCode -ne 0) {
        throw "Failed to check out dev branch for '$($RepositoryPlan.LocalPath)'."
    }
}

function Test-GmaGithubRepositoryExists {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Repository
    )

    return (Invoke-GmaQuietNativeCommand `
        -FilePath 'gh' `
        -Arguments @('repo', 'view', $Repository, '--json', 'name', '--jq', '.name')) -eq 0
}

function Ensure-GmaGithubRepository {
    param(
        [Parameter(Mandatory = $true)]
        [object] $RepositoryPlan
    )

    $fullName = "$Owner/$($RepositoryPlan.Name)"
    if (-not $WhatIfPreference -and (Test-GmaGithubRepositoryExists $fullName)) {
        Write-Host "Repository exists: $fullName"
        return
    }

    $arguments = @(
        'repo',
        'create',
        $fullName,
        "--$Visibility",
        '--description',
        $RepositoryPlan.Description,
        '--disable-wiki'
    )

    Invoke-GmaGithubCli `
        -Arguments $arguments `
        -Target $fullName `
        -Action "Create $Visibility GitHub repository"
}

function Configure-GmaGithubRepository {
    param(
        [Parameter(Mandatory = $true)]
        [object] $RepositoryPlan
    )

    $fullName = "$Owner/$($RepositoryPlan.Name)"
    $arguments = @(
        'repo',
        'edit',
        $fullName,
        '--description',
        $RepositoryPlan.Description,
        '--default-branch',
        $DefaultBranch,
        '--delete-branch-on-merge',
        '--allow-update-branch',
        '--enable-squash-merge',
        '--enable-merge-commit=false',
        '--enable-rebase-merge=false',
        '--enable-wiki=false'
    )

    foreach ($topic in $RepositoryPlan.Topics) {
        $arguments += '--add-topic'
        $arguments += $topic
    }

    Invoke-GmaGithubCli `
        -Arguments $arguments `
        -Target $fullName `
        -Action "Configure repository settings"
}

function Protect-GmaGithubBranch {
    param(
        [Parameter(Mandatory = $true)]
        [object] $RepositoryPlan
    )

    $fullName = "$Owner/$($RepositoryPlan.Name)"
    $requiredStatusChecks = if ($RequiredStatusChecks.Count -gt 0) {
        @{
            strict = $true
            contexts = $RequiredStatusChecks
        }
    }
    else {
        $null
    }

    $payload = @{
        required_status_checks = $requiredStatusChecks
        enforce_admins = $true
        required_pull_request_reviews = @{
            dismiss_stale_reviews = $true
            require_code_owner_reviews = $false
            required_approving_review_count = 1
            require_last_push_approval = $true
        }
        restrictions = $null
        required_linear_history = $true
        allow_force_pushes = $false
        allow_deletions = $false
        block_creations = $false
        required_conversation_resolution = $true
    }

    if ($WhatIfPreference) {
        Invoke-GmaGithubCli `
            -Arguments @(
                'api',
                '--method',
                'PUT',
                "repos/$fullName/branches/$DefaultBranch/protection",
                '--input',
                '<generated-branch-protection-payload>'
            ) `
            -Target "$fullName/$DefaultBranch" `
            -Action 'Configure branch protection'
        return
    }

    $payloadPath = New-TemporaryFile
    try {
        $payload | ConvertTo-Json -Depth 10 -Compress | Set-Content -LiteralPath $payloadPath.FullName -NoNewline -Encoding utf8
        Invoke-GmaGithubCli `
            -Arguments @(
                'api',
                '--method',
                'PUT',
                "repos/$fullName/branches/$DefaultBranch/protection",
                '--input',
                $payloadPath.FullName
            ) `
            -Target "$fullName/$DefaultBranch" `
            -Action 'Configure branch protection'
    }
    finally {
        Remove-Item -LiteralPath $payloadPath.FullName -Force -ErrorAction SilentlyContinue
    }
}

function Push-GmaCandidateRepository {
    param(
        [Parameter(Mandatory = $true)]
        [object] $RepositoryPlan
    )

    $fullName = "$Owner/$($RepositoryPlan.Name)"
    $sshUrl = Get-GmaStage8SshUrl $RepositoryPlan
    Assert-GmaCandidatePushPreconditions $RepositoryPlan

    $remoteNames = git -C $RepositoryPlan.LocalPath remote
    if ($remoteNames -contains 'origin') {
        if ($PSCmdlet.ShouldProcess($RepositoryPlan.LocalPath, "Set origin to $sshUrl")) {
            git -C $RepositoryPlan.LocalPath remote set-url origin $sshUrl
            if ($LASTEXITCODE -ne 0) {
                throw "Failed to set origin for $($RepositoryPlan.LocalPath)."
            }
        }
    }
    elseif ($PSCmdlet.ShouldProcess($RepositoryPlan.LocalPath, "Add origin $sshUrl")) {
        git -C $RepositoryPlan.LocalPath remote add origin $sshUrl
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to add origin for $($RepositoryPlan.LocalPath)."
        }
    }

    $pushMain = $true
    if (-not $WhatIfPreference) {
        $remoteMain = Invoke-GmaNativeOutput `
            -FilePath 'git' `
            -Arguments @('ls-remote', '--heads', $sshUrl, 'main')
        if ($remoteMain.ExitCode -ne 0) {
            throw "Could not inspect remote repository '$fullName'. Create it first and verify SSH access."
        }

        if ($remoteMain.Output.Count -gt 0) {
            $fetchMainExitCode = Invoke-GmaQuietNativeCommand `
            -FilePath 'git' `
            -Arguments @('-C', $RepositoryPlan.LocalPath, 'fetch', '--quiet', 'origin', 'main')
            if ($fetchMainExitCode -ne 0) {
                throw "Could not fetch remote main for '$fullName'."
            }

            $mainIsFastForward = (Invoke-GmaQuietNativeCommand `
            -FilePath 'git' `
            -Arguments @('-C', $RepositoryPlan.LocalPath, 'merge-base', '--is-ancestor', 'FETCH_HEAD', 'main')) -eq 0
            if (-not $mainIsFastForward) {
                $message = "Remote main for '$fullName' is not an ancestor of the local candidate main. Refusing to overwrite existing history."
                if ($SkipDivergedMain) {
                    Write-Warning "$message Skipping main push; dev can still be pushed."
                    $pushMain = $false
                }
                else {
                    throw "$message Rerun with -SkipDivergedMain to push only dev, or reconcile the remote manually."
                }
            }
        }
    }

    foreach ($branch in @('main', 'dev')) {
        $hasBranch = (Invoke-GmaQuietNativeCommand `
            -FilePath 'git' `
            -Arguments @('-C', $RepositoryPlan.LocalPath, 'show-ref', '--verify', '--quiet', "refs/heads/$branch")) -eq 0
        if (-not $hasBranch) {
            throw "Candidate repository '$($RepositoryPlan.LocalPath)' does not contain required branch '$branch'."
        }
    }

    $branchesToPush = if ($pushMain) { @('main', 'dev') } else { @('dev') }
    if ($PSCmdlet.ShouldProcess($fullName, "Push $($branchesToPush -join ' and ') branch(es)")) {
        git -C $RepositoryPlan.LocalPath push -u origin @branchesToPush
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to push candidate repository '$fullName'."
        }
    }
}

$repositoryPlans = Get-GmaSelectedStage8RepositoryPlans
$hasAction = $InitializeCandidates -or $AuditRepositories -or $CreateRepositories -or $PushCandidates -or $ConfigureRepositories -or $ProtectBranches

if (-not $hasAction) {
    Write-Host 'Stage 8 repository plan:'
    foreach ($repositoryPlan in $repositoryPlans) {
        Write-Host "- $Owner/$($repositoryPlan.Name) <= $($repositoryPlan.LocalPath)"
    }

    Write-Host ''
    if ($SkipSkeleton) {
        Write-Host 'Skeleton repository is excluded by -SkipSkeleton.'
    }

    Write-Host 'No changes were requested. Add -InitializeCandidates, -AuditRepositories, -CreateRepositories, -PushCandidates, -ConfigureRepositories, or -ProtectBranches.'
    return
}

$requiresGithubCli = $CreateRepositories -or $ConfigureRepositories -or $ProtectBranches
if ($requiresGithubCli -and -not $WhatIfPreference) {
    Assert-GmaGithubCliReady
}

if ($PushCandidates -and -not $WhatIfPreference) {
    foreach ($repositoryPlan in $repositoryPlans) {
        Assert-GmaCandidatePushPreconditions $repositoryPlan
    }
}

foreach ($repositoryPlan in $repositoryPlans) {
    if ($InitializeCandidates) {
        Initialize-GmaCandidateRepository $repositoryPlan
    }
}

if ($AuditRepositories) {
    Write-GmaRemoteRepositoryAudit $repositoryPlans
}

foreach ($repositoryPlan in $repositoryPlans) {
    if ($CreateRepositories) {
        Ensure-GmaGithubRepository $repositoryPlan
    }

    if ($PushCandidates) {
        Push-GmaCandidateRepository $repositoryPlan
    }

    if ($ConfigureRepositories) {
        Configure-GmaGithubRepository $repositoryPlan
    }

    if ($ProtectBranches) {
        Protect-GmaGithubBranch $repositoryPlan
    }
}

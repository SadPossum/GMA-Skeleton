[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [string] $Owner = 'SadPossum',

    [string] $RemoteHostAlias = 'github.com-private',

    [string] $DefaultBranch = 'dev',

    [string] $StageRoot = '.agents\stage8d\2',

    [switch] $Audit,

    [switch] $PrintCommands,

    [switch] $WriteCommandPlan,

    [switch] $UseLocalCandidates,

    [switch] $CreateLocalRehearsal,

    [switch] $ProveLocalRecursiveClone,

    [switch] $WriteConvertedSolution,

    [switch] $RewriteSkeletonDocsForSubmoduleLayout,

    [switch] $SkipLocalValidation,

    [switch] $Force,

    [string] $GitUserName = 'SadPossum',

    [string] $GitUserEmail = '258739@bk.ru',

    [string] $CommandPlanPath = '.tmp\gma-stage9-submodule-plan.ps1',

    [string] $LocalRehearsalPath = '.agents\stage9\local-submodule-rehearsal',

    [string] $LocalCloneProofPath = '.agents\stage9\local-recursive-clone-proof',

    [string] $ConvertedSolutionPath = '.tmp\gma-stage9-converted-solution.slnx',

    [string] $SkeletonRootPath = '.'
)

. (Join-Path $PSScriptRoot 'common.ps1')

function Resolve-GmaStage9Path {
    param([Parameter(Mandatory = $true)][string] $Path)

    if ([System.IO.Path]::IsPathRooted($Path)) {
        return [System.IO.Path]::GetFullPath($Path)
    }

    return [System.IO.Path]::GetFullPath((Join-GmaPath $Path))
}

function Get-GmaStage9SubmodulePlans {
    $stageRootPath = Resolve-GmaStage9Path $StageRoot

    return @(
        [pscustomobject] @{
            Name = 'framework'
            Repository = 'GMA-Framework'
            MountPath = 'gma\framework'
            SourceFolder = 'Framework'
            LocalCandidatePath = Join-Path $stageRootPath 'repos\GMA-Framework'
        },
        [pscustomobject] @{
            Name = 'administration'
            Repository = 'GMA-Module-Administration'
            MountPath = 'gma\modules\administration'
            SourceFolder = 'Administration'
            LocalCandidatePath = Join-Path $stageRootPath 'repos\GMA-Module-Administration'
        },
        [pscustomobject] @{
            Name = 'auth'
            Repository = 'GMA-Module-Auth'
            MountPath = 'gma\modules\auth'
            SourceFolder = 'Auth'
            LocalCandidatePath = Join-Path $stageRootPath 'repos\GMA-Module-Auth'
        },
        [pscustomobject] @{
            Name = 'files'
            Repository = 'GMA-Module-Files'
            MountPath = 'gma\modules\files'
            SourceFolder = 'Files'
            LocalCandidatePath = Join-Path $stageRootPath 'repos\GMA-Module-Files'
        },
        [pscustomobject] @{
            Name = 'notifications'
            Repository = 'GMA-Module-Notifications'
            MountPath = 'gma\modules\notifications'
            SourceFolder = 'Notifications'
            LocalCandidatePath = Join-Path $stageRootPath 'repos\GMA-Module-Notifications'
        },
        [pscustomobject] @{
            Name = 'task-runtime'
            Repository = 'GMA-Module-Task-Runtime'
            MountPath = 'gma\modules\task-runtime'
            SourceFolder = 'TaskRuntime'
            LocalCandidatePath = Join-Path $stageRootPath 'repos\GMA-Module-Task-Runtime'
        },
        [pscustomobject] @{
            Name = 'tenancy'
            Repository = 'GMA-Module-Tenancy'
            MountPath = 'gma\modules\tenancy'
            SourceFolder = 'Tenancy'
            LocalCandidatePath = Join-Path $stageRootPath 'repos\GMA-Module-Tenancy'
        }
    )
}

function Get-GmaStage9RootPackageSolutionFiles {
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

function Get-GmaStage9RemoteUrl {
    param([Parameter(Mandatory = $true)][string] $Repository)

    return "git@${RemoteHostAlias}:${Owner}/${Repository}.git"
}

function ConvertTo-GmaStage9PowerShellLiteral {
    param([Parameter(Mandatory = $true)][string] $Value)

    return "'" + ($Value -replace "'", "''") + "'"
}

function Assert-GmaStage9SafeRehearsalPath {
    param([Parameter(Mandatory = $true)][string] $Path)

    $repositoryRoot = [System.IO.Path]::GetFullPath((Get-GmaRepositoryRoot)).TrimEnd('\', '/')
    $agentsRoot = [System.IO.Path]::GetFullPath((Join-GmaPath '.agents')).TrimEnd('\', '/')
    $resolvedPath = [System.IO.Path]::GetFullPath($Path).TrimEnd('\', '/')

    if (-not $resolvedPath.StartsWith($agentsRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to create or remove Stage 9 rehearsal outside '.agents': $resolvedPath"
    }

    if ([string]::Equals($resolvedPath, $agentsRoot, [System.StringComparison]::OrdinalIgnoreCase) -or
        [string]::Equals($resolvedPath, $repositoryRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing unsafe Stage 9 rehearsal path: $resolvedPath"
    }
}

function Set-GmaStage9LocalGitIdentity {
    param([Parameter(Mandatory = $true)][string] $RepositoryPath)

    Invoke-GmaStage9Command -WorkingDirectory $RepositoryPath -Arguments @('config', 'user.name', $GitUserName)
    Invoke-GmaStage9Command -WorkingDirectory $RepositoryPath -Arguments @('config', 'user.email', $GitUserEmail)
}

function Test-GmaStage9StandaloneGitRepository {
    param([Parameter(Mandatory = $true)][string] $Path)

    if (-not (Test-Path -LiteralPath $Path -PathType Container)) {
        return $false
    }

    $topLevel = git -C $Path rev-parse --show-toplevel 2>$null
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($topLevel)) {
        return $false
    }

    return [string]::Equals(
        [System.IO.Path]::GetFullPath($topLevel).TrimEnd('\', '/'),
        [System.IO.Path]::GetFullPath($Path).TrimEnd('\', '/'),
        [System.StringComparison]::OrdinalIgnoreCase)
}

function Get-GmaStage9MountState {
    param([Parameter(Mandatory = $true)][string] $MountPath)

    $absolutePath = Join-GmaPath $MountPath
    if (-not (Test-Path -LiteralPath $absolutePath)) {
        return 'missing'
    }

    if (Test-Path -LiteralPath (Join-Path $absolutePath '.git')) {
        return 'git-checkout'
    }

    $item = Get-Item -LiteralPath $absolutePath -Force
    if ($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) {
        return 'reparse-point'
    }

    return 'plain-directory'
}

function Test-GmaStage9RemoteReachable {
    param([Parameter(Mandatory = $true)][string] $RemoteUrl)

    $previousErrorActionPreference = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        & git ls-remote --heads $RemoteUrl *>$null
        return $LASTEXITCODE -eq 0
    }
    finally {
        $ErrorActionPreference = $previousErrorActionPreference
    }
}

function Get-GmaStage9SubmoduleSource {
    param([Parameter(Mandatory = $true)][object] $Plan)

    if ($UseLocalCandidates) {
        return [System.IO.Path]::GetFullPath($Plan.LocalCandidatePath)
    }

    return Get-GmaStage9RemoteUrl $Plan.Repository
}

function Get-GmaStage9CommandPlanLines {
    $stageRootPath = Resolve-GmaStage9Path $StageRoot
    $skeletonPath = Join-Path $stageRootPath 'skeleton'
    $plans = Get-GmaStage9SubmodulePlans

    $lines = [System.Collections.Generic.List[string]]::new()
    $lines.Add('# Stage 9 source-first skeleton conversion plan.')
    $lines.Add('# Review before running. This plan assumes Stage 8 repositories exist and contain the prepared histories.')
    $lines.Add('$ErrorActionPreference = ''Stop''')
    $lines.Add('')
    $lines.Add('git status --short --branch')
    $lines.Add('if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }')
    $lines.Add('$dirty = @(git status --short)')
    $lines.Add('if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }')
    $lines.Add('if ($dirty.Count -gt 0) { throw "Stage 9 conversion requires a clean working tree before submodules are added." }')
    $lines.Add('')
    $lines.Add('# Bring in the already-proven skeleton source-root and CI shape before removing old source folders.')
    $lines.Add('powershell -NoProfile -ExecutionPolicy Bypass -File eng\gma-stage9.ps1 -WriteConvertedSolution -ConvertedSolutionPath GMA-Skeleton.slnx -Force')
    $lines.Add('if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }')
    $lines.Add("Copy-Item -LiteralPath '$skeletonPath\Gma.SourceRoots.props.example' -Destination 'Gma.SourceRoots.props.example' -Force")
    $lines.Add('New-Item -ItemType Directory -Force -Path ''.github\workflows'' | Out-Null')
    $lines.Add("Copy-Item -LiteralPath '$skeletonPath\.github\workflows\validate.yml' -Destination '.github\workflows\validate.yml' -Force")
    $lines.Add('powershell -NoProfile -ExecutionPolicy Bypass -File eng\gma-stage9.ps1 -RewriteSkeletonDocsForSubmoduleLayout -SkeletonRootPath . -Force')
    $lines.Add('if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }')
    $lines.Add('')
    $lines.Add('New-Item -ItemType Directory -Force -Path ''gma\modules'' | Out-Null')
    foreach ($plan in $plans) {
        $submoduleSource = Get-GmaStage9SubmoduleSource $plan
        $submoduleCommand = if ($UseLocalCandidates) {
            'git -c protocol.file.allow=always submodule add'
        }
        else {
            'git submodule add'
        }

        $lines.Add("$submoduleCommand -f -b $DefaultBranch $(ConvertTo-GmaStage9PowerShellLiteral $submoduleSource) $(ConvertTo-GmaStage9PowerShellLiteral ($plan.MountPath -replace '\\', '/'))")
        $lines.Add('if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }')
    }

    $lines.Add('')
    $lines.Add('powershell -NoProfile -ExecutionPolicy Bypass -File eng\gma-bootstrap.ps1 -SourceLayout GmaSubmodules -Force')
    $lines.Add('if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }')
    $lines.Add('git submodule status --recursive')
    $lines.Add('if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }')
    $lines.Add('')
    $lines.Add('# Restore and build the submodule-backed composition before deleting the old reusable source folders.')
    $lines.Add('dotnet restore GMA-Skeleton.slnx')
    $lines.Add('if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }')
    $lines.Add('dotnet build GMA-Skeleton.slnx --no-restore -m:1')
    $lines.Add('if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }')
    $lines.Add('')
    $lines.Add('# After validation is green, remove reusable source that is now owned by submodules.')
    $lines.Add('git rm -r src\Framework')
    $lines.Add('git rm -r src\Modules\Administration src\Modules\Auth src\Modules\Files src\Modules\Notifications src\Modules\TaskRuntime src\Modules\Tenancy')
    $lines.Add('git rm Gma.Framework.slnx Gma.Modules.Administration.slnx Gma.Modules.Auth.slnx Gma.Modules.Files.slnx Gma.Modules.Notifications.slnx Gma.Modules.TaskRuntime.slnx Gma.Modules.Tenancy.slnx')
    $lines.Add('')
    $lines.Add('# Prove the final skeleton shape after old source and root package entrypoints are gone.')
    $lines.Add('dotnet restore GMA-Skeleton.slnx')
    $lines.Add('if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }')
    $lines.Add('dotnet build GMA-Skeleton.slnx --no-restore -m:1')
    $lines.Add('if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }')
    $lines.Add('dotnet test GMA-Skeleton.slnx --no-build --logger "console;verbosity=minimal"')
    $lines.Add('if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }')
    $lines.Add('')
    $lines.Add('# Keep skeleton-owned hosts, examples, docs, requests, and composition tests in this repository.')
    $lines.Add('git status --short --branch')

    return $lines.ToArray()
}

function Invoke-GmaStage9Command {
    param(
        [Parameter(Mandatory = $true)]
        [string[]] $Arguments,

        [Parameter(Mandatory = $true)]
        [string] $WorkingDirectory
    )

    & git -C $WorkingDirectory @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "git $($Arguments -join ' ') failed with exit code $LASTEXITCODE."
    }
}

function Format-GmaStage9ConvertedSolutionPath {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Path,

        [Parameter(Mandatory = $true)]
        [bool] $HasLeadingSlash
    )

    if ($HasLeadingSlash) {
        return "/$Path"
    }

    return $Path
}

function Convert-GmaStage9SolutionPath {
    param([Parameter(Mandatory = $true)][string] $Path)

    $normalizedPath = $Path -replace '\\', '/'
    $hasLeadingSlash = $normalizedPath.StartsWith('/', [System.StringComparison]::Ordinal)
    if ($hasLeadingSlash) {
        $normalizedPath = $normalizedPath.TrimStart('/')
    }

    $convertedPath = $normalizedPath

    if ($normalizedPath -eq 'src/Framework/src/') {
        return Format-GmaStage9ConvertedSolutionPath 'gma/framework/src/' $hasLeadingSlash
    }

    if ($normalizedPath -like 'src/Framework/docs/*') {
        $convertedPath = $normalizedPath -replace '^src/Framework/docs/', 'gma/framework/docs/'
        return Format-GmaStage9ConvertedSolutionPath $convertedPath $hasLeadingSlash
    }

    if ($normalizedPath -like 'src/Framework/eng/*') {
        $convertedPath = $normalizedPath -replace '^src/Framework/eng/', 'gma/framework/eng/'
        return Format-GmaStage9ConvertedSolutionPath $convertedPath $hasLeadingSlash
    }

    if ($normalizedPath -like 'src/Framework/tests/*') {
        $convertedPath = $normalizedPath -replace '^src/Framework/tests/', 'gma/framework/tests/'
        return Format-GmaStage9ConvertedSolutionPath $convertedPath $hasLeadingSlash
    }

    if ($normalizedPath -like 'src/Framework/*') {
        $convertedPath = $normalizedPath -replace '^src/Framework/', 'gma/framework/src/'
        return Format-GmaStage9ConvertedSolutionPath $convertedPath $hasLeadingSlash
    }

    foreach ($plan in Get-GmaStage9SubmodulePlans | Where-Object { $_.Name -ne 'framework' }) {
        $modulePrefix = "src/Modules/$($plan.SourceFolder)/"
        $moduleMount = ($plan.MountPath -replace '\\', '/')

        if ($normalizedPath -eq "${modulePrefix}src/") {
            return Format-GmaStage9ConvertedSolutionPath "$moduleMount/src/" $hasLeadingSlash
        }

        if ($normalizedPath -like "${modulePrefix}docs/*") {
            $convertedPath = $normalizedPath -replace "^$([regex]::Escape($modulePrefix))docs/", "$moduleMount/docs/"
            return Format-GmaStage9ConvertedSolutionPath $convertedPath $hasLeadingSlash
        }

        if ($normalizedPath -like "${modulePrefix}tests/*") {
            $convertedPath = $normalizedPath -replace "^$([regex]::Escape($modulePrefix))tests/", "$moduleMount/tests/"
            return Format-GmaStage9ConvertedSolutionPath $convertedPath $hasLeadingSlash
        }

        if ($normalizedPath -like "$modulePrefix*") {
            $convertedPath = $normalizedPath -replace "^$([regex]::Escape($modulePrefix))", "$moduleMount/src/"
            return Format-GmaStage9ConvertedSolutionPath $convertedPath $hasLeadingSlash
        }
    }

    return Format-GmaStage9ConvertedSolutionPath $convertedPath $hasLeadingSlash
}

function Write-GmaStage9ConvertedSolution {
    param([Parameter(Mandatory = $true)][string] $DestinationPath)

    $sourceSolutionPath = Join-GmaPath 'GMA-Skeleton.slnx'
    $destinationSolutionPath = Resolve-GmaStage9Path $DestinationPath
    $rootSolutionPath = [System.IO.Path]::GetFullPath($sourceSolutionPath)
    if ([System.String]::Equals($destinationSolutionPath, $rootSolutionPath, [System.StringComparison]::OrdinalIgnoreCase) -and
        -not $Force) {
        throw "Refusing to overwrite the working-tree solution without -Force: $destinationSolutionPath"
    }

    $rootPackageSolutionFiles = Get-GmaStage9RootPackageSolutionFiles
    $sourceLines = [System.IO.File]::ReadAllLines($sourceSolutionPath)
    $convertedLines = foreach ($line in $sourceLines) {
        $shouldSkipRootPackageSolution = $false
        foreach ($solutionFile in $rootPackageSolutionFiles) {
            if ($line.IndexOf("<File Path=`"$solutionFile`" />", [System.StringComparison]::Ordinal) -ge 0) {
                $shouldSkipRootPackageSolution = $true
                break
            }
        }

        if ($shouldSkipRootPackageSolution) {
            continue
        }

        [System.Text.RegularExpressions.Regex]::Replace(
            $line,
            '(?<attribute>Path|Name)="(?<path>[^"]+)"',
            {
                param($match)

                $convertedPath = Convert-GmaStage9SolutionPath $match.Groups['path'].Value
                return "$($match.Groups['attribute'].Value)=`"$convertedPath`""
            })
    }

    $directory = Split-Path -Parent $destinationSolutionPath
    if (-not (Test-Path -LiteralPath $directory -PathType Container)) {
        New-Item -ItemType Directory -Force -Path $directory | Out-Null
    }

    if ($PSCmdlet.ShouldProcess($destinationSolutionPath, 'Write converted Stage 9 skeleton solution')) {
        [System.IO.File]::WriteAllLines(
            $destinationSolutionPath,
            $convertedLines,
            [System.Text.UTF8Encoding]::new($false))
        Write-Host "Wrote converted Stage 9 skeleton solution: $destinationSolutionPath"
    }
}

function Convert-GmaStage9DocumentationText {
    param([Parameter(Mandatory = $true)][string] $Source)

    $converted = $Source
    $converted = $converted.Replace('../src/Framework/', '../gma/framework/')
    $converted = $converted.Replace('../../src/Framework/', '../../gma/framework/')
    $converted = $converted.Replace('(src/Framework/', '(gma/framework/')

    foreach ($plan in Get-GmaStage9SubmodulePlans | Where-Object { $_.Name -ne 'framework' }) {
        $moduleMount = $plan.MountPath -replace '\\', '/'
        $converted = $converted.Replace("../src/Modules/$($plan.SourceFolder)/", "../$moduleMount/")
        $converted = $converted.Replace("../../src/Modules/$($plan.SourceFolder)/", "../../$moduleMount/")
        $converted = $converted.Replace("(src/Modules/$($plan.SourceFolder)/", "($moduleMount/")
    }

    $converted = $converted.Replace(
        'Reusable framework and module documentation lives with the source that owns it. In this monorepo staging layout those docs are still under staged source-package roots:',
        'Reusable framework and module documentation lives with the source that owns it. In the source-first skeleton layout those docs live under mounted GMA repositories:')
    $converted = $converted.Replace(
        'Keep reusable framework docs in `gma/framework/docs/` with the framework source repository.',
        'Keep reusable framework docs in `gma/framework/docs/` once framework source is mounted as a submodule.')
    $converted = $converted.Replace(
        'Keep reusable module docs in `src/Modules/<Module>/docs/` while modules are staged in this monorepo.',
        'Keep reusable module docs in `gma/modules/<alias>/docs/` once modules are mounted as submodules.')

    return $converted
}

function Rewrite-GmaStage9SkeletonDocsForSubmoduleLayout {
    param([Parameter(Mandatory = $true)][string] $RootPath)

    $resolvedRootPath = Resolve-GmaStage9Path $RootPath
    $repositoryRoot = [System.IO.Path]::GetFullPath((Get-GmaRepositoryRoot)).TrimEnd('\', '/')
    $normalizedRootPath = [System.IO.Path]::GetFullPath($resolvedRootPath).TrimEnd('\', '/')
    if ([System.String]::Equals($normalizedRootPath, $repositoryRoot, [System.StringComparison]::OrdinalIgnoreCase) -and
        -not $Force) {
        throw "Refusing to rewrite working-tree skeleton docs without -Force: $normalizedRootPath"
    }

    $markdownFiles = @()
    $readmePath = Join-Path $resolvedRootPath 'README.md'
    if (Test-Path -LiteralPath $readmePath -PathType Leaf) {
        $markdownFiles += $readmePath
    }

    $docsPath = Join-Path $resolvedRootPath 'docs'
    if (Test-Path -LiteralPath $docsPath -PathType Container) {
        $markdownFiles += @(Get-ChildItem -LiteralPath $docsPath -Filter '*.md' -Recurse | ForEach-Object { $_.FullName })
    }

    foreach ($markdownFile in $markdownFiles) {
        $source = [System.IO.File]::ReadAllText($markdownFile)
        $converted = Convert-GmaStage9DocumentationText $source
        if ($converted -ne $source -and $PSCmdlet.ShouldProcess($markdownFile, 'Rewrite skeleton docs for submodule layout')) {
            [System.IO.File]::WriteAllText($markdownFile, $converted, [System.Text.UTF8Encoding]::new($false))
        }
    }
}

function Sync-GmaStage9SkeletonOwnedArtifacts {
    param([Parameter(Mandatory = $true)][string] $DestinationRoot)

    $sourceRoot = Get-GmaRepositoryRoot
    $docsSourcePath = Join-Path $sourceRoot 'docs'
    $docsDestinationPath = Join-Path $DestinationRoot 'docs'
    if (Test-Path -LiteralPath $docsDestinationPath -PathType Container) {
        Remove-Item -LiteralPath $docsDestinationPath -Recurse -Force
    }

    Copy-Item -LiteralPath $docsSourcePath -Destination $DestinationRoot -Recurse -Force
    Copy-Item -LiteralPath (Join-Path $sourceRoot 'README.md') -Destination (Join-Path $DestinationRoot 'README.md') -Force

    foreach ($scriptName in @('gma-github-stage8.ps1', 'gma-stage9.ps1', 'new-gma-app.ps1')) {
        $sourceScript = Join-Path $sourceRoot "eng\$scriptName"
        if (Test-Path -LiteralPath $sourceScript -PathType Leaf) {
            Copy-Item -LiteralPath $sourceScript -Destination (Join-Path $DestinationRoot "eng\$scriptName") -Force
        }
    }

    Write-GmaStage9ConvertedSolution (Join-Path $DestinationRoot 'GMA-Skeleton.slnx')
    Rewrite-GmaStage9SkeletonDocsForSubmoduleLayout $DestinationRoot

    foreach ($solutionFile in Get-GmaStage9RootPackageSolutionFiles) {
        $solutionPath = Join-Path $DestinationRoot $solutionFile
        if (Test-Path -LiteralPath $solutionPath -PathType Leaf) {
            Remove-Item -LiteralPath $solutionPath -Force
        }
    }
}

function New-GmaStage9LocalRehearsal {
    $stageRootPath = Resolve-GmaStage9Path $StageRoot
    $skeletonPath = Join-Path $stageRootPath 'skeleton'
    $rehearsalPath = Resolve-GmaStage9Path $LocalRehearsalPath
    Assert-GmaStage9SafeRehearsalPath $rehearsalPath

    if (-not (Test-GmaStage9StandaloneGitRepository $skeletonPath)) {
        throw "Stage 9 local rehearsal requires a standalone skeleton candidate repository: $skeletonPath"
    }

    $missingCandidates = @(Get-GmaStage9SubmodulePlans |
        Where-Object { -not (Test-GmaStage9StandaloneGitRepository $_.LocalCandidatePath) } |
        ForEach-Object { $_.LocalCandidatePath })
    if ($missingCandidates.Count -gt 0) {
        throw "Stage 9 local rehearsal is missing candidate repositories:`n - $($missingCandidates -join "`n - ")"
    }

    if (Test-Path -LiteralPath $rehearsalPath) {
        if (-not $Force) {
            throw "Stage 9 rehearsal path already exists. Rerun with -Force to recreate it: $rehearsalPath"
        }

        if ($PSCmdlet.ShouldProcess($rehearsalPath, 'Remove existing Stage 9 local rehearsal')) {
            Remove-Item -LiteralPath $rehearsalPath -Recurse -Force
        }
    }

    $parent = Split-Path -Parent $rehearsalPath
    if (-not (Test-Path -LiteralPath $parent -PathType Container)) {
        New-Item -ItemType Directory -Force -Path $parent | Out-Null
    }

    if ($PSCmdlet.ShouldProcess($rehearsalPath, 'Clone Stage 8D skeleton candidate for local submodule rehearsal')) {
        & git clone $skeletonPath $rehearsalPath
        if ($LASTEXITCODE -ne 0) {
            throw "git clone failed with exit code $LASTEXITCODE."
        }
    }

    if ($PSCmdlet.ShouldProcess($rehearsalPath, 'Overlay current skeleton-owned docs, scripts, and converted solution')) {
        Sync-GmaStage9SkeletonOwnedArtifacts $rehearsalPath
    }

    foreach ($plan in Get-GmaStage9SubmodulePlans) {
        $submoduleSource = [System.IO.Path]::GetFullPath($plan.LocalCandidatePath)
        $submodulePath = $plan.MountPath -replace '\\', '/'
        if ($PSCmdlet.ShouldProcess((Join-Path $rehearsalPath $plan.MountPath), "Add local submodule from $submoduleSource")) {
            Invoke-GmaStage9Command `
                -WorkingDirectory $rehearsalPath `
                -Arguments @('-c', 'protocol.file.allow=always', 'submodule', 'add', '-f', '-b', $DefaultBranch, $submoduleSource, $submodulePath)
        }
    }

    $bootstrapScript = Join-Path $rehearsalPath 'eng\gma-bootstrap.ps1'
    if ($PSCmdlet.ShouldProcess($rehearsalPath, 'Bootstrap submodule source roots')) {
        & powershell -NoProfile -ExecutionPolicy Bypass -File $bootstrapScript -SourceLayout GmaSubmodules -Force
        if ($LASTEXITCODE -ne 0) {
            throw "Stage 9 local rehearsal bootstrap failed with exit code $LASTEXITCODE."
        }
    }

    Invoke-GmaStage9Command -WorkingDirectory $rehearsalPath -Arguments @('submodule', 'status', '--recursive')

    if (-not $SkipLocalValidation) {
        $solutionPath = Join-Path $rehearsalPath 'GMA-Skeleton.slnx'
        Invoke-GmaDotNet -WorkingDirectory $rehearsalPath -Arguments @('restore', $solutionPath)
        Invoke-GmaDotNet -WorkingDirectory $rehearsalPath -Arguments @('build', $solutionPath, '--no-restore', '-m:1')
    }

    Write-Host "Stage 9 local submodule rehearsal ready: $rehearsalPath"
}

function Ensure-GmaStage9LocalRehearsalCommit {
    param([Parameter(Mandatory = $true)][string] $RehearsalPath)

    if (-not (Test-GmaStage9StandaloneGitRepository $RehearsalPath)) {
        throw "Stage 9 recursive clone proof requires a standalone rehearsal repository: $RehearsalPath"
    }

    if (-not (Test-Path -LiteralPath (Join-Path $RehearsalPath '.gitmodules') -PathType Leaf)) {
        throw "Stage 9 recursive clone proof requires .gitmodules in the rehearsal repository. Run -CreateLocalRehearsal first."
    }

    Set-GmaStage9LocalGitIdentity $RehearsalPath

    $status = @(git -C $RehearsalPath status --porcelain)
    if ($LASTEXITCODE -ne 0) {
        throw "git status failed in Stage 9 rehearsal repository with exit code $LASTEXITCODE."
    }

    if ($status.Count -eq 0) {
        return
    }

    Invoke-GmaStage9Command -WorkingDirectory $RehearsalPath -Arguments @('add', '-A')
    Invoke-GmaStage9Command -WorkingDirectory $RehearsalPath -Arguments @('commit', '-m', 'Stage 9 local submodule rehearsal')
}

function New-GmaStage9LocalRecursiveCloneProof {
    $rehearsalPath = Resolve-GmaStage9Path $LocalRehearsalPath
    $cloneProofPath = Resolve-GmaStage9Path $LocalCloneProofPath
    Assert-GmaStage9SafeRehearsalPath $cloneProofPath

    if (-not (Test-Path -LiteralPath $rehearsalPath -PathType Container)) {
        throw "Stage 9 recursive clone proof requires a local rehearsal repository. Run -CreateLocalRehearsal first: $rehearsalPath"
    }

    Ensure-GmaStage9LocalRehearsalCommit $rehearsalPath

    if (Test-Path -LiteralPath $cloneProofPath) {
        if (-not $Force) {
            throw "Stage 9 recursive clone proof path already exists. Rerun with -Force to recreate it: $cloneProofPath"
        }

        if ($PSCmdlet.ShouldProcess($cloneProofPath, 'Remove existing Stage 9 recursive clone proof')) {
            Remove-Item -LiteralPath $cloneProofPath -Recurse -Force
        }
    }

    $parent = Split-Path -Parent $cloneProofPath
    if (-not (Test-Path -LiteralPath $parent -PathType Container)) {
        New-Item -ItemType Directory -Force -Path $parent | Out-Null
    }

    if ($PSCmdlet.ShouldProcess($cloneProofPath, 'Clone Stage 9 rehearsal with recursive local submodules')) {
        $previousGitAllowProtocol = $env:GIT_ALLOW_PROTOCOL
        $env:GIT_ALLOW_PROTOCOL = 'file:git:ssh:https'
        try {
            & git -c protocol.file.allow=always clone --recurse-submodules $rehearsalPath $cloneProofPath
            if ($LASTEXITCODE -ne 0) {
                throw "git clone --recurse-submodules failed with exit code $LASTEXITCODE."
            }
        }
        finally {
            $env:GIT_ALLOW_PROTOCOL = $previousGitAllowProtocol
        }
    }

    $bootstrapScript = Join-Path $cloneProofPath 'eng\gma-bootstrap.ps1'
    if ($PSCmdlet.ShouldProcess($cloneProofPath, 'Bootstrap recursive clone source roots')) {
        & powershell -NoProfile -ExecutionPolicy Bypass -File $bootstrapScript -SourceLayout GmaSubmodules -Force
        if ($LASTEXITCODE -ne 0) {
            throw "Stage 9 recursive clone bootstrap failed with exit code $LASTEXITCODE."
        }
    }

    Invoke-GmaStage9Command -WorkingDirectory $cloneProofPath -Arguments @('submodule', 'status', '--recursive')

    if (-not $SkipLocalValidation) {
        $solutionPath = Join-Path $cloneProofPath 'GMA-Skeleton.slnx'
        Invoke-GmaDotNet -WorkingDirectory $cloneProofPath -Arguments @('restore', $solutionPath)
        Invoke-GmaDotNet -WorkingDirectory $cloneProofPath -Arguments @('build', $solutionPath, '--no-restore', '-m:1')
    }

    Write-Host "Stage 9 recursive clone proof ready: $cloneProofPath"
}

function Invoke-GmaStage9Audit {
    $stageRootPath = Resolve-GmaStage9Path $StageRoot
    $skeletonPath = Join-Path $stageRootPath 'skeleton'
    $skeletonSolutionPath = Join-Path $skeletonPath 'GMA-Skeleton.slnx'
    $skeletonSourceRootExamplePath = Join-Path $skeletonPath 'Gma.SourceRoots.props.example'

    Write-Host 'Stage 9 submodule conversion audit:'
    Write-Host "Repository: $(Get-GmaRepositoryRoot)"
    Write-Host "Stage root: $stageRootPath"
    Write-Host ''

    $skeletonSolutionReady = (Test-Path -LiteralPath $skeletonSolutionPath -PathType Leaf) -and
        (Select-String -LiteralPath $skeletonSolutionPath -Pattern 'gma/framework' -Quiet) -and
        (Select-String -LiteralPath $skeletonSolutionPath -Pattern 'gma/modules/auth' -Quiet)
    $skeletonSourceRootsReady = Test-Path -LiteralPath $skeletonSourceRootExamplePath -PathType Leaf
    $skeletonRepositoryReady = Test-GmaStage9StandaloneGitRepository $skeletonPath

    [pscustomobject] @{
        Check = 'skeleton-candidate-git'
        Ready = $skeletonRepositoryReady
        Detail = $skeletonPath
    }
    [pscustomobject] @{
        Check = 'skeleton-slnx-gma-paths'
        Ready = $skeletonSolutionReady
        Detail = $skeletonSolutionPath
    }
    [pscustomobject] @{
        Check = 'skeleton-source-root-example'
        Ready = $skeletonSourceRootsReady
        Detail = $skeletonSourceRootExamplePath
    }

    Write-Host ''
    Get-GmaStage9SubmodulePlans | ForEach-Object {
        $remoteUrl = Get-GmaStage9RemoteUrl $_.Repository
        [pscustomobject] @{
            Name = $_.Name
            MountPath = $_.MountPath
            LocalCandidateReady = Test-GmaStage9StandaloneGitRepository $_.LocalCandidatePath
            MountState = Get-GmaStage9MountState $_.MountPath
            RemoteReachable = Test-GmaStage9RemoteReachable $remoteUrl
            Remote = $remoteUrl
        }
    } | Format-Table -AutoSize
}

$hasAction = $Audit -or
    $PrintCommands -or
    $WriteCommandPlan -or
    $CreateLocalRehearsal -or
    $ProveLocalRecursiveClone -or
    $WriteConvertedSolution -or
    $RewriteSkeletonDocsForSubmoduleLayout

if (-not $hasAction) {
    Write-Host 'Stage 9 submodule plan:'
    Get-GmaStage9SubmodulePlans | ForEach-Object {
        [pscustomobject] @{
            Name = $_.Name
            Repository = "$Owner/$($_.Repository)"
            MountPath = $_.MountPath
            Remote = Get-GmaStage9RemoteUrl $_.Repository
        }
    } | Format-Table -AutoSize
    Write-Host 'No changes were requested. Add -Audit, -PrintCommands, -WriteCommandPlan, -CreateLocalRehearsal, -ProveLocalRecursiveClone, -WriteConvertedSolution, or -RewriteSkeletonDocsForSubmoduleLayout.'
    return
}

if ($WriteConvertedSolution) {
    Write-GmaStage9ConvertedSolution $ConvertedSolutionPath
}

if ($RewriteSkeletonDocsForSubmoduleLayout) {
    Rewrite-GmaStage9SkeletonDocsForSubmoduleLayout $SkeletonRootPath
}

if ($Audit) {
    Invoke-GmaStage9Audit
}

if ($PrintCommands) {
    Get-GmaStage9CommandPlanLines | ForEach-Object { Write-Output $_ }
}

if ($WriteCommandPlan) {
    $resolvedCommandPlanPath = Resolve-GmaStage9Path $CommandPlanPath
    $directory = Split-Path -Parent $resolvedCommandPlanPath
    if (-not (Test-Path -LiteralPath $directory -PathType Container)) {
        New-Item -ItemType Directory -Force -Path $directory | Out-Null
    }

    if ($PSCmdlet.ShouldProcess($resolvedCommandPlanPath, 'Write Stage 9 command plan')) {
        [System.IO.File]::WriteAllLines(
            $resolvedCommandPlanPath,
            (Get-GmaStage9CommandPlanLines),
            [System.Text.UTF8Encoding]::new($false))
        Write-Host "Wrote Stage 9 command plan: $resolvedCommandPlanPath"
    }
}

if ($CreateLocalRehearsal) {
    New-GmaStage9LocalRehearsal
}

if ($ProveLocalRecursiveClone) {
    New-GmaStage9LocalRecursiveCloneProof
}

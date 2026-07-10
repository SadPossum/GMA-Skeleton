param(
    [switch] $SkipRestore,
    [switch] $SkipBuild
)

. (Join-Path $PSScriptRoot 'common.ps1')

# Validates the source-first skeleton layout. Framework and reusable-module
# packages are mounted as submodules, so their focused solutions must be
# package-local and use local docs/eng/src/tests paths.

function New-GmaSourcePackage {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Solution,

        [Parameter(Mandatory = $true)]
        [string[]] $AllowedFolders,

        [Parameter(Mandatory = $true)]
        [string[]] $RequiredPaths
    )

    [pscustomobject] @{
        Solution = $Solution
        AllowedFolders = $AllowedFolders
        RequiredPaths = $RequiredPaths
    }
}

function ConvertTo-GmaSolutionPath {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Path
    )

    return $Path.Replace('\', '/')
}

function Get-GmaSolutionEntryPaths {
    param(
        [Parameter(Mandatory = $true)]
        [xml] $SolutionXml
    )

    return @($SolutionXml.SelectNodes('//*[@Path]') | ForEach-Object {
        ConvertTo-GmaSolutionPath -Path $_.GetAttribute('Path')
    } | Sort-Object)
}

function Test-GmaSolutionContainsPath {
    param(
        [Parameter(Mandatory = $true)]
        [string[]] $EntryPaths,

        [Parameter(Mandatory = $true)]
        [string] $Path
    )

    $normalizedPath = ConvertTo-GmaSolutionPath -Path $Path
    return $EntryPaths -contains $normalizedPath
}

function Get-GmaRelativePath {
    param(
        [Parameter(Mandatory = $true)]
        [string] $BasePath,

        [Parameter(Mandatory = $true)]
        [string] $TargetPath
    )

    $baseFullPath = [System.IO.Path]::GetFullPath($BasePath)
    if (-not ($baseFullPath.EndsWith('\', [System.StringComparison]::Ordinal) -or
            $baseFullPath.EndsWith('/', [System.StringComparison]::Ordinal))) {
        $baseFullPath += [System.IO.Path]::DirectorySeparatorChar
    }

    $targetFullPath = [System.IO.Path]::GetFullPath($TargetPath)
    $baseUri = [System.Uri]::new($baseFullPath)
    $targetUri = [System.Uri]::new($targetFullPath)
    return [System.Uri]::UnescapeDataString($baseUri.MakeRelativeUri($targetUri).ToString()).Replace('/', [System.IO.Path]::DirectorySeparatorChar)
}

$packages = @(
    New-GmaSourcePackage `
        -Solution 'gma\framework\Gma.Framework.slnx' `
        -AllowedFolders @('/.github/', '/Solution Items/', '/docs/', '/eng/', '/src/', '/tests/') `
        -RequiredPaths @(
            'gma\framework\docs\README.md',
            'gma\framework\eng\new-module.ps1',
            'gma\framework\tests\Gma.Framework.Tests\Gma.Framework.Tests.csproj'
        )
    New-GmaSourcePackage `
        -Solution 'gma\modules\access-control\Gma.Modules.AccessControl.slnx' `
        -AllowedFolders @('/.github/', '/Solution Items/', '/docs/', '/eng/', '/src/', '/tests/') `
        -RequiredPaths @(
            'gma\modules\access-control\docs\README.md',
            'gma\modules\access-control\src\Gma.Modules.AccessControl.AdminApi\Gma.Modules.AccessControl.AdminApi.csproj',
            'gma\modules\access-control\src\Gma.Modules.AccessControl.AdminCli\Gma.Modules.AccessControl.AdminCli.csproj',
            'gma\modules\access-control\src\Gma.Modules.AccessControl.Application\Gma.Modules.AccessControl.Application.csproj',
            'gma\modules\access-control\tests\Gma.Modules.AccessControl.Tests\Gma.Modules.AccessControl.Tests.csproj'
        )
    New-GmaSourcePackage `
        -Solution 'gma\modules\administration\Gma.Modules.Administration.slnx' `
        -AllowedFolders @('/.github/', '/Solution Items/', '/docs/', '/eng/', '/src/', '/tests/') `
        -RequiredPaths @(
            'gma\modules\administration\docs\README.md',
            'gma\modules\administration\src\Gma.Modules.Administration.Application\Gma.Modules.Administration.Application.csproj',
            'gma\modules\administration\tests\Gma.Modules.Administration.Tests\Gma.Modules.Administration.Tests.csproj'
        )
    New-GmaSourcePackage `
        -Solution 'gma\modules\auth\Gma.Modules.Auth.slnx' `
        -AllowedFolders @('/.github/', '/Solution Items/', '/docs/', '/eng/', '/src/', '/tests/') `
        -RequiredPaths @(
            'gma\modules\auth\docs\README.md',
            'gma\modules\auth\src\Gma.Modules.Auth.Application\Gma.Modules.Auth.Application.csproj',
            'gma\modules\auth\tests\Gma.Modules.Auth.Tests\Gma.Modules.Auth.Tests.csproj'
        )
    New-GmaSourcePackage `
        -Solution 'gma\modules\files\Gma.Modules.Files.slnx' `
        -AllowedFolders @('/.github/', '/Solution Items/', '/docs/', '/eng/', '/src/', '/tests/') `
        -RequiredPaths @(
            'gma\modules\files\docs\README.md',
            'gma\modules\files\src\Gma.Modules.Files.Api\Gma.Modules.Files.Api.csproj'
        )
    New-GmaSourcePackage `
        -Solution 'gma\modules\notifications\Gma.Modules.Notifications.slnx' `
        -AllowedFolders @('/.github/', '/Solution Items/', '/docs/', '/eng/', '/src/', '/tests/') `
        -RequiredPaths @(
            'gma\modules\notifications\docs\README.md',
            'gma\modules\notifications\src\Gma.Modules.Notifications.Application\Gma.Modules.Notifications.Application.csproj',
            'gma\modules\notifications\tests\Gma.Modules.Notifications.Tests\Gma.Modules.Notifications.Tests.csproj'
        )
    New-GmaSourcePackage `
        -Solution 'gma\modules\task-runtime\Gma.Modules.TaskRuntime.slnx' `
        -AllowedFolders @('/.github/', '/Solution Items/', '/docs/', '/eng/', '/src/', '/tests/') `
        -RequiredPaths @(
            'gma\modules\task-runtime\docs\README.md',
            'gma\modules\task-runtime\src\Gma.Modules.TaskRuntime.Application\Gma.Modules.TaskRuntime.Application.csproj'
        )
    New-GmaSourcePackage `
        -Solution 'gma\modules\tenancy\Gma.Modules.Tenancy.slnx' `
        -AllowedFolders @('/.github/', '/Solution Items/', '/docs/', '/eng/', '/src/', '/tests/') `
        -RequiredPaths @(
            'gma\modules\tenancy\docs\README.md',
            'gma\modules\tenancy\src\Gma.Modules.Tenancy.Api\Gma.Modules.Tenancy.Api.csproj'
        )
)

$allowedPackageRootFiles = @(
    '.editorconfig',
    '.gitattributes',
    '.gitignore',
    'Directory.Build.props',
    'Directory.Packages.props',
    'global.json',
    'Gma.SourceRoots.props.example',
    'LICENSE',
    'nuget.config',
    'README.md'
)

$errors = [System.Collections.Generic.List[string]]::new()

foreach ($package in $packages) {
    $solutionPath = Join-GmaPath $package.Solution
    if (-not (Test-Path -LiteralPath $solutionPath -PathType Leaf)) {
        $errors.Add("$($package.Solution) is missing.")
        continue
    }

    try {
        $solutionText = Get-Content -LiteralPath $solutionPath -Raw
        [xml] $solutionXml = $solutionText
    }
    catch {
        $errors.Add("$($package.Solution) is not valid XML: $($_.Exception.Message)")
        continue
    }

    $entryPaths = Get-GmaSolutionEntryPaths -SolutionXml $solutionXml

    foreach ($folder in $solutionXml.SelectNodes('//Folder')) {
        $folderName = $folder.GetAttribute('Name')
        $isAllowedFolder = @($package.AllowedFolders | Where-Object {
            $folderName -eq $_ -or $folderName.StartsWith($_, [System.StringComparison]::Ordinal)
        }).Count -gt 0
        if (-not $isAllowedFolder) {
            $errors.Add("$($package.Solution) contains non-package-local folder '$folderName'.")
        }
    }

    foreach ($entryPath in $entryPaths) {
        if ($entryPath.StartsWith('../', [System.StringComparison]::Ordinal) -or
            $entryPath.StartsWith('/', [System.StringComparison]::Ordinal)) {
            $errors.Add("$($package.Solution) lists non-local entry '$entryPath'.")
            continue
        }

        if ($allowedPackageRootFiles -contains $entryPath) {
            continue
        }

        $firstSegment = ($entryPath -split '/', 2)[0]
        if ($package.AllowedFolders -notcontains "/$firstSegment/") {
            $errors.Add("$($package.Solution) lists '$entryPath' outside allowed package folders.")
        }
    }

    foreach ($requiredPath in $package.RequiredPaths) {
        $absoluteRequiredPath = Join-GmaPath $requiredPath
        if (-not (Test-Path -LiteralPath $absoluteRequiredPath)) {
            $errors.Add("$($package.Solution) required path '$requiredPath' does not exist.")
            continue
        }

        $solutionDirectory = [System.IO.Path]::GetDirectoryName($solutionPath)
        if ([string]::IsNullOrWhiteSpace($solutionDirectory)) {
            $errors.Add("$($package.Solution) has no solution directory.")
            continue
        }

        $expectedEntry = Get-GmaRelativePath -BasePath $solutionDirectory -TargetPath $absoluteRequiredPath

        if (-not (Test-GmaSolutionContainsPath -EntryPaths $entryPaths -Path $expectedEntry)) {
            $errors.Add("$($package.Solution) does not list required path '$expectedEntry'.")
        }
    }
}

$staleRootPaths = @(
    'docs/adr',
    'docs/guidelines',
    'docs/modules',
    'docs/templates',
    'docs/examples/catalog-module.md',
    'docs/examples/ordering-module.md',
    'docs/examples/task-samples-module.md',
    'src/Framework',
    'src/Modules/Administration',
    'src/Modules/Auth',
    'src/Modules/Files',
    'src/Modules/Notifications',
    'src/Modules/TaskRuntime',
    'src/Modules/Tenancy'
)

foreach ($staleRootPath in $staleRootPaths) {
    if (Test-Path -LiteralPath (Join-GmaPath $staleRootPath)) {
        $errors.Add("Stale root-owned path '$staleRootPath' should live with the owning source package.")
    }
}

$allowedRootTestProjects = @(
    'Architecture.Tests',
    'Integration.Tests',
    'ServiceDefaults.Tests'
)

$rootTestsPath = Join-GmaPath 'tests'
if (Test-Path -LiteralPath $rootTestsPath -PathType Container) {
    foreach ($testProject in Get-ChildItem -LiteralPath $rootTestsPath -Filter '*.csproj' -Recurse) {
        $projectName = [System.IO.Path]::GetFileNameWithoutExtension($testProject.Name)
        if ($allowedRootTestProjects -notcontains $projectName) {
            $repositoryRoot = Get-GmaRepositoryRoot
            $relativePath = $testProject.FullName
            if ($relativePath.StartsWith($repositoryRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
                $relativePath = $relativePath.Substring($repositoryRoot.Length).TrimStart('\', '/')
            }

            $errors.Add("Root test project '$relativePath' should live under its owning source package.")
        }
    }
}

if ($errors.Count -gt 0) {
    throw "Source package checks failed:`n - $($errors -join "`n - ")"
}

foreach ($package in $packages) {
    $solutionPath = Join-GmaPath $package.Solution

    if (-not $SkipRestore) {
        Invoke-GmaDotNet -Arguments @('restore', $solutionPath)
    }

    if (-not $SkipBuild) {
        Invoke-GmaDotNet -Arguments @('build', $solutionPath, '--no-restore', '-m:1')
    }
}

Write-Host 'Source-package checks passed.'

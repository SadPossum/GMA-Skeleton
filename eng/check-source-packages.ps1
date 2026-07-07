param(
    [switch] $SkipRestore,
    [switch] $SkipBuild
)

. (Join-Path $PSScriptRoot 'common.ps1')

function New-GmaSourcePackage {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Solution,

        [Parameter(Mandatory = $true)]
        [string] $SourcePrefix,

        [Parameter(Mandatory = $true)]
        [string[]] $AllowedFolders,

        [Parameter(Mandatory = $true)]
        [string[]] $RequiredPaths
    )

    [pscustomobject] @{
        Solution = $Solution
        SourcePrefix = $SourcePrefix
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

function Test-GmaSolutionContainsPath {
    param(
        [Parameter(Mandatory = $true)]
        [string] $SolutionText,

        [Parameter(Mandatory = $true)]
        [string] $Path
    )

    $normalizedPath = ConvertTo-GmaSolutionPath -Path $Path
    return $SolutionText.Contains("Path=`"$normalizedPath`"")
}

$packages = @(
    New-GmaSourcePackage `
        -Solution 'Gma.Framework.slnx' `
        -SourcePrefix 'src/Framework/' `
        -AllowedFolders @('/docs/', '/eng/', '/src/', '/tests/') `
        -RequiredPaths @(
            'src/Framework/docs/README.md',
            'src/Framework/eng/new-module.ps1',
            'src/Framework/tests/Gma.Framework.Tests/Gma.Framework.Tests.csproj'
        )
    New-GmaSourcePackage `
        -Solution 'Gma.Modules.Administration.slnx' `
        -SourcePrefix 'src/Modules/Administration/' `
        -AllowedFolders @('/docs/', '/src/', '/tests/') `
        -RequiredPaths @(
            'src/Modules/Administration/docs/README.md',
            'src/Modules/Administration/Gma.Modules.Administration.Application/Gma.Modules.Administration.Application.csproj',
            'src/Modules/Administration/tests/Gma.Modules.Administration.Tests/Gma.Modules.Administration.Tests.csproj'
        )
    New-GmaSourcePackage `
        -Solution 'Gma.Modules.Auth.slnx' `
        -SourcePrefix 'src/Modules/Auth/' `
        -AllowedFolders @('/docs/', '/src/', '/tests/') `
        -RequiredPaths @(
            'src/Modules/Auth/docs/README.md',
            'src/Modules/Auth/Gma.Modules.Auth.Application/Gma.Modules.Auth.Application.csproj',
            'src/Modules/Auth/tests/Gma.Modules.Auth.Tests/Gma.Modules.Auth.Tests.csproj'
        )
    New-GmaSourcePackage `
        -Solution 'Gma.Modules.Catalog.slnx' `
        -SourcePrefix 'src/Modules/Catalog/' `
        -AllowedFolders @('/docs/', '/src/', '/tests/') `
        -RequiredPaths @(
            'src/Modules/Catalog/docs/README.md',
            'src/Modules/Catalog/Catalog.Application/Catalog.Application.csproj',
            'src/Modules/Catalog/tests/Catalog.Tests/Catalog.Tests.csproj'
        )
    New-GmaSourcePackage `
        -Solution 'Gma.Modules.Files.slnx' `
        -SourcePrefix 'src/Modules/Files/' `
        -AllowedFolders @('/docs/', '/src/', '/tests/') `
        -RequiredPaths @(
            'src/Modules/Files/docs/README.md',
            'src/Modules/Files/Gma.Modules.Files.Api/Gma.Modules.Files.Api.csproj'
        )
    New-GmaSourcePackage `
        -Solution 'Gma.Modules.Notifications.slnx' `
        -SourcePrefix 'src/Modules/Notifications/' `
        -AllowedFolders @('/docs/', '/src/', '/tests/') `
        -RequiredPaths @(
            'src/Modules/Notifications/docs/README.md',
            'src/Modules/Notifications/Gma.Modules.Notifications.Application/Gma.Modules.Notifications.Application.csproj',
            'src/Modules/Notifications/tests/Gma.Modules.Notifications.Tests/Gma.Modules.Notifications.Tests.csproj'
        )
    New-GmaSourcePackage `
        -Solution 'Gma.Modules.Ordering.slnx' `
        -SourcePrefix 'src/Modules/Ordering/' `
        -AllowedFolders @('/docs/', '/src/', '/tests/') `
        -RequiredPaths @(
            'src/Modules/Ordering/docs/README.md',
            'src/Modules/Ordering/Ordering.Application/Ordering.Application.csproj',
            'src/Modules/Ordering/tests/Ordering.Tests/Ordering.Tests.csproj'
        )
    New-GmaSourcePackage `
        -Solution 'Gma.Modules.TaskRuntime.slnx' `
        -SourcePrefix 'src/Modules/TaskRuntime/' `
        -AllowedFolders @('/docs/', '/src/', '/tests/') `
        -RequiredPaths @(
            'src/Modules/TaskRuntime/docs/README.md',
            'src/Modules/TaskRuntime/Gma.Modules.TaskRuntime.Application/Gma.Modules.TaskRuntime.Application.csproj'
        )
    New-GmaSourcePackage `
        -Solution 'Gma.Modules.TaskSamples.slnx' `
        -SourcePrefix 'src/Modules/TaskSamples/' `
        -AllowedFolders @('/docs/', '/src/', '/tests/') `
        -RequiredPaths @(
            'src/Modules/TaskSamples/docs/README.md',
            'src/Modules/TaskSamples/TaskSamples.Application/TaskSamples.Application.csproj'
        )
    New-GmaSourcePackage `
        -Solution 'Gma.Modules.Tenancy.slnx' `
        -SourcePrefix 'src/Modules/Tenancy/' `
        -AllowedFolders @('/docs/', '/src/', '/tests/') `
        -RequiredPaths @(
            'src/Modules/Tenancy/docs/README.md',
            'src/Modules/Tenancy/Gma.Modules.Tenancy.Api/Gma.Modules.Tenancy.Api.csproj'
        )
)

$errors = [System.Collections.Generic.List[string]]::new()

foreach ($package in $packages) {
    $solutionPath = Join-GmaPath $package.Solution
    if (-not (Test-Path -LiteralPath $solutionPath -PathType Leaf)) {
        $errors.Add("$($package.Solution) is missing.")
        continue
    }

    $solutionText = Get-Content -LiteralPath $solutionPath -Raw
    try {
        [xml] $solutionXml = $solutionText
    }
    catch {
        $errors.Add("$($package.Solution) is not valid XML: $($_.Exception.Message)")
        continue
    }

    foreach ($folder in $solutionXml.SelectNodes('//Folder')) {
        $folderName = $folder.GetAttribute('Name')
        if ($package.AllowedFolders -notcontains $folderName) {
            $errors.Add("$($package.Solution) contains non-package-local folder '$folderName'.")
        }
    }

    foreach ($node in $solutionXml.SelectNodes('//*[@Path]')) {
        $entryPath = ConvertTo-GmaSolutionPath -Path $node.GetAttribute('Path')
        if (-not $entryPath.StartsWith($package.SourcePrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
            $errors.Add("$($package.Solution) lists '$entryPath' outside '$($package.SourcePrefix)'.")
        }
    }

    foreach ($requiredPath in $package.RequiredPaths) {
        if (-not (Test-Path -LiteralPath (Join-GmaPath $requiredPath))) {
            $errors.Add("$($package.Solution) required path '$requiredPath' does not exist.")
        }

        if (-not (Test-GmaSolutionContainsPath -SolutionText $solutionText -Path $requiredPath)) {
            $errors.Add("$($package.Solution) does not list required path '$requiredPath'.")
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
    'docs/examples/task-samples-module.md'
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

Write-Host 'Source package dry-run checks passed.'

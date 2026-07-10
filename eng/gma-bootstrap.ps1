[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [switch] $Force,

    [ValidateSet('Monorepo', 'GmaSubmodules')]
    [string] $SourceLayout = 'Monorepo'
)

. (Join-Path $PSScriptRoot 'common.ps1')

function Write-GmaSourceRootsFile {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Path,

        [Parameter(Mandatory = $true)]
        [string[]] $Lines,

        [Parameter(Mandatory = $true)]
        [string] $Description
    )

    if ((Test-Path -LiteralPath $Path) -and -not $Force) {
        Write-Host "$Description already exists. Use -Force to refresh it: $Path"
        return
    }

    $directory = Split-Path -Parent $Path
    if (-not (Test-Path -LiteralPath $directory -PathType Container)) {
        if ($WhatIfPreference) {
            Write-Host "Would skip $Description because '$directory' does not exist yet."
            return
        }

        throw "Cannot write $Description because '$directory' does not exist. Run eng/gma-update.ps1 -Init after adding submodules, or mount the local source repositories first."
    }

    $action = if (Test-Path -LiteralPath $Path) { 'Overwrite' } else { 'Create' }
    if ($PSCmdlet.ShouldProcess($Path, "$action $Description")) {
        [System.IO.File]::WriteAllLines($Path, $Lines, [System.Text.UTF8Encoding]::new($false))
        Write-Host "$action ${Description}: $Path"
    }
}

function Get-GmaRootSourceRootsLines {
    return @(
        '<Project>',
        '  <PropertyGroup>',
        '    <GmaFrameworkRoot>$(MSBuildThisFileDirectory)gma\framework\src\</GmaFrameworkRoot>',
        '    <GmaModulesRoot>$(MSBuildThisFileDirectory)gma\modules\</GmaModulesRoot>',
        '    <GmaModuleAccessControlRoot>$(GmaModulesRoot)access-control\src\</GmaModuleAccessControlRoot>',
        '    <GmaModuleAdministrationRoot>$(GmaModulesRoot)administration\src\</GmaModuleAdministrationRoot>',
        '    <GmaModuleAuthRoot>$(GmaModulesRoot)auth\src\</GmaModuleAuthRoot>',
        '    <GmaModuleCatalogRoot>$(GmaRepositoryRoot)src\Modules\Catalog\</GmaModuleCatalogRoot>',
        '    <GmaModuleFilesRoot>$(GmaModulesRoot)files\src\</GmaModuleFilesRoot>',
        '    <GmaModuleNotificationsRoot>$(GmaModulesRoot)notifications\src\</GmaModuleNotificationsRoot>',
        '    <GmaModuleOrderingRoot>$(GmaRepositoryRoot)src\Modules\Ordering\</GmaModuleOrderingRoot>',
        '    <GmaModuleTaskRuntimeRoot>$(GmaModulesRoot)task-runtime\src\</GmaModuleTaskRuntimeRoot>',
        '    <GmaModuleTaskSamplesRoot>$(GmaRepositoryRoot)src\Modules\TaskSamples\</GmaModuleTaskSamplesRoot>',
        '    <GmaModuleTenancyRoot>$(GmaModulesRoot)tenancy\src\</GmaModuleTenancyRoot>',
        '  </PropertyGroup>',
        '</Project>'
    )
}

function Get-GmaFrameworkSourceRootsLines {
    return @(
        '<Project>',
        '  <PropertyGroup>',
        '    <GmaFrameworkRoot>$(MSBuildThisFileDirectory)src\</GmaFrameworkRoot>',
        '  </PropertyGroup>',
        '</Project>'
    )
}

function Get-GmaModuleSourceRootsLines {
    return @(
        '<Project>',
        '  <PropertyGroup>',
        '    <GmaFrameworkRoot>$(MSBuildThisFileDirectory)..\..\framework\src\</GmaFrameworkRoot>',
        '    <GmaModulesRoot>$(MSBuildThisFileDirectory)..\</GmaModulesRoot>',
        '    <GmaModuleAccessControlRoot>$(GmaModulesRoot)access-control\src\</GmaModuleAccessControlRoot>',
        '    <GmaModuleAdministrationRoot>$(GmaModulesRoot)administration\src\</GmaModuleAdministrationRoot>',
        '    <GmaModuleAuthRoot>$(GmaModulesRoot)auth\src\</GmaModuleAuthRoot>',
        '    <GmaModuleCatalogRoot>$(MSBuildThisFileDirectory)..\..\..\src\Modules\Catalog\</GmaModuleCatalogRoot>',
        '    <GmaModuleFilesRoot>$(GmaModulesRoot)files\src\</GmaModuleFilesRoot>',
        '    <GmaModuleNotificationsRoot>$(GmaModulesRoot)notifications\src\</GmaModuleNotificationsRoot>',
        '    <GmaModuleOrderingRoot>$(MSBuildThisFileDirectory)..\..\..\src\Modules\Ordering\</GmaModuleOrderingRoot>',
        '    <GmaModuleTaskRuntimeRoot>$(GmaModulesRoot)task-runtime\src\</GmaModuleTaskRuntimeRoot>',
        '    <GmaModuleTaskSamplesRoot>$(MSBuildThisFileDirectory)..\..\..\src\Modules\TaskSamples\</GmaModuleTaskSamplesRoot>',
        '    <GmaModuleTenancyRoot>$(GmaModulesRoot)tenancy\src\</GmaModuleTenancyRoot>',
        '  </PropertyGroup>',
        '</Project>'
    )
}

if ($SourceLayout -eq 'Monorepo') {
    $source = Join-GmaPath 'Gma.SourceRoots.props.example'
    $target = Join-GmaPath 'Gma.SourceRoots.props'

    if (-not (Test-Path -LiteralPath $source)) {
        throw "Missing source-root example file at '$source'."
    }

    if ((Test-Path -LiteralPath $target) -and -not $Force) {
        Write-Host "Gma.SourceRoots.props already exists. Use -Force to refresh it from the example."
        return
    }

    $action = if (Test-Path -LiteralPath $target) { 'Overwrite' } else { 'Create' }
    if ($PSCmdlet.ShouldProcess($target, "$action local source-root configuration")) {
        Copy-Item -LiteralPath $source -Destination $target -Force
        Write-Host "$action local source-root configuration: $target"
    }

    return
}

$moduleAliases = @(
    'access-control',
    'administration',
    'auth',
    'files',
    'notifications',
    'task-runtime',
    'tenancy'
)

$sourceRootTargets = [System.Collections.Generic.List[object]]::new()
$sourceRootTargets.Add([pscustomobject] @{
    Path = Join-GmaPath 'Gma.SourceRoots.props'
    Lines = Get-GmaRootSourceRootsLines
    Description = 'root app-style source-root configuration'
})
$sourceRootTargets.Add([pscustomobject] @{
    Path = Join-GmaPath 'gma\framework\Gma.SourceRoots.props'
    Lines = Get-GmaFrameworkSourceRootsLines
    Description = 'framework source-root configuration'
})

foreach ($moduleAlias in $moduleAliases) {
    $sourceRootTargets.Add([pscustomobject] @{
        Path = Join-GmaPath "gma\modules\$moduleAlias\Gma.SourceRoots.props"
        Lines = Get-GmaModuleSourceRootsLines
        Description = "$moduleAlias module source-root configuration"
    })
}

if (-not $WhatIfPreference) {
    $missingTargetDirectories = @($sourceRootTargets |
        ForEach-Object { Split-Path -Parent $_.Path } |
        Where-Object { -not (Test-Path -LiteralPath $_ -PathType Container) } |
        Sort-Object -Unique)

    if ($missingTargetDirectories.Count -gt 0) {
        throw "Cannot bootstrap GMA submodule source roots because these checkout directories are missing:`n - $($missingTargetDirectories -join "`n - ")`nRun eng/gma-update.ps1 -Init after adding submodules, or mount the local source repositories first."
    }
}

foreach ($target in $sourceRootTargets) {
    Write-GmaSourceRootsFile `
        -Path $target.Path `
        -Lines $target.Lines `
        -Description $target.Description
}

[CmdletBinding()]
param()

. (Join-Path $PSScriptRoot 'common.ps1')

$matrixRoot = Join-GmaPath '.tmp\generated-selection-matrix'
$temporaryRoot = [System.IO.Path]::GetFullPath((Join-GmaPath '.tmp')).TrimEnd('\', '/') + [System.IO.Path]::DirectorySeparatorChar
$resolvedMatrixRoot = [System.IO.Path]::GetFullPath($matrixRoot)
if (-not $resolvedMatrixRoot.StartsWith($temporaryRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing to clean generated selection matrix outside '$temporaryRoot': $resolvedMatrixRoot"
}

if (Test-Path -LiteralPath $resolvedMatrixRoot) {
    Remove-Item -LiteralPath $resolvedMatrixRoot -Recurse -Force
}

$cases = @(
    [pscustomobject] @{ Name = 'AccessControlOnly'; Modules = @('access-control'); Extensions = @() },
    [pscustomobject] @{ Name = 'AuthOnly'; Modules = @('auth'); Extensions = @() },
    [pscustomobject] @{ Name = 'NotificationsOnly'; Modules = @('notifications'); Extensions = @() },
    [pscustomobject] @{ Name = 'OrganizationsOnly'; Modules = @('organizations'); Extensions = @() },
    [pscustomobject] @{ Name = 'OrganizationsAccessControl'; Modules = @('organizations', 'access-control'); Extensions = @('Organizations.AccessControl') },
    [pscustomobject] @{ Name = 'OrganizationsTenancy'; Modules = @('organizations', 'tenancy'); Extensions = @('Organizations.Tenancy') },
    [pscustomobject] @{ Name = 'AuthNotifications'; Modules = @('auth', 'notifications'); Extensions = @('Auth.Notifications') },
    [pscustomobject] @{ Name = 'AuthOrganizations'; Modules = @('auth', 'organizations'); Extensions = @('Auth.Organizations') },
    [pscustomobject] @{ Name = 'AuthOrganizationsTenancy'; Modules = @('auth', 'organizations', 'tenancy'); Extensions = @('Auth.Organizations', 'Organizations.Tenancy') },
    [pscustomobject] @{
        Name = 'AdministrationAccessControl'
        Modules = @('administration', 'access-control')
        Hosts = @('Api', 'AdminApi', 'AdminCli')
        Extensions = @()
    },
    [pscustomobject] @{
        Name = 'AllAdmin'
        Modules = @('access-control', 'administration', 'auth', 'files', 'notifications', 'organizations', 'task-runtime', 'tenancy')
        Hosts = @('Api', 'AdminApi', 'AdminCli')
        Extensions = @('Auth.Notifications', 'Auth.Organizations', 'Organizations.AccessControl', 'Organizations.Tenancy')
    }
)

$sharedExtensionTokens = @(
    'GMA-Extensions',
    'GmaExtensionsRoot',
    'GmaModuleAccessControlRoot',
    'gma/extensions/Gma.SourceRoots.props'
)
$extensionTokens = @{
    'Auth.Notifications' = @('Gma.Extensions.Auth.Notifications', 'AddAuthNotificationsExtension')
    'Auth.Organizations' = @('Gma.Extensions.Auth.Organizations', 'AddAuthOrganizationsExtension')
    'Organizations.AccessControl' = @('Gma.Extensions.Organizations.AccessControl', 'AddOrganizationsAccessControlExtension')
    'Organizations.Tenancy' = @('Gma.Extensions.Organizations.Tenancy', 'AddOrganizationsTenancyExtension')
}

foreach ($case in $cases) {
    $outputPath = Join-Path $resolvedMatrixRoot $case.Name
    $caseHosts = if ($case.PSObject.Properties.Name -contains 'Hosts') { @($case.Hosts) } else { @('Api') }
    & (Join-Path $PSScriptRoot 'new-gma-app.ps1') `
        -Name $case.Name `
        -OutputPath $outputPath `
        -Modules $case.Modules `
        -Hosts $caseHosts

    $conditionalSurfacePaths = @(
        (Join-Path $outputPath 'Gma.SourceRoots.props.example'),
        (Join-Path $outputPath 'docs\gma-source.md'),
        (Join-Path $outputPath "src\Hosts\$($case.Name).Host.Api\$($case.Name).Host.Api.csproj"),
        (Join-Path $outputPath "src\Hosts\$($case.Name).Host.Api\Program.cs")
    )
    if ($caseHosts -contains 'AdminApi') {
        $conditionalSurfacePaths += @(
            (Join-Path $outputPath "src\Hosts\$($case.Name).Host.AdminApi\$($case.Name).Host.AdminApi.csproj"),
            (Join-Path $outputPath "src\Hosts\$($case.Name).Host.AdminApi\Program.cs"),
            (Join-Path $outputPath "src\Hosts\$($case.Name).Host.AdminApi\appsettings.json")
        )
    }
    if ($caseHosts -contains 'AdminCli') {
        $conditionalSurfacePaths += @(
            (Join-Path $outputPath "src\Hosts\$($case.Name).Host.AdminCli\$($case.Name).Host.AdminCli.csproj"),
            (Join-Path $outputPath "src\Hosts\$($case.Name).Host.AdminCli\Program.cs"),
            (Join-Path $outputPath "src\Hosts\$($case.Name).Host.AdminCli\appsettings.json")
        )
    }
    $surfacePaths = @(
        (Join-Path $outputPath 'Directory.Build.props'),
        (Join-Path $outputPath 'eng\gma-bootstrap.ps1')
    ) + $conditionalSurfacePaths
    $surface = $surfacePaths | ForEach-Object { [System.IO.File]::ReadAllText($_) }
    $combinedSurface = $surface -join "`n"
    $conditionalSurface = ($conditionalSurfacePaths | ForEach-Object { [System.IO.File]::ReadAllText($_) }) -join "`n"
    $expectedTokens = @($case.Extensions | ForEach-Object { $extensionTokens[$_] })
    $unexpectedExtensionTokens = @($extensionTokens.Keys |
        Where-Object { $case.Extensions -notcontains $_ } |
        ForEach-Object { $extensionTokens[$_] })
    $missingTokens = @(($sharedExtensionTokens + $expectedTokens) | Where-Object {
        $combinedSurface.IndexOf($_, [System.StringComparison]::Ordinal) -lt 0
    })
    $unexpectedTokens = @($unexpectedExtensionTokens | Where-Object {
        $conditionalSurface.IndexOf($_, [System.StringComparison]::Ordinal) -ge 0
    })

    if ($case.Extensions.Count -gt 0 -and $missingTokens.Count -gt 0) {
        throw "$($case.Name) is missing extension composition tokens: $($missingTokens -join ', ')"
    }

    if ($unexpectedTokens.Count -gt 0) {
        throw "$($case.Name) unexpectedly composes extension tokens: $($unexpectedTokens -join ', ')"
    }

    $expectedBootstrapFlags = @(
        ('$includeAuthNotificationsExtension = $' + ($case.Extensions -contains 'Auth.Notifications').ToString().ToLowerInvariant())
        ('$includeAuthOrganizationsExtension = $' + ($case.Extensions -contains 'Auth.Organizations').ToString().ToLowerInvariant())
        ('$includeOrganizationsAccessControlExtension = $' + ($case.Extensions -contains 'Organizations.AccessControl').ToString().ToLowerInvariant())
        ('$includeOrganizationsTenancyExtension = $' + ($case.Extensions -contains 'Organizations.Tenancy').ToString().ToLowerInvariant())
        ('$includeExtensions = $' + ($case.Extensions.Count -gt 0).ToString().ToLowerInvariant())
    )
    $bootstrap = [System.IO.File]::ReadAllText((Join-Path $outputPath 'eng\gma-bootstrap.ps1'))
    foreach ($expectedBootstrapFlag in $expectedBootstrapFlags) {
        if ($bootstrap.IndexOf($expectedBootstrapFlag, [System.StringComparison]::Ordinal) -lt 0) {
            throw "$($case.Name) generated an incorrect extension bootstrap flag: $expectedBootstrapFlag"
        }
    }

    $apiSettings = [System.IO.File]::ReadAllText(
        (Join-Path $outputPath "src\Hosts\$($case.Name).Host.Api\appsettings.json"))
    $organizationSettingsTokens = @(
        '"Organizations"',
        '"InvitationHistoryDays"',
        '"/api/organization-invitations"',
        '"/api/organization-enrollment"'
    )
    $expectsOrganizations = $case.Modules -contains 'organizations'
    foreach ($organizationSettingsToken in $organizationSettingsTokens) {
        $containsToken = $apiSettings.IndexOf(
            $organizationSettingsToken,
            [System.StringComparison]::Ordinal) -ge 0
        if ($containsToken -ne $expectsOrganizations) {
            throw "$($case.Name) generated incorrect Organizations settings for token: $organizationSettingsToken"
        }
    }

    if ($case.Modules -contains 'files') {
        $apiDevelopmentSettings = [System.IO.File]::ReadAllText(
            (Join-Path $outputPath "src\Hosts\$($case.Name).Host.Api\appsettings.Development.json"))
        $baseFileSettings = $apiSettings | ConvertFrom-Json
        $developmentFileSettings = $apiDevelopmentSettings | ConvertFrom-Json
        if ($null -ne $baseFileSettings.FileManagement.PSObject.Properties['RequireContentInspection']) {
            throw "$($case.Name) generated the obsolete FileManagement:RequireContentInspection setting."
        }

        if (-not $baseFileSettings.Files.Uploads.RequireTrustedContentType -or
            -not $baseFileSettings.Files.Uploads.RequireContentInspection) {
            throw "$($case.Name) did not generate fail-closed production Files upload policy."
        }

        if ($developmentFileSettings.Files.Uploads.RequireTrustedContentType -or
            $developmentFileSettings.Files.Uploads.RequireContentInspection) {
            throw "$($case.Name) did not generate explicit local Files upload opt-outs."
        }
    }

    if ($caseHosts -contains 'AdminApi') {
        $requiredAdminTokens = @(
            'Gma.Modules.Administration.AdminApi',
            'Gma.Modules.AccessControl.AdminApi',
            'AddAdminApiModule<AdministrationAdminApiModule>',
            'AddAdminApiModule<AccessControlAdminApiModule>',
            'Gma.Modules.Administration.AdminCli',
            'Gma.Modules.AccessControl.AdminCli',
            'AddAdminModule<AdministrationAdminCliModule>',
            'AddAdminModule<AccessControlAdminCliModule>',
            'Gma.Modules.Administration.Persistence.SqlServerMigrations',
            'Gma.Modules.AccessControl.Persistence.PostgreSqlMigrations',
            'AddGmaEntityFrameworkReadinessCheck<Gma.Modules.Administration.Persistence.AdminDbContext>',
            'AddGmaEntityFrameworkReadinessCheck<Gma.Modules.AccessControl.Persistence.AccessControlDbContext>',
            'ContentRootPath = AppContext.BaseDirectory',
            'catch (OptionsValidationException exception)',
            'catch (ModuleCompositionValidationException exception)',
            'CopyToOutputDirectory="PreserveNewest"',
            '"Administration"',
            '"Audit"',
            '"AccessControl"',
            '"Bootstrap"'
        )
        if ($case.Name -eq 'AllAdmin') {
            $requiredAdminTokens += @(
                'AddAuthAdminApiModule(AuthProfile.Global())',
                'AddAuthAdminModule(AuthProfile.Global())',
                'AddAdminApiModule<NotificationsAdminApiModule>',
                'AddAdminApiModule<OrganizationsAdminApiModule>',
                'AddAdminApiModule<TaskRuntimeAdminApiModule>',
                'AddAdminModule<OrganizationsAdminCliModule>',
                'AddAdminModule<TaskRuntimeAdminCliModule>',
                'AddGmaEntityFrameworkReadinessCheck<Gma.Modules.TaskRuntime.Persistence.TaskRuntimeDbContext>'
            )
        }

        $missingAdminTokens = @($requiredAdminTokens | Where-Object {
            $conditionalSurface.IndexOf($_, [System.StringComparison]::Ordinal) -lt 0
        })
        if ($missingAdminTokens.Count -gt 0) {
            throw "$($case.Name) is missing admin composition tokens: $($missingAdminTokens -join ', ')"
        }
    }
}

$buildCaseName = 'AllAdmin'
$buildCaseRoot = Join-Path $resolvedMatrixRoot $buildCaseName
$sourceRootContent = [System.IO.File]::ReadAllText(
    (Join-Path $buildCaseRoot 'Gma.SourceRoots.props.example'))
$directorySeparator = [string][System.IO.Path]::DirectorySeparatorChar
$frameworkSourceRoot = [System.IO.Path]::GetFullPath((Join-GmaPath 'gma\framework\src')).TrimEnd('\', '/') + $directorySeparator
$extensionsSourceRoot = [System.IO.Path]::GetFullPath((Join-GmaPath 'gma\extensions\src')).TrimEnd('\', '/') + $directorySeparator
$modulesSourceRoot = [System.IO.Path]::GetFullPath((Join-GmaPath 'gma\modules')).TrimEnd('\', '/') + $directorySeparator
$sourceRootContent = $sourceRootContent.Replace(
    '$(MSBuildThisFileDirectory)gma\framework\src\',
    $frameworkSourceRoot)
$sourceRootContent = $sourceRootContent.Replace(
    '$(MSBuildThisFileDirectory)gma\extensions\src\',
    $extensionsSourceRoot)
$sourceRootContent = $sourceRootContent.Replace(
    '$(MSBuildThisFileDirectory)gma\modules\',
    $modulesSourceRoot)
$sourceRootContent = $sourceRootContent.Replace('\', $directorySeparator)
[System.IO.File]::WriteAllText(
    (Join-Path $buildCaseRoot 'Gma.SourceRoots.props'),
    $sourceRootContent,
    [System.Text.UTF8Encoding]::new($false))

& dotnet restore (Join-Path $buildCaseRoot "$buildCaseName.slnx")
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
& dotnet build (Join-Path $buildCaseRoot "$buildCaseName.slnx") --no-restore -m:1
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$invalidAdminOutput = Join-Path $resolvedMatrixRoot 'InvalidAdminSelection'
try {
    & (Join-Path $PSScriptRoot 'new-gma-app.ps1') `
        -Name InvalidAdminSelection `
        -OutputPath $invalidAdminOutput `
        -Modules @('access-control') `
        -Hosts @('Api', 'AdminApi')
    throw 'Invalid admin selection unexpectedly succeeded.'
}
catch {
    if ($_.Exception.Message -notlike '*require both administration and access-control*') {
        throw
    }
}

Write-Host 'Generated app selection matrix passed for public, admin, and opt-in extension composition.'

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
    [pscustomobject] @{ Name = 'AuthOrganizationsTenancy'; Modules = @('auth', 'organizations', 'tenancy'); Extensions = @('Auth.Organizations', 'Organizations.Tenancy') }
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
    & (Join-Path $PSScriptRoot 'new-gma-app.ps1') `
        -Name $case.Name `
        -OutputPath $outputPath `
        -Modules $case.Modules

    $conditionalSurfacePaths = @(
        (Join-Path $outputPath 'Gma.SourceRoots.props.example'),
        (Join-Path $outputPath 'docs\gma-source.md'),
        (Join-Path $outputPath "src\Hosts\$($case.Name).Host.Api\$($case.Name).Host.Api.csproj"),
        (Join-Path $outputPath "src\Hosts\$($case.Name).Host.Api\Program.cs")
    )
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
}

Write-Host 'Generated app selection matrix passed for AccessControl, Auth, Notifications, Organizations, and their opt-in extensions.'

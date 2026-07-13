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
    [pscustomobject] @{ Name = 'AuthOnly'; Modules = @('auth'); HasExtension = $false },
    [pscustomobject] @{ Name = 'NotificationsOnly'; Modules = @('notifications'); HasExtension = $false },
    [pscustomobject] @{ Name = 'AuthNotifications'; Modules = @('auth', 'notifications'); HasExtension = $true }
)

$extensionTokens = @(
    'GMA-Extensions',
    'GmaExtensionsRoot',
    'Gma.Extensions.Auth.Notifications',
    'AddAuthNotificationsExtension',
    'gma/extensions/Gma.SourceRoots.props'
)

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
    $missingTokens = @($extensionTokens | Where-Object {
        $combinedSurface.IndexOf($_, [System.StringComparison]::Ordinal) -lt 0
    })
    $unexpectedTokens = @($extensionTokens | Where-Object {
        $conditionalSurface.IndexOf($_, [System.StringComparison]::Ordinal) -ge 0
    })

    if ($case.HasExtension -and $missingTokens.Count -gt 0) {
        throw "$($case.Name) is missing extension composition tokens: $($missingTokens -join ', ')"
    }

    if (-not $case.HasExtension -and $unexpectedTokens.Count -gt 0) {
        throw "$($case.Name) unexpectedly composes the Auth + Notifications extension: $($unexpectedTokens -join ', ')"
    }

    $expectedBootstrapFlag = if ($case.HasExtension) {
        '$includeAuthNotificationsExtension = $true'
    }
    else {
        '$includeAuthNotificationsExtension = $false'
    }
    $bootstrap = [System.IO.File]::ReadAllText((Join-Path $outputPath 'eng\gma-bootstrap.ps1'))
    if ($bootstrap.IndexOf($expectedBootstrapFlag, [System.StringComparison]::Ordinal) -lt 0) {
        throw "$($case.Name) generated an incorrect extension bootstrap flag."
    }
}

Write-Host 'Generated app selection matrix passed: Auth-only, Notifications-only, and Auth+Notifications.'

param(
    [string] $LaunchProfile = 'https',

    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]] $DotNetArguments
)

. (Join-Path $PSScriptRoot 'common.ps1')

$projectPath = Join-GmaPath 'src\Hosts\Host.Api\Host.Api.csproj'
$projectDirectory = Split-Path -Parent $projectPath

$arguments = @(
    'run',
    '--project',
    $projectPath,
    '--launch-profile',
    $LaunchProfile
)

$arguments += $DotNetArguments | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }

Invoke-GmaDotNet -Arguments $arguments -WorkingDirectory $projectDirectory

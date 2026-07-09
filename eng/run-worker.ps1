param(
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]] $DotNetArguments
)

. (Join-Path $PSScriptRoot 'common.ps1')

$projectPath = Join-GmaPath 'src\Hosts\Host.Worker\Host.Worker.csproj'
$projectDirectory = Split-Path -Parent $projectPath

$previousDotnetEnvironment = $env:DOTNET_ENVIRONMENT
if ([string]::IsNullOrWhiteSpace($previousDotnetEnvironment)) {
    $env:DOTNET_ENVIRONMENT = 'Development'
}

try {
    Invoke-GmaDotNet -Arguments (@(
        'run',
        '--project',
        $projectPath
    ) + ($DotNetArguments | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })) -WorkingDirectory $projectDirectory
}
finally {
    if ([string]::IsNullOrWhiteSpace($previousDotnetEnvironment)) {
        Remove-Item Env:\DOTNET_ENVIRONMENT -ErrorAction SilentlyContinue
    }
    else {
        $env:DOTNET_ENVIRONMENT = $previousDotnetEnvironment
    }
}

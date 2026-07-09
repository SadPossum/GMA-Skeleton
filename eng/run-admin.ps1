param(
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]] $AdminArgs
)

. (Join-Path $PSScriptRoot 'common.ps1')

$previousDotnetEnvironment = $env:DOTNET_ENVIRONMENT
if ([string]::IsNullOrWhiteSpace($previousDotnetEnvironment)) {
    $env:DOTNET_ENVIRONMENT = 'Development'
}

try {
    Invoke-GmaDotNet -Arguments (@(
        'run',
        '--project',
        (Join-GmaPath 'src\Hosts\Host.AdminCli\Host.AdminCli.csproj'),
        '--'
    ) + $AdminArgs)
}
finally {
    if ([string]::IsNullOrWhiteSpace($previousDotnetEnvironment)) {
        Remove-Item Env:\DOTNET_ENVIRONMENT -ErrorAction SilentlyContinue
    }
    else {
        $env:DOTNET_ENVIRONMENT = $previousDotnetEnvironment
    }
}

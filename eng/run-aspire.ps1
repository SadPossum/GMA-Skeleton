param(
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]] $DotNetArguments
)

. (Join-Path $PSScriptRoot 'common.ps1')

$projectPath = Join-GmaPath 'src\Hosts\AppHost\AppHost.csproj'
$projectDirectory = Split-Path -Parent $projectPath

$arguments = @('run', '--project', $projectPath)
$arguments += $DotNetArguments | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }

Invoke-GmaDotNet -Arguments $arguments -WorkingDirectory $projectDirectory

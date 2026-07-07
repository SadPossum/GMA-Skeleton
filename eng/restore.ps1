param(
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]] $DotNetArguments
)

. (Join-Path $PSScriptRoot 'common.ps1')

Invoke-GmaDotNet -Arguments @('tool', 'restore')

$arguments = @('restore', (Join-GmaPath 'GenericModularApi.slnx'))
$arguments += $DotNetArguments | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }

Invoke-GmaDotNet -Arguments $arguments

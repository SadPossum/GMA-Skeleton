param(
    [switch] $NoRestore,

    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]] $DotNetArguments
)

. (Join-Path $PSScriptRoot 'common.ps1')

$arguments = @('build', (Join-GmaPath 'GenericModularApi.slnx'))

if ($NoRestore) {
    $arguments += '--no-restore'
}

$arguments += $DotNetArguments | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }

Invoke-GmaDotNet -Arguments $arguments

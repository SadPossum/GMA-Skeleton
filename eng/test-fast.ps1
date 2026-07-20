param(
    [switch] $NoBuild,

    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]] $DotNetArguments
)

. (Join-Path $PSScriptRoot 'common.ps1')

$arguments = @(
    'test',
    (Join-GmaPath 'GMA-Skeleton.slnx'),
    '--filter',
    'Category!=Docker',
    '--logger',
    'console;verbosity=minimal',
    '-m:1',
    '-nr:false'
)

if ($NoBuild) {
    $arguments += '--no-build'
}

$arguments += $DotNetArguments | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }

Invoke-GmaDotNet -Arguments $arguments

[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [switch] $Force
)

. (Join-Path $PSScriptRoot 'common.ps1')

$source = Join-GmaPath 'Gma.SourceRoots.props.example'
$target = Join-GmaPath 'Gma.SourceRoots.props'

if (-not (Test-Path -LiteralPath $source)) {
    throw "Missing source-root example file at '$source'."
}

if ((Test-Path -LiteralPath $target) -and -not $Force) {
    Write-Host "Gma.SourceRoots.props already exists. Use -Force to refresh it from the example."
    return
}

$action = if (Test-Path -LiteralPath $target) { 'Overwrite' } else { 'Create' }
if ($PSCmdlet.ShouldProcess($target, "$action local source-root configuration")) {
    Copy-Item -LiteralPath $source -Destination $target -Force
    Write-Host "$action local source-root configuration: $target"
}

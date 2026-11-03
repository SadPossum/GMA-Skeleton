[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string] $WorkflowPath
)

. (Join-Path $PSScriptRoot 'common.ps1')

$content = [IO.File]::ReadAllText($WorkflowPath)
$steps = [regex]::Matches($content,
    '(?ms)^      - name: Resolve candidate identity\r?\n(?<step>.*?)(?=^      - |\z)')
if ($steps.Count -ne 1) {
    throw 'Release workflow requires exactly one candidate identity step.'
}
$step = $steps[0].Groups['step'].Value
$run = [regex]::Match($step, '(?ms)^        run: \|\r?\n(?<body>.*)\z')
if (-not $run.Success) {
    throw 'Release candidate identity step must contain one literal PowerShell script.'
}
$script = [regex]::Replace($run.Groups['body'].Value, '(?m)^          ', '')

# Exercise the actual workflow fragment with GitHub-style expression rendering.
# The probe is harmless and its tag is accepted by Git's real ref validator.
$script:releaseIdentityProbeInvoked = $false
function Get-GmaReleaseIdentityProbe {
    $script:releaseIdentityProbeInvoked = $true
}
$probeTag = 'v1.2.3$(Get-GmaReleaseIdentityProbe)'
& git check-ref-format "refs/tags/$probeTag"
if ($LASTEXITCODE -ne 0) {
    throw 'The release metadata probe must be a valid Git tag name.'
}
$cases = @(
    @{ Kind = 'tag'; Name = 'v1.2.3'; Version = 'v1.2.3'; RequireTag = 'true' },
    @{ Kind = 'tag'; Name = 'v1.2.3-rc.1'; Version = 'v1.2.3-rc.1'; RequireTag = 'true' },
    @{ Kind = 'branch'; Name = 'dev'; Version = 'candidate-aaaaaaaaaaaa'; RequireTag = 'false' },
    @{ Kind = 'tag'; Name = $probeTag; Version = $probeTag; RequireTag = 'true' }
)
$environmentNames = @('GITHUB_OUTPUT', 'RELEASE_REF_TYPE', 'RELEASE_REF_NAME', 'RELEASE_SOURCE_SHA')
$original = @{}
foreach ($name in $environmentNames) {
    $original[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
}
$directory = Join-Path ([IO.Path]::GetTempPath()) ('gma-release-identity-' + [Guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($directory)
try {
    foreach ($case in $cases) {
        $env:GITHUB_OUTPUT = Join-Path $directory 'output.txt'
        [IO.File]::WriteAllText($env:GITHUB_OUTPUT, '')
        $env:RELEASE_REF_TYPE = $case.Kind
        $env:RELEASE_REF_NAME = $case.Name
        $env:RELEASE_SOURCE_SHA = 'a' * 40
        $rendered = $script.Replace('${{ github.ref_type }}', $case.Kind).
            Replace('${{ github.ref_name }}', $case.Name).
            Replace('${{ github.sha }}', $env:RELEASE_SOURCE_SHA)
        $script:releaseIdentityProbeInvoked = $false
        & ([scriptblock]::Create($rendered)) | Out-Null
        if ($script:releaseIdentityProbeInvoked) {
            throw 'Release workflow executed tag metadata as PowerShell code.'
        }
        $actual = @([IO.File]::ReadAllLines($env:GITHUB_OUTPUT))
        $expected = @("version=$($case.Version)", "require-tag=$($case.RequireTag)")
        if ($actual.Count -ne 2 -or
            -not [string]::Equals(($actual -join "`n"), ($expected -join "`n"), [StringComparison]::Ordinal)) {
            throw 'Release workflow did not preserve the exact candidate identity and tag requirement.'
        }
    }
    foreach ($binding in @(
            'RELEASE_REF_TYPE: ${{ github.ref_type }}',
            'RELEASE_REF_NAME: ${{ github.ref_name }}',
            'RELEASE_SOURCE_SHA: ${{ github.sha }}')) {
        if (-not $step.Contains($binding, [StringComparison]::Ordinal)) {
            throw "Release candidate identity is missing metadata binding '$binding'."
        }
    }
    if ($script.Contains('${{', [StringComparison]::Ordinal)) {
        throw 'Release identity metadata must enter through environment values.'
    }
}
finally {
    foreach ($name in $environmentNames) {
        [Environment]::SetEnvironmentVariable($name, $original[$name], 'Process')
    }
    Remove-Item -LiteralPath $directory -Recurse -Force
}

Write-Host 'Release tag, prerelease, branch candidate and non-executing metadata cases pass.'

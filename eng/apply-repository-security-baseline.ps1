[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $OutputPath,

    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$')]
    [string] $RepositorySlug,

    [Parameter(Mandatory = $true)]
    [string] $RepositoryDisplayName,

    [Parameter(Mandatory = $true)]
    [ValidateSet('nuget', 'npm')]
    [string] $PackageEcosystem,

    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[0-9a-f]{40}$')]
    [string] $SecurityBaselineCommit,

    [ValidatePattern('^$|^v[0-9]+\.[0-9]+\.[0-9]+(?:[-+][A-Za-z0-9.-]+)?$')]
    [string] $SupportedVersion = '',

    [switch] $IncludeGitSubmodules,

    [switch] $Force
)

. (Join-Path $PSScriptRoot 'common.ps1')

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$resolvedOutputPath = [System.IO.Path]::GetFullPath($OutputPath)
if (-not [System.IO.Directory]::Exists($resolvedOutputPath)) {
    throw "Repository path '$resolvedOutputPath' does not exist."
}

$templateCheckPath = Join-Path `
    $PSScriptRoot `
    'repository-security\check-repository-security.ps1'
if (-not [System.IO.File]::Exists($templateCheckPath)) {
    throw 'Repository security guard template is missing.'
}

function Write-BaselineFile {
    param(
        [Parameter(Mandatory = $true)]
        [string] $RelativePath,

        [Parameter(Mandatory = $true)]
        [AllowEmptyString()]
        [string[]] $Lines
    )

    $path = Join-Path $resolvedOutputPath $RelativePath
    if ([System.IO.File]::Exists($path) -and -not $Force) {
        throw "Refusing to overwrite existing baseline file '$RelativePath'. Use -Force after reviewing it."
    }

    $directory = [System.IO.Path]::GetDirectoryName($path)
    if (-not [string]::IsNullOrWhiteSpace($directory)) {
        [System.IO.Directory]::CreateDirectory($directory) | Out-Null
    }
    [System.IO.File]::WriteAllLines(
        $path,
        $Lines,
        [System.Text.UTF8Encoding]::new($false))
}

$dependencyEcosystems = [System.Collections.Generic.List[string]]::new()
$dependencyEcosystems.Add('github-actions')
if ($IncludeGitSubmodules) {
    $dependencyEcosystems.Add('gitsubmodule')
}
$dependencyEcosystems.Add($PackageEcosystem)

$manifest = [ordered] @{
    schemaVersion = 1
    repository = $RepositorySlug
    securityBaseline = [ordered] @{
        repository = 'SadPossum/GMA-Skeleton'
        commit = $SecurityBaselineCommit
    }
    dependencyEcosystems = @($dependencyEcosystems)
}
Write-BaselineFile `
    -RelativePath '.gma\repository-security.json' `
    -Lines @(($manifest | ConvertTo-Json -Depth 6))
Write-BaselineFile `
    -RelativePath '.gma\security-exceptions.json' `
    -Lines @(
        '{',
        '  "schemaVersion": 1,',
        '  "exceptions": []',
        '}'
    )

$dependabotLines = [System.Collections.Generic.List[string]]::new()
$dependabotLines.Add('version: 2')
$dependabotLines.Add('updates:')
foreach ($ecosystem in $dependencyEcosystems) {
    $dependabotLines.Add("  - package-ecosystem: $ecosystem")
    $dependabotLines.Add('    directory: /')
    $dependabotLines.Add('    schedule:')
    $dependabotLines.Add('      interval: weekly')
    $dependabotLines.Add(
        "    open-pull-requests-limit: $(if ($ecosystem -eq 'github-actions') { 5 } else { 10 })")
    if ($ecosystem -eq 'nuget') {
        $dependabotLines.Add('    groups:')
        $dependabotLines.Add('      dotnet-runtime:')
        $dependabotLines.Add('        patterns:')
        $dependabotLines.Add("          - 'Microsoft.*'")
        $dependabotLines.Add("          - 'Aspire.*'")
        $dependabotLines.Add('      test-tooling:')
        $dependabotLines.Add('        dependency-type: development')
        $dependabotLines.Add('        patterns:')
        $dependabotLines.Add("          - 'Microsoft.NET.Test.Sdk'")
        $dependabotLines.Add("          - 'xunit*'")
        $dependabotLines.Add("          - 'coverlet.*'")
    }
    $dependabotLines.Add('')
}
$dependabotLines.RemoveAt($dependabotLines.Count - 1)
Write-BaselineFile `
    -RelativePath '.github\dependabot.yml' `
    -Lines $dependabotLines

$repositoryBytes = [System.Text.Encoding]::UTF8.GetBytes($RepositorySlug)
$sha256 = [System.Security.Cryptography.SHA256]::Create()
try {
    $repositoryHash = $sha256.ComputeHash($repositoryBytes)
}
finally {
    $sha256.Dispose()
}
$scheduleMinute = [int] $repositoryHash[0] % 60
$scheduleDay = [int] $repositoryHash[1] % 7

$workflowLines = @(
    'name: Security Baseline',
    '',
    'on:',
    '  pull_request:',
    '  push:',
    '    branches:',
    '      - main',
    '      - dev',
    '  schedule:',
    "    - cron: '$scheduleMinute 4 * * $scheduleDay'",
    '  workflow_dispatch:',
    '',
    'permissions:',
    '  contents: read',
    '  security-events: write',
    '',
    'concurrency:',
    '  group: security-${{ github.ref }}',
    '  cancel-in-progress: true',
    '',
    'jobs:',
    '  scan:',
    '    name: Owned source, dependency and configuration scan',
    '    runs-on: ubuntu-latest',
    '    timeout-minutes: 30',
    '    steps:',
    '      - name: Checkout owned source',
    '        uses: actions/checkout@9c091bb21b7c1c1d1991bb908d89e4e9dddfe3e0 # v7.0.0',
    '        with:',
    '          fetch-depth: 0',
    '          persist-credentials: false',
    '',
    '      - name: Validate repository security policy',
    '        shell: pwsh',
    '        run: ./eng/check-repository-security.ps1',
    '',
    '      - name: Run repository security baseline',
    "        uses: SadPossum/GMA-Skeleton/.github/actions/security-baseline@$SecurityBaselineCommit",
    '        with:',
    '          exception-file: .gma/security-exceptions.json',
    '',
    '      - name: Publish code-scanning evidence',
    '        if: ${{ always() && (github.event_name != ''pull_request'' || github.event.pull_request.head.repo.full_name == github.repository) }}',
    '        uses: github/codeql-action/upload-sarif@7188fc363630916deb702c7fdcf4e481b751f97a # v4.37.1',
    '        with:',
    '          sarif_file: artifacts/security/trivy-results.sarif',
    '          category: trivy-owned-source',
    '',
    '      - name: Retain security evidence',
    '        if: ${{ always() }}',
    '        uses: actions/upload-artifact@043fb46d1a93c77aae656e7c1c64a875d1fc6a0a # v7.0.1',
    '        with:',
    '          name: security-evidence-${{ github.sha }}',
    '          path: artifacts/security',
    '          if-no-files-found: error',
    '          retention-days: 30'
)
Write-BaselineFile `
    -RelativePath '.github\workflows\security.yml' `
    -Lines $workflowLines

$supportedVersionLines = if ([string]::IsNullOrWhiteSpace($SupportedVersion)) {
    @(
        "$RepositoryDisplayName has not published a supported production release. The ``dev`` branch is an actively changing pre-release line; security fixes are made there and will be included in the first supported release.",
        '',
        '| Version | Supported |',
        '| --- | --- |',
        '| `dev` | Pre-release security fixes |',
        '| Production releases | None yet |'
    )
}
else {
    @(
        "$RepositoryDisplayName is independently versioned. Security fixes are made on ``dev`` and included in the next tagged release. During the pre-1.0 period, only the latest tagged release and ``dev`` receive fixes.",
        '',
        '| Version | Supported |',
        '| --- | --- |',
        '| `dev` | Pre-release security fixes |',
        "| ``$SupportedVersion`` | Yes |",
        '| Older releases | No |'
    )
}
$securityPolicyLines = @(
    '# Security Policy',
    '',
    '## Supported Versions',
    ''
) + $supportedVersionLines + @(
    '',
    '## Report a Vulnerability',
    '',
    "Use GitHub's [private vulnerability reporting form](https://github.com/$RepositorySlug/security/advisories/new). Do not open a public issue for an undisclosed vulnerability and do not include credentials, personal data, payment data, identity documents, or third-party confidential data in a report.",
    '',
    'Include the affected commit or release, impact, reproducible steps or a minimal proof of concept, and any known mitigation. A composition issue may also be reported to GMA-Skeleton or the owning product repository; maintainers will route it privately.',
    '',
    'We aim to acknowledge a complete report within three business days and provide an initial assessment within seven business days. These are response targets, not a contractual support SLA. Please coordinate public disclosure until a fix or mitigation is available.',
    '',
    'There is currently no paid bug-bounty programme. Good-faith, non-destructive research against systems and data you own is welcome; denial of service, social engineering, persistence, and access to another person''s data are out of scope.'
)
Write-BaselineFile `
    -RelativePath 'SECURITY.md' `
    -Lines $securityPolicyLines

Write-BaselineFile `
    -RelativePath 'eng\check-repository-security.ps1' `
    -Lines ([System.IO.File]::ReadAllLines($templateCheckPath))

Write-Host "Applied the repository security baseline to $RepositorySlug."

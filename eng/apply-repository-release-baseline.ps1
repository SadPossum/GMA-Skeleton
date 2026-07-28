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
    [ValidatePattern('^[a-z0-9][a-z0-9.-]{1,63}$')]
    [string] $ArtifactName,

    [Parameter(Mandatory = $true)]
    [ValidateSet('source', 'composition')]
    [string] $ReleaseKind,

    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[0-9a-f]{40}$')]
    [string] $ReleaseEvidenceCommit,

    [ValidatePattern(
        '^$|^v[0-9]+\.[0-9]+\.[0-9]+(?:-[0-9A-Za-z.-]+)?$')]
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
    'repository-release\check-repository-release.ps1'
if (-not [System.IO.File]::Exists($templateCheckPath)) {
    throw 'Repository release guard template is missing.'
}

$securityManifestPath = Join-Path `
    $resolvedOutputPath `
    '.gma\repository-security.json'
foreach ($relativePath in @(
    '.gma\repository-security.json',
    '.gma\security-exceptions.json',
    'eng\check-repository-security.ps1'
)) {
    if (-not [System.IO.File]::Exists(
            (Join-Path $resolvedOutputPath $relativePath))) {
        throw "Repository security baseline file '$relativePath' is required."
    }
}

try {
    $securityManifest = [System.IO.File]::ReadAllText(
        $securityManifestPath) | ConvertFrom-Json
}
catch {
    throw 'Repository security manifest is not valid JSON.'
}
if ($null -eq $securityManifest -or
    $securityManifest.repository -ne $RepositorySlug -or
    $null -eq $securityManifest.securityBaseline -or
    $securityManifest.securityBaseline.repository -ne
        'SadPossum/GMA-Skeleton' -or
    $securityManifest.securityBaseline.commit -isnot [string] -or
    $securityManifest.securityBaseline.commit -notmatch
        '^[0-9a-f]{40}$' -or
    $securityManifest.securityBaseline.commit -eq ('0' * 40)) {
    throw 'Repository security manifest cannot anchor release evidence.'
}
$securityBaselineCommit = $securityManifest.securityBaseline.commit

$sourceSetPath = if ($ReleaseKind -eq 'composition') {
    if (-not [System.IO.File]::Exists(
            (Join-Path $resolvedOutputPath 'eng\export-source-set.ps1'))) {
        throw 'A composition release requires eng/export-source-set.ps1.'
    }
    'artifacts/gma-source-set.json'
}
else {
    $null
}

$targetPaths = @(
    '.github\workflows\release-evidence.yml',
    '.gma\release-evidence.json',
    'eng\check-repository-release.ps1',
    'SUPPORT.md'
)
if (-not $Force) {
    $existingPaths = @(
        $targetPaths |
            Where-Object {
                [System.IO.File]::Exists(
                    (Join-Path $resolvedOutputPath $_))
            }
    )
    if ($existingPaths.Count -gt 0) {
        throw "Refusing to overwrite release baseline file '$($existingPaths[0])'. Use -Force after reviewing it."
    }
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
    $directory = [System.IO.Path]::GetDirectoryName($path)
    if (-not [string]::IsNullOrWhiteSpace($directory)) {
        [System.IO.Directory]::CreateDirectory($directory) | Out-Null
    }
    [System.IO.File]::WriteAllLines(
        $path,
        $Lines,
        [System.Text.UTF8Encoding]::new($false))
}

$manifest = [ordered] @{
    schemaVersion = 1
    repository = $RepositorySlug
    artifactName = $ArtifactName
    releaseKind = $ReleaseKind
    releaseEvidence = [ordered] @{
        repository = 'SadPossum/GMA-Skeleton'
        commit = $ReleaseEvidenceCommit
    }
    sourceSetPath = $sourceSetPath
}
Write-BaselineFile `
    -RelativePath '.gma\release-evidence.json' `
    -Lines @(($manifest | ConvertTo-Json -Depth 6))

$checkoutLines = [System.Collections.Generic.List[string]]::new()
$checkoutLines.Add('      - name: Checkout release source')
$checkoutLines.Add(
    '        uses: actions/checkout@9c091bb21b7c1c1d1991bb908d89e4e9dddfe3e0 # v7.0.0')
$checkoutLines.Add('        with:')
$checkoutLines.Add('          fetch-depth: 0')
if ($IncludeGitSubmodules) {
    $checkoutLines.Add('          submodules: recursive')
    $checkoutLines.Add(
        '          token: ${{ secrets.GMA_CI_TOKEN || github.token }}')
}
$checkoutLines.Add('          persist-credentials: false')

$workflowLines = [System.Collections.Generic.List[string]]::new()
@(
    'name: Release Evidence',
    '',
    'on:',
    '  push:',
    '    tags:',
    "      - 'v*'",
    '  workflow_dispatch:',
    '',
    'jobs:',
    '  evidence:',
    '    name: Build and attest source evidence',
    '    runs-on: ubuntu-latest',
    '    timeout-minutes: 30',
    '    permissions:',
    '      contents: read',
    '      attestations: write',
    '      id-token: write',
    '    steps:'
) | ForEach-Object { $workflowLines.Add($_) }
$checkoutLines | ForEach-Object { $workflowLines.Add($_) }
@(
    '',
    '      - name: Validate release policy',
    '        shell: pwsh',
    '        run: ./eng/check-repository-release.ps1',
    ''
) | ForEach-Object { $workflowLines.Add($_) }
if ($ReleaseKind -eq 'composition') {
    @(
        '      - name: Export source bill of materials',
        '        shell: pwsh',
        '        run: ./eng/export-source-set.ps1 -RequireClean',
        ''
    ) | ForEach-Object { $workflowLines.Add($_) }
}
@(
    '      - name: Resolve candidate identity',
    '        id: identity',
    '        shell: pwsh',
    '        run: |',
    "          if ('`${{ github.ref_type }}' -eq 'tag') {",
    '            "version=${{ github.ref_name }}" >> $env:GITHUB_OUTPUT',
    '            "require-tag=true" >> $env:GITHUB_OUTPUT',
    '          }',
    '          else {',
    "            `$shortCommit = '`${{ github.sha }}'.Substring(0, 12)",
    '            "version=candidate-$shortCommit" >> $env:GITHUB_OUTPUT',
    '            "require-tag=false" >> $env:GITHUB_OUTPUT',
    '          }',
    '',
    '      - name: Scan release source and create SBOM',
    "        uses: SadPossum/GMA-Skeleton/.github/actions/security-baseline@$securityBaselineCommit",
    '        with:',
    '          output-directory: artifacts/release-security',
    '          exception-file: .gma/security-exceptions.json',
    '',
    '      - name: Create source release evidence',
    '        id: release-evidence',
    "        uses: SadPossum/GMA-Skeleton/.github/actions/source-release-evidence@$ReleaseEvidenceCommit",
    '        with:',
    '          release-version: ${{ steps.identity.outputs.version }}',
    '          source-ref: ${{ github.ref }}',
    '          security-evidence-directory: artifacts/release-security',
    '          output-directory: artifacts/release',
    '          require-tag: ${{ steps.identity.outputs.require-tag }}',
    '',
    '      - name: Attest release provenance',
    '        uses: actions/attest@f7c74d28b9d84cb8768d0b8ca14a4bac6ef463e6 # v4.2.0',
    '        with:',
    '          subject-checksums: ${{ steps.release-evidence.outputs.checksums-path }}',
    '',
    '      - name: Attest source SBOM',
    '        uses: actions/attest@f7c74d28b9d84cb8768d0b8ca14a4bac6ef463e6 # v4.2.0',
    '        with:',
    '          subject-path: ${{ steps.release-evidence.outputs.archive-path }}',
    '          sbom-path: ${{ steps.release-evidence.outputs.sbom-path }}',
    '',
    '      - name: Retain release evidence',
    '        uses: actions/upload-artifact@043fb46d1a93c77aae656e7c1c64a875d1fc6a0a # v7.0.1',
    '        with:',
    '          name: release-evidence-${{ github.sha }}',
    '          path: ${{ steps.release-evidence.outputs.evidence-directory }}',
    '          if-no-files-found: error',
    '          retention-days: 90',
    '',
    '  publish:',
    '    name: Publish immutable tag assets',
    "    if: startsWith(github.ref, 'refs/tags/v')",
    '    needs: evidence',
    '    runs-on: ubuntu-latest',
    '    timeout-minutes: 10',
    '    permissions:',
    '      contents: write',
    '    steps:',
    '      - name: Download attested evidence',
    '        uses: actions/download-artifact@3e5f45b2cfb9172054b4087a40e8e0b5a5461e7c # v8.0.1',
    '        with:',
    '          name: release-evidence-${{ github.sha }}',
    '          path: artifacts/release',
    '',
    '      - name: Create GitHub release',
    '        shell: pwsh',
    '        env:',
    '          GH_TOKEN: ${{ github.token }}',
    '          RELEASE_TAG: ${{ github.ref_name }}',
    '        run: |',
    '          & gh release view $env:RELEASE_TAG --json tagName 2>$null |',
    '            Out-Null',
    '          if ($LASTEXITCODE -eq 0) {',
    '            throw "Release ''$env:RELEASE_TAG'' already exists; assets are immutable."',
    '          }',
    '',
    '          $arguments = @(',
    "            'release',",
    "            'create',",
    '            $env:RELEASE_TAG,',
    "            '--verify-tag',",
    "            '--generate-notes',",
    "            '--title',",
    '            $env:RELEASE_TAG',
    '          )',
    "          if (`$env:RELEASE_TAG.Contains('-')) {",
    "            `$arguments += '--prerelease'",
    '          }',
    '          $arguments += @(',
    "            Get-ChildItem -LiteralPath 'artifacts/release' -File |",
    '              Sort-Object Name |',
    '              ForEach-Object FullName',
    '          )',
    '',
    '          & gh @arguments',
    '          if ($LASTEXITCODE -ne 0) {',
    "            throw 'GitHub release creation failed.'",
    '          }'
) | ForEach-Object { $workflowLines.Add($_) }
Write-BaselineFile `
    -RelativePath '.github\workflows\release-evidence.yml' `
    -Lines $workflowLines

$releaseChannelLines = if ([string]::IsNullOrWhiteSpace(
        $SupportedVersion)) {
    @(
        "The ``dev`` branch is $RepositoryDisplayName's changing integration line. Workflow-dispatch runs create reviewable candidate evidence, but they are not supported releases.",
        '',
        '| Channel | Status |',
        '| --- | --- |',
        '| `dev` | Pre-release integration |',
        '| Tagged production release | None yet |'
    )
}
else {
    @(
        "$RepositoryDisplayName is independently versioned. The ``dev`` branch is the changing integration line; immutable SemVer tags are release boundaries.",
        '',
        '| Channel | Status |',
        '| --- | --- |',
        '| `dev` | Pre-release integration |',
        "| ``$SupportedVersion`` | Current tagged release |"
    )
}
$distributionLine = if ($ReleaseKind -eq 'composition') {
    'A release contains the owned repository archive plus a source-set manifest identifying every composed repository commit. Consumers must resolve that exact source set.'
}
else {
    'A release contains the owned repository source archive, release manifest, checksums, CycloneDX SBOM, and GitHub attestations.'
}
$supportLines = @(
    '# Support Policy',
    '',
    '## Release Channels',
    ''
) + $releaseChannelLines + @(
    '',
    '## Compatibility',
    '',
    $distributionLine,
    '',
    'Pre-1.0 releases may contain breaking changes between minor versions. Compatibility promises belong to each repository and its tagged release notes; composition repositories do not replace those contracts.',
    '',
    '## End Of Life',
    '',
    'Only `dev` and the current tagged release receive fixes during the pre-1.0 period. Older tags are end of life when a newer tag is published unless a release note explicitly states otherwise.',
    '',
    '## Support',
    '',
    'Security reports follow `SECURITY.md`. Maintenance is best effort and has no contractual support SLA.'
)
Write-BaselineFile `
    -RelativePath 'SUPPORT.md' `
    -Lines $supportLines

Write-BaselineFile `
    -RelativePath 'eng\check-repository-release.ps1' `
    -Lines ([System.IO.File]::ReadAllLines($templateCheckPath))

Write-Host "Applied the repository release baseline to $RepositorySlug."

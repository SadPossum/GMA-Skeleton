[CmdletBinding()]
param(
    [switch] $RequireRolloutTooling
)

. (Join-Path $PSScriptRoot 'common.ps1')

$root = Get-GmaRepositoryRoot
$requiredFiles = @(
    '.github\actions\source-release-evidence\action.yml',
    '.github\actions\source-release-evidence\create-source-release-evidence.ps1',
    '.github\workflows\release-evidence.yml',
    '.gma\release-evidence.json',
    'eng\repository-release\check-repository-release.ps1',
    'SUPPORT.md'
)
if ($RequireRolloutTooling) {
    $requiredFiles += 'eng\apply-repository-release-baseline.ps1'
}
foreach ($relativePath in $requiredFiles) {
    if (-not (Test-Path -LiteralPath (Join-Path $root $relativePath) -PathType Leaf)) {
        throw "Missing release-evidence baseline file '$relativePath'."
    }
}

& (Join-Path $root 'eng\repository-release\check-repository-release.ps1') `
    -RepositoryRoot $root `
    -LocalImplementation

$action = [System.IO.File]::ReadAllText(
    (Join-Path $root '.github\actions\source-release-evidence\action.yml'))
foreach ($token in @(
    'create-source-release-evidence.ps1',
    'default: .gma/release-evidence.json',
    'release-version:',
    'source-ref:',
    'require-tag:',
    'archive-path:',
    'checksums-path:',
    'sbom-path:')) {
    if ($action.IndexOf($token, [System.StringComparison]::Ordinal) -lt 0) {
        throw "Source release action is missing required token '$token'."
    }
}

function Write-FixtureSecuritySummary {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Path,

        [Parameter(Mandatory = $true)]
        [string] $SourceCommit
    )

    $scannerSummaries = @(
        foreach ($category in @(
            'vulnerability',
            'secret',
            'misconfiguration',
            'license'
        )) {
            [ordered] @{
                category = $category
                count = 0
                severities = [ordered] @{
                    unknown = 0
                    low = 0
                    medium = 0
                    high = 0
                    critical = 0
                }
            }
        }
    )
    $summary = [ordered] @{
        schemaVersion = 1
        sourceCommit = $SourceCommit
        ciRunCorrelation = 'github-actions:12345:1'
        status = 'passed'
        scanners = $scannerSummaries
    }
    [System.IO.File]::WriteAllText(
        $Path,
        ($summary | ConvertTo-Json -Depth 8) + "`n",
        [System.Text.UTF8Encoding]::new($false))
}

$scriptPath = Join-Path $root `
    '.github\actions\source-release-evidence\create-source-release-evidence.ps1'
$testRoot = Join-Path `
    ([System.IO.Path]::GetTempPath()) `
    ('gma-release-evidence-' + [System.Guid]::NewGuid().ToString('N'))
[System.IO.Directory]::CreateDirectory($testRoot) | Out-Null
$originalGitHubSha = $env:GITHUB_SHA

try {
    & git -C $testRoot init -b dev | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw 'Unable to initialize the release-evidence fixture.'
    }
    & git -C $testRoot config user.name 'GMA Release Test'
    & git -C $testRoot config user.email 'release-test@example.invalid'
    & git -C $testRoot config core.autocrlf false
    & git -C $testRoot remote add origin `
        'https://github.com/SadPossum/Release-Evidence-Fixture.git'

    [System.IO.Directory]::CreateDirectory(
        (Join-Path $testRoot '.gma')) | Out-Null
    [System.IO.File]::WriteAllText(
        (Join-Path $testRoot 'README.md'),
        "# Release fixture`n",
        [System.Text.UTF8Encoding]::new($false))
    $fixtureManifest = [ordered] @{
        schemaVersion = 1
        repository = 'SadPossum/Release-Evidence-Fixture'
        artifactName = 'release-evidence-fixture'
        releaseKind = 'composition'
        releaseEvidence = [ordered] @{
            repository = 'SadPossum/GMA-Skeleton'
            commit = 'a' * 40
        }
        sourceSetPath = 'artifacts/gma-source-set.json'
    }
    [System.IO.File]::WriteAllText(
        (Join-Path $testRoot '.gma\release-evidence.json'),
        ($fixtureManifest | ConvertTo-Json -Depth 6) + "`n",
        [System.Text.UTF8Encoding]::new($false))

    & git -C $testRoot add -- README.md .gma/release-evidence.json
    & git -C $testRoot commit -m 'Create release fixture' | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw 'Unable to commit the release-evidence fixture.'
    }
    $fixtureCommit = @(& git -C $testRoot rev-parse HEAD)[0].Trim()
    $env:GITHUB_SHA = $fixtureCommit

    $securityDirectory = Join-Path $testRoot 'artifacts\release-security'
    [System.IO.Directory]::CreateDirectory($securityDirectory) | Out-Null
    [System.IO.File]::WriteAllText(
        (Join-Path $securityDirectory 'sbom.cdx.json'),
        '{"bomFormat":"CycloneDX","specVersion":"1.6","version":1,"components":[]}',
        [System.Text.UTF8Encoding]::new($false))
    Write-FixtureSecuritySummary `
        -Path (Join-Path $securityDirectory 'security-summary.json') `
        -SourceCommit $fixtureCommit
    $sourceSet = [ordered] @{
        schemaVersion = 2
        generatedAtUtc = '2026-07-28T00:00:00.0000000Z'
        rootCommit = $fixtureCommit
        sdkVersion = '10.0.300'
        centralPackagesSha256 = 'c' * 64
        repositories = @(
            [ordered] @{
                path = '.'
                url = ''
                commit = $fixtureCommit
                branch = 'dev'
                configuredBranch = ''
                dirty = $false
            },
            [ordered] @{
                path = 'gma/framework'
                url = 'https://github.com/SadPossum/GenericModularApi.git'
                commit = 'd' * 40
                branch = ''
                configuredBranch = 'dev'
                dirty = $false
            }
        )
    }
    [System.IO.File]::WriteAllText(
        (Join-Path $testRoot 'artifacts\gma-source-set.json'),
        ($sourceSet | ConvertTo-Json -Depth 8) + "`n",
        [System.Text.UTF8Encoding]::new($false))

    $firstResult = & $scriptPath `
        -RepositoryRoot $testRoot `
        -ReleaseVersion "candidate-$($fixtureCommit.Substring(0, 12))" `
        -SourceRef 'refs/heads/dev' `
        -SecurityEvidenceDirectory 'artifacts/release-security' `
        -OutputDirectory 'artifacts/release-first' |
        ConvertFrom-Json
    $secondResult = & $scriptPath `
        -RepositoryRoot $testRoot `
        -ReleaseVersion "candidate-$($fixtureCommit.Substring(0, 12))" `
        -SourceRef 'refs/heads/dev' `
        -SecurityEvidenceDirectory 'artifacts/release-security' `
        -OutputDirectory 'artifacts/release-second' |
        ConvertFrom-Json

    $firstArchiveHash = (Get-FileHash `
        -LiteralPath $firstResult.archivePath `
        -Algorithm SHA256).Hash
    $secondArchiveHash = (Get-FileHash `
        -LiteralPath $secondResult.archivePath `
        -Algorithm SHA256).Hash
    $firstManifestHash = (Get-FileHash `
        -LiteralPath $firstResult.manifestPath `
        -Algorithm SHA256).Hash
    $secondManifestHash = (Get-FileHash `
        -LiteralPath $secondResult.manifestPath `
        -Algorithm SHA256).Hash
    if ($firstArchiveHash -ne $secondArchiveHash -or
        $firstManifestHash -ne $secondManifestHash) {
        throw 'Release archive or deterministic manifest is not reproducible.'
    }

    $checksumLines = @(
        [System.IO.File]::ReadAllLines($firstResult.checksumsPath)
    )
    $missingChecksumTokens = @(
        foreach ($requiredToken in @(
            'release-evidence-fixture-candidate-',
            'release-manifest.json',
            'sbom.cdx.json',
            'security-summary.json',
            'source-set.json'
        )) {
            $matchingLines = @(
                $checksumLines |
                    Where-Object {
                        $_.IndexOf(
                            $requiredToken,
                            [System.StringComparison]::Ordinal) -ge 0
                    }
            )
            if ($matchingLines.Count -ne 1) {
                $requiredToken
            }
        }
    )
    if ($checksumLines.Count -ne 5 -or
        $missingChecksumTokens.Count -gt 0) {
        throw 'Release checksums do not cover every public evidence asset.'
    }

    & git -C $testRoot tag v0.1.0 $fixtureCommit
    $taggedResult = & $scriptPath `
        -RepositoryRoot $testRoot `
        -ReleaseVersion 'v0.1.0' `
        -SourceRef 'refs/tags/v0.1.0' `
        -SecurityEvidenceDirectory 'artifacts/release-security' `
        -OutputDirectory 'artifacts/release-tagged' `
        -RequireTag |
        ConvertFrom-Json
    if (-not [System.IO.File]::Exists($taggedResult.archivePath)) {
        throw 'Exact tag release evidence was not created.'
    }

    $tagMismatchRejected = $false
    try {
        & $scriptPath `
            -RepositoryRoot $testRoot `
            -ReleaseVersion 'v0.1.1' `
            -SourceRef 'refs/tags/v0.1.1' `
            -SecurityEvidenceDirectory 'artifacts/release-security' `
            -OutputDirectory 'artifacts/release-mismatch' `
            -RequireTag |
            Out-Null
    }
    catch {
        $tagMismatchRejected = $true
    }
    if (-not $tagMismatchRejected) {
        throw 'A tag that does not identify HEAD was not rejected.'
    }

    Write-FixtureSecuritySummary `
        -Path (Join-Path $securityDirectory 'security-summary.json') `
        -SourceCommit ('b' * 40)
    $mismatchedScanRejected = $false
    try {
        & $scriptPath `
            -RepositoryRoot $testRoot `
            -ReleaseVersion "candidate-$($fixtureCommit.Substring(0, 12))" `
            -SourceRef 'refs/heads/dev' `
            -SecurityEvidenceDirectory 'artifacts/release-security' `
            -OutputDirectory 'artifacts/release-mismatched-scan' |
            Out-Null
    }
    catch {
        $mismatchedScanRejected = $true
    }
    if (-not $mismatchedScanRejected) {
        throw 'Security evidence from another commit was not rejected.'
    }
    Write-FixtureSecuritySummary `
        -Path (Join-Path $securityDirectory 'security-summary.json') `
        -SourceCommit $fixtureCommit

    $securitySummaryPath = Join-Path `
        $securityDirectory `
        'security-summary.json'
    $securitySummaryWithPayload =
        [System.IO.File]::ReadAllText($securitySummaryPath) |
        ConvertFrom-Json
    $securitySummaryWithPayload |
        Add-Member `
            -NotePropertyName findingPayload `
            -NotePropertyValue 'must-not-publish'
    [System.IO.File]::WriteAllText(
        $securitySummaryPath,
        ($securitySummaryWithPayload | ConvertTo-Json -Depth 8) + "`n",
        [System.Text.UTF8Encoding]::new($false))
    $payloadSummaryRejected = $false
    try {
        & $scriptPath `
            -RepositoryRoot $testRoot `
            -ReleaseVersion "candidate-$($fixtureCommit.Substring(0, 12))" `
            -SourceRef 'refs/heads/dev' `
            -SecurityEvidenceDirectory 'artifacts/release-security' `
            -OutputDirectory 'artifacts/release-payload-summary' |
            Out-Null
    }
    catch {
        $payloadSummaryRejected = $true
    }
    if (-not $payloadSummaryRejected) {
        throw 'A security summary containing finding payload was not rejected.'
    }
    Write-FixtureSecuritySummary `
        -Path $securitySummaryPath `
        -SourceCommit $fixtureCommit

    [System.IO.File]::WriteAllText(
        (Join-Path $testRoot 'README.md'),
        "# Dirty release fixture`n",
        [System.Text.UTF8Encoding]::new($false))
    $dirtyTreeRejected = $false
    try {
        & $scriptPath `
            -RepositoryRoot $testRoot `
            -ReleaseVersion "candidate-$($fixtureCommit.Substring(0, 12))" `
            -SourceRef 'refs/heads/dev' `
            -SecurityEvidenceDirectory 'artifacts/release-security' `
            -OutputDirectory 'artifacts/release-dirty' |
            Out-Null
    }
    catch {
        $dirtyTreeRejected = $true
    }
    if (-not $dirtyTreeRejected) {
        throw 'A dirty tracked source tree was not rejected.'
    }
}
finally {
    $env:GITHUB_SHA = $originalGitHubSha
    Remove-Item -LiteralPath $testRoot -Recurse -Force
}

if ($RequireRolloutTooling) {
    $rolloutRoot = Join-Path `
        ([System.IO.Path]::GetTempPath()) `
        ('gma-release-rollout-' + [System.Guid]::NewGuid().ToString('N'))
    [System.IO.Directory]::CreateDirectory($rolloutRoot) | Out-Null

    try {
        & git -C $rolloutRoot init -b dev | Out-Null
        if ($LASTEXITCODE -ne 0) {
            throw 'Unable to initialize the release rollout fixture.'
        }
        & git -C $rolloutRoot remote add origin `
            'https://github.com/SadPossum/Generated-Release-Test.git'

        foreach ($directory in @('.gma', 'eng')) {
            [System.IO.Directory]::CreateDirectory(
                (Join-Path $rolloutRoot $directory)) | Out-Null
        }
        $securityBaselineCommit = 'b' * 40
        $releaseEvidenceCommit = 'c' * 40
        $securityManifest = [ordered] @{
            schemaVersion = 1
            repository = 'SadPossum/Generated-Release-Test'
            securityBaseline = [ordered] @{
                repository = 'SadPossum/GMA-Skeleton'
                commit = $securityBaselineCommit
            }
            dependencyEcosystems = @('github-actions', 'nuget')
        }
        [System.IO.File]::WriteAllText(
            (Join-Path $rolloutRoot '.gma\repository-security.json'),
            ($securityManifest | ConvertTo-Json -Depth 6) + "`n",
            [System.Text.UTF8Encoding]::new($false))
        [System.IO.File]::WriteAllText(
            (Join-Path $rolloutRoot '.gma\security-exceptions.json'),
            "{`"schemaVersion`":1,`"exceptions`":[]}`n",
            [System.Text.UTF8Encoding]::new($false))
        [System.IO.File]::WriteAllText(
            (Join-Path $rolloutRoot 'eng\check-repository-security.ps1'),
            "param([string] `$RepositoryRoot)`n",
            [System.Text.UTF8Encoding]::new($false))
        [System.IO.Directory]::CreateDirectory(
            (Join-Path $rolloutRoot '.github\workflows')) | Out-Null
        [System.IO.File]::WriteAllText(
            (Join-Path $rolloutRoot '.github\workflows\security.yml'),
            @(
                'name: Security Baseline',
                'jobs:',
                '  scan:',
                '    steps:',
                '      - name: Run repository security baseline',
                "        uses: SadPossum/GMA-Skeleton/.github/actions/security-baseline@$securityBaselineCommit"
            ) -join "`n",
            [System.Text.UTF8Encoding]::new($false))

        & (Join-Path $root 'eng\apply-repository-release-baseline.ps1') `
            -OutputPath $rolloutRoot `
            -RepositorySlug 'SadPossum/Generated-Release-Test' `
            -RepositoryDisplayName 'Generated Release Test' `
            -ArtifactName 'generated-release-test' `
            -ReleaseKind source `
            -ReleaseEvidenceCommit $releaseEvidenceCommit `
            -SupportedVersion 'v0.2.0'

        $generatedGuard = Join-Path `
            $rolloutRoot `
            'eng\check-repository-release.ps1'
        & $generatedGuard -RepositoryRoot $rolloutRoot

        $generatedWorkflowPath = Join-Path `
            $rolloutRoot `
            '.github\workflows\release-evidence.yml'
        $generatedWorkflow = [System.IO.File]::ReadAllText(
            $generatedWorkflowPath)
        [System.IO.File]::WriteAllText(
            $generatedWorkflowPath,
            $generatedWorkflow.Replace(
                $releaseEvidenceCommit,
                ('d' * 40)),
            [System.Text.UTF8Encoding]::new($false))

        $driftRejected = $false
        try {
            & $generatedGuard -RepositoryRoot $rolloutRoot
        }
        catch {
            $driftRejected = $true
        }
        if (-not $driftRejected) {
            throw 'Generated release baseline did not reject action drift.'
        }
    }
    finally {
        Remove-Item -LiteralPath $rolloutRoot -Recurse -Force
    }
}

Write-Host 'Reusable source release evidence is valid.'

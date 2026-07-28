[CmdletBinding()]
param(
    [string] $ExpectedPrivateReportingUrl,

    [switch] $RequireRolloutTooling
)

. (Join-Path $PSScriptRoot 'common.ps1')

$root = Get-GmaRepositoryRoot
$requiredFiles = @(
    '.github\actions\security-baseline\action.yml',
    '.github\actions\security-baseline\convert-security-exceptions.ps1',
    '.github\actions\security-baseline\write-security-evidence-summary.ps1',
    '.github\dependabot.yml',
    '.github\workflows\codeql.yml',
    '.github\workflows\security.yml',
    '.gma\security-exceptions.json',
    'SECURITY.md'
)
if ($RequireRolloutTooling) {
    $requiredFiles += @(
        'eng\apply-repository-security-baseline.ps1',
        'eng\repository-security\check-repository-security.ps1'
    )
}

foreach ($relativePath in $requiredFiles) {
    $path = Join-Path $root $relativePath
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Missing repository security baseline file '$relativePath'."
    }
}

$securityAction = [System.IO.File]::ReadAllText(
    (Join-Path $root '.github\actions\security-baseline\action.yml'))
$requiredActionTokens = @(
    'aquasecurity/setup-trivy@3fb12ec12f41e471780db15c232d5dd185dcb514',
    'version: v0.70.0',
    '--scanners vuln,secret,misconfig,license',
    '--severity "$SCAN_SEVERITY"',
    '--format json',
    'trivy convert',
    '--format sarif',
    '--format cyclonedx',
    '--exit-code 1',
    '--ignore-unfixed=false',
    'security-summary.json',
    'convert-security-exceptions.ps1',
    'default: .gma/security-exceptions.json',
    '--ignorefile "$exception_ignore_file"',
    'write-security-evidence-summary.ps1',
    'default: 20m'
)
foreach ($token in $requiredActionTokens) {
    if ($securityAction.IndexOf($token, [System.StringComparison]::Ordinal) -lt 0) {
        throw "Repository security action is missing required token '$token'."
    }
}

$exceptionScript = Join-Path $root `
    '.github\actions\security-baseline\convert-security-exceptions.ps1'
$exceptionTestDirectory = Join-Path `
    ([System.IO.Path]::GetTempPath()) `
    ('gma-security-exceptions-' + [System.Guid]::NewGuid().ToString('N'))
[System.IO.Directory]::CreateDirectory($exceptionTestDirectory) | Out-Null

try {
    $exceptionInputPath = Join-Path $exceptionTestDirectory 'exceptions.json'
    $exceptionOutputPath = Join-Path $exceptionTestDirectory 'exceptions.yaml'
    $observedAtUtc = [datetimeoffset] '2026-07-28T00:00:00Z'
    $validExceptionDocument = [ordered] @{
        schemaVersion = 1
        exceptions = @(
            [ordered] @{
                scanner = 'vulnerability'
                findingId = 'CVE-2099-0001'
                owner = 'security-maintainers'
                reason = 'The affected feature is not included in this source set.'
                expiresOn = '2026-08-31'
                purls = @('pkg:nuget/Example.Package@1.0.0')
            },
            [ordered] @{
                scanner = 'secret'
                findingId = 'synthetic-secret-rule'
                owner = 'security-maintainers'
                reason = 'The fixture contains a documented non-secret sentinel.'
                expiresOn = '2026-08-31'
                paths = @('tests/Fixtures/sentinel.txt')
            }
        )
    }
    [System.IO.File]::WriteAllText(
        $exceptionInputPath,
        ($validExceptionDocument | ConvertTo-Json -Depth 8),
        [System.Text.UTF8Encoding]::new($false))

    $exceptionCount = & $exceptionScript `
        -InputPath $exceptionInputPath `
        -OutputPath $exceptionOutputPath `
        -ObservedAtUtc $observedAtUtc
    if ($exceptionCount -ne 2) {
        throw 'Security exception converter returned an invalid entry count.'
    }

    $exceptionYaml = [System.IO.File]::ReadAllText($exceptionOutputPath)
    foreach ($token in @(
        'vulnerabilities:',
        'secrets:',
        'CVE-2099-0001',
        'pkg:nuget/Example.Package@1.0.0',
        'tests/Fixtures/sentinel.txt',
        'expired_at: 2026-08-31',
        'owner=security-maintainers; reason=')) {
        if ($exceptionYaml.IndexOf(
            $token,
            [System.StringComparison]::Ordinal) -lt 0) {
            throw "Security exception YAML is missing required token '$token'."
        }
    }

    $emptyExceptionCount = & $exceptionScript `
        -InputPath (Join-Path $root '.gma\security-exceptions.json') `
        -OutputPath $exceptionOutputPath `
        -ObservedAtUtc $observedAtUtc
    if ($emptyExceptionCount -ne 0 -or
        [System.IO.File]::ReadAllText($exceptionOutputPath).Trim() -ne '{}') {
        throw 'Empty security exception handling is invalid.'
    }

    $invalidExceptionDocuments = @(
        [pscustomobject] @{
            Name = 'expired'
            Document = [ordered] @{
                schemaVersion = 1
                exceptions = @(
                    [ordered] @{
                        scanner = 'secret'
                        findingId = 'expired-rule'
                        owner = 'security-maintainers'
                        reason = 'This exception has already expired.'
                        expiresOn = '2026-07-27'
                        paths = @('tests/fixture.txt')
                    }
                )
            }
        },
        [pscustomobject] @{
            Name = 'unscoped'
            Document = [ordered] @{
                schemaVersion = 1
                exceptions = @(
                    [ordered] @{
                        scanner = 'license'
                        findingId = 'GPL-3.0'
                        owner = 'security-maintainers'
                        reason = 'This exception is intentionally missing a scope.'
                        expiresOn = '2026-08-31'
                    }
                )
            }
        },
        [pscustomobject] @{
            Name = 'too-long'
            Document = [ordered] @{
                schemaVersion = 1
                exceptions = @(
                    [ordered] @{
                        scanner = 'misconfiguration'
                        findingId = 'AVD-TEST-0001'
                        owner = 'security-maintainers'
                        reason = 'This exception exceeds the maximum review interval.'
                        expiresOn = '2027-07-28'
                        paths = @('deploy/test.yaml')
                    }
                )
            }
        },
        [pscustomobject] @{
            Name = 'unknown-property'
            Document = [ordered] @{
                schemaVersion = 1
                exceptions = @()
                allowEverything = $true
            }
        }
    )

    foreach ($invalidCase in $invalidExceptionDocuments) {
        [System.IO.File]::WriteAllText(
            $exceptionInputPath,
            ($invalidCase.Document | ConvertTo-Json -Depth 8),
            [System.Text.UTF8Encoding]::new($false))
        $rejected = $false
        try {
            & $exceptionScript `
                -InputPath $exceptionInputPath `
                -OutputPath $exceptionOutputPath `
                -ObservedAtUtc $observedAtUtc |
                Out-Null
        }
        catch {
            $rejected = $true
        }

        if (-not $rejected) {
            throw "Security exception case '$($invalidCase.Name)' was not rejected."
        }
    }
}
finally {
    Remove-Item -LiteralPath $exceptionTestDirectory -Recurse -Force
}

if ($RequireRolloutTooling) {
    $rolloutTestDirectory = Join-Path `
        ([System.IO.Path]::GetTempPath()) `
        ('gma-repository-security-' + [System.Guid]::NewGuid().ToString('N'))
    [System.IO.Directory]::CreateDirectory($rolloutTestDirectory) | Out-Null

    try {
        $baselineCommit = 'b' * 40
        & (Join-Path $root 'eng\apply-repository-security-baseline.ps1') `
            -OutputPath $rolloutTestDirectory `
            -RepositorySlug 'SadPossum/Generated-Security-Test' `
            -RepositoryDisplayName 'Generated Security Test' `
            -PackageEcosystem nuget `
            -SecurityBaselineCommit $baselineCommit `
            -SupportedVersion 'v0.2.0' `
            -IncludeGitSubmodules

        $generatedGuard = Join-Path `
            $rolloutTestDirectory `
            'eng\check-repository-security.ps1'
        & $generatedGuard -RepositoryRoot $rolloutTestDirectory

        $generatedDependabot = [System.IO.File]::ReadAllText(
            (Join-Path $rolloutTestDirectory '.github\dependabot.yml'))
        if ($generatedDependabot.EndsWith(
                "`n`n",
                [System.StringComparison]::Ordinal)) {
            throw 'Generated Dependabot policy has a trailing blank line.'
        }

        $generatedWorkflowPath = Join-Path `
            $rolloutTestDirectory `
            '.github\workflows\security.yml'
        $generatedWorkflow = [System.IO.File]::ReadAllText(
            $generatedWorkflowPath)
        [System.IO.File]::WriteAllText(
            $generatedWorkflowPath,
            $generatedWorkflow.Replace($baselineCommit, ('c' * 40)),
            [System.Text.UTF8Encoding]::new($false))

        $driftRejected = $false
        try {
            & $generatedGuard -RepositoryRoot $rolloutTestDirectory
        }
        catch {
            $driftRejected = $true
        }
        if (-not $driftRejected) {
            throw 'Generated repository security baseline did not reject action drift.'
        }
    }
    finally {
        Remove-Item -LiteralPath $rolloutTestDirectory -Recurse -Force
    }
}

$summaryScript = Join-Path $root `
    '.github\actions\security-baseline\write-security-evidence-summary.ps1'
$summaryTestDirectory = Join-Path `
    ([System.IO.Path]::GetTempPath()) `
    ('gma-security-summary-' + [System.Guid]::NewGuid().ToString('N'))
[System.IO.Directory]::CreateDirectory($summaryTestDirectory) | Out-Null

try {
    $inputPath = Join-Path $summaryTestDirectory 'trivy.json'
    $outputPath = Join-Path $summaryTestDirectory 'security-summary.json'
    $sampleReport = [ordered] @{
        Results = @(
            [ordered] @{
                Target = 'private/path.txt'
                Vulnerabilities = @(
                    [ordered] @{
                        VulnerabilityID = 'CVE-DO-NOT-LEAK'
                        Severity = 'HIGH'
                    }
                )
                Secrets = @(
                    [ordered] @{
                        RuleID = 'secret-rule'
                        Severity = 'CRITICAL'
                        Match = 'AKIA-DO-NOT-LEAK'
                        Title = 'Do not leak this title'
                    }
                )
                Misconfigurations = @(
                    [ordered] @{
                        ID = 'MISCONFIG-DO-NOT-LEAK'
                        Severity = 'CUSTOM'
                    }
                )
            }
        )
    }
    [System.IO.File]::WriteAllText(
        $inputPath,
        ($sampleReport | ConvertTo-Json -Depth 8),
        [System.Text.UTF8Encoding]::new($false))

    & $summaryScript `
        -InputPath $inputPath `
        -OutputPath $outputPath `
        -ScanExitCode 1 `
        -SourceCommit ('a' * 40) `
        -CiRunId '12345' `
        -CiRunAttempt '2'

    $summaryText = [System.IO.File]::ReadAllText($outputPath)
    $summary = $summaryText | ConvertFrom-Json
    $expectedRootProperties = @(
        'schemaVersion',
        'sourceCommit',
        'ciRunCorrelation',
        'status',
        'scanners'
    )
    $actualRootProperties = @($summary.PSObject.Properties.Name)
    if (($actualRootProperties -join ',') -ne
        ($expectedRootProperties -join ',')) {
        throw 'Security evidence summary does not have the closed root shape.'
    }

    if ($summary.schemaVersion -ne 1 -or
        $summary.sourceCommit -ne ('a' * 40) -or
        $summary.ciRunCorrelation -ne 'github-actions:12345:2' -or
        $summary.status -ne 'findings') {
        throw 'Security evidence summary provenance or status is invalid.'
    }

    $expectedCategories = @(
        'vulnerability',
        'secret',
        'misconfiguration',
        'license'
    )
    $actualCategories = @($summary.scanners | ForEach-Object { $_.category })
    if (($actualCategories -join ',') -ne ($expectedCategories -join ',')) {
        throw 'Security evidence summary scanner categories are not bounded.'
    }

    $scannerByCategory = @{}
    foreach ($scanner in $summary.scanners) {
        $scannerByCategory[$scanner.category] = $scanner
    }

    if ($scannerByCategory.vulnerability.count -ne 1 -or
        $scannerByCategory.vulnerability.severities.high -ne 1 -or
        $scannerByCategory.secret.count -ne 1 -or
        $scannerByCategory.secret.severities.critical -ne 1 -or
        $scannerByCategory.misconfiguration.count -ne 1 -or
        $scannerByCategory.misconfiguration.severities.unknown -ne 1 -or
        $scannerByCategory.license.count -ne 0) {
        throw 'Security evidence summary counts are invalid.'
    }

    foreach ($forbiddenValue in @(
        'private/path.txt',
        'CVE-DO-NOT-LEAK',
        'AKIA-DO-NOT-LEAK',
        'Do not leak this title',
        'MISCONFIG-DO-NOT-LEAK',
        'CUSTOM')) {
        if ($summaryText.IndexOf(
            $forbiddenValue,
            [System.StringComparison]::Ordinal) -ge 0) {
            throw "Security evidence summary leaked '$forbiddenValue'."
        }
    }

    [System.IO.File]::WriteAllText(
        $inputPath,
        '{"Results":[]}',
        [System.Text.UTF8Encoding]::new($false))
    & $summaryScript `
        -InputPath $inputPath `
        -OutputPath $outputPath `
        -ScanExitCode 0 `
        -SourceCommit 'not-a-commit' `
        -CiRunId 'not-a-run' `
        -CiRunAttempt 'not-an-attempt'

    $cleanSummary = [System.IO.File]::ReadAllText($outputPath) |
        ConvertFrom-Json
    $cleanFindingCount = @(
        $cleanSummary.scanners |
            Measure-Object -Property count -Sum).Sum
    if ($cleanSummary.status -ne 'passed' -or
        $null -ne $cleanSummary.sourceCommit -or
        $null -ne $cleanSummary.ciRunCorrelation -or
        $cleanFindingCount -ne 0) {
        throw 'Clean security evidence summary handling is invalid.'
    }

    & $summaryScript `
        -InputPath (Join-Path $summaryTestDirectory 'missing.json') `
        -OutputPath $outputPath `
        -ScanExitCode 2
    $failedSummary = [System.IO.File]::ReadAllText($outputPath) |
        ConvertFrom-Json
    if ($failedSummary.status -ne 'failed') {
        throw 'Missing scanner evidence is not reported as failed.'
    }
}
finally {
    Remove-Item -LiteralPath $summaryTestDirectory -Recurse -Force
}

$securityWorkflow = [System.IO.File]::ReadAllText(
    (Join-Path $root '.github\workflows\security.yml'))
$requiredWorkflowTokens = @(
    'uses: ./.github/actions/security-baseline',
    'exception-file: .gma/security-exceptions.json',
    'github/codeql-action/upload-sarif@7188fc363630916deb702c7fdcf4e481b751f97a',
    'actions/upload-artifact@043fb46d1a93c77aae656e7c1c64a875d1fc6a0a',
    'security-events: write',
    'retention-days: 30'
)
foreach ($token in $requiredWorkflowTokens) {
    if ($securityWorkflow.IndexOf($token, [System.StringComparison]::Ordinal) -lt 0) {
        throw "Repository security workflow is missing required token '$token'."
    }
}

$dependabotPolicy = [System.IO.File]::ReadAllText(
    (Join-Path $root '.github\dependabot.yml'))
foreach ($ecosystem in @('github-actions', 'gitsubmodule', 'nuget')) {
    $token = "package-ecosystem: $ecosystem"
    if ($dependabotPolicy.IndexOf(
        $token,
        [System.StringComparison]::Ordinal) -lt 0) {
        throw "Dependency update policy is missing ecosystem '$ecosystem'."
    }
}

$codeQlWorkflow = [System.IO.File]::ReadAllText(
    (Join-Path $root '.github\workflows\codeql.yml'))
foreach ($token in @(
    'github/codeql-action/init@7188fc363630916deb702c7fdcf4e481b751f97a',
    'github/codeql-action/analyze@7188fc363630916deb702c7fdcf4e481b751f97a',
    'build-mode: manual',
    'languages: csharp')) {
    if ($codeQlWorkflow.IndexOf($token, [System.StringComparison]::Ordinal) -lt 0) {
        throw "CodeQL workflow is missing required token '$token'."
    }
}

$securityPolicy = [System.IO.File]::ReadAllText((Join-Path $root 'SECURITY.md'))
foreach ($token in @(
    'Supported Versions',
    'private vulnerability reporting')) {
    if ($securityPolicy.IndexOf($token, [System.StringComparison]::Ordinal) -lt 0) {
        throw "Repository security policy is missing required token '$token'."
    }
}

if (-not [string]::IsNullOrWhiteSpace($ExpectedPrivateReportingUrl) -and
    $securityPolicy.IndexOf($ExpectedPrivateReportingUrl, [System.StringComparison]::Ordinal) -lt 0) {
    throw "Repository security policy does not link the expected private reporting URL '$ExpectedPrivateReportingUrl'."
}

$workflowFiles = Get-ChildItem -LiteralPath (Join-Path $root '.github') -Recurse -File |
    Where-Object { $_.Extension -in @('.yml', '.yaml') }
$usesPattern = [regex]'(?m)^\s*-?\s*uses:\s*([^\s#]+)'
foreach ($file in $workflowFiles) {
    $content = [System.IO.File]::ReadAllText($file.FullName)
    foreach ($match in $usesPattern.Matches($content)) {
        $reference = $match.Groups[1].Value
        if ($reference.StartsWith('./', [System.StringComparison]::Ordinal)) {
            continue
        }

        if ($reference -notmatch '^[^@\s]+@[0-9a-fA-F]{40}$') {
            $relativePath = $file.FullName.Substring(
                $root.TrimEnd('\', '/').Length).TrimStart('\', '/')
            throw "GitHub Action reference '$reference' in '$relativePath' is not pinned to an immutable commit."
        }
    }
}

Write-Host 'Repository security policy, evidence workflows, and immutable action pins are valid.'

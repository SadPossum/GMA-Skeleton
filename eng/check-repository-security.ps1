[CmdletBinding()]
param(
    [string] $ExpectedPrivateReportingUrl
)

. (Join-Path $PSScriptRoot 'common.ps1')

$root = Get-GmaRepositoryRoot
$requiredFiles = @(
    '.github\actions\security-baseline\action.yml',
    '.github\actions\security-baseline\write-security-evidence-summary.ps1',
    '.github\dependabot.yml',
    '.github\workflows\codeql.yml',
    '.github\workflows\security.yml',
    'SECURITY.md'
)

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
    'write-security-evidence-summary.ps1',
    'default: 20m'
)
foreach ($token in $requiredActionTokens) {
    if ($securityAction.IndexOf($token, [System.StringComparison]::Ordinal) -lt 0) {
        throw "Repository security action is missing required token '$token'."
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
            $relativePath = [System.IO.Path]::GetRelativePath($root, $file.FullName)
            throw "GitHub Action reference '$reference' in '$relativePath' is not pinned to an immutable commit."
        }
    }
}

Write-Host 'Repository security policy, evidence workflows, and immutable action pins are valid.'

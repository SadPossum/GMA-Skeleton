[CmdletBinding()]
param(
    [string] $ExpectedPrivateReportingUrl
)

. (Join-Path $PSScriptRoot 'common.ps1')

$root = Get-GmaRepositoryRoot
$requiredFiles = @(
    '.github\actions\security-baseline\action.yml',
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
    '--format sarif',
    '--format cyclonedx',
    '--exit-code 1',
    '--ignore-unfixed=false',
    'default: 20m'
)
foreach ($token in $requiredActionTokens) {
    if ($securityAction.IndexOf($token, [System.StringComparison]::Ordinal) -lt 0) {
        throw "Repository security action is missing required token '$token'."
    }
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

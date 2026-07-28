[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string] $InputPath,
    [Parameter(Mandatory = $true)][string] $OutputPath,
    [Parameter(Mandatory = $true)][int] $ScanExitCode,
    [string] $SourceCommit = $env:GITHUB_SHA,
    [string] $CiRunId = $env:GITHUB_RUN_ID,
    [string] $CiRunAttempt = $env:GITHUB_RUN_ATTEMPT
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$severityNames = @('UNKNOWN', 'LOW', 'MEDIUM', 'HIGH', 'CRITICAL')
$scannerCollections = [ordered] @{
    vulnerability = 'Vulnerabilities'
    secret = 'Secrets'
    misconfiguration = 'Misconfigurations'
    license = 'Licenses'
}

$scannerStates = [ordered] @{}
foreach ($category in $scannerCollections.Keys) {
    $severityCounts = [ordered] @{}
    foreach ($severity in $severityNames) {
        $severityCounts[$severity] = 0
    }

    $scannerStates[$category] = [ordered] @{
        count = 0
        severities = $severityCounts
    }
}

$reportReadable = $false
if (Test-Path -LiteralPath $InputPath -PathType Leaf) {
    try {
        $report = [System.IO.File]::ReadAllText($InputPath) |
            ConvertFrom-Json
        $reportReadable = $true

        foreach ($result in @($report.Results)) {
            if ($null -eq $result) {
                continue
            }

            foreach ($entry in $scannerCollections.GetEnumerator()) {
                $collectionProperty = $result.PSObject.Properties[$entry.Value]
                if ($null -eq $collectionProperty -or
                    $null -eq $collectionProperty.Value) {
                    continue
                }

                foreach ($finding in @($collectionProperty.Value)) {
                    $severityProperty = $finding.PSObject.Properties['Severity']
                    $severity = if ($null -eq $severityProperty) {
                        'UNKNOWN'
                    }
                    else {
                        ([string] $severityProperty.Value).ToUpperInvariant()
                    }

                    if ($severity -notin $severityNames) {
                        $severity = 'UNKNOWN'
                    }

                    $state = $scannerStates[$entry.Key]
                    $state.count++
                    $state.severities[$severity]++
                }
            }
        }
    }
    catch {
        $reportReadable = $false
    }
}

$totalCount = 0
$scannerSummaries = @(
    foreach ($category in $scannerCollections.Keys) {
        $state = $scannerStates[$category]
        $totalCount += $state.count

        [ordered] @{
            category = $category
            count = $state.count
            severities = [ordered] @{
                unknown = $state.severities.UNKNOWN
                low = $state.severities.LOW
                medium = $state.severities.MEDIUM
                high = $state.severities.HIGH
                critical = $state.severities.CRITICAL
            }
        }
    }
)

$status = if (-not $reportReadable) {
    'failed'
}
elseif ($ScanExitCode -eq 0 -and $totalCount -eq 0) {
    'passed'
}
elseif ($ScanExitCode -eq 1 -and $totalCount -gt 0) {
    'findings'
}
else {
    'failed'
}

$validatedCommit = if ($SourceCommit -match '^[0-9a-fA-F]{40}$') {
    $SourceCommit.ToLowerInvariant()
}
else {
    $null
}

$runCorrelation = if ($CiRunId -match '^[0-9]{1,20}$' -and
    $CiRunAttempt -match '^[0-9]{1,10}$') {
    "github-actions:${CiRunId}:${CiRunAttempt}"
}
else {
    $null
}

$summary = [ordered] @{
    schemaVersion = 1
    sourceCommit = $validatedCommit
    ciRunCorrelation = $runCorrelation
    status = $status
    scanners = $scannerSummaries
}

$outputDirectory = Split-Path -Parent $OutputPath
if (-not [string]::IsNullOrWhiteSpace($outputDirectory)) {
    [System.IO.Directory]::CreateDirectory($outputDirectory) | Out-Null
}

$json = $summary | ConvertTo-Json -Depth 8
[System.IO.File]::WriteAllText(
    $OutputPath,
    "$json`n",
    [System.Text.UTF8Encoding]::new($false))

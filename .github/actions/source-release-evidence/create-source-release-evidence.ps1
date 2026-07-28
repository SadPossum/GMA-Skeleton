[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $RepositoryRoot,

    [string] $ManifestPath = '.gma/release-evidence.json',

    [Parameter(Mandatory = $true)]
    [ValidatePattern(
        '^(?:v[0-9]+\.[0-9]+\.[0-9]+(?:-[0-9A-Za-z.-]+)?|candidate-[0-9a-f]{12})$')]
    [string] $ReleaseVersion,

    [Parameter(Mandatory = $true)]
    [ValidatePattern('^refs/(?:heads|tags)/[A-Za-z0-9._/-]+$')]
    [string] $SourceRef,

    [string] $SecurityEvidenceDirectory = 'artifacts/release-security',

    [string] $OutputDirectory = 'artifacts/release',

    [switch] $RequireTag
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Assert-ClosedObject {
    param(
        [Parameter(Mandatory = $true)]
        [object] $Value,

        [Parameter(Mandatory = $true)]
        [string[]] $AllowedProperties,

        [Parameter(Mandatory = $true)]
        [string] $Context
    )

    $unknownProperties = @(
        $Value.PSObject.Properties.Name |
            Where-Object { $AllowedProperties -notcontains $_ }
    )
    if ($unknownProperties.Count -gt 0) {
        throw "$Context contains unsupported properties."
    }
}

function Read-BoundedJsonDocument {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Path,

        [Parameter(Mandatory = $true)]
        [long] $MaximumBytes,

        [Parameter(Mandatory = $true)]
        [string] $Context
    )

    if (-not [System.IO.File]::Exists($Path)) {
        throw "Missing $Context file."
    }

    $fileInfo = [System.IO.FileInfo]::new($Path)
    if ($fileInfo.Length -gt $MaximumBytes) {
        throw "$Context file exceeds its size limit."
    }

    try {
        $document = [System.IO.File]::ReadAllText($Path) | ConvertFrom-Json
    }
    catch {
        throw "$Context file is not valid JSON."
    }

    if ($null -eq $document -or
        $document -is [string] -or
        $document -is [System.Array]) {
        throw "$Context file must contain an object."
    }

    return $document
}

function Resolve-RepositoryPath {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Root,

        [Parameter(Mandatory = $true)]
        [string] $Path,

        [Parameter(Mandatory = $true)]
        [string] $Context
    )

    if ([System.IO.Path]::IsPathRooted($Path) -or
        $Path -match '^[A-Za-z]:[\\/]' -or
        $Path -match '^[\\/]' -or
        $Path -match '(^|[\\/])\.\.([\\/]|$)') {
        throw "$Context must be repository relative."
    }

    $resolvedPath = [System.IO.Path]::GetFullPath((Join-Path $Root $Path))
    $rootPrefix = $Root.TrimEnd(
        [System.IO.Path]::DirectorySeparatorChar,
        [System.IO.Path]::AltDirectorySeparatorChar) +
        [System.IO.Path]::DirectorySeparatorChar
    if (-not $resolvedPath.StartsWith(
            $rootPrefix,
            [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "$Context resolves outside the repository."
    }

    return $resolvedPath
}

function Invoke-GitText {
    param(
        [Parameter(Mandatory = $true)]
        [string[]] $Arguments,

        [switch] $AllowFailure
    )

    $rows = @(& git -C $script:ResolvedRepositoryRoot @Arguments 2>$null)
    if ($LASTEXITCODE -ne 0 -and -not $AllowFailure) {
        throw "git $($Arguments -join ' ') failed."
    }

    return @($rows | ForEach-Object { $_.Trim() })
}

function Get-Sha256 {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Path
    )

    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).
        Hash.ToLowerInvariant()
}

function Assert-NonNegativeInteger {
    param(
        [AllowNull()]
        [object] $Value,

        [Parameter(Mandatory = $true)]
        [string] $Context
    )

    if (($Value -isnot [int] -and $Value -isnot [long]) -or
        $Value -lt 0) {
        throw "$Context must be a non-negative integer."
    }
}

$script:ResolvedRepositoryRoot =
    [System.IO.Path]::GetFullPath($RepositoryRoot)
if (-not [System.IO.Directory]::Exists($script:ResolvedRepositoryRoot)) {
    throw "Repository root '$RepositoryRoot' does not exist."
}
if (-not [System.IO.Directory]::Exists(
        (Join-Path $script:ResolvedRepositoryRoot '.git'))) {
    $gitDirectory = @(
        Invoke-GitText -Arguments @('rev-parse', '--git-dir')
    )
    if ($gitDirectory.Count -ne 1) {
        throw 'Repository root is not a Git worktree.'
    }
}

$trackedChanges = @(
    Invoke-GitText -Arguments @(
        'status',
        '--porcelain=v1',
        '--untracked-files=no'
    )
)
if ($trackedChanges.Count -gt 0) {
    throw 'Release evidence requires a clean tracked source tree.'
}

$sourceCommitRows = @(
    Invoke-GitText -Arguments @('rev-parse', 'HEAD')
)
if ($sourceCommitRows.Count -ne 1 -or
    $sourceCommitRows[0] -notmatch '^[0-9a-f]{40}$') {
    throw 'Unable to resolve the source commit.'
}
$sourceCommit = $sourceCommitRows[0]

if (-not [string]::IsNullOrWhiteSpace($env:GITHUB_SHA) -and
    $env:GITHUB_SHA -ne $sourceCommit) {
    throw 'The checked-out commit does not match GITHUB_SHA.'
}

if ($RequireTag) {
    if ($ReleaseVersion -notmatch
        '^v[0-9]+\.[0-9]+\.[0-9]+(?:-[0-9A-Za-z.-]+)?$' -or
        $SourceRef -ne "refs/tags/$ReleaseVersion") {
        throw 'A published release requires an exact SemVer tag ref.'
    }

    $tagCommitRows = @(
        Invoke-GitText -Arguments @(
            'rev-parse',
            "$ReleaseVersion^{commit}"
        )
    )
    if ($tagCommitRows.Count -ne 1 -or
        $tagCommitRows[0] -ne $sourceCommit) {
        throw 'The release tag does not point at the checked-out commit.'
    }
}
elseif ($SourceRef.StartsWith(
        'refs/tags/',
        [System.StringComparison]::Ordinal)) {
    throw 'A tag ref must enable require-tag.'
}

$commitTimeRows = @(
    Invoke-GitText -Arguments @(
        'show',
        '-s',
        '--format=%cI',
        $sourceCommit
    )
)
$sourceCommittedAt = [datetimeoffset]::MinValue
if ($commitTimeRows.Count -ne 1 -or
    -not [datetimeoffset]::TryParse(
        $commitTimeRows[0],
        [System.Globalization.CultureInfo]::InvariantCulture,
        [System.Globalization.DateTimeStyles]::RoundtripKind,
        [ref] $sourceCommittedAt)) {
    throw 'Unable to resolve the source commit timestamp.'
}

$resolvedManifestPath = Resolve-RepositoryPath `
    -Root $script:ResolvedRepositoryRoot `
    -Path $ManifestPath `
    -Context 'Release-evidence manifest path'
$manifest = Read-BoundedJsonDocument `
    -Path $resolvedManifestPath `
    -MaximumBytes 16KB `
    -Context 'release-evidence manifest'
Assert-ClosedObject `
    -Value $manifest `
    -AllowedProperties @(
        'schemaVersion',
        'repository',
        'artifactName',
        'releaseKind',
        'releaseEvidence',
        'sourceSetPath'
    ) `
    -Context 'Release-evidence manifest'

if ($manifest.schemaVersion -ne 1 -or
    $manifest.repository -isnot [string] -or
    $manifest.repository -notmatch
        '^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$' -or
    $manifest.artifactName -isnot [string] -or
    $manifest.artifactName -notmatch
        '^[a-z0-9][a-z0-9.-]{1,63}$' -or
    @('source', 'composition') -notcontains $manifest.releaseKind) {
    throw 'Release-evidence manifest identity is invalid.'
}

if ($null -eq $manifest.releaseEvidence -or
    $manifest.releaseEvidence -is [string] -or
    $manifest.releaseEvidence -is [System.Array]) {
    throw 'Release-evidence implementation reference must be an object.'
}
Assert-ClosedObject `
    -Value $manifest.releaseEvidence `
    -AllowedProperties @('repository', 'commit') `
    -Context 'Release-evidence implementation reference'
if ($manifest.releaseEvidence.repository -ne 'SadPossum/GMA-Skeleton' -or
    $manifest.releaseEvidence.commit -isnot [string] -or
    $manifest.releaseEvidence.commit -notmatch '^[0-9a-f]{40}$') {
    throw 'Release-evidence implementation reference is invalid.'
}

$remoteRows = @(
    Invoke-GitText -Arguments @('remote', 'get-url', 'origin')
)
if ($remoteRows.Count -ne 1) {
    throw 'Repository origin URL is unavailable.'
}
$remoteMatch = [regex]::Match(
    $remoteRows[0],
    '(?i)(?:github\.com(?:-private)?[:/])(?<slug>[^/:\s]+/[^/\s]+?)(?:\.git)?$')
if (-not $remoteMatch.Success -or
    $remoteMatch.Groups['slug'].Value -ne $manifest.repository) {
    throw 'Release-evidence manifest does not match the GitHub origin.'
}

$sourceSetPath = $null
if ($manifest.releaseKind -eq 'composition') {
    if ($manifest.sourceSetPath -isnot [string] -or
        [string]::IsNullOrWhiteSpace($manifest.sourceSetPath)) {
        throw 'A composition release requires sourceSetPath.'
    }
    $sourceSetPath = Resolve-RepositoryPath `
        -Root $script:ResolvedRepositoryRoot `
        -Path $manifest.sourceSetPath `
        -Context 'Composition source-set path'
}
elseif ($null -ne $manifest.sourceSetPath) {
    throw 'A source release must set sourceSetPath to null.'
}

$resolvedSecurityEvidenceDirectory = Resolve-RepositoryPath `
    -Root $script:ResolvedRepositoryRoot `
    -Path $SecurityEvidenceDirectory `
    -Context 'Security evidence directory'
$sourceSbomPath = Join-Path `
    $resolvedSecurityEvidenceDirectory `
    'sbom.cdx.json'
$sourceSecuritySummaryPath = Join-Path `
    $resolvedSecurityEvidenceDirectory `
    'security-summary.json'
$sbom = Read-BoundedJsonDocument `
    -Path $sourceSbomPath `
    -MaximumBytes 64MB `
    -Context 'CycloneDX SBOM'
if ($sbom.bomFormat -ne 'CycloneDX') {
    throw 'Release SBOM must use CycloneDX JSON.'
}
$securitySummary = Read-BoundedJsonDocument `
    -Path $sourceSecuritySummaryPath `
    -MaximumBytes 1MB `
    -Context 'security summary'
Assert-ClosedObject `
    -Value $securitySummary `
    -AllowedProperties @(
        'schemaVersion',
        'sourceCommit',
        'ciRunCorrelation',
        'status',
        'scanners'
    ) `
    -Context 'Security summary'
if ($securitySummary.schemaVersion -ne 1 -or
    $securitySummary.status -ne 'passed' -or
    $securitySummary.sourceCommit -ne $sourceCommit -or
    $securitySummary.ciRunCorrelation -isnot [string] -or
    $securitySummary.ciRunCorrelation -notmatch
        '^github-actions:[0-9]{1,20}:[0-9]{1,10}$') {
    throw 'Release security summary must prove a passing scan of the release commit.'
}
$expectedScannerCategories = @(
    'vulnerability',
    'secret',
    'misconfiguration',
    'license'
)
$scanners = @($securitySummary.scanners)
if ($scanners.Count -ne $expectedScannerCategories.Count) {
    throw 'Release security summary must contain the closed scanner set.'
}
for ($index = 0; $index -lt $expectedScannerCategories.Count; $index++) {
    $scanner = $scanners[$index]
    if ($null -eq $scanner -or
        $scanner -is [string] -or
        $scanner -is [System.Array]) {
        throw 'Release security scanner summary must be an object.'
    }
    Assert-ClosedObject `
        -Value $scanner `
        -AllowedProperties @('category', 'count', 'severities') `
        -Context 'Release security scanner summary'
    if ($scanner.category -ne $expectedScannerCategories[$index]) {
        throw 'Release security scanner categories are invalid.'
    }
    Assert-NonNegativeInteger `
        -Value $scanner.count `
        -Context "Release security scanner '$($scanner.category)' count"
    if ($scanner.count -ne 0 -or
        $null -eq $scanner.severities -or
        $scanner.severities -is [string] -or
        $scanner.severities -is [System.Array]) {
        throw 'A passing release security summary cannot contain findings.'
    }
    Assert-ClosedObject `
        -Value $scanner.severities `
        -AllowedProperties @(
            'unknown',
            'low',
            'medium',
            'high',
            'critical'
        ) `
        -Context 'Release security severity summary'
    foreach ($severityName in @(
        'unknown',
        'low',
        'medium',
        'high',
        'critical'
    )) {
        $severityCount = $scanner.severities.$severityName
        Assert-NonNegativeInteger `
            -Value $severityCount `
            -Context "Release security '$severityName' count"
        if ($severityCount -ne 0) {
            throw 'A passing release security summary cannot contain findings.'
        }
    }
}

$sourceSet = $null
if ($null -ne $sourceSetPath) {
    $sourceSet = Read-BoundedJsonDocument `
        -Path $sourceSetPath `
        -MaximumBytes 4MB `
        -Context 'composition source set'
    Assert-ClosedObject `
        -Value $sourceSet `
        -AllowedProperties @(
            'schemaVersion',
            'generatedAtUtc',
            'rootCommit',
            'sdkVersion',
            'centralPackagesSha256',
            'repositories'
        ) `
        -Context 'Composition source set'
    $sourceRepositories = @($sourceSet.repositories)
    if ($sourceSet.schemaVersion -ne 2 -or
        $sourceSet.rootCommit -ne $sourceCommit -or
        $sourceSet.sdkVersion -isnot [string] -or
        [string]::IsNullOrWhiteSpace($sourceSet.sdkVersion) -or
        $sourceSet.centralPackagesSha256 -isnot [string] -or
        $sourceSet.centralPackagesSha256 -notmatch '^[0-9a-f]{64}$' -or
        $sourceRepositories.Count -eq 0) {
        throw 'Composition source set does not identify the release source.'
    }

    $observedPaths = [System.Collections.Generic.HashSet[string]]::new(
        [System.StringComparer]::Ordinal)
    foreach ($repository in $sourceRepositories) {
        if ($null -eq $repository -or
            $repository -is [string] -or
            $repository -is [System.Array]) {
            throw 'Composition repository entry must be an object.'
        }
        Assert-ClosedObject `
            -Value $repository `
            -AllowedProperties @(
                'path',
                'url',
                'commit',
                'branch',
                'configuredBranch',
                'dirty'
            ) `
            -Context 'Composition repository entry'
        if ($repository.path -isnot [string] -or
            [string]::IsNullOrWhiteSpace($repository.path) -or
            $repository.commit -isnot [string] -or
            $repository.commit -notmatch '^[0-9a-f]{40}$' -or
            $repository.dirty -isnot [bool] -or
            $repository.dirty -or
            -not $observedPaths.Add($repository.path)) {
            throw 'Composition repository entry is invalid or dirty.'
        }
        if ($repository.path -ne '.') {
            Resolve-RepositoryPath `
                -Root $script:ResolvedRepositoryRoot `
                -Path $repository.path `
                -Context 'Composition repository path' |
                Out-Null
        }
    }

    $rootRepository = @(
        $sourceRepositories |
            Where-Object { $_.path -eq '.' }
    )
    if ($rootRepository.Count -ne 1 -or
        $rootRepository[0].commit -ne $sourceCommit) {
        throw 'Composition source set root entry is invalid.'
    }
}

$resolvedOutputDirectory = Resolve-RepositoryPath `
    -Root $script:ResolvedRepositoryRoot `
    -Path $OutputDirectory `
    -Context 'Release output directory'
[System.IO.Directory]::CreateDirectory($resolvedOutputDirectory) | Out-Null

$archiveFileName =
    "$($manifest.artifactName)-$ReleaseVersion.zip"
$archivePath = Join-Path $resolvedOutputDirectory $archiveFileName
if ([System.IO.File]::Exists($archivePath)) {
    [System.IO.File]::Delete($archivePath)
}

& git -C $script:ResolvedRepositoryRoot archive `
    '--format=zip' `
    "--prefix=$($manifest.artifactName)-$ReleaseVersion/" `
    "--output=$archivePath" `
    $sourceCommit
if ($LASTEXITCODE -ne 0 -or
    -not [System.IO.File]::Exists($archivePath)) {
    throw 'Unable to create the deterministic source archive.'
}

$publishedSbomPath = Join-Path `
    $resolvedOutputDirectory `
    'sbom.cdx.json'
$publishedSecuritySummaryPath = Join-Path `
    $resolvedOutputDirectory `
    'security-summary.json'
[System.IO.File]::Copy($sourceSbomPath, $publishedSbomPath, $true)
[System.IO.File]::Copy(
    $sourceSecuritySummaryPath,
    $publishedSecuritySummaryPath,
    $true)

$publishedArtifacts = [System.Collections.Generic.List[object]]::new()
$publishedArtifacts.Add([ordered] @{
    role = 'source-archive'
    file = $archiveFileName
})
$publishedArtifacts.Add([ordered] @{
    role = 'source-sbom'
    file = 'sbom.cdx.json'
})
$publishedArtifacts.Add([ordered] @{
    role = 'security-summary'
    file = 'security-summary.json'
})

$publishedSourceSetPath = $null
if ($null -ne $sourceSetPath) {
    $publishedSourceSetPath = Join-Path `
        $resolvedOutputDirectory `
        'source-set.json'
    [System.IO.File]::Copy(
        $sourceSetPath,
        $publishedSourceSetPath,
        $true)
    $publishedArtifacts.Add([ordered] @{
        role = 'composition-source-set'
        file = 'source-set.json'
    })
}

$releaseManifestPath = Join-Path `
    $resolvedOutputDirectory `
    'release-manifest.json'
$releaseManifest = [ordered] @{
    schemaVersion = 1
    repository = $manifest.repository
    artifactName = $manifest.artifactName
    releaseKind = $manifest.releaseKind
    releaseVersion = $ReleaseVersion
    sourceRef = $SourceRef
    sourceCommit = $sourceCommit
    sourceCommittedAtUtc = $sourceCommittedAt.
        ToUniversalTime().
        ToString('O')
    artifacts = $publishedArtifacts.ToArray()
}
[System.IO.File]::WriteAllText(
    $releaseManifestPath,
    ($releaseManifest | ConvertTo-Json -Depth 6) +
        [Environment]::NewLine,
    [System.Text.UTF8Encoding]::new($false))

$checksumPaths = [System.Collections.Generic.List[string]]::new()
$checksumPaths.Add($archivePath)
$checksumPaths.Add($publishedSbomPath)
$checksumPaths.Add($publishedSecuritySummaryPath)
if ($null -ne $publishedSourceSetPath) {
    $checksumPaths.Add($publishedSourceSetPath)
}
$checksumPaths.Add($releaseManifestPath)

$checksumLines = @(
    $checksumPaths |
        Sort-Object { [System.IO.Path]::GetFileName($_) } |
        ForEach-Object {
            "$(Get-Sha256 $_) *$([System.IO.Path]::GetFileName($_))"
        }
)
$checksumsPath = Join-Path $resolvedOutputDirectory 'SHA256SUMS'
[System.IO.File]::WriteAllLines(
    $checksumsPath,
    $checksumLines,
    [System.Text.UTF8Encoding]::new($false))

[pscustomobject] @{
    archivePath = $archivePath
    manifestPath = $releaseManifestPath
    checksumsPath = $checksumsPath
    sbomPath = $publishedSbomPath
    evidenceDirectory = $resolvedOutputDirectory
} | ConvertTo-Json -Compress

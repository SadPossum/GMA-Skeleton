[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $InputPath,

    [Parameter(Mandatory = $true)]
    [string] $OutputPath,

    [datetimeoffset] $ObservedAtUtc = [datetimeoffset]::UtcNow,

    [ValidateRange(1, 365)]
    [int] $MaximumFutureDays = 90
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$maximumFileBytes = 64KB
$maximumExceptions = 100
$maximumScopesPerException = 16
$allowedRootProperties = @('schemaVersion', 'exceptions')
$allowedExceptionProperties = @(
    'scanner',
    'findingId',
    'owner',
    'reason',
    'expiresOn',
    'paths',
    'purls'
)
$scannerSections = [ordered] @{
    vulnerability = 'vulnerabilities'
    misconfiguration = 'misconfigurations'
    secret = 'secrets'
    license = 'licenses'
}

function Assert-ClosedObject {
    param(
        [Parameter(Mandatory = $true)]
        [object] $Value,

        [Parameter(Mandatory = $true)]
        [string[]] $AllowedProperties,

        [Parameter(Mandatory = $true)]
        [string] $Context
    )

    $actualProperties = @($Value.PSObject.Properties.Name)
    $unknownProperties = @(
        $actualProperties |
            Where-Object { $AllowedProperties -notcontains $_ }
    )
    if ($unknownProperties.Count -gt 0) {
        throw "$Context contains unsupported properties."
    }
}

function Assert-BoundedText {
    param(
        [AllowNull()]
        [object] $Value,

        [Parameter(Mandatory = $true)]
        [string] $Context,

        [Parameter(Mandatory = $true)]
        [int] $MinimumLength,

        [Parameter(Mandatory = $true)]
        [int] $MaximumLength
    )

    if ($Value -isnot [string] -or
        $Value.Length -lt $MinimumLength -or
        $Value.Length -gt $MaximumLength -or
        $Value -match '[\x00-\x1F\x7F]') {
        throw "$Context is not valid bounded text."
    }
}

function Get-OptionalStringArray {
    param(
        [Parameter(Mandatory = $true)]
        [object] $Value,

        [Parameter(Mandatory = $true)]
        [string] $PropertyName,

        [Parameter(Mandatory = $true)]
        [string] $Context,

        [Parameter(Mandatory = $true)]
        [int] $MaximumItemLength
    )

    if ($Value.PSObject.Properties.Name -notcontains $PropertyName) {
        return @()
    }

    $propertyValue = $Value.$PropertyName
    if ($null -eq $propertyValue -or $propertyValue -isnot [System.Array]) {
        throw "$Context.$PropertyName must be an array."
    }

    $items = @($propertyValue)
    if ($items.Count -gt $maximumScopesPerException) {
        throw "$Context.$PropertyName contains too many entries."
    }

    foreach ($item in $items) {
        Assert-BoundedText `
            -Value $item `
            -Context "$Context.$PropertyName entry" `
            -MinimumLength 1 `
            -MaximumLength $MaximumItemLength
    }

    if (@($items | Select-Object -Unique).Count -ne $items.Count) {
        throw "$Context.$PropertyName contains duplicate entries."
    }

    return $items
}

function ConvertTo-YamlString {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Value
    )

    return ($Value | ConvertTo-Json -Compress)
}

$resolvedInputPath = [System.IO.Path]::GetFullPath($InputPath)
if (-not [System.IO.File]::Exists($resolvedInputPath)) {
    throw "Security exception file '$InputPath' does not exist."
}

$fileInfo = [System.IO.FileInfo]::new($resolvedInputPath)
if ($fileInfo.Length -gt $maximumFileBytes) {
    throw 'Security exception file exceeds the 64 KiB limit.'
}

$documentText = [System.IO.File]::ReadAllText($resolvedInputPath)
if ([string]::IsNullOrWhiteSpace($documentText)) {
    throw 'Security exception file is empty.'
}

try {
    $document = $documentText | ConvertFrom-Json
}
catch {
    throw 'Security exception file is not valid JSON.'
}

if ($null -eq $document) {
    throw 'Security exception file must contain an object.'
}

Assert-ClosedObject `
    -Value $document `
    -AllowedProperties $allowedRootProperties `
    -Context 'Security exception document'

if ($document.PSObject.Properties.Name -notcontains 'schemaVersion' -or
    $document.schemaVersion -ne 1) {
    throw 'Security exception schemaVersion must be 1.'
}

if ($document.PSObject.Properties.Name -notcontains 'exceptions' -or
    $null -eq $document.exceptions -or
    $document.exceptions -isnot [System.Array]) {
    throw 'Security exception document must contain an exceptions array.'
}

$exceptions = @($document.exceptions)
if ($exceptions.Count -gt $maximumExceptions) {
    throw "Security exception document exceeds the $maximumExceptions-entry limit."
}

$observedDate = $ObservedAtUtc.UtcDateTime.Date
$latestExpiryDate = $observedDate.AddDays($MaximumFutureDays)
$normalizedExceptions = [System.Collections.Generic.List[object]]::new()
$identities = [System.Collections.Generic.HashSet[string]]::new(
    [System.StringComparer]::Ordinal)

for ($index = 0; $index -lt $exceptions.Count; $index++) {
    $exception = $exceptions[$index]
    $context = "Security exception $index"
    if ($null -eq $exception -or $exception -is [string]) {
        throw "$context must be an object."
    }

    Assert-ClosedObject `
        -Value $exception `
        -AllowedProperties $allowedExceptionProperties `
        -Context $context

    foreach ($requiredProperty in @(
        'scanner',
        'findingId',
        'owner',
        'reason',
        'expiresOn')) {
        if ($exception.PSObject.Properties.Name -notcontains $requiredProperty) {
            throw "$context is missing required metadata."
        }
    }

    Assert-BoundedText `
        -Value $exception.scanner `
        -Context "$context.scanner" `
        -MinimumLength 1 `
        -MaximumLength 32
    if (-not $scannerSections.Contains($exception.scanner)) {
        throw "$context.scanner is unsupported."
    }

    Assert-BoundedText `
        -Value $exception.findingId `
        -Context "$context.findingId" `
        -MinimumLength 1 `
        -MaximumLength 200
    Assert-BoundedText `
        -Value $exception.owner `
        -Context "$context.owner" `
        -MinimumLength 1 `
        -MaximumLength 100
    Assert-BoundedText `
        -Value $exception.reason `
        -Context "$context.reason" `
        -MinimumLength 10 `
        -MaximumLength 500
    Assert-BoundedText `
        -Value $exception.expiresOn `
        -Context "$context.expiresOn" `
        -MinimumLength 10 `
        -MaximumLength 10

    $expiryDate = [datetime]::MinValue
    if (-not [datetime]::TryParseExact(
        $exception.expiresOn,
        'yyyy-MM-dd',
        [System.Globalization.CultureInfo]::InvariantCulture,
        [System.Globalization.DateTimeStyles]::None,
        [ref] $expiryDate)) {
        throw "$context.expiresOn must use yyyy-MM-dd."
    }
    if ($expiryDate.Date -le $observedDate) {
        throw "$context is expired."
    }
    if ($expiryDate.Date -gt $latestExpiryDate) {
        throw "$context exceeds the $MaximumFutureDays-day expiry limit."
    }

    $paths = @(
        Get-OptionalStringArray `
            -Value $exception `
            -PropertyName 'paths' `
            -Context $context `
            -MaximumItemLength 256
    )
    foreach ($path in $paths) {
        if ([System.IO.Path]::IsPathRooted($path) -or
            $path -match '^[A-Za-z]:[\\/]' -or
            $path -match '^[\\/]' -or
            $path -match '(^|[\\/])\.\.([\\/]|$)') {
            throw "$context.paths must contain repository-relative paths."
        }
    }

    $purls = @(
        Get-OptionalStringArray `
            -Value $exception `
            -PropertyName 'purls' `
            -Context $context `
            -MaximumItemLength 512
    )
    if ($purls.Count -gt 0 -and $exception.scanner -ne 'vulnerability') {
        throw "$context.purls is valid only for vulnerability findings."
    }
    foreach ($purl in $purls) {
        if (-not $purl.StartsWith('pkg:', [System.StringComparison]::Ordinal)) {
            throw "$context.purls entries must be package URLs."
        }
    }

    if ($paths.Count -eq 0 -and $purls.Count -eq 0) {
        throw "$context must be narrowed by paths or purls."
    }

    $identitySeparator = [string] [char] 0x1F
    $identity = @(
        $exception.scanner,
        $exception.findingId,
        (@($paths | Sort-Object) -join $identitySeparator),
        (@($purls | Sort-Object) -join $identitySeparator)
    ) -join $identitySeparator
    if (-not $identities.Add($identity)) {
        throw "$context duplicates another exception scope."
    }

    $normalizedExceptions.Add([pscustomobject] @{
        Scanner = $exception.scanner
        FindingId = $exception.findingId
        Owner = $exception.owner
        Reason = $exception.reason
        ExpiresOn = $expiryDate.ToString(
            'yyyy-MM-dd',
            [System.Globalization.CultureInfo]::InvariantCulture)
        Paths = $paths
        Purls = $purls
    })
}

$yamlLines = [System.Collections.Generic.List[string]]::new()
if ($normalizedExceptions.Count -eq 0) {
    $yamlLines.Add('{}')
}
else {
    foreach ($scanner in $scannerSections.Keys) {
        $scannerExceptions = @(
            $normalizedExceptions |
                Where-Object { $_.Scanner -eq $scanner }
        )
        if ($scannerExceptions.Count -eq 0) {
            continue
        }

        $yamlLines.Add("$($scannerSections[$scanner]):")
        foreach ($exception in $scannerExceptions) {
            $yamlLines.Add(
                "  - id: $(ConvertTo-YamlString $exception.FindingId)")
            if ($exception.Paths.Count -gt 0) {
                $yamlLines.Add('    paths:')
                foreach ($path in $exception.Paths) {
                    $yamlLines.Add("      - $(ConvertTo-YamlString $path)")
                }
            }
            if ($exception.Purls.Count -gt 0) {
                $yamlLines.Add('    purls:')
                foreach ($purl in $exception.Purls) {
                    $yamlLines.Add("      - $(ConvertTo-YamlString $purl)")
                }
            }
            $yamlLines.Add("    expired_at: $($exception.ExpiresOn)")
            $statement = "owner=$($exception.Owner); reason=$($exception.Reason)"
            $yamlLines.Add(
                "    statement: $(ConvertTo-YamlString $statement)")
        }
    }
}

$resolvedOutputPath = [System.IO.Path]::GetFullPath($OutputPath)
$outputDirectory = [System.IO.Path]::GetDirectoryName($resolvedOutputPath)
if (-not [string]::IsNullOrWhiteSpace($outputDirectory)) {
    [System.IO.Directory]::CreateDirectory($outputDirectory) | Out-Null
}
[System.IO.File]::WriteAllLines(
    $resolvedOutputPath,
    $yamlLines,
    [System.Text.UTF8Encoding]::new($false))

Write-Output $normalizedExceptions.Count

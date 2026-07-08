Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$script:RepositoryRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path

function Get-GmaRepositoryRoot {
    return $script:RepositoryRoot
}

function Join-GmaPath {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Path
    )

    $normalizedPath = $Path
    $normalizedPath = $normalizedPath.Replace('\', [System.IO.Path]::DirectorySeparatorChar)
    $normalizedPath = $normalizedPath.Replace('/', [System.IO.Path]::DirectorySeparatorChar)

    return Join-Path $script:RepositoryRoot $normalizedPath
}

function Resolve-GmaDotNet {
    $candidates = @()
    $resolutionErrors = @()

    if (-not [string]::IsNullOrWhiteSpace($env:GMA_DOTNET)) {
        $candidates += $env:GMA_DOTNET
    }

    $candidates += 'dotnet'

    foreach ($candidate in $candidates) {
        try {
            Push-Location -LiteralPath $script:RepositoryRoot
            try {
                $version = & $candidate --version 2>$null
            }
            finally {
                Pop-Location
            }

            if ($LASTEXITCODE -ne 0) {
                $resolutionErrors += "Candidate '$candidate' exited with code $LASTEXITCODE."
                continue
            }

            if ($version -match '^10\.') {
                return $candidate
            }

            $resolutionErrors += "Candidate '$candidate' is version '$version'."
        }
        catch {
            if (-not [string]::IsNullOrWhiteSpace($env:GMA_DOTNET) -and $candidate -eq $env:GMA_DOTNET) {
                throw
            }

            $resolutionErrors += "Candidate '$candidate' failed: $($_.Exception.Message)"
        }
    }

    $details = if ($resolutionErrors.Count -gt 0) {
        " Tried candidates: $($resolutionErrors -join ' ')"
    }
    else {
        ''
    }

    throw "Could not resolve a .NET 10 SDK. Set GMA_DOTNET or install the .NET 10 SDK.$details"
}

function Invoke-GmaDotNet {
    param(
        [Parameter(Mandatory = $true)]
        [string[]] $Arguments,

        [string] $WorkingDirectory = $script:RepositoryRoot
    )

    $dotnet = Resolve-GmaDotNet
    Push-Location -LiteralPath $WorkingDirectory
    try {
        & $dotnet @Arguments
    }
    finally {
        Pop-Location
    }

    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}

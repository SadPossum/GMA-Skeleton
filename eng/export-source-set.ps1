param(
    [string] $OutputPath = 'artifacts/gma-source-set.json',
    [switch] $RequireClean
)

$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot 'common.ps1')

$repositoryRoot = Get-GmaRepositoryRoot
$resolvedOutputPath = if ([System.IO.Path]::IsPathRooted($OutputPath)) {
    [System.IO.Path]::GetFullPath($OutputPath)
}
else {
    [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot $OutputPath))
}

function Invoke-GmaGitText {
    param(
        [Parameter(Mandatory = $true)]
        [string] $WorkingDirectory,

        [Parameter(Mandatory = $true)]
        [string[]] $Arguments
    )

    $output = & git -C $WorkingDirectory @Arguments 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "git -C '$WorkingDirectory' $($Arguments -join ' ') failed: $($output -join [Environment]::NewLine)"
    }

    return ($output -join [Environment]::NewLine).Trim()
}

function Get-GmaRepositoryEntry {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Path,

        [Parameter(Mandatory = $true)]
        [string] $RelativePath,

        [string] $Url = ''
    )

    $status = Invoke-GmaGitText -WorkingDirectory $Path -Arguments @('status', '--porcelain=v1', '--untracked-files=all')
    $branch = Invoke-GmaGitText -WorkingDirectory $Path -Arguments @('branch', '--show-current')

    return [ordered]@{
        path = $RelativePath.Replace('\', '/')
        url = $Url
        commit = Invoke-GmaGitText -WorkingDirectory $Path -Arguments @('rev-parse', 'HEAD')
        branch = $branch
        dirty = -not [string]::IsNullOrWhiteSpace($status)
    }
}

$submoduleConfiguration = @(& git -C $repositoryRoot config --file .gitmodules --get-regexp '^submodule\..*\.path$' 2>&1)
if ($LASTEXITCODE -ne 0) {
    throw "Unable to read .gitmodules: $($submoduleConfiguration -join [Environment]::NewLine)"
}

$submodules = @($submoduleConfiguration |
    ForEach-Object {
        $key, $path = $_ -split '\s+', 2
        $name = $key.Substring('submodule.'.Length, $key.Length - 'submodule.'.Length - '.path'.Length)
        [pscustomobject]@{
            Name = $name
            Path = $path
        }
    } |
    Sort-Object Path)

$repositories = New-Object 'System.Collections.Generic.List[object]'
$repositories.Add((Get-GmaRepositoryEntry -Path $repositoryRoot -RelativePath '.'))

foreach ($submodule in $submodules) {
    $submodulePath = $submodule.Path
    $fullPath = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot $submodulePath))
    if (-not (Test-Path -LiteralPath $fullPath -PathType Container)) {
        throw "Submodule '$submodulePath' is not initialized. Run eng/gma-bootstrap.ps1 first."
    }

    $url = (Invoke-GmaGitText -WorkingDirectory $repositoryRoot -Arguments @(
        'config',
        '--file',
        '.gitmodules',
        '--get',
        "submodule.$($submodule.Name).url")).Trim()
    $repositories.Add((Get-GmaRepositoryEntry -Path $fullPath -RelativePath $submodulePath -Url $url))
}

$dirtyRepositories = @($repositories | Where-Object { $_.dirty })
if ($RequireClean -and $dirtyRepositories.Count -gt 0) {
    throw "A release source set must be clean. Dirty repositories: $($dirtyRepositories.path -join ', ')."
}

$globalJsonPath = Join-Path $repositoryRoot 'global.json'
$packagesPath = Join-Path $repositoryRoot 'Directory.Packages.props'
$manifest = [ordered]@{
    schemaVersion = 1
    generatedAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
    rootCommit = $repositories[0].commit
    sdkVersion = (Get-Content -LiteralPath $globalJsonPath -Raw | ConvertFrom-Json).sdk.version
    centralPackagesSha256 = (Get-FileHash -LiteralPath $packagesPath -Algorithm SHA256).Hash.ToLowerInvariant()
    repositories = $repositories.ToArray()
}

$outputDirectory = Split-Path -Parent $resolvedOutputPath
if (-not [string]::IsNullOrWhiteSpace($outputDirectory)) {
    New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
}

[System.IO.File]::WriteAllText(
    $resolvedOutputPath,
    ($manifest | ConvertTo-Json -Depth 6) + [Environment]::NewLine,
    [System.Text.UTF8Encoding]::new($false))
Write-Output "sourceSet=$resolvedOutputPath"

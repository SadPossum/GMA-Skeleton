param(
    [string] $Branch = 'dev'
)

. (Join-Path $PSScriptRoot 'common.ps1')

function Invoke-GmaGitOutput {
    param(
        [Parameter(Mandatory = $true)]
        [string[]] $Arguments,

        [string] $WorkingDirectory = (Get-GmaRepositoryRoot)
    )

    Push-Location -LiteralPath $WorkingDirectory
    try {
        $output = & git @Arguments 2>&1
    }
    finally {
        Pop-Location
    }

    if ($LASTEXITCODE -ne 0) {
        throw "git $($Arguments -join ' ') failed in '$WorkingDirectory': $($output -join [Environment]::NewLine)"
    }

    return @($output)
}

function Test-GmaGitSuccess {
    param(
        [Parameter(Mandatory = $true)]
        [string[]] $Arguments,

        [string] $WorkingDirectory = (Get-GmaRepositoryRoot)
    )

    Push-Location -LiteralPath $WorkingDirectory
    try {
        & git @Arguments *> $null
        return $LASTEXITCODE -eq 0
    }
    finally {
        Pop-Location
    }
}

$repositoryRoot = Get-GmaRepositoryRoot
$gitmodulesPath = Join-GmaPath '.gitmodules'

if (-not (Test-Path -LiteralPath $gitmodulesPath -PathType Leaf)) {
    Write-Host 'No .gitmodules file found. Nothing to check.'
    return
}

$pathRows = @(Invoke-GmaGitOutput -Arguments @(
    'config',
    '--file',
    $gitmodulesPath,
    '--get-regexp',
    '^submodule\..*\.path$'
))

$errors = [System.Collections.Generic.List[string]]::new()
$results = [System.Collections.Generic.List[object]]::new()

foreach ($pathRow in $pathRows) {
    if ($pathRow -notmatch '^submodule\.(?<name>.+)\.path\s+(?<path>.+)$') {
        $errors.Add("Could not parse .gitmodules path row '$pathRow'.")
        continue
    }

    $name = $Matches['name']
    $relativePath = $Matches['path']
    $submodulePath = Join-GmaPath $relativePath
    $configuredBranch = @(Invoke-GmaGitOutput -Arguments @(
            'config',
            '--file',
            $gitmodulesPath,
            '--get',
            "submodule.$name.branch"
        ))[0]

    if ([string]::IsNullOrWhiteSpace($configuredBranch)) {
        $errors.Add("Submodule '$relativePath' does not declare branch '$Branch' in .gitmodules.")
        continue
    }

    if (-not [string]::Equals($configuredBranch, $Branch, [System.StringComparison]::Ordinal)) {
        $errors.Add("Submodule '$relativePath' tracks '$configuredBranch' in .gitmodules, expected '$Branch'.")
    }

    if (-not (Test-Path -LiteralPath $submodulePath -PathType Container)) {
        $errors.Add("Submodule checkout missing at '$relativePath'. Run eng/gma-update.ps1 -Init first.")
        continue
    }

    if (-not (Test-GmaGitSuccess -Arguments @('diff', '--quiet', '--', $relativePath))) {
        $errors.Add("Submodule gitlink '$relativePath' has unstaged changes in the skeleton. Commit the updated pointer or reset the submodule.")
    }

    if (-not (Test-GmaGitSuccess -Arguments @('diff', '--cached', '--quiet', '--', $relativePath))) {
        $errors.Add("Submodule gitlink '$relativePath' has staged but uncommitted changes in the skeleton.")
    }

    $dirtyRows = @(Invoke-GmaGitOutput -Arguments @('status', '--porcelain') -WorkingDirectory $submodulePath)
    if ($dirtyRows.Count -gt 0) {
        $errors.Add("Submodule '$relativePath' has uncommitted local changes.")
    }

    $currentCommit = @(Invoke-GmaGitOutput -Arguments @('rev-parse', 'HEAD') -WorkingDirectory $submodulePath)[0].Trim()
    $recordedCommit = @(Invoke-GmaGitOutput -Arguments @('rev-parse', "HEAD:$relativePath"))[0].Trim()
    $remoteRows = @(Invoke-GmaGitOutput -Arguments @('ls-remote', 'origin', "refs/heads/$Branch") -WorkingDirectory $submodulePath)
    $remoteRow = @($remoteRows | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })

    if ($remoteRow.Count -ne 1 -or $remoteRow[0] -notmatch '^(?<sha>[0-9a-fA-F]{40})\s+refs/heads/') {
        $errors.Add("Could not resolve origin/$Branch for submodule '$relativePath'.")
        continue
    }

    $remoteCommit = $Matches['sha'].ToLowerInvariant()
    $currentCommit = $currentCommit.ToLowerInvariant()
    $recordedCommit = $recordedCommit.ToLowerInvariant()

    if (-not [string]::Equals($currentCommit, $remoteCommit, [System.StringComparison]::Ordinal)) {
        $errors.Add("Submodule '$relativePath' is at $($currentCommit.Substring(0, 7)), but origin/$Branch is $($remoteCommit.Substring(0, 7)). Run eng/gma-update.ps1 -Remote, validate, then commit the pointer.")
    }

    if (-not [string]::Equals($recordedCommit, $remoteCommit, [System.StringComparison]::Ordinal)) {
        $errors.Add("Skeleton records '$relativePath' at $($recordedCommit.Substring(0, 7)), but origin/$Branch is $($remoteCommit.Substring(0, 7)). Commit the latest submodule pointer.")
    }

    $results.Add([pscustomobject]@{
            Path = $relativePath
            Branch = $configuredBranch
            Current = $currentCommit.Substring(0, 7)
            Remote = $remoteCommit.Substring(0, 7)
            Recorded = $recordedCommit.Substring(0, 7)
        })
}

if ($results.Count -gt 0) {
    $results | Format-Table -AutoSize
}

if ($errors.Count -gt 0) {
    throw "GMA submodule dev-head check failed:`n - $($errors -join "`n - ")"
}

Write-Host "All GMA submodule pointers match origin/$Branch."

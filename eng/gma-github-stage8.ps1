[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [string] $Owner = 'SadPossum',

    [ValidateSet('private', 'public', 'internal')]
    [string] $Visibility = 'private',

    [string] $RemoteHostAlias = 'github.com-private',

    [string] $StageRoot = '.agents\stage8d\2',

    [string] $DefaultBranch = 'dev',

    [string] $GitUserName = 'SadPossum',

    [string] $GitUserEmail = '258739@bk.ru',

    [string[]] $RequiredStatusChecks = @(),

    [switch] $InitializeCandidates,

    [switch] $CreateRepositories,

    [switch] $PushCandidates,

    [switch] $ConfigureRepositories,

    [switch] $ProtectBranches,

    [switch] $AllowDirtyCandidates
)

. (Join-Path $PSScriptRoot 'common.ps1')

function Resolve-GmaLocalPath {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Path
    )

    if ([System.IO.Path]::IsPathRooted($Path)) {
        return $Path
    }

    return Join-GmaPath $Path
}

function Get-GmaStage8RepositoryPlans {
    $stageRootPath = Resolve-GmaLocalPath $StageRoot

    return @(
        [pscustomobject] @{
            Name = 'gma-framework'
            LocalPath = Join-Path $stageRootPath 'repos\gma-framework'
            Description = 'Reusable GMA framework packages for modular monolith applications.'
            Topics = @('gma', 'dotnet', 'framework', 'modular-monolith')
        },
        [pscustomobject] @{
            Name = 'gma-module-administration'
            LocalPath = Join-Path $stageRootPath 'repos\gma-module-administration'
            Description = 'Reusable GMA Administration module with RBAC, audit, CLI, and admin API surfaces.'
            Topics = @('gma', 'dotnet', 'module', 'administration')
        },
        [pscustomobject] @{
            Name = 'gma-module-auth'
            LocalPath = Join-Path $stageRootPath 'repos\gma-module-auth'
            Description = 'Reusable GMA Auth module for first-party member authentication and administration.'
            Topics = @('gma', 'dotnet', 'module', 'auth')
        },
        [pscustomobject] @{
            Name = 'gma-module-files'
            LocalPath = Join-Path $stageRootPath 'repos\gma-module-files'
            Description = 'Reusable GMA Files module front door for optional file-management workflows.'
            Topics = @('gma', 'dotnet', 'module', 'files')
        },
        [pscustomobject] @{
            Name = 'gma-module-notifications'
            LocalPath = Join-Path $stageRootPath 'repos\gma-module-notifications'
            Description = 'Reusable GMA Notifications module for persisted notifications and streaming front doors.'
            Topics = @('gma', 'dotnet', 'module', 'notifications')
        },
        [pscustomobject] @{
            Name = 'gma-module-task-runtime'
            LocalPath = Join-Path $stageRootPath 'repos\gma-module-task-runtime'
            Description = 'Reusable GMA TaskRuntime module for persisted task runs and operator control.'
            Topics = @('gma', 'dotnet', 'module', 'tasks')
        },
        [pscustomobject] @{
            Name = 'gma-module-tenancy'
            LocalPath = Join-Path $stageRootPath 'repos\gma-module-tenancy'
            Description = 'Reusable GMA Tenancy module for optional tenant front doors and contracts.'
            Topics = @('gma', 'dotnet', 'module', 'tenancy')
        },
        [pscustomobject] @{
            Name = 'gma-skeleton'
            LocalPath = Join-Path $stageRootPath 'skeleton'
            Description = 'GMA skeleton and composition repository for source-first modular monolith applications.'
            Topics = @('gma', 'dotnet', 'template', 'modular-monolith')
        }
    )
}

function Assert-GmaGithubCliReady {
    if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
        throw 'GitHub CLI is required for Stage 8 repository setup. Install gh, run gh auth login, then rerun this script.'
    }

    & gh auth status
    if ($LASTEXITCODE -ne 0) {
        throw 'GitHub CLI is installed but not authenticated. Run gh auth login or set GH_TOKEN/GITHUB_TOKEN.'
    }
}

function Invoke-GmaGithubCli {
    param(
        [Parameter(Mandatory = $true)]
        [string[]] $Arguments,

        [Parameter(Mandatory = $true)]
        [string] $Target,

        [Parameter(Mandatory = $true)]
        [string] $Action
    )

    if ($PSCmdlet.ShouldProcess($Target, $Action)) {
        & gh @Arguments
        if ($LASTEXITCODE -ne 0) {
            throw "GitHub CLI command failed while running '$Action' for '$Target'."
        }
    }
}

function Invoke-GmaQuietNativeCommand {
    param(
        [Parameter(Mandatory = $true)]
        [string] $FilePath,

        [Parameter(Mandatory = $true)]
        [string[]] $Arguments
    )

    $previousErrorActionPreference = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        & $FilePath @Arguments *> $null
        return $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $previousErrorActionPreference
    }
}

function Test-GmaCandidateOwnsGitRepository {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Path
    )

    if (-not (Test-Path -LiteralPath $Path -PathType Container)) {
        return $false
    }

    $previousErrorActionPreference = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        $repositoryRoot = git -C $Path rev-parse --show-toplevel 2>$null
        $exitCode = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $previousErrorActionPreference
    }

    if ($exitCode -ne 0 -or [string]::IsNullOrWhiteSpace($repositoryRoot)) {
        return $false
    }

    $expectedRoot = [System.IO.Path]::GetFullPath((Resolve-Path -LiteralPath $Path).Path).TrimEnd('\', '/')
    $actualRoot = [System.IO.Path]::GetFullPath($repositoryRoot).TrimEnd('\', '/')

    return [string]::Equals($expectedRoot, $actualRoot, [System.StringComparison]::OrdinalIgnoreCase)
}

function Assert-GmaCandidateOwnsGitRepository {
    param(
        [Parameter(Mandatory = $true)]
        [object] $RepositoryPlan
    )

    if (-not (Test-GmaCandidateOwnsGitRepository $RepositoryPlan.LocalPath)) {
        throw "Candidate path '$($RepositoryPlan.LocalPath)' is not a standalone Git repository. Run this script with -InitializeCandidates after generating Stage 8 candidates."
    }
}

function Ensure-GmaSkeletonCandidateMountIgnores {
    param(
        [Parameter(Mandatory = $true)]
        [object] $RepositoryPlan
    )

    if ($RepositoryPlan.Name -ne 'gma-skeleton') {
        return
    }

    $gmaPath = Join-Path $RepositoryPlan.LocalPath 'gma'
    $moduleMountPath = Join-Path $gmaPath 'modules'
    New-Item -ItemType Directory -Force -Path $moduleMountPath | Out-Null
    New-Item -ItemType File -Force -Path (Join-Path $gmaPath '.gitkeep') | Out-Null
    New-Item -ItemType File -Force -Path (Join-Path $moduleMountPath '.gitkeep') | Out-Null

    $gitIgnorePath = Join-Path $RepositoryPlan.LocalPath '.gitignore'
    $gitIgnore = if (Test-Path -LiteralPath $gitIgnorePath) {
        Get-Content -LiteralPath $gitIgnorePath
    }
    else {
        @()
    }

    if ($gitIgnore -contains '# GMA source repository mount points') {
        return
    }

    Add-Content -LiteralPath $gitIgnorePath -Encoding utf8 -Value @(
        '',
        '# GMA source repository mount points',
        '/gma/framework/',
        '/gma/modules/*/'
    )
}

function Initialize-GmaCandidateRepository {
    param(
        [Parameter(Mandatory = $true)]
        [object] $RepositoryPlan
    )

    if (-not (Test-Path -LiteralPath $RepositoryPlan.LocalPath -PathType Container)) {
        throw "Candidate repository path is missing: $($RepositoryPlan.LocalPath)"
    }

    if ($WhatIfPreference) {
        $null = $PSCmdlet.ShouldProcess(
            $RepositoryPlan.LocalPath,
            "Initialize local candidate repository, commit contents as $GitUserName <$GitUserEmail>, and ensure main/dev branches")
        return
    }

    if (-not (Test-GmaCandidateOwnsGitRepository $RepositoryPlan.LocalPath)) {
        git -C $RepositoryPlan.LocalPath init --quiet
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to initialize Git repository at '$($RepositoryPlan.LocalPath)'."
        }
    }

    Ensure-GmaSkeletonCandidateMountIgnores $RepositoryPlan

    git -C $RepositoryPlan.LocalPath config user.name $GitUserName
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to set local Git user.name for '$($RepositoryPlan.LocalPath)'."
    }

    git -C $RepositoryPlan.LocalPath config user.email $GitUserEmail
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to set local Git user.email for '$($RepositoryPlan.LocalPath)'."
    }

    git -C $RepositoryPlan.LocalPath config core.autocrlf false
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to set local Git core.autocrlf for '$($RepositoryPlan.LocalPath)'."
    }

    git -C $RepositoryPlan.LocalPath config core.longpaths true
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to set local Git core.longpaths for '$($RepositoryPlan.LocalPath)'."
    }

    $hasCommit = (Invoke-GmaQuietNativeCommand `
        -FilePath 'git' `
        -Arguments @('-C', $RepositoryPlan.LocalPath, 'rev-parse', '--verify', 'HEAD')) -eq 0
    $status = git -C $RepositoryPlan.LocalPath status --porcelain
    if (-not $hasCommit -or $status) {
        git -C $RepositoryPlan.LocalPath add -A
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to stage candidate repository '$($RepositoryPlan.LocalPath)'."
        }

        $cachedDiffExitCode = Invoke-GmaQuietNativeCommand `
            -FilePath 'git' `
            -Arguments @('-C', $RepositoryPlan.LocalPath, 'diff', '--cached', '--quiet')
        if ($cachedDiffExitCode -eq 1) {
            git -C $RepositoryPlan.LocalPath commit --quiet -m "chore: initialize $($RepositoryPlan.Name) candidate"
            if ($LASTEXITCODE -ne 0) {
                throw "Failed to commit candidate repository '$($RepositoryPlan.LocalPath)'."
            }
        }
        elseif ($cachedDiffExitCode -ne 0) {
            throw "Failed to inspect staged candidate changes for '$($RepositoryPlan.LocalPath)'."
        }
        elseif (-not $hasCommit) {
            throw "Candidate repository '$($RepositoryPlan.LocalPath)' has no commit and no staged content."
        }
    }

    $currentBranch = git -C $RepositoryPlan.LocalPath branch --show-current
    $hasMain = (Invoke-GmaQuietNativeCommand `
        -FilePath 'git' `
        -Arguments @('-C', $RepositoryPlan.LocalPath, 'show-ref', '--verify', '--quiet', 'refs/heads/main')) -eq 0
    if (-not $hasMain) {
        if ($currentBranch -and $currentBranch -ne 'dev') {
            git -C $RepositoryPlan.LocalPath branch -M main
        }
        else {
            git -C $RepositoryPlan.LocalPath branch main
        }

        if ($LASTEXITCODE -ne 0) {
            throw "Failed to ensure main branch for '$($RepositoryPlan.LocalPath)'."
        }
    }

    $hasDev = (Invoke-GmaQuietNativeCommand `
        -FilePath 'git' `
        -Arguments @('-C', $RepositoryPlan.LocalPath, 'show-ref', '--verify', '--quiet', 'refs/heads/dev')) -eq 0
    if (-not $hasDev) {
        git -C $RepositoryPlan.LocalPath branch dev main
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to ensure dev branch for '$($RepositoryPlan.LocalPath)'."
        }
    }

    $checkoutExitCode = Invoke-GmaQuietNativeCommand `
        -FilePath 'git' `
        -Arguments @('-C', $RepositoryPlan.LocalPath, 'checkout', 'dev')
    if ($checkoutExitCode -ne 0) {
        throw "Failed to check out dev branch for '$($RepositoryPlan.LocalPath)'."
    }
}

function Test-GmaGithubRepositoryExists {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Repository
    )

    return (Invoke-GmaQuietNativeCommand `
        -FilePath 'gh' `
        -Arguments @('repo', 'view', $Repository, '--json', 'name', '--jq', '.name')) -eq 0
}

function Ensure-GmaGithubRepository {
    param(
        [Parameter(Mandatory = $true)]
        [object] $RepositoryPlan
    )

    $fullName = "$Owner/$($RepositoryPlan.Name)"
    if (-not $WhatIfPreference -and (Test-GmaGithubRepositoryExists $fullName)) {
        Write-Host "Repository exists: $fullName"
        return
    }

    $arguments = @(
        'repo',
        'create',
        $fullName,
        "--$Visibility",
        '--description',
        $RepositoryPlan.Description,
        '--disable-wiki'
    )

    Invoke-GmaGithubCli `
        -Arguments $arguments `
        -Target $fullName `
        -Action "Create $Visibility GitHub repository"
}

function Configure-GmaGithubRepository {
    param(
        [Parameter(Mandatory = $true)]
        [object] $RepositoryPlan
    )

    $fullName = "$Owner/$($RepositoryPlan.Name)"
    $arguments = @(
        'repo',
        'edit',
        $fullName,
        '--description',
        $RepositoryPlan.Description,
        '--default-branch',
        $DefaultBranch,
        '--delete-branch-on-merge',
        '--allow-update-branch',
        '--enable-squash-merge',
        '--enable-merge-commit=false',
        '--enable-rebase-merge=false',
        '--enable-wiki=false'
    )

    foreach ($topic in $RepositoryPlan.Topics) {
        $arguments += '--add-topic'
        $arguments += $topic
    }

    Invoke-GmaGithubCli `
        -Arguments $arguments `
        -Target $fullName `
        -Action "Configure repository settings"
}

function Protect-GmaGithubBranch {
    param(
        [Parameter(Mandatory = $true)]
        [object] $RepositoryPlan
    )

    $fullName = "$Owner/$($RepositoryPlan.Name)"
    $requiredStatusChecks = if ($RequiredStatusChecks.Count -gt 0) {
        @{
            strict = $true
            contexts = $RequiredStatusChecks
        }
    }
    else {
        $null
    }

    $payload = @{
        required_status_checks = $requiredStatusChecks
        enforce_admins = $true
        required_pull_request_reviews = @{
            dismiss_stale_reviews = $true
            require_code_owner_reviews = $false
            required_approving_review_count = 1
            require_last_push_approval = $true
        }
        restrictions = $null
        required_linear_history = $true
        allow_force_pushes = $false
        allow_deletions = $false
        block_creations = $false
        required_conversation_resolution = $true
    }

    if ($WhatIfPreference) {
        Invoke-GmaGithubCli `
            -Arguments @(
                'api',
                '--method',
                'PUT',
                "repos/$fullName/branches/$DefaultBranch/protection",
                '--input',
                '<generated-branch-protection-payload>'
            ) `
            -Target "$fullName/$DefaultBranch" `
            -Action 'Configure branch protection'
        return
    }

    $payloadPath = New-TemporaryFile
    try {
        $payload | ConvertTo-Json -Depth 10 -Compress | Set-Content -LiteralPath $payloadPath.FullName -NoNewline -Encoding utf8
        Invoke-GmaGithubCli `
            -Arguments @(
                'api',
                '--method',
                'PUT',
                "repos/$fullName/branches/$DefaultBranch/protection",
                '--input',
                $payloadPath.FullName
            ) `
            -Target "$fullName/$DefaultBranch" `
            -Action 'Configure branch protection'
    }
    finally {
        Remove-Item -LiteralPath $payloadPath.FullName -Force -ErrorAction SilentlyContinue
    }
}

function Push-GmaCandidateRepository {
    param(
        [Parameter(Mandatory = $true)]
        [object] $RepositoryPlan
    )

    $fullName = "$Owner/$($RepositoryPlan.Name)"
    $sshUrl = "git@${RemoteHostAlias}:$Owner/$($RepositoryPlan.Name).git"
    if (-not (Test-Path -LiteralPath $RepositoryPlan.LocalPath -PathType Container)) {
        throw "Candidate repository path is missing: $($RepositoryPlan.LocalPath)"
    }

    Assert-GmaCandidateOwnsGitRepository $RepositoryPlan

    $status = git -C $RepositoryPlan.LocalPath status --short
    if ($status -and -not $AllowDirtyCandidates) {
        throw "Candidate repository has uncommitted changes: $($RepositoryPlan.LocalPath). Commit, clean, or rerun with -AllowDirtyCandidates."
    }

    $remoteNames = git -C $RepositoryPlan.LocalPath remote
    if ($remoteNames -contains 'origin') {
        if ($PSCmdlet.ShouldProcess($RepositoryPlan.LocalPath, "Set origin to $sshUrl")) {
            git -C $RepositoryPlan.LocalPath remote set-url origin $sshUrl
            if ($LASTEXITCODE -ne 0) {
                throw "Failed to set origin for $($RepositoryPlan.LocalPath)."
            }
        }
    }
    elseif ($PSCmdlet.ShouldProcess($RepositoryPlan.LocalPath, "Add origin $sshUrl")) {
        git -C $RepositoryPlan.LocalPath remote add origin $sshUrl
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to add origin for $($RepositoryPlan.LocalPath)."
        }
    }

    foreach ($branch in @('main', 'dev')) {
        $hasBranch = (Invoke-GmaQuietNativeCommand `
            -FilePath 'git' `
            -Arguments @('-C', $RepositoryPlan.LocalPath, 'show-ref', '--verify', '--quiet', "refs/heads/$branch")) -eq 0
        if (-not $hasBranch) {
            throw "Candidate repository '$($RepositoryPlan.LocalPath)' does not contain required branch '$branch'."
        }
    }

    if ($PSCmdlet.ShouldProcess($fullName, 'Push main and dev branches')) {
        git -C $RepositoryPlan.LocalPath push -u origin main dev
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to push candidate repository '$fullName'."
        }
    }
}

$repositoryPlans = Get-GmaStage8RepositoryPlans
$hasAction = $InitializeCandidates -or $CreateRepositories -or $PushCandidates -or $ConfigureRepositories -or $ProtectBranches

if (-not $hasAction) {
    Write-Host 'Stage 8 repository plan:'
    foreach ($repositoryPlan in $repositoryPlans) {
        Write-Host "- $Owner/$($repositoryPlan.Name) <= $($repositoryPlan.LocalPath)"
    }

    Write-Host ''
    Write-Host 'No changes were requested. Add -InitializeCandidates, -CreateRepositories, -PushCandidates, -ConfigureRepositories, or -ProtectBranches.'
    return
}

$requiresGithubCli = $CreateRepositories -or $ConfigureRepositories -or $ProtectBranches
if ($requiresGithubCli -and -not $WhatIfPreference) {
    Assert-GmaGithubCliReady
}

foreach ($repositoryPlan in $repositoryPlans) {
    if ($InitializeCandidates) {
        Initialize-GmaCandidateRepository $repositoryPlan
    }

    if ($CreateRepositories) {
        Ensure-GmaGithubRepository $repositoryPlan
    }

    if ($PushCandidates) {
        Push-GmaCandidateRepository $repositoryPlan
    }

    if ($ConfigureRepositories) {
        Configure-GmaGithubRepository $repositoryPlan
    }

    if ($ProtectBranches) {
        Protect-GmaGithubBranch $repositoryPlan
    }
}

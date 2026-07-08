# Setup

## Prerequisites

- Windows PowerShell.
- .NET 10 SDK. The repo pins SDK `10.0.300` in `global.json`.
- Docker Desktop for SQL Server, PostgreSQL, NATS, Aspire resources, and Docker-backed integration tests.
- An editor that understands `.slnx` files or plain C# projects.

The scripts resolve `dotnet` in this order:

1. `$env:GMA_DOTNET`
2. `dotnet` on `PATH`

The resolved SDK must be .NET 10. If your .NET 10 SDK is not on `PATH`, set `GMA_DOTNET` to the full `dotnet.exe` path before running `eng/*.ps1`.

Base `appsettings.json` files contain configuration shape and non-secret defaults only. Local disposable connection strings, JWT signing material, and refresh-token peppers live in `appsettings.Development.json`. Production and shared environments must provide `ConnectionStrings:*`, `Auth:Jwt:SigningKey`, and `Auth:RefreshTokens:Pepper` through environment variables, user secrets, a vault, or another secret provider.

## First Run

```powershell
.\eng\restore.ps1
.\eng\build.ps1 -NoRestore
.\eng\test-fast.ps1 -NoBuild
```

`restore.ps1` restores both local tools and NuGet packages.

The root `.github\workflows\validate.yml` runs the same non-Docker path on `main`, `dev`, pull requests, and manual dispatch: restore, build with `-m:1`, then `test-fast.ps1 -NoBuild`.

## Run With Aspire

```powershell
.\eng\run-aspire.ps1
```

Aspire starts:

- `Host.Api`
- SQL Server
- PostgreSQL
- NATS with JetStream
- service defaults and local observability wiring

`Host.AdminApi` is intentionally not part of the normal Aspire graph. To include it for local administration API work, set:

```text
AppHost__AdminApi__Enabled=true
```

The admin API still stays a separate composition root; the flag only adds it to the local Aspire resource graph.

`Host.Worker` is also disabled by default. To include the optional background worker and demonstrate separated publishing for the default Auth module, set:

```text
AppHost__Worker__Enabled=true
```

When the worker flag is enabled, AppHost sets HTTP hosts to `NatsJetStream:Enabled=false` and starts `Host.Worker` with `Worker:Modules:Auth=true` and `NatsJetStream:Enabled=true`. Consumers and task workers remain disabled until you also enable their settings and module groups.

## Run API Only

```powershell
.\eng\run-api.ps1
```

The default launch profile is `https`.

## Run Worker Only

```powershell
.\eng\run-worker.ps1
```

The worker starts with all background loops disabled unless configuration enables publishing, consumers, or task workers. Set `Worker__Modules__*` for the modules this process should compose.

## Useful Local URLs

- API HTTPS: `https://localhost:7293`
- API HTTP: `http://localhost:5054`
- Admin API HTTPS: `https://localhost:50789`
- Admin API HTTP: `http://localhost:50790`
- Swagger: `/swagger` in Development, provided by the shared OpenAPI adapter
- Health: `/health`
- Liveness: `/alive`

## HTTP Requests

Use [../../requests/auth.http](../../requests/auth.http) for Auth API examples.

Set these variables in the request file:

- `host`
- `tenant`
- `username`
- `password`
- `accessToken`
- `refreshToken`

## Configuration Keys

Core runtime keys:

- `ApplicationIdentity:DisplayName`
- `ApplicationIdentity:Namespace`
- `Persistence:Provider`
- `ConnectionStrings:SqlServer`
- `ConnectionStrings:PostgreSql`
- `ConnectionStrings:nats`
- `ConnectionStrings:redis` when Redis caching is enabled
- `Caching:Enabled`
- `Caching:Provider`
- `Caching:DefaultDistributedExpiration`
- `Caching:DefaultLocalExpiration`
- `Caching:MaximumPayloadBytes`
- `Caching:MaximumKeyLength`
- `Caching:KeyPrefix` optional physical override; defaults to `ApplicationIdentity:Namespace`
- `Caching:Redis:ConnectionName`
- `Caching:Redis:InstanceName`
- `Tenancy:Enabled`
- `Tenancy:HeaderName`
- `Tenancy:LocalDefaultTenantId`
- `Outbox:BatchSize`
- `Outbox:PollIntervalMilliseconds`
- `Outbox:LockDurationMilliseconds`
- `Outbox:MaxAttempts`
- `NatsJetStream:Enabled`
- `NatsJetStream:StreamName` optional physical override; defaults from `ApplicationIdentity:Namespace`
- `ConnectionStrings:nats` when JetStream publishing is enabled
- `NatsConsumers:Enabled`
- `NatsConsumers:DurablePrefix` optional physical override; defaults to `ApplicationIdentity:Namespace`
- `NatsConsumers:FetchBatchSize`
- `NatsConsumers:PollInterval`
- `NatsConsumers:AckWait`
- `NatsConsumers:MaxDeliver`
- `NatsConsumers:HandlerTimeout`
- `NatsConsumers:NakDelay`
- `Worker:Modules:Auth`
- `Worker:Modules:Catalog`
- `Worker:Modules:Ordering`
- `Worker:Modules:TaskRuntime`
- `Worker:Modules:TaskSamples`
- `Tasks:Worker:Enabled`
- `Tasks:Worker:WorkerGroups`
- `Tasks:Worker:BatchSize`
- `Tasks:Worker:MaxConcurrency`
- `Tasks:Worker:PollInterval`
- `Tasks:Worker:LeaseDuration`
- `Tasks:Worker:HandlerTimeout`
- `Tasks:Worker:RetryBaseDelay`
- `Tasks:Worker:RetryMaxDelay`
- `Tasks:Worker:TimeoutScannerEnabled`
- `Tasks:Worker:MetricsSamplerEnabled`
- `Observability:Prometheus:Enabled`
- `Observability:Prometheus:EndpointPath`
- `Observability:Otlp:Enabled`
- `Observability:Otlp:Endpoint`
- `Observability:Otlp:ExportMetrics`
- `Observability:Otlp:ExportTraces`
- `Observability:Otlp:ExportLogs`
- `Auth:RefreshTokenLifetimeDays`
- `Auth:RefreshTokens:Pepper`
- `Auth:Jwt:Issuer` optional override; defaults to `ApplicationIdentity:DisplayName`
- `Auth:Jwt:Audience` optional override; defaults to `ApplicationIdentity:DisplayName`
- `Auth:Jwt:SigningKey`
- `Auth:Jwt:AccessTokenLifetimeMinutes`
- `Administration:Bootstrap:AllowWhenAssignmentsExist`
- `Administration:Bootstrap:OwnerRoleName`
- `Administration:Api:ActorIdClaim`
- `Administration:Api:TenantIdClaim`
- `Administration:Api:RequireTenantClaimMatch`
- `Administration:Api:AllowGeneratedPasswordResponses`

Development enables the Prometheus scrape endpoint at `/metrics`. OTLP export remains disabled until explicitly configured.

## Docker Tests

Fast tests exclude Docker:

```powershell
.\eng\test-fast.ps1 -NoBuild
```

Docker tests require Docker and set `GMA_REQUIRE_DOCKER_TESTS=true`:

```powershell
.\eng\test-docker.ps1 -NoBuild
```

The normal full test command may skip Docker tests locally when Docker is unavailable:

```powershell
dotnet test GenericModularApi.slnx --no-build --logger "console;verbosity=minimal"
```

## Focused Solution Entrypoints

Use `GenericModularApi.slnx` for the full skeleton/composition repository. Use the focused `.slnx` files when working inside a future source repository boundary:

```text
Gma.Framework.slnx
Gma.Modules.Administration.slnx
Gma.Modules.Auth.slnx
Gma.Modules.Files.slnx
Gma.Modules.Notifications.slnx
Gma.Modules.TaskRuntime.slnx
Gma.Modules.Tenancy.slnx
```

Those root focused solutions are monorepo staging entrypoints: their paths start at `src/Framework` or `src/Modules/<Module>` so the skeleton can validate every package from one checkout. Each package root also has a package-local solution with the same contents but local paths:

```text
src/Framework/Gma.Framework.slnx
src/Modules/Auth/Gma.Modules.Auth.slnx
src/Modules/Notifications/Gma.Modules.Notifications.slnx
```

Open the package-local solution when you want to work as if the framework or module were already its own repository. The source-package checker compares each root focused solution with its package-local mirror so they stay aligned instead of becoming another manual list to maintain.

Focused framework and reusable-module tests are colocated with their current monorepo staging roots, for example `src/Framework/tests/Gma.Framework.Tests` and `src/Modules/Auth/tests/Gma.Modules.Auth.Tests`. After extraction, package-owned tests should move to the independent repository root `tests/` folder. Cross-module architecture and integration tests stay under the skeleton repository-level `tests/` folder.

Catalog, Ordering, and TaskSamples are skeleton-owned examples, not reusable GMA source packages. They do not have standalone `.slnx` entrypoints; validate them through `GenericModularApi.slnx` and the skeleton-level test suites.

Focused module `.slnx` files list only module-owned projects, focused module tests, and the module-owned docs index. They do not list framework projects as solution entries; framework dependencies resolve through `GmaFrameworkRoot` project references so the same module solution can build in the monorepo, an extracted module repo, or a source-submodule application checkout. In an extracted repository, the `.slnx`, `docs/`, `tests/`, `eng/`, and `src/` folders should live at the repository root rather than under preserved monorepo parent folders.

To dry-run the source-package boundary in the current monorepo:

```powershell
.\eng\check-source-packages.ps1 -SkipRestore
```

The script verifies focused `.slnx` ownership, stale root docs/tests, package-local docs/tests/scripts, and builds every focused package solution. Omit `-SkipRestore` when package references changed.

## Source-First GMA Development

The current repository still contains the framework and reusable modules directly, but project references are ready for future source submodule composition. Framework references go through `GmaFrameworkRoot`, with checked-in defaults in `Directory.Build.props`.

To create a local override file:

```powershell
.\eng\gma-bootstrap.ps1
```

This copies `Gma.SourceRoots.props.example` to ignored `Gma.SourceRoots.props`. Edit that local file when a production app stores the framework or reusable modules outside the default `src/Framework` and `src/Modules` layout.

For an app-style checkout that already has flattened GMA source repositories mounted under `gma/framework` and `gma/modules/<alias>`, bootstrap the root, framework, and module-local source-root files with:

```powershell
.\eng\gma-bootstrap.ps1 -SourceLayout GmaSubmodules
```

That command writes ignored `Gma.SourceRoots.props` files at the skeleton root, `gma/framework`, and each mounted reusable module root. The module-local files are required because each source repository imports its own `Directory.Build.props`; the skeleton root props file does not flow into projects inside a mounted source repository. Use `-WhatIf` to preview writes and `-Force` to refresh existing local files.

When moving from local rehearsal to real GitHub repositories, use the guarded Stage 8 helper:

```powershell
.\eng\gma-github-stage8.ps1
```

With no action switches, the helper prints the planned `SadPossum/gma-*` repositories and local candidate paths without touching GitHub. Use `-InitializeCandidates` after generating the local Stage 8 candidates so each split folder becomes a standalone local repository with `main` and `dev` branches and the expected local author. That initialization also refreshes the skeleton candidate from the current skeleton-owned docs/scripts and converted submodule `.slnx`, while keeping root package `.slnx` entrypoints out of the skeleton candidate. Use `-AuditRepositories` for a read-only SSH check of which planned remotes already exist. Add `-Repository gma-framework` or another explicit repository name when only part of the external repository set is ready. Once GitHub CLI is installed and authenticated, run explicit switches such as `-CreateRepositories`, `-PushCandidates`, `-ConfigureRepositories`, and `-ProtectBranches`. Keep `-WhatIf` on the first external pass. If a remote `main` already has unrelated history, the helper refuses to overwrite it; use `-SkipDivergedMain` to push only `dev` and reconcile `main` manually. `-PushCandidates` also refuses to push `gma-skeleton` until Stage 9 has produced real `.gitmodules` entries and submodule gitlinks; `-AllowUnconvertedSkeletonPush` exists only for a deliberate placeholder skeleton push. Use `-SkipSkeleton` for the normal first publishing pass when framework/module repositories should move before the skeleton is converted.

For source-split dry runs on Windows, keep sandbox paths short and clone with `core.longpaths=true`. Early extraction proofs used short ignored `.agents` roots and validated composition clones against local source repositories. The final package-repo rehearsal should use flattened package roots such as `gma-framework\src\Gma.Framework.Results` and `gma-module-auth\src\Gma.Modules.Auth.Application`, then mount them into applications at ergonomic paths such as `gma\framework` and `gma\modules\auth`.

Before replacing the skeleton/composition repository with real submodules, use the Stage 9 helper:

```powershell
.\eng\gma-stage9.ps1
.\eng\gma-stage9.ps1 -Audit
.\eng\gma-stage9.ps1 -WriteCommandPlan
.\eng\gma-stage9.ps1 -WriteConvertedSolution
.\eng\gma-stage9.ps1 -CreateLocalRehearsal -Force
.\eng\gma-stage9.ps1 -ProveLocalRecursiveClone -Force
```

The helper is non-destructive unless you run the generated command plan yourself, explicitly request a local rehearsal under `.agents`, or pass `-Force` to a working-tree rewrite helper. It verifies the Stage 8D skeleton candidate, local candidate repositories, current mount paths, and remote reachability; then it can write `.tmp\gma-stage9-submodule-plan.ps1` with the reviewed conversion sequence. `-WriteConvertedSolution` writes a converted submodule-backed solution to `.tmp\gma-stage9-converted-solution.slnx` by default, so the `.slnx` path conversion can be reviewed without touching the root solution. The command plan updates the `.slnx`, source-root shape, recursive-submodule CI workflow, and skeleton docs before adding submodules; then it validates the submodule-backed composition, removes old reusable source and root package `.slnx` entrypoints only after validation is green, and builds again after cleanup.

Use `-CreateLocalRehearsal -Force` to clone the prepared skeleton into `.agents\stage9\local-submodule-rehearsal`, overlay current skeleton-owned docs/scripts, generate the converted solution, add real local submodules with `protocol.file.allow=always`, bootstrap source roots, and prove the submodule-backed composition before touching the real worktree.

After the rehearsal exists, use `-ProveLocalRecursiveClone -Force` to commit the ignored rehearsal repository, clone it into `.agents\stage9\local-recursive-clone-proof` with recursive submodules, bootstrap source roots, and restore/build the clean clone. This local proof does not replace the later real GitHub clone check, but it catches broken `.gitmodules`, stale solution items, root package `.slnx` leakage, source-root, documentation-link, and submodule checkout assumptions before any external repository work.

To create a new source-first production app shell:

```powershell
.\eng\new-gma-app.ps1 -Name SampleApp -OutputPath .tmp\SampleApp -UseLocalStage8Candidates
cd .tmp\SampleApp
.\eng\gma-bootstrap.ps1
.\eng\gma-validate.ps1
```

The generated app keeps app-owned common code in `SampleApp.SharedKernel`, mounts GMA framework and explicitly selected reusable modules under `gma\...`, composes selected public API modules in the generated API host, and validates through its own `.slnx`. Use `-Modules auth,notifications` or another explicit list for the reusable modules the app wants; omit `-Modules` for a framework-only shell, or use `-Modules all` only for full local proof. Admin CLI/API and worker-only module surfaces stay explicit app-owned host work. The `-UseLocalStage8Candidates` switch is only for local proof before the real repositories are available; production apps should use real submodules at the same mount paths. The template also includes `.github\workflows\validate.yml`; set `GMA_CI_TOKEN` when private GMA submodules need cross-repository read access in GitHub Actions.

See [Source-first apps](source-first-apps.md) for the app layout, patch/update workflow, upstream-vs-app-local guidance, and submodule detached-HEAD warning.

Useful source-first helpers:

```powershell
.\eng\gma-status.ps1
.\eng\gma-update.ps1
.\eng\check-source-packages.ps1 -SkipRestore
.\eng\gma-validate.ps1 -FocusedSolutions
```

`gma-status` reports dirty working tree and submodule state when submodules exist. `gma-update` is a no-op until `.gitmodules` exists. `check-source-packages` validates package-local solution ownership and builds focused packages. `gma-validate` validates the all-up solution and, with `-FocusedSolutions`, each framework/module entrypoint.

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
dotnet test GMA-Skeleton.slnx --no-build --logger "console;verbosity=minimal"
```

## Focused Solution Entrypoints

Use `GMA-Skeleton.slnx` for the full skeleton/composition repository. Use the focused package-local `.slnx` files when working inside a framework or reusable-module source boundary:

```text
gma/framework/Gma.Framework.slnx
gma/modules/administration/Gma.Modules.Administration.slnx
gma/modules/auth/Gma.Modules.Auth.slnx
gma/modules/files/Gma.Modules.Files.slnx
gma/modules/notifications/Gma.Modules.Notifications.slnx
gma/modules/task-runtime/Gma.Modules.TaskRuntime.slnx
gma/modules/tenancy/Gma.Modules.Tenancy.slnx
```

Those focused solutions live inside the mounted source repositories. Their paths are local to each package root, so a module can be opened either from this skeleton checkout or from its own repository:

```text
gma/framework/Gma.Framework.slnx
gma/modules/auth/Gma.Modules.Auth.slnx
gma/modules/notifications/Gma.Modules.Notifications.slnx
```

Open the package-local solution when you want to work as if the framework or module were already its own repository. The source-package checker verifies each focused solution stays local to `docs/`, `eng/`, `src/`, and `tests/`, and rejects stale root-owned reusable package folders.

Focused framework and reusable-module tests are colocated with their package repositories, for example `gma/framework/tests/Gma.Framework.Tests` and `gma/modules/auth/tests/Gma.Modules.Auth.Tests`. Cross-module architecture and integration tests stay under the skeleton repository-level `tests/` folder.

Catalog, Ordering, and TaskSamples are skeleton-owned examples, not reusable GMA source packages. They do not have standalone `.slnx` entrypoints; validate them through `GMA-Skeleton.slnx` and the skeleton-level test suites.

Focused module `.slnx` files list only module-owned projects, focused module tests, and the module-owned docs index. They do not list framework projects as solution entries; framework dependencies resolve through `GmaFrameworkRoot` project references so the same module solution can build in the monorepo, an extracted module repo, or a source-submodule application checkout. In an extracted repository, the `.slnx`, `docs/`, `tests/`, `eng/`, and `src/` folders should live at the repository root rather than under preserved monorepo parent folders.

To dry-run the source-package boundary in the current monorepo:

```powershell
.\eng\check-source-packages.ps1 -SkipRestore
```

The script verifies focused `.slnx` ownership, stale root docs/tests, package-local docs/tests/scripts, and builds every focused package solution. Omit `-SkipRestore` when package references changed.

## Source-First GMA Development

The skeleton repository consumes the framework and reusable modules as Git submodules under `gma/framework` and `gma/modules/<alias>`. Framework references go through `GmaFrameworkRoot`, and module references go through `GmaModule*Root` properties with checked-in defaults in `Directory.Build.props`.

To create a local override file:

```powershell
.\eng\gma-bootstrap.ps1
```

This copies `Gma.SourceRoots.props.example` to ignored `Gma.SourceRoots.props`. Edit that local file when a production app stores the framework or reusable modules outside the default `gma/framework` and `gma/modules` layout.

For an app-style checkout that already has flattened GMA source repositories mounted under `gma/framework` and `gma/modules/<alias>`, bootstrap the root, framework, and module-local source-root files with:

```powershell
.\eng\gma-bootstrap.ps1 -SourceLayout GmaSubmodules
```

That command writes ignored `Gma.SourceRoots.props` files at the skeleton root, `gma/framework`, and each mounted reusable module root. The module-local files are required because each source repository imports its own `Directory.Build.props`; the skeleton root props file does not flow into projects inside a mounted source repository. Use `-WhatIf` to preview writes and `-Force` to refresh existing local files.

The one-off Stage 8/Stage 9 source-split automation is no longer part of the live `eng/` workflow. The decisions and command history remain in [the rebrand/source-split architecture note](../architecture/gma-rebrand-and-source-repo-split.md), but day-to-day work should use the source-first scripts listed below.

To create a new source-first production app shell:

```powershell
.\eng\new-gma-app.ps1 -Name SampleApp -OutputPath .tmp\SampleApp -Modules auth,notifications
cd .tmp\SampleApp
.\eng\gma-bootstrap.ps1
.\eng\gma-validate.ps1
```

The generated app keeps process entrypoints under `src\Hosts`, app-owned modules under `src\Modules`, and app-owned shared code in `src\Shared\SampleApp.SharedKernel`. It mounts GMA framework and explicitly selected reusable modules under `gma\...`, composes selected public API modules in the generated API host, and validates through its own `.slnx`. Use `-Modules auth,notifications` or another explicit list for the reusable modules the app wants; omit `-Modules` for a framework-only shell, or use `-Modules all` only when you deliberately want every reusable module mounted for a full local proof. The root README stays app-facing, while generated GMA operating notes live in `docs\gma-source.md`. Admin CLI/API and worker-only module surfaces stay explicit app-owned host work. The template also includes `.github\workflows\validate.yml`; set `GMA_CI_TOKEN` only when private GMA submodules need cross-repository read access in GitHub Actions.

See [Source-first apps](source-first-apps.md) for the app layout, patch/update workflow, upstream-vs-app-local guidance, and submodule detached-HEAD warning.

Useful source-first helpers:

```powershell
.\eng\gma-status.ps1
.\eng\gma-update.ps1
.\eng\check-source-packages.ps1 -SkipRestore
.\eng\gma-validate.ps1 -FocusedSolutions
```

`gma-status` reports dirty working tree, source-root, and submodule state. `gma-update` runs `git submodule update --recursive`, with `-Init` for first checkout and `-Remote` when you deliberately want to move mounted repositories to their configured branch tips. `check-source-packages` validates package-local solution ownership and builds focused packages. `gma-validate` validates the all-up solution and, with `-FocusedSolutions`, each framework/module entrypoint.

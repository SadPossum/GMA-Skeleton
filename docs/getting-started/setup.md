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
Gma.Modules.Catalog.slnx
Gma.Modules.Files.slnx
Gma.Modules.Notifications.slnx
Gma.Modules.Ordering.slnx
Gma.Modules.TaskSamples.slnx
Gma.Modules.TaskRuntime.slnx
Gma.Modules.Tenancy.slnx
```

Focused framework, reusable-module, and example-module tests are colocated with their future source roots, for example `src/Framework/tests/Gma.Framework.Tests`, `src/Modules/Auth/tests/Gma.Modules.Auth.Tests`, and `src/Modules/Catalog/tests/Catalog.Tests`. Cross-module architecture and integration tests stay under the repository-level `tests/` folder.

Focused module `.slnx` files list only module-owned projects, focused module tests, and the module-owned docs index. They do not list framework projects as solution entries; framework dependencies resolve through `GmaFrameworkRoot` project references so the same module solution can build in the monorepo, an extracted module repo, or a source-submodule application checkout.

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

For source-split dry runs on Windows, keep sandbox paths short and clone with `core.longpaths=true`. The current extraction proof used a short ignored `.agents\sr\1` root and validated a composition clone whose `src\Framework` and reusable module folders pointed at extracted source repositories.

Useful source-first helpers:

```powershell
.\eng\gma-status.ps1
.\eng\gma-update.ps1
.\eng\check-source-packages.ps1 -SkipRestore
.\eng\gma-validate.ps1 -FocusedSolutions
```

`gma-status` reports dirty working tree and submodule state when submodules exist. `gma-update` is a no-op until `.gitmodules` exists. `check-source-packages` validates package-local solution ownership and builds focused packages. `gma-validate` validates the all-up solution and, with `-FocusedSolutions`, each framework/module entrypoint.

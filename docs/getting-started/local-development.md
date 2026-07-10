# Local Development

## Daily Loop

Use the repo scripts from the root:

```powershell
.\eng\restore.ps1
.\eng\build.ps1 -NoRestore
.\eng\test-fast.ps1 -NoBuild
```

For a full local confidence pass:

```powershell
.\eng\verify.ps1
```

`verify.ps1` runs restore, build, provider migration drift checks, and fast tests.

## Running the App

Prefer Aspire when testing infrastructure behavior:

```powershell
.\eng\run-aspire.ps1
```

The default Aspire graph runs `Host.Api` plus infrastructure. To add the optional admin API to the local graph, set:

```text
AppHost__AdminApi__Enabled=true
```

To add the optional background worker to the local graph, set:

```text
AppHost__Worker__Enabled=true
```

With that flag, Aspire starts `Host.Worker`, sets API-side `NatsJetStream:Enabled=false`, and enables worker-side Auth outbox publishing with `Worker:Modules:Auth=true`. This is the local separated-publishing profile. NATS consumers and task workers stay disabled unless you opt into them with settings such as:

```text
NatsConsumers__Enabled=true
Worker__Modules__Catalog=true
Worker__Modules__Ordering=true
Worker__Modules__TaskRuntime=true
Tasks__Worker__Enabled=true
Tasks__Worker__WorkerGroups__0=projection-workers
```

Run the relevant module migrations before enabling worker loops that query those module tables. Avoid enabling API-side publishing and worker-side publishing together unless you are intentionally testing multi-instance outbox claiming.

Use API-only mode when external services are already running:

```powershell
.\eng\run-api.ps1
```

Run the worker directly when external services are already running:

```powershell
.\eng\run-worker.ps1
```

`run-worker.ps1` defaults `DOTNET_ENVIRONMENT` to `Development` when the caller has not already set it.

## Running the Admin CLI

The admin CLI is optional and uses its own host:

```powershell
.\eng\run-admin.ps1 --help
```

`run-admin.ps1` defaults `DOTNET_ENVIRONMENT` to `Development` for that local process when the caller has not already set it, so the CLI uses the same disposable local settings as the HTTP hosts. Set `DOTNET_ENVIRONMENT` explicitly for non-local runs.

Bootstrap the first owner:

```powershell
.\eng\run-admin.ps1 -- admin bootstrap --actor owner --yes
```

Create and assign a tenant-scoped support role:

```powershell
.\eng\run-admin.ps1 -- admin roles create --actor owner --name support
.\eng\run-admin.ps1 -- admin roles grant --actor owner --role support --permission auth.members.read
.\eng\run-admin.ps1 -- admin roles grant --actor owner --role support --permission auth.members.create
.\eng\run-admin.ps1 -- admin roles assign --actor owner --target-actor support --role support --tenant default
```

Create an Auth member with a generated password:

```powershell
.\eng\run-admin.ps1 -- auth members create --actor support --tenant default --username user@example.com --username-type email --generate-password
```

Generated passwords are printed once and are not logged or audited.

## Running the Admin API

The admin API is optional and uses its own host:

```powershell
.\eng\run-admin-api.ps1
```

`Host.AdminApi` requires bearer authentication. The current local setup uses the Auth JWT configuration, so obtain a token through the public Auth API or another configured identity provider and call admin routes with:

```http
Authorization: Bearer <access-token>
X-Tenant-Id: default
```

By default, tenant-scoped admin API calls require any present token `scope_id` claim to match `X-Tenant-Id`. That default claim name is centralized as `ApplicationClaimNames.ScopeId`; `ApplicationClaimNames.TenantId` remains an alias for compatibility. Tokens without a scope claim are allowed for external identity-provider or global-admin scenarios, but RBAC must still grant the actor access to the requested tenant. The knobs are:

```text
Administration__Api__TenantIdClaim=scope_id
Administration__Api__RequireTenantClaimMatch=true
```

`Host.Api` does not expose `/api/admin/*`.

Generated password responses are disabled by default for admin HTTP. To allow `generatePassword=true` responses in local or controlled environments:

```text
Administration__Api__AllowGeneratedPasswordResponses=true
```

Keep this disabled in environments where gateway, proxy, or client logs may capture response bodies.
The checked-in `requests/admin-api.http` examples use manual password fields so they work with the default disabled policy.

## Optional Caching

Caching is disabled by default. Enable in-process memory caching with:

```text
Caching__Enabled=true
Caching__Provider=Memory
```

To run Redis through Aspire, set `AppHost:Redis:Enabled=true` for the AppHost. Aspire starts Redis, injects `ConnectionStrings:redis`, and selects the Redis provider for the Aspire-composed HTTP hosts and worker when they are enabled.

For standalone API, admin API, or admin CLI Redis mode, provide:

```text
Caching__Enabled=true
Caching__Provider=Redis
ConnectionStrings__redis=localhost:6379
```

Redis is not part of the normal local stack unless explicitly enabled.

## Switching Persistence Providers

Set `Persistence:Provider` to:

- `SqlServer`
- `PostgreSql`

The default provider is SQL Server.

Each provider has its own migration assembly for each module that owns persistence.

## Adding Migrations

```powershell
.\eng\add-migration.ps1 -Module Auth -Provider SqlServer -Name AddExample
.\eng\add-migration.ps1 -Module Auth -Provider PostgreSql -Name AddExample
.\eng\add-migration.ps1 -Module AccessControl -Provider SqlServer -Name AddExample
.\eng\add-migration.ps1 -Module Catalog -Provider SqlServer -Name AddExample
.\eng\add-migration.ps1 -Module Ordering -Provider PostgreSql -Name AddExample
```

The script discovers the module persistence project, provider-specific migrations project, and DbContext for both app-owned modules under `src/Modules` and reusable GMA modules under `gma/modules`. Use `-Context <Name>DbContext` only when a module has more than one DbContext or uses a non-obvious name.

After changing EF mappings or migrations, check all provider snapshots:

```powershell
.\eng\check-migrations.ps1 -NoBuild
```

The script runs `dotnet-ef migrations has-pending-model-changes` for every provider migration project under `src/Modules` and `gma/modules`.

## Adding a Module

Create a minimal module:

```powershell
.\eng\new-module.ps1 -Name Billing
```

Create a module with persistence and provider migration projects:

```powershell
.\eng\new-module.ps1 -Name Billing -Persistence -SqlServerMigrations -PostgreSqlMigrations
```

Create a richer optional-module shell:

```powershell
.\eng\new-module.ps1 -Name Billing -Persistence -SqlServerMigrations -PostgreSqlMigrations -Outbox -Inbox -AdminCli -AdminApi -Cache
```

Register the module in `Host.Api` during scaffolding:

```powershell
.\eng\new-module.ps1 -Name Billing -RegisterInHost
```

`-RegisterInHost` inserts the module at the explicit `// module-scaffold:public-api-modules` marker in `src/Hosts/Host.Api/Program.cs`.
Manual registration is usually better until the module is real. Optional modules should remain explicit host choices.

The root script is a skeleton convenience wrapper. The canonical framework-owned scaffolder implementation lives at `gma/framework/eng/new-module.ps1`, and the wrapper passes the composition repository root to it.

## Build Discipline

- Keep warnings as errors.
- Use central package management in `Directory.Packages.props`.
- Do not hide package versions in individual project files.
- Do not introduce cross-module project references except to `.Contracts` projects.
- Keep SQL Server and PostgreSQL migration snapshots synchronized.
- Do not apply database migrations from `Host.Api` startup.
- Run `.\eng\check-source-packages.ps1 -SkipRestore` after moving docs, tests, scripts, focused `.slnx` files, or source-package project references.

## When Something Fails

- Restore first: `.\eng\restore.ps1`
- Rebuild without stale artifacts: `.\eng\build.ps1`
- Check source-package boundaries: `.\eng\check-source-packages.ps1 -SkipRestore`
- Run fast tests: `.\eng\test-fast.ps1`
- If infrastructure behavior is involved, run Docker tests: `.\eng\test-docker.ps1`

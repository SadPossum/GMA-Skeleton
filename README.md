# GenericModularApi

GenericModularApi is a .NET 10 modular monolith skeleton for building projects from optional, replaceable modules.

The repo is intentionally small and explicit:

- modules are registered by the host, not discovered by assembly scanning;
- cross-module communication goes through contracts and integration events;
- EF Core is the practical unit of work;
- tenant support starts with shared-database isolation through `TenantId`;
- reliable cross-boundary publishing goes through outbox tables and a NATS JetStream adapter.
- optional cache-aside reads use provider-neutral contracts, HybridCache, and an opt-in Redis adapter.
- optional administration uses a separate CLI host, persisted RBAC/audit, and feature-owned admin front doors.
- optional admin HTTP APIs use a separate `Host.AdminApi` composition root.
- optional background processing uses a separate `Host.Worker` composition root when deployments want HTTP hosts to avoid publisher, consumer, and task-worker load.

## Quick Start

```powershell
.\eng\restore.ps1
.\eng\build.ps1 -NoRestore
.\eng\test-fast.ps1 -NoBuild
```

Run the full local development stack with Aspire:

```powershell
.\eng\run-aspire.ps1
```

Run only the API:

```powershell
.\eng\run-api.ps1
```

Run the optional admin CLI locally:

```powershell
.\eng\run-admin.ps1 -- admin roles list --actor owner
```

Run the optional admin API locally:

```powershell
.\eng\run-admin-api.ps1
```

Run the optional worker locally:

```powershell
.\eng\run-worker.ps1
```

Run the optional worker in Aspire by setting:

```powershell
$env:AppHost__Worker__Enabled = 'true'
.\eng\run-aspire.ps1
```

Docker-backed tests are skippable by default. To require them:

```powershell
.\eng\test-docker.ps1 -NoBuild
```

Standalone source-repo entrypoints are available for focused framework/module work:

```powershell
dotnet build Gma.Framework.slnx --no-restore
dotnet build Gma.Modules.Auth.slnx --no-restore
dotnet build Gma.Modules.Notifications.slnx --no-restore
```

For future source-submodule layouts, create a local source-root override with:

```powershell
.\eng\gma-bootstrap.ps1
```

## Documentation

Start with [docs/README.md](docs/README.md).

Useful entry points:

- [Setup](docs/getting-started/setup.md)
- [Architecture Overview](docs/architecture/overview.md)
- [GMA Framework Docs](src/Framework/docs/README.md)
- [Module System](src/Framework/docs/architecture/module-system.md)
- [Administration Architecture](src/Framework/docs/architecture/administration.md)
- [Messaging and Outbox](src/Framework/docs/architecture/messaging-and-outbox.md)
- [Tasks and Daemons](src/Framework/docs/architecture/tasks-and-daemons.md)
- [Auth Module](src/Modules/Auth/docs/README.md)
- [Administration Module](src/Modules/Administration/docs/README.md)
- [Tenancy Module](src/Modules/Tenancy/docs/README.md)
- [Naming Conventions](src/Framework/docs/guidelines/naming-conventions.md)
- [Development Guidelines](src/Framework/docs/guidelines/development-guidelines.md)
- [Documentation Guidelines](src/Framework/docs/guidelines/documentation-guidelines.md)

## Request Examples

HTTP examples live in [requests/auth.http](requests/auth.http) and [requests/admin-api.http](requests/admin-api.http).

## Project Status

This is a work-in-progress skeleton. The current reusable modules are Auth, Tenancy, Administration, Notifications, and TaskRuntime.
Catalog, Ordering, and TaskSamples are compiled optional example modules used to prove stored data, admin surfaces, caching, cross-module integration, notifications, and task execution without being registered in default hosts.

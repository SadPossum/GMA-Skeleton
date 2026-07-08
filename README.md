# GMA-Skeleton

GMA-Skeleton is a .NET 10 modular monolith skeleton for building projects from optional, replaceable modules.

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
dotnet build gma\framework\Gma.Framework.slnx --no-restore
dotnet build gma\modules\auth\Gma.Modules.Auth.slnx --no-restore
dotnet build gma\modules\notifications\Gma.Modules.Notifications.slnx --no-restore
```

For future source-submodule layouts, create a local source-root override with:

```powershell
.\eng\gma-bootstrap.ps1
```

To generate a new production app shell that consumes selected GMA source repositories, start with [Source-First Apps](docs/getting-started/source-first-apps.md).

## Documentation

Start with [docs/README.md](docs/README.md).

Useful entry points:

- [Setup](docs/getting-started/setup.md)
- [Source-First Apps](docs/getting-started/source-first-apps.md)
- [Architecture Overview](docs/architecture/overview.md)
- [GMA Framework Docs](https://github.com/SadPossum/GMA-Framework/blob/dev/docs/README.md)
- [Module System](https://github.com/SadPossum/GMA-Framework/blob/dev/docs/architecture/module-system.md)
- [Administration Architecture](https://github.com/SadPossum/GMA-Framework/blob/dev/docs/architecture/administration.md)
- [Messaging and Outbox](https://github.com/SadPossum/GMA-Framework/blob/dev/docs/architecture/messaging-and-outbox.md)
- [Tasks and Daemons](https://github.com/SadPossum/GMA-Framework/blob/dev/docs/architecture/tasks-and-daemons.md)
- [Auth Module](https://github.com/SadPossum/GMA-Module-Auth/blob/dev/docs/README.md)
- [Administration Module](https://github.com/SadPossum/GMA-Module-Administration/blob/dev/docs/README.md)
- [Tenancy Module](https://github.com/SadPossum/GMA-Module-Tenancy/blob/dev/docs/README.md)
- [Naming Conventions](https://github.com/SadPossum/GMA-Framework/blob/dev/docs/guidelines/naming-conventions.md)
- [Development Guidelines](https://github.com/SadPossum/GMA-Framework/blob/dev/docs/guidelines/development-guidelines.md)
- [Documentation Guidelines](https://github.com/SadPossum/GMA-Framework/blob/dev/docs/guidelines/documentation-guidelines.md)

## Request Examples

HTTP examples live in [requests/auth.http](requests/auth.http) and [requests/admin-api.http](requests/admin-api.http).

## Project Status

This is a work-in-progress skeleton. The current reusable modules are Auth, Tenancy, Administration, Notifications, and TaskRuntime.
Catalog, Ordering, and TaskSamples are compiled optional example modules used to prove stored data, admin surfaces, caching, cross-module integration, notifications, and task execution without being registered in default hosts.

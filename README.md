# GMA-Skeleton

[![Validate](https://github.com/SadPossum/GMA-Skeleton/actions/workflows/validate.yml/badge.svg?branch=dev)](https://github.com/SadPossum/GMA-Skeleton/actions/workflows/validate.yml)
[![Docker Tests](https://github.com/SadPossum/GMA-Skeleton/actions/workflows/docker-tests.yml/badge.svg?branch=dev)](https://github.com/SadPossum/GMA-Skeleton/actions/workflows/docker-tests.yml)

GMA-Skeleton is a .NET 10 modular monolith skeleton for building projects from optional, replaceable modules.

The repo is intentionally small and explicit:

- modules are registered by the host, not discovered by assembly scanning;
- reusable modules remain independently buildable; optional cross-module bridges live in `GMA-Extensions` and are composed by the host;
- cross-module communication goes through contracts and integration events;
- EF Core is the practical unit of work;
- tenant support starts with shared-database isolation through `TenantId`;
- reliable cross-boundary publishing goes through outbox tables and a NATS JetStream adapter.
- optional cache-aside reads use provider-neutral contracts, HybridCache, and an opt-in Redis adapter.
- optional administration uses separate CLI and HTTP hosts, AccessControl-owned persisted RBAC, Administration-owned audit, and feature-owned admin front doors.
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
dotnet build gma\extensions\Gma.Extensions.slnx --no-restore
dotnet build gma\modules\auth\Gma.Modules.Auth.slnx --no-restore
dotnet build gma\modules\notifications\Gma.Modules.Notifications.slnx --no-restore
```

For future source-submodule layouts, create a local source-root override with:

```powershell
.\eng\gma-bootstrap.ps1
```

To generate a new production app shell that consumes selected GMA source repositories, start with [Source-First Apps](docs/getting-started/source-first-apps.md).

## GitHub Actions

The `Validate` workflow runs the normal non-Docker lane on `dev`, `main`, pull requests, and manual dispatch. It checks submodule pointers against each reusable repository's `dev` head, bootstraps source roots, restores, builds, checks provider migration drift, and runs fast tests through `eng/verify.ps1`.

The `Docker Tests` workflow runs for relevant pull-request/main changes, on a weekly schedule, and by manual dispatch. It runs Docker-backed integration tests with `GMA_REQUIRE_DOCKER_TESTS=true`.

## Documentation

Start with [docs/README.md](docs/README.md).

Useful entry points:

- [Setup](docs/getting-started/setup.md)
- [Source-First Apps](docs/getting-started/source-first-apps.md)
- [Architecture Overview](docs/architecture/overview.md)
- [GMA Framework Docs](https://github.com/SadPossum/GMA-Framework/blob/dev/docs/README.md)
- [GMA Extensions](https://github.com/SadPossum/GMA-Extensions/blob/dev/docs/README.md)
- [Module System](https://github.com/SadPossum/GMA-Framework/blob/dev/docs/architecture/module-system.md)
- [Administration Architecture](https://github.com/SadPossum/GMA-Framework/blob/dev/docs/architecture/administration.md)
- [Messaging and Outbox](https://github.com/SadPossum/GMA-Framework/blob/dev/docs/architecture/messaging-and-outbox.md)
- [Tasks and Daemons](https://github.com/SadPossum/GMA-Framework/blob/dev/docs/architecture/tasks-and-daemons.md)
- [AccessControl Module](https://github.com/SadPossum/GMA-Module-Access-Control/blob/dev/docs/README.md)
- [Auth Module](https://github.com/SadPossum/GMA-Module-Auth/blob/dev/docs/README.md)
- [Administration Module](https://github.com/SadPossum/GMA-Module-Administration/blob/dev/docs/README.md)
- [Files Module](https://github.com/SadPossum/GMA-Module-Files/blob/dev/docs/README.md)
- [Notifications Module](https://github.com/SadPossum/GMA-Module-Notifications/blob/dev/docs/README.md)
- [Organizations Module](https://github.com/SadPossum/GMA-Module-Organizations/blob/dev/docs/README.md)
- [TaskRuntime Module](https://github.com/SadPossum/GMA-Module-Task-Runtime/blob/dev/docs/README.md)
- [Tenancy Module](https://github.com/SadPossum/GMA-Module-Tenancy/blob/dev/docs/README.md)
- [Naming Conventions](https://github.com/SadPossum/GMA-Framework/blob/dev/docs/guidelines/naming-conventions.md)
- [Development Guidelines](https://github.com/SadPossum/GMA-Framework/blob/dev/docs/guidelines/development-guidelines.md)
- [Documentation Guidelines](https://github.com/SadPossum/GMA-Framework/blob/dev/docs/guidelines/documentation-guidelines.md)

## Request Examples

HTTP examples live in [requests/auth.http](requests/auth.http) and [requests/admin-api.http](requests/admin-api.http).

## Project Status

This is a work-in-progress skeleton. The current reusable modules are AccessControl, Administration, Auth, Files, Notifications, Organizations, TaskRuntime, and Tenancy. Optional reusable cross-module composition lives in GMA Extensions.
Catalog, Ordering, and TaskSamples are compiled optional example modules used to prove stored data, admin surfaces, caching, cross-module integration, notifications, and task execution without being registered in default hosts.

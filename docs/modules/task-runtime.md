# TaskRuntime Module

The `TaskRuntime` module is an optional persisted runtime for queued tasks, long-running task handlers, progress reporting, retries, cancellation, and operator control. It is reusable infrastructure, not an example module and not a scheduler framework by itself.

## Projects

```text
Gma.Modules.TaskRuntime.Contracts
Gma.Modules.TaskRuntime.Application
Gma.Modules.TaskRuntime.Persistence
Gma.Modules.TaskRuntime.Persistence.SqlServerMigrations
Gma.Modules.TaskRuntime.Persistence.PostgreSqlMigrations
Gma.Modules.TaskRuntime.Admin.Contracts
Gma.Modules.TaskRuntime.AdminCli
Gma.Modules.TaskRuntime.AdminApi
```

`TaskRuntime` is not registered in the default `Host.Api`, `Host.AdminCli`, or `Host.AdminApi`. Applications compose it explicitly when they want persisted task runs or admin task controls.

## Responsibilities

The module owns:

- persisted task runs;
- persisted control messages;
- run enqueue/list/get/stats/cancel/retry/control use cases;
- SQL Server and PostgreSQL migrations for the `tasks` schema;
- admin CLI and admin API front doors for task operations.

The shared task contracts and worker loop live outside the module in `Gma.Framework.Tasks`, `Gma.Framework.Tasks.Infrastructure`, and optional bridge packages. Task-owning modules still own their payload contracts and handlers.

## Composition

Compose the runtime store only in hosts that need task persistence or task admin operations:

```csharp
builder.Services.AddTaskRuntimeApplication();
builder.AddTaskRuntimePersistence();
```

Compose the worker loop only in hosts that should execute queued work:

```csharp
builder.AddTaskWorkerRuntime();
```

If task handlers dispatch commands, also compose the CQRS bridge:

```csharp
builder.AddTaskCqrs();
```

For tenant-scoped task payloads, compose the tenancy task bridge:

```csharp
builder.AddTenantTaskExecutionContext();
```

`TaskRuntimeProfiles.Default` is selected by `Gma.Modules.TaskRuntime.AdminCli` and `Gma.Modules.TaskRuntime.AdminApi`. The profile requires the persisted run store, runtime reporter, and control channel provided by `Gma.Modules.TaskRuntime.Persistence`.

## Admin CLI

`Gma.Modules.TaskRuntime.AdminCli` contributes the `tasks` command surface when explicitly registered by an admin CLI host:

```text
tasks runs list
tasks runs list --status retry-scheduled
tasks runs stats
tasks runs get --run-id <id>
tasks runs enqueue --module <module> --task <task> --payload-json <json>|--payload-file <path>
tasks runs control --run-id <id> --command tasks.pause|tasks.resume|tasks.cancel|tasks.drain --yes
tasks runs cancel --run-id <id> --yes
tasks runs retry --run-id <id> --yes
```

## Admin API

`Gma.Modules.TaskRuntime.AdminApi` maps admin-only endpoints under:

```text
/api/admin/tasks/runs
```

The API uses the same application commands and queries as the CLI. It is intended for operator tooling, not public product workflows.

## Permissions

The module declares:

| Permission | Purpose |
| --- | --- |
| `tasks.runs.read` | List, inspect, and view task run stats. |
| `tasks.runs.create` | Enqueue task runs. |
| `tasks.runs.cancel` | Cancel task runs. |
| `tasks.runs.retry` | Retry terminal task runs. |
| `tasks.runs.control` | Send control messages to running task handlers. |

Task runtime permissions are global operator permissions and are not tenant-scoped by default.

## Persistence

Schema:

```text
tasks
```

Migration history table:

```text
tasks.__ef_migrations_history
```

Tables:

- `task_runs`
- `task_control_messages`

Provider-specific migrations exist for SQL Server and PostgreSQL. Tests and deployment automation apply migrations explicitly; default hosts do not auto-migrate.

## Boundaries

- Task payload contracts belong to the module that owns the task.
- Task handlers are registered explicitly through shared task registration helpers.
- `TaskRuntime` persists run state and exposes operator controls; it does not know module domain internals.
- Default API/admin hosts do not start workers or register the task admin front doors.
- External schedulers remain optional adapters that enqueue the same `TaskRunRequest` shape.

See [Tasks and Daemons](../architecture/tasks-and-daemons.md) for the shared task model and [TaskSamples Example Module](../examples/task-samples-module.md) for compiled example handlers.

# TaskSamples Example Module

`TaskSamples` is a compiled, non-host-registered example that demonstrates how a module declares and handles background work without depending on a scheduler package.

## Projects

- `TaskSamples.Contracts` owns module metadata and serialized task payload contracts. Payloads declare `generate-report` v1/v2, `flaky-report`, and `slow-report` through split task attributes, and module metadata references them with `ModuleDescriptor.Create(...).WithTask<TPayload>().Build()`.
- `TaskSamples.Application` owns handlers, a schedule provider, command, command handler, and output port.

The module is intentionally not registered in `Host.Api`, `Host.AdminCli`, or `Host.AdminApi`.

## Runtime Flow

1. A host explicitly composes `Gma.Modules.TaskRuntime.Persistence`, `AddTaskWorkerRuntime()`, and `AddTaskSamplesApplication()`.
2. Code enqueues a `TaskRunRequest` for module `task-samples`, task `generate-report`, worker group `samples`, and payload version `1` or `2`.
3. The worker claims the run from the `tasks.task_runs` table.
4. The runtime deserializes `GenerateReportTaskPayload`.
5. `GenerateReportTaskHandler` dispatches `RecordTaskSampleReportCommand` through `ITaskCommandDispatcher` from `Gma.Framework.Tasks.Cqrs`.
6. The command handler writes to `ITaskSampleReportSink`.
7. The runtime marks the run succeeded after handler completion.

## Rules Demonstrated

- Task payload attributes, descriptors, and task-handler registration must match, including task kind, scope context, payload version, worker group, and control-message support.
- Serialized task payloads that appear in module metadata live in the owning module contracts layer.
- Task payload versions are explicit: v1 and v2 handlers share the same logical task name but use different `payloadVersion` metadata.
- `FlakyReportTaskHandler` demonstrates retry behavior by failing until a configured attempt.
- `SlowReportTaskHandler` demonstrates heartbeat/progress reporting plus cooperative pause/resume/cancel/drain handling through `ITaskControlLoop`.
- `TaskSamplesScheduleProvider` demonstrates code-defined schedules that enqueue task requests through the optional scheduler adapter and use the default version-aware schedule dedupe shape.
- Task handlers use shared application contracts, not HTTP, CLI, EF, or scheduler APIs.
- Scope-aware tasks require a scope id on the run request.
- The persisted task runtime is optional host composition, not default host behavior.

## Tests

`TaskRuntimeIntegrationTests` proves:

- SQL Server and PostgreSQL migrations create the runtime schema.
- Lease claims are exclusive.
- expired locks are reclaimable.
- wrong-worker status updates do not mutate a run.
- retry scheduling blocks claims until due.
- requester metadata is persisted.
- queued cancellation and reclaimed running cancellation finish as terminal canceled runs.
- application-level enqueue rejects invalid JSON.
- dedupe keys prevent duplicate active runs.
- scheduled dedupe keys include module, task, schedule, payload version, and occurrence so v1/v2 payloads do not suppress each other.
- payload version `2` claims route to the v2 handler registration.
- stale running tasks can be marked timed out and retried through the application command path.
- application-level stats count task runs by status.
- application-level control commands enqueue persisted control messages for non-terminal runs.
- the hosted worker processes the sample task through persisted runtime state.

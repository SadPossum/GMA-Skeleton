# Source Compatibility Revalidation Task

Status: completed
Date: 2026-08-05

## Goal

Rehearse the current Framework and reusable-module candidates through the source-first Skeleton, publish them in dependency order, and pin the verified source set.

## Findings

- the Framework schedule provider contract now streams definitions with `IAsyncEnumerable` so large or remote schedule sources are not materialized eagerly;
- the Skeleton-owned `TaskSamples` provider still returned a materialized task list;
- mandatory outbox scope-resolver forwarding correctly requires the matching Organizations update, so Framework and Organizations pointers must move together;
- current reusable lifecycle contracts exposed public enums without a reserved unknown value or owned JSON wire format, and grouped many public contract types in single files;
- source-layout documentation guards treated sibling Framework and module worktrees as if they were escaping the Skeleton repository;
- Notifications gained an optional admin CLI, while the Skeleton catalog and generated-app manifest still modeled only its admin API;
- project-file source-root overrides do not flow through every transitive repository boundary, so unpublished sibling-worktree rehearsals need global MSBuild properties and generated-matrix source-root overrides.

## Slice

1. Align the Skeleton-owned task sample with the streaming schedule contract and cancellation semantics.
2. Rehearse Skeleton architecture/composition against the migrated Framework, Files, and Organizations worktrees through local source-root overrides.
3. Keep reusable source in its owning repositories; do not copy it into Skeleton or record pointers to uncommitted work.
4. Run generated-app, solution, architecture, security, release, and source-package checks that do not require Docker.

## Result

- `TaskSamplesScheduleProvider` now streams its single definition and observes enumeration cancellation.
- Architecture coverage includes the Notifications admin CLI and validates source-package-local documentation links against each package root.
- Access Control and Task Runtime lifecycle contracts keep one public type per file, reserve `Unknown = 0`, and own strict kebab-case JSON converters; Organizations and Notifications domain transitions follow the same unknown-value rule.
- Notifications persistence and tests declare every Framework project they import directly.
- The generated-app matrix accepts optional Framework, Extensions, and Modules source roots. Its default remains the pinned source set used by CI, while maintainers can compile against unpublished sibling worktrees without moving submodule pointers.
- The generated `AllAdmin` host composes `NotificationsAdminCliModule` as well as the Notifications admin API.
- Catalog and Ordering own SQL Server migrations that apply the Framework ordinal `ScopeId` collation contract to their example schemas; their PostgreSQL models remain unchanged.
- The Skeleton now pins the published Framework and reusable-module commits exercised by this task.

## Verification

- package builds completed with zero warnings for Access Control, Task Runtime, Organizations, and Notifications;
- focused contract-wire tests passed: Access Control 55, Task Runtime 20, Organizations 42;
- Skeleton architecture tests passed: 269;
- the generated-app selection matrix passed against the published source set, including a zero-warning `AllAdmin` build;
- Skeleton solution synchronization, repository security, release, and reusable source-package checks passed;
- the full Skeleton build completed with zero warnings;
- all PostgreSQL and SQL Server migration-drift checks passed;
- 2,385 non-Docker tests passed, including 269 architecture tests.

## Acceptance

- the Skeleton sample compiles against the streaming task contract;
- current Framework and reusable-module sources compose without source breaks;
- source-package and generated-solution guardrails remain green;
- the tracked Skeleton worktree contains only Skeleton-owned alignment changes and published child pointers.

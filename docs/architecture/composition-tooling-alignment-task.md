# Composition Tooling Alignment Task

Status: complete

## Summary

Align the reusable GMA repository tooling with patterns proven in BunkFy without moving BunkFy domain behavior into GMA.

The framework repository owns reusable tooling that runs after `gma/framework` is mounted. The skeleton owns production app generation and composition examples. Applications keep small wrappers, product host choices, and multi-app workspace orchestration.

## Decisions

1. Extend the framework module scaffolder with an optional project prefix and configurable public API host paths. Keep its current unprefixed output as the compatibility default.
2. Make migration discovery independent of a specific product or `Gma.Modules` prefix.
3. Add reusable solution synchronization and source-workspace validation implementations under framework `eng/`; composition repositories expose thin wrappers with their own solution and host settings.
4. Keep `gma-update.ps1 -Init` app-local because the framework checkout may not exist yet. Add an explicit editable-branch mode without changing pinned detached-checkout behavior by default.
5. Keep `new-gma-app.ps1` skeleton-owned. Generated applications must not carry a second copy of the app generator.
6. Generate product module projects as `<Application>.Modules.<Module>.*`, plus optional API, admin, worker, service-default, and Aspire host surfaces.
7. Generate immutable CI action pins, Windows and Linux fast validation, optional Docker validation, dependency update configuration, and a reproducible source-set manifest.

## Framework Scope

- parameterized module project naming;
- parameterized public API host registration;
- generic provider migration target discovery and drift checks;
- deterministic `.slnx` project and operational-file synchronization;
- generic submodule status/head validation and source-package checks where the framework is already mounted;
- focused tooling tests and documentation.

No runtime package gains a dependency from this work.

## Skeleton Scope

- update app generation to consume framework-owned tooling through thin wrappers;
- add optional host selection while preserving a small API-only default;
- generate product-branded module and solution guardrails;
- exercise generated app and generated module fixtures;
- split oversized developer-experience guards by responsibility without changing coverage.

## BunkFy Scope

- replace post-generation regex branding with the parameterized framework scaffolder;
- use generic migration and solution tooling through app-specific wrappers;
- remove the stale local `new-gma-app.ps1` copy and retain a short source-mount guide;
- align CI, dependency automation, and source-set release evidence with the skeleton;
- keep BunkFy super-workspace and frontend orchestration product-owned.

## Explicit Non-Goals

- moving adapter acquisition, Ingestion authority, remote lease, checkpoint, or reservation-change semantics into GMA;
- moving the BunkFy ingestion runtime baseline into the skeleton;
- imposing BunkFy file-size or Staff cleanup policies on every GMA application;
- creating a separate tooling repository before more than the mounted framework needs to own the shared implementation.

## Verification

- Framework builds with zero warnings and all 916 tests pass.
- Skeleton solution drift, zero-warning build, 14 provider migration checks, all 1,457 fast tests, and focused source-package checks pass.
- A generated all-host application with ServiceDefaults, Aspire, and a branded persisted/admin module builds with zero warnings; its four application guards and 915 copied Framework tests pass.
- BunkFy solution drift, zero-warning build, 16 provider migration checks, all 1,516 fast tests, and focused source-package checks pass.
- Schema-v2 source-set manifests resolve the composition root plus eight GMA repositories in both Skeleton and BunkFy.
- The two Framework worktrees have the same base commit and byte-identical changed-file sets.

Docker-backed tests were not run as part of this tooling pass. Clean-state and remote-head validation remains a publication gate because it can only pass after a composition commit records its updated Framework pointer.

## Publication Order

1. Commit and publish the Framework tooling change.
2. Update Skeleton and BunkFy to the published Framework commit.
3. Run the submodule remote-head guards from clean composition worktrees.
4. Commit and publish Skeleton and BunkFy independently.

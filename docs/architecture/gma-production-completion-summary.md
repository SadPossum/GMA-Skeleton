# GMA Production Completion Summary

Date: 2026-07-20

This pass brought the current GMA domains and their BunkFy consumption to a production-development baseline. The detailed findings, decisions, commits, and CI evidence remain in the [production completion audit](gma-production-completion-audit-task.md).

## Architecture

- Framework remains limited to generic, dependency-neutral primitives.
- Each reusable module owns its domain behavior, persistence, migrations, and contracts without depending on sibling modules or product code.
- Optional cross-module behavior lives in explicitly selected GMA Extensions.
- Skeleton is the canonical source-first composition, scaffolding, migration, and guardrail reference.
- BunkFy owns workspace, staff, property, reservation, and other product policies; none of that terminology or behavior leaked into GMA.

## Domain Highlights

| Domain | Important changes |
| --- | --- |
| Organizations | Protected invitation and enrollment token responses from caching, added sensitive-route throttling, and introduced bounded opt-in retention with PostgreSQL and SQL Server proof. |
| Tenancy | Published a typed current-tenant contract, verified fail-closed scope resolution and isolation, and added standalone dependency and package guards. |
| Files | Added full-digest versioned storage namespaces with legacy read/delete compatibility, safer private downloads, generic scope errors, and cancellation-aware MinIO streaming. |
| Notifications | Added bounded delivery waves, retry clamping, a process-shared durable stream monitor and heartbeat recovery, relational concurrency proof, and stricter current-scope authorization in BunkFy. |
| TaskRuntime | Added atomic enqueue deduplication, provider-aware concurrent claims, lease-generation fencing, optimistic concurrency, race-safe controls, total-count pagination, and bounded retention behavior. |
| Auth | Closed 32 security, concurrency, lifetime, validation, OpenAPI, and persistence findings, including strict attempt acquisition, serialized recovery/MFA replacement, absolute session lifetime, durable refresh-reuse revocation, safer authenticator changes, and clearer public contracts. |
| AccessControl | Added bounded batch authorization, efficient persisted decisions, a product assignment-policy seam, scoped bulk revocation, Organizations lifecycle cleanup through Extensions, and dual-provider relational proof. |
| Administration | Made missing audit persistence fail visibly, made terminal audit writes cancellation-independent, added a distinct unaudited CLI outcome, removed wildcard permissions from admin requests, completed generated admin-host composition, and documented legacy RBAC recovery. |

## Shared And Consumer Work

- Added generic Framework transaction, relational key-lock, provider-error, authorization, audit, and composition primitives only where repeated reusable behavior justified them.
- Added explicit project and namespace allowlists to GMA Extensions so bridges cannot reach module domain, persistence, admin, API, or product internals.
- Hardened source-package ownership checks, generated-app selection checks, remote-head guards, solution synchronization, migration drift checks, vulnerability audits, and deterministic source-set export.
- Aligned BunkFy to the final published GMA commits and made its normal backend verification enforce source-package ownership.
- Kept Administration audit endpoints out of BunkFy's public API and kept BunkFy-specific eligibility and lifecycle ordering inside BunkFy.

## Verification

- Audited 159 projects and 846 project references with no forbidden Framework-to-module/product or reusable-module-to-sibling/product edge.
- All ten GMA source repositories passed exact-commit CI and were clean at their configured `dev` heads.
- Skeleton passed a zero-warning build, provider migration checks, generated composition checks, 1,946 non-Docker tests, and its required Docker suite.
- BunkFy backend passed 2,071 non-Docker tests and 27 Docker tests with no skips; the root clean-checkout backend/frontend workflow also passed.
- The final eleven-repository source-set manifest is clean and deterministic apart from its generation timestamp.

## Intentionally Future Work

Optional authentication methods and providers, advanced ABAC/ReBAC policy, role-management UX, legal retention and erasure policy, tamper-evident audit archives, external schedulers, and deployment-specific secrets, backup, recovery, and capacity decisions remain separate future slices. They are not hidden inside Framework or presented as completed product behavior.

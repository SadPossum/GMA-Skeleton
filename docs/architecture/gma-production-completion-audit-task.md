# GMA Production Completion Audit Task

Status: in progress
Date: 2026-07-20

## Goal

Close the domain-by-domain production pass with one repository-wide audit. The audit must prove that the completed domains still compose without weakening the project architecture:

- Framework owns generic, dependency-neutral primitives;
- modules own their domain behavior and data;
- optional cross-module behavior lives in explicit GMA Extensions;
- Skeleton is the canonical composition, generation, and guardrail reference;
- applications such as BunkFy own product policy and consume only published GMA contracts.

This task is a closure and integration slice. It does not reopen completed domain design or introduce another domain.

## Completed Domain Records

- Organizations lifecycle consistency;
- Tenancy contract verification;
- Files production hardening;
- Notifications production hardening;
- TaskRuntime production hardening;
- Auth domain completion;
- AccessControl domain completion;
- Administration domain completion.

Each module record remains the authoritative description of its domain-specific findings, ownership boundaries, deferred work, and verification evidence.

## Findings

1. Current Framework guidance still assigned persisted role-management HTTP endpoints to Administration even though AccessControl owns those endpoints and data. The root module index also omitted Organizations.
2. GMA Extensions correctly owns the optional cross-module implementations and uses deliberate module contracts or adapter policy seams, but the repository had no mechanical guard preventing a future extension from reaching domain, persistence, admin, API, or product internals.
3. AccessControl's completion task and Administration's completion task and upgrade guide were not listed in their focused `.slnx` files. The release-oriented source-package checker detected the drift, but ordinary Skeleton and generated-app validation did not run its ownership-only mode.
4. The source graph itself remains sound: Framework has no module/product dependency, reusable modules have no sibling/product dependency, and every current multi-module implementation remains in Extensions or an application composition root.

## Decisions

- Preserve Extensions references to explicit Organizations application policy ports and Notifications projector/email-adapter seams. Those are deliberate dependency-inversion SPIs, not access to domain or persistence internals.
- Give every current Extensions project an explicit module-project and namespace allowlist. New extensions or new module seams must update that allowlist deliberately.
- Run source-package ownership checks without duplicate restore/build work in normal Skeleton, generated-app, and BunkFy backend verification. Keep the heavier focused package builds as release/audit evidence.
- Keep module completion records and upgrade guidance inside the focused module solution that owns them.
- Correct current ownership documentation without rewriting historical migration records.

## Audit Slices

### 1. Repository And Publication Integrity

- verify every GMA source repository is clean and its recorded commit matches `origin/dev`;
- verify Skeleton records the current published Framework, Extensions, and module commits;
- verify BunkFy records the same current GMA commits without touching product-owned work;
- confirm exact-head GitHub validation is green for every source repository and both composition consumers.

### 2. Architectural Ownership

- inspect project references and package references across Framework, modules, Extensions, and Skeleton;
- prove reusable modules do not depend on sibling reusable modules or product source;
- prove Framework does not depend on module or product source;
- prove cross-module implementations are confined to explicit Extensions projects;
- prove public product hosts do not accidentally expose administration-only surfaces.

### 3. Canonical Composition

- run Skeleton solution, migration, architecture, source-package, and generated-selection checks;
- exercise the minimal generated application, each supported extension pair, all admin hosts, and the all-modules composition;
- run required relational and Docker-backed suites without silent skips;
- verify generated source-set manifests and operational files remain deterministic.

### 4. Product Consumption

- run BunkFy architecture, migration, host, integration, and Docker checks against the final GMA pins;
- run the clean recursive BunkFy workspace validation, including the committed frontend contract surface;
- preserve unrelated local frontend and company-readiness work;
- verify BunkFy-specific workspace, property, staff, reservation, ingestion, and UI behavior remains product-owned.

### 5. Closure Record

- record exact commits and CI runs for the final published graph;
- document any genuinely deferred production concerns without presenting them as completed behavior;
- leave all repositories clean except explicitly preserved user-owned work;
- close the persistent completion goal only when every acceptance criterion is satisfied.

## Acceptance Criteria

- all current GMA source heads have exact-commit green validation;
- Framework and every module retain their intended dependency boundaries;
- every supported cross-module edge is explicit in GMA Extensions and selected deliberately by composition;
- Skeleton validates all supported generated application shapes on Windows and Linux;
- required provider and Docker suites execute with no hidden skips;
- Skeleton and BunkFy record current published GMA commits and pass clean-checkout validation;
- no BunkFy-specific behavior or naming is introduced into Framework, reusable modules, Extensions, or Skeleton defaults;
- all task records and repository documentation describe the same current ownership model;
- no unresolved correctness, security, migration, or composition finding remains hidden behind a passing aggregate build.

## Explicit Non-Goals

- adding a new reusable domain;
- implementing product roadmap features;
- forcing optional infrastructure or adapters into default applications;
- inventing distributed transaction guarantees across independently owned stores;
- replacing explicit product policy with generic framework behavior.

## Interim Evidence

- Framework `5018dca3dec66fa802b398cc5fdcbc4ffa96e962` passes exact-head validation in run `29771222125`.
- Extensions `a17c8c764f25faa610dfc34e8fa612f700224034` passes its new boundary guard, zero-warning build, 30 tests, package audit, and exact-head Windows/Linux validation in run `29771230860`.
- AccessControl `d130af6cca657203a921994a4d89b34233a2501a` passes exact-head validation and required relational proof in run `29771530816`.
- Administration `b9a3f63cc1ea333a9a60ea5ab6f2c182120afbab` passes exact-head validation and required relational proof in run `29771544534`.
- The complete focused source-package ownership and build gate passes across Framework, Extensions, and all eight reusable modules.
- The Skeleton generated-selection matrix creates every isolated module, extension pair, Administration/AccessControl admin host, and all-modules admin application; the all-modules application restores and builds with zero warnings.
- The all-up zero-warning build and provider migration-drift checks pass. After correcting the audit-page ownership/index declarations found by the first aggregate run, all 1,946 fast tests pass with zero skips, including 265 architecture and 16 integration tests.

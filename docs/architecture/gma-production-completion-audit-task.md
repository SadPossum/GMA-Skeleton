# GMA Production Completion Audit Task

Status: complete
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

## Final Published Source Graph

| Source | Commit | Exact-head validation |
| --- | --- | --- |
| Framework | `5018dca3dec66fa802b398cc5fdcbc4ffa96e962` | `29771222125` |
| Extensions | `a17c8c764f25faa610dfc34e8fa612f700224034` | `29771230860` |
| AccessControl | `d130af6cca657203a921994a4d89b34233a2501a` | `29771530816` |
| Administration | `b9a3f63cc1ea333a9a60ea5ab6f2c182120afbab` | `29771544534` |
| Auth | `2094231164e0e089bc4f16117b77efc8c3e096a6` | `29741381402` |
| Files | `84aa025f05ccfdd1c57b655830ff12e6c27d041c` | `29704165005` |
| Notifications | `18dfd01387980e0555b76b17abdcf514f5f4bf2c` | `29707451591` |
| Organizations | `7392ce78c404f3774c370d05eb405742504f64e9` | `29696866002` |
| TaskRuntime | `863c2da3955e938d4db714e04d27788a2a3cf02b` | `29711192213` |
| Tenancy | `1faa831d963cd4cf18d9dd8e5d6073c17be81bef` | `29701138271` |

Every listed run completed successfully for the exact commit recorded by Skeleton and BunkFy. The post-commit remote-head guard also proves that every recorded source commit matches its configured `origin/dev` tip.

## Final Composition Evidence

- Skeleton implementation commit `8d4a820f15322d9742f3e860e171c6de948bba11` passes exact-head Windows/Linux validation in run `29772497276` and required Docker validation in run `29772509368`.
- The complete focused source-package ownership and zero-warning build gate passes across Framework, Extensions, and all eight reusable modules. Structured inspection covered 159 projects and 846 project references and found no Framework-to-module/product or reusable-module-to-sibling/product edge. Package inspection found no forbidden module or product package dependency.
- Extensions passes its explicit project and namespace allowlists, 30 tests, and vulnerability audit. Its deliberate Organizations policy ports and Notifications projector/email-adapter seams remain dependency-inversion SPIs rather than domain or persistence access.
- The Skeleton generated-selection matrix creates every isolated module, supported extension pair, Administration/AccessControl admin host, and all-modules admin application. The all-modules application restores and builds with zero warnings.
- The all-up zero-warning build, every provider migration-drift check, and all 1,946 non-Docker tests pass with zero skips, including 265 architecture and 16 integration tests.
- BunkFy backend commit `37de68aa9c3e8109659ecbd883b0ad04c64a6452` consumes the same published graph and enforces the source-package ownership gate. All 2,071 non-Docker tests and all 27 Docker tests pass locally with zero skips; exact-head Windows/Linux validation run `29773784955` and Docker run `29773784943` both pass.
- BunkFy root commit `eaf211c90fb57e648111c93c5d65505c4ce806a4` records only the backend pointer update. Clean recursive validation run `29774484690` passes the submodule guards, backend verification, committed frontend contract checks, frontend tests, and frontend build.
- Unrelated local BunkFy web changes and `docs/company-readiness` remain uncommitted and untouched.

## Intentionally Deferred Production Concerns

These are future capability or deployment decisions, not hidden acceptance failures for the completed domains:

- optional Auth adapters such as passkeys, additional OTP methods, trusted devices, concrete identity providers, and email transports; mandatory verification, MFA, and risk-assurance mapping remain product policy;
- advanced access models such as ABAC/ReBAC, deny grants, scheduled access, cascading delegated revocation, profile templates, invitation-time role UX, and migration away from compatibility roles;
- legal-hold, erasure, universal retention, SIEM/archive, cryptographic audit chains, and distributed transaction guarantees between module mutations and the separate audit store;
- organization deletion/merge/hierarchy/billing workflows, join-request lifecycle expansion, and product-specific staff onboarding or invitation delivery;
- external scheduler adapters and production deployment choices such as secrets, key custody, storage/provider topology, backup, recovery, and real-environment capacity testing.

The owning module completion records remain authoritative when one of these concerns becomes an active slice. No new domain should begin by smuggling one of them into Framework or an unrelated reusable module.

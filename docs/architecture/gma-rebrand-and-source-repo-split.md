# GMA Rebrand And Source Repo Split Plan

Status: in progress; Stages 1-6 are implemented in the current repository, Stage 7 has local dry-run proof in the ignored `.agents` sandbox, Stage 8A has local candidate repositories with standalone and composed validation, and Stage 8D has flattened local repository-shape proof including split-skeleton architecture tests. Real GitHub repositories, history-preserving extraction, and permanent submodule replacement remain future work.

This plan prepares the current repository for a source-first future where the reusable framework and reusable modules can evolve as independent Git repositories while production applications still consume them as editable source through submodules.

The intended end state is:

- Framework code is branded as `Gma.Framework.*`.
- Reusable first-party modules are branded as `Gma.Modules.<Module>.*`.
- Business applications can keep their own `Shared` or equivalent app-specific projects without colliding with GMA framework code.
- Each independent framework/module repository has its own `.slnx`, tests, docs, and Git history.
- A production application composes selected framework and module repositories as source submodules and pins exact commits.
- NuGet packaging remains possible later, but it is not the primary development path while the framework is still learning from production applications.

## Research Anchors

- .NET namespace guidelines recommend stable product or technology names and PascalCasing for namespaces. This supports `Gma.Framework.*` and `Gma.Modules.*` rather than `GMA.*` or lower-case code namespaces: <https://learn.microsoft.com/en-us/dotnet/standard/design-guidelines/names-of-namespaces>
- .NET capitalization guidelines use PascalCasing for public member, type, and namespace names, and do not treat long acronyms as all caps. This supports `Gma` over `GMA`: <https://learn.microsoft.com/en-us/dotnet/standard/design-guidelines/capitalization-conventions>
- In .NET 10, `dotnet new sln` creates `.slnx` by default. Microsoft describes `.slnx` as stable, understandable, widely supported, and easier to maintain: <https://learn.microsoft.com/en-us/dotnet/core/compatibility/sdk/10.0/dotnet-new-sln-slnx-default>
- `dotnet sln` can list, add, remove, and migrate `.sln`/`.slnx` files. This gives the migration a supported CLI path: <https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-sln>
- Git submodules keep another Git repository as a subdirectory while the parent repository records a commit pointer instead of owning the nested files. That matches the source-first, independently versioned dependency model: <https://git-scm.com/book/en/v2/Git-Tools-Submodules>
- GitHub Actions `actions/checkout` supports `submodules: true` and `submodules: recursive`, and can also check out multiple repositories into nested paths. Private secondary repositories need an explicit token or SSH key: <https://github.com/actions/checkout>
- .NET library dependency guidance warns about dependency graph friction and recommends minimizing unnecessary dependencies. The source-first split should preserve the current small optional package seams instead of turning every module into a heavy dependency bundle: <https://learn.microsoft.com/en-us/dotnet/standard/library-guidance/dependencies>

## Naming Decision

Use `Gma` for code, project, assembly, and package identity:

```text
Gma.Framework.Results
Gma.Framework.Cqrs
Gma.Framework.Messaging
Gma.Framework.Messaging.Nats
Gma.Framework.Tenancy
Gma.Framework.FileManagement.Minio

Gma.Modules.Auth.Contracts
Gma.Modules.Auth.Domain
Gma.Modules.Auth.Application
Gma.Modules.Auth.Persistence
Gma.Modules.Auth.Api

Gma.Modules.Notifications.*
Gma.Modules.Administration.*
Gma.Modules.Files.*
```

Use lower-case `gma` only for runtime identifiers where lower-case is already natural or conventional:

```text
gma-admin
gma:{environment}:...
gma.<module>.<event>.v<version>
```

Do not use `GMA.*` for code namespaces. It fights the normal .NET readability convention and makes names visually loud once there are many packages.

Do not use `Shared.*` for the reusable framework after the rebrand. Future business applications should be free to create their own `Shared`, `Acme.Shared`, `StayQuest.Shared`, or equivalent app-owned shared projects without confusion.

## Repository Model Decision

Prefer source-first Git submodules over NuGet packages for first-party production app development while GMA is still evolving.

The production application should look like this:

```text
my-business-app/
  MyBusinessApp.slnx
  src/
    Host.Api/
    Host.Worker/
    Modules/
      MyBusinessFeature/
    MyBusiness.Shared/
  gma/
    framework/              # submodule: SadPossum/gma-framework
    modules/
      auth/                 # submodule: SadPossum/gma-module-auth
      administration/       # submodule: SadPossum/gma-module-administration
      notifications/        # submodule: SadPossum/gma-module-notifications
      files/                # submodule: SadPossum/gma-module-files
```

The business app `.slnx` references selected `.csproj` files from submodules directly. It does not nest or reference child `.slnx` files.

Each independent repository still owns its own `.slnx` for standalone work:

```text
gma-framework/
  src/
    Gma.Framework.Results/
    Gma.Framework.Cqrs/
    Gma.Framework.Messaging/
  tests/
    Gma.Framework.Tests/
  docs/
  eng/
  Gma.Framework.slnx

gma-module-auth/
  src/
    Gma.Modules.Auth.Contracts/
    Gma.Modules.Auth.Domain/
    Gma.Modules.Auth.Application/
    Gma.Modules.Auth.Persistence/
    Gma.Modules.Auth.Api/
  tests/
    Gma.Modules.Auth.Tests/
  docs/
  eng/
  Gma.Modules.Auth.slnx

gma-module-notifications/
  src/
    Gma.Modules.Notifications.Contracts/
    Gma.Modules.Notifications.Application/
    Gma.Modules.Notifications.Persistence/
  tests/
    Gma.Modules.Notifications.Tests/
  docs/
  eng/
  Gma.Modules.Notifications.slnx

my-business-app/
  MyBusinessApp.slnx
```

The source dependency lock is the parent application's submodule pointer. Updating GMA in an application is a normal app commit that moves one or more submodule commits.

## Module Split Decision

Split reusable modules into their own repositories only after the current repository proves the `Gma.*` rename and source layout.

Recommended first reusable module repositories:

```text
gma-module-auth
gma-module-administration
gma-module-notifications
gma-module-files
```

Consider later, after the first split is boring:

```text
gma-module-task-runtime
gma-module-tenancy
```

Keep teaching/example modules in the skeleton/composition repository for now:

```text
Catalog
Ordering
TaskSamples
```

Those examples are valuable because they prove cross-module contracts, caching, tasks, projection rebuilds, messaging, admin surfaces, and API composition. They are not reusable product modules yet.

Tenancy needs a deliberate split decision during implementation:

- `Gma.Framework.Tenancy.*` should stay in the framework because many optional capabilities bridge to tenant context.
- The optional tenant API/module front door can become `Gma.Modules.Tenancy.*` only if it grows into reusable module behavior beyond framework composition.
- Do not let module repositories depend on a tenancy module repo just to use tenant abstractions; tenant abstractions belong in the framework.

## Source Reference Strategy

Module repositories need to build in two contexts:

- standalone module development;
- nested inside a production application with a separately checked-out framework submodule.

Avoid hardcoding only one path shape. Introduce an explicit source-root convention before repository extraction.

Recommended convention:

```text
GmaFrameworkRoot
GmaModulesRoot
GmaModuleAuthRoot
GmaModuleAdministrationRoot
GmaModuleCatalogRoot
GmaModuleNotificationsRoot
GmaModuleFilesRoot
GmaModuleOrderingRoot
GmaModuleTaskRuntimeRoot
GmaModuleTaskSamplesRoot
GmaModuleTenancyRoot
```

Each reusable module repository should import an ignored local source-roots file from its root `Directory.Build.props` if it exists:

```xml
<Import Project="$(MSBuildThisFileDirectory)Gma.SourceRoots.props"
        Condition="Exists('$(MSBuildThisFileDirectory)Gma.SourceRoots.props')" />
```

The checked-in fallback can point to the normal standalone development layout. Application bootstrap scripts can generate ignored `Gma.SourceRoots.props` files inside submodules so Visual Studio, Rider, the .NET CLI, and CI all resolve the same framework root.

When `Gma.SourceRoots.props` uses relative paths, anchor them with `$(MSBuildThisFileDirectory)`. Bare relative values such as `..\..\gma-framework\...` are later consumed by `ProjectReference` items and can be evaluated from the consuming project directory instead of the props file directory.

Cross-module project references must use the per-module root properties, for example `$(GmaModuleCatalogRoot)Catalog.Contracts\Catalog.Contracts.csproj`, not physical sibling paths such as `..\..\Catalog\...`. The physical path works in the current monorepo, but it breaks as soon as a module is checked out as an independent source repository.

This avoids one bad tradeoff:

- no duplicate nested framework submodule per module in production apps;
- no forced NuGet dependency while framework source is still evolving;
- no brittle assumption that every clone lives in the same parent folder.

Independent repositories should be flattened around their own product root. The monorepo staging layout can keep `src\Framework\...` and `src\Modules\Auth\...`, but extracted repositories should not keep those parent folders inside themselves. For example, the Auth repository should contain `src\Gma.Modules.Auth.Application\Gma.Modules.Auth.Application.csproj`, not `src\Modules\Auth\Gma.Modules.Auth.Application\Gma.Modules.Auth.Application.csproj`.

In a business app checkout, the submodule folder can be ergonomic even if the remote repository name is longer:

```text
gma/modules/auth                # checkout of SadPossum/gma-module-auth
gma/modules/administration      # checkout of SadPossum/gma-module-administration
```

The corresponding source-root values should point at the flattened package `src` folder:

```xml
<GmaFrameworkRoot>$(MSBuildThisFileDirectory)gma\framework\src\</GmaFrameworkRoot>
<GmaModuleAuthRoot>$(MSBuildThisFileDirectory)gma\modules\auth\src\</GmaModuleAuthRoot>
<GmaModuleAdministrationRoot>$(MSBuildThisFileDirectory)gma\modules\administration\src\</GmaModuleAdministrationRoot>
```

## Stage 0: Preflight And Decision Lock

Goal: freeze the intended migration shape before touching thousands of names.

Actions:

- Confirm the current `dev` branch is clean and pushed.
- Create a branch for the rebrand, for example `codex/gma-rebrand-source-split`.
- Add this document and, if useful, an ADR that records `Gma.*`, source-first submodules, and `.slnx`.
- Decide whether the current all-up repository becomes `gma-skeleton`, `gma-composition`, or remains `GenericModularApi` until extraction.
- Decide whether public package IDs will be `Gma.*` or `SadPossum.Gma.*` later. This does not block source-first work.

Validation:

```powershell
git status --short --branch
dotnet build GenericModularApi.slnx --no-restore -m:1
dotnet test GenericModularApi.slnx --no-build --logger "console;verbosity=minimal"
```

Agent goal:

```text
Inspect the current GenericModularApi repo, docs, solution, branch state, and architecture guards. Lock the GMA rebrand/source-split decisions in docs without changing code behavior yet. Preserve the modular optional architecture, keep source-first submodules as the preferred future development model, and add any ADR/planning notes needed before a broad rename.
```

## Stage 1: Rename Code Identity To Gma.*

Goal: make the current repository compile with `Gma.*` project, assembly, namespace, and test identity while behavior stays unchanged.

Actions:

- Rename namespaces from `Shared.*` to `Gma.Framework.*`.
- Rename reusable modules from `Auth.*`, `Administration.*`, `Notifications.*`, and `Files.*` to `Gma.Modules.<Module>.*`.
- Rename optional module contracts and metadata consistently.
- Rename test namespaces and `InternalsVisibleTo` values.
- Rename project files and update project references.
- Update all `using` directives.
- Update scripts, docs, architecture guards, module metadata tests, dependency allow-lists, and solution entries.
- Keep runtime logical module names stable unless there is a deliberate reason to change them. For example, the Auth module can still have a logical module name of `Auth`; the code identity becomes `Gma.Modules.Auth`.
- Keep runtime configurable prefixes configurable. Do not hardwire `gma` back into application identity where the previous configurable identity work intentionally made it project-specific.

Suggested target shape:

```text
src/Framework/Gma.Framework.Results/Gma.Framework.Results.csproj
src/Framework/Gma.Framework.Cqrs/Gma.Framework.Cqrs.csproj
src/Framework/Gma.Framework.Messaging/Gma.Framework.Messaging.csproj

src/Modules/Auth/Gma.Modules.Auth.Contracts/Gma.Modules.Auth.Contracts.csproj
src/Modules/Auth/Gma.Modules.Auth.Application/Gma.Modules.Auth.Application.csproj
src/Modules/Auth/Gma.Modules.Auth.Api/Gma.Modules.Auth.Api.csproj
```

Validation:

```powershell
rg "namespace Shared|using Shared|Shared\." src tests docs eng
rg "namespace Auth|using Auth|Auth\." src tests docs eng
rg "InternalsVisibleTo\(\"Shared|InternalsVisibleTo\(\"Auth|InternalsVisibleTo\(\"Notifications|InternalsVisibleTo\(\"Files" src tests
dotnet restore GenericModularApi.slnx
dotnet build GenericModularApi.slnx --no-restore -m:1
dotnet test GenericModularApi.slnx --no-build --logger "console;verbosity=minimal"
```

Expected rough edges:

- Old names may remain in changelog-style docs or migration history names if changing them would break historical meaning.
- EF migration class names can usually stay if they are internal history, but project/namespace names should still align.
- Architecture tests will need a careful update because many of them intentionally encode current project names and references.

Agent goal:

```text
Perform the first broad GMA identity rename in the current repo: Shared.* becomes Gma.Framework.*, reusable module projects become Gma.Modules.<Module>.*, and all project files, namespaces, usings, InternalsVisibleTo values, tests, scripts, docs, and architecture guards are aligned. Keep behavior and runtime logical module names stable unless explicitly documented. Validate with restore, build, full no-build tests, and source-wide searches for stale identity names.
```

## Stage 2: Rebrand Shared As Framework

Goal: make `Framework` the conceptual and folder boundary for reusable GMA primitives.

Actions:

- Move `src/Shared` to `src/Framework`.
- Move `tests/Shared.Tests` to `src/Framework/tests/Gma.Framework.Tests`.
- Update documentation wording from "shared packages" to "framework packages" where it refers to reusable GMA code.
- Keep "shared" as a generic concept only when talking about application-owned shared code or .NET shared concepts.
- Update solution folders and architecture diagrams.
- Keep `src/Framework/eng/new-module.ps1` as the framework-owned scaffolder implementation, with skeleton repositories exposing a thin `eng/new-module.ps1` wrapper that passes their composition root and composition solution filename.
- Update dependency boundary tests from "Shared" language to "Framework" language.

Validation:

```powershell
rg "src\\Shared|src/Shared|tests\\Shared.Tests|tests/Shared.Tests" .
rg "shared package|Shared package|Shared project|shared-project" docs tests src
dotnet build GenericModularApi.slnx --no-restore -m:1
dotnet test GenericModularApi.slnx --no-build --logger "console;verbosity=minimal"
```

Agent goal:

```text
Rebrand the current Shared area into the GMA Framework area. Move folders, tests, solution folders, docs, scripts, and architecture guard language so reusable platform code is consistently described as framework code. Preserve all optional capability boundaries and keep business-application shared code conceptually separate from GMA framework code.
```

## Stage 3: Module Repository Readiness

Goal: make reusable modules clean enough to be extracted without dragging skeleton-only code or other modules across boundaries.

Actions:

- Add a module repository readiness checklist to each reusable module doc.
- Ensure each reusable module depends only on framework projects and its own projects.
- Ensure cross-module communication uses contracts/events only.
- Verify `Administration` does not depend on Auth internals.
- Verify `Notifications` does not define business notification visibility rules beyond its own delivery/read state.
- Verify `Files` remains storage/front-door focused and does not pretend to be a generic business document/ACL system.
- Decide whether `TaskRuntime` is extracted now or later.
- Decide whether `Gma.Modules.Tenancy.Api` is a module repo or stays with framework/skeleton for now.
- Keep examples out of reusable module repositories.

Validation:

```powershell
dotnet test tests\Architecture.Tests\Architecture.Tests.csproj --no-build --filter "FullyQualifiedName~ModuleBoundaryTests|FullyQualifiedName~DeveloperExperienceGuardTests" --logger "console;verbosity=minimal"
rg "ProjectReference.*Modules" src/Modules/Auth src/Modules/Administration src/Modules/Notifications src/Modules/Files -g "*.csproj"
```

Agent goal:

```text
Audit reusable modules for repository extraction readiness. Tighten dependencies, docs, architecture tests, and module boundaries so Auth, Administration, Notifications, and Files can each become independent source repositories depending only on Gma.Framework.* and their own projects. Keep Catalog, Ordering, and TaskSamples as examples in the skeleton/composition repo.
```

## Stage 4: Switch Current Repository To SLNX

Goal: make `.slnx` the normal solution format for the all-up repository before adding per-repo solutions.

Actions:

- Run `dotnet sln GenericModularApi.sln migrate`.
- Rename the all-up solution to the chosen current repo identity if needed, for example `Gma.Skeleton.slnx` or `GenericModularApi.slnx`.
- Update build/test scripts and docs to use `.slnx`.
- Update CI references.
- Remove the legacy `.sln` only after IDE, CLI, and scripts are verified. Done in the current repo after `GenericModularApi.slnx`, focused package `.slnx` files, and architecture checks passed.

Validation:

```powershell
dotnet sln GenericModularApi.slnx list
dotnet restore GenericModularApi.slnx
dotnet build GenericModularApi.slnx --no-restore -m:1
dotnet test GenericModularApi.slnx --no-build --logger "console;verbosity=minimal"
rg "GenericModularApi\.sln([^x]|$)" docs eng .github README.md
```

Agent goal:

```text
Migrate the repository from legacy .sln to .slnx using the supported dotnet CLI flow. Update scripts, docs, validation commands, and CI references. Remove the legacy solution only after restore, build, test, and solution listing prove the .slnx is complete.
```

## Stage 5: Add Framework And Module SLNX Entrypoints In The Current Repo

Goal: prove each future repository has a standalone build/test front door before physically splitting Git repositories.

Add these solution files inside the current repo first:

```text
Gma.Framework.slnx
Gma.Modules.Auth.slnx
Gma.Modules.Administration.slnx
Gma.Modules.Notifications.slnx
Gma.Modules.Files.slnx
Gma.Modules.TaskRuntime.slnx
Gma.Modules.Tenancy.slnx
```

Keep two entrypoints per package while the monorepo is still the staging area:

- root focused solutions such as `Gma.Modules.Auth.slnx`, with paths rooted at the skeleton checkout;
- package-local mirrors such as `src/Modules/Auth/Gma.Modules.Auth.slnx`, with paths local to the future module repository root.

The root focused solutions are convenient for all-up validation from the skeleton. The package-local mirrors are convenient for editing a framework or module in isolation and should move with the package when it becomes its own repository.

Each module `.slnx` should include:

- that module's source projects;
- its focused tests when the module already has a focused test project;
- any migration projects owned by that module.
- the module-owned docs index.

Each module `.slnx` should not include:

- application hosts;
- example modules;
- framework projects as solution entries; framework dependencies are resolved through `GmaFrameworkRoot` project references;
- unrelated reusable modules except through contract-only references if that module truly owns such a dependency.

Catalog, Ordering, and TaskSamples are compiled skeleton examples, not reusable GMA modules. Do not create `Gma.Modules.Catalog.slnx`, `Gma.Modules.Ordering.slnx`, `Gma.Modules.TaskSamples.slnx`, or package-local mirrors for them unless they are deliberately promoted to reusable packages later.

Validation:

```powershell
dotnet restore Gma.Framework.slnx
dotnet build Gma.Framework.slnx --no-restore -m:1
dotnet test Gma.Framework.slnx --no-build --logger "console;verbosity=minimal"

dotnet restore Gma.Modules.Auth.slnx
dotnet build Gma.Modules.Auth.slnx --no-restore -m:1
dotnet test Gma.Modules.Auth.slnx --no-build --logger "console;verbosity=minimal"
```

Repeat for each reusable module solution.

Focused framework/module tests should live under the future source repository root, for example:

```text
src/Framework/tests/Gma.Framework.Tests/Gma.Framework.Tests.csproj
src/Modules/Auth/tests/Gma.Modules.Auth.Tests/Gma.Modules.Auth.Tests.csproj
```

Agent goal:

```text
Add standalone .slnx entrypoints for the future framework and reusable module repositories while still inside the current monorepo. Each .slnx must include only the projects that future repo owns, source-owned docs, plus necessary framework dependencies and focused tests. Validate every new solution independently and keep the all-up solution green.
```

## Stage 6: Source Root And Submodule Development Tooling

Goal: make source-first composition pleasant before creating separate repositories.

Actions:

- Add `Gma.SourceRoots.props.example`.
- Add ignored `Gma.SourceRoots.props` to `.gitignore`.
- Update project references in future module repos to resolve framework roots through `GmaFrameworkRoot`.
- Add scripts:
  - `eng/gma-status.ps1`
  - `eng/gma-bootstrap.ps1`
  - `eng/gma-update.ps1`
  - `eng/gma-validate.ps1`
- Document common flows:
  - clone all submodules;
  - edit framework from a production app checkout;
  - push framework PR from inside a submodule;
  - update the parent app submodule pointer;
  - recover detached HEAD in a submodule;
  - verify dirty nested repositories before committing an app pointer update.

Validation:

```powershell
eng\gma-bootstrap.ps1 -WhatIf
eng\gma-status.ps1
eng\gma-validate.ps1
```

Agent goal:

```text
Add source-root and submodule development tooling for future GMA source dependencies. Make framework/module project references configurable for standalone repo development and nested production-app submodule development. Document and test bootstrap, status, update, validation, dirty-check, and PR workflows without creating external repositories yet.
```

## Stage 7: Repository Extraction Dry Run

Goal: prove repository extraction from a fresh clone before creating or pushing real GitHub repositories.

Actions:

- Work from a fresh clone, not the active working tree.
- On Windows, keep the dry-run root short, for example `.agents\sr\1`, and clone with `core.longpaths=true`.
- Install `git-filter-repo` into an ignored local tools folder if it is not available globally.
- Extract framework history into a local `gma-framework` repository.
- Extract each reusable module history into local module repositories.
- Preserve authorship and tags where practical.
- Keep a rollback branch and do not rewrite the current source repository history.
- Validate each extracted repository with its own `.slnx`.
- Validate a local composition app that consumes extracted repositories as source dependencies and builds the all-up app.
- Keep module `.slnx` files ownership-only: they should list only module-owned projects, focused tests, and the module-owned docs index. Framework dependencies come from `GmaFrameworkRoot`, not from framework projects listed inside the module solution.
- Run `eng/check-source-packages.ps1` in the composition repository before and after extraction-oriented moves. It verifies focused `.slnx` ownership, stale root docs/tests, package-local docs/tests/scripts, and focused package builds.

Extraction options:

- Prefer `git filter-repo` in a fresh clone for precise history extraction if available.
- Use `git subtree split` only if it is enough for the desired history and produces simpler local results.
- If history preservation is not worth the cost for some module, create a clean initial import commit and document that decision.

Validation:

```powershell
git status --short --branch
dotnet restore Gma.Framework.slnx
dotnet build Gma.Framework.slnx --no-restore -m:1
dotnet test Gma.Framework.slnx --no-build --logger "console;verbosity=minimal"
```

Repeat for each extracted module repository and the local composition app. In the source monorepo, use this shortcut before a full extraction run:

```powershell
.\eng\check-source-packages.ps1 -SkipRestore
```

Local dry-run result:

- A previous history-preserving dry run proved `git filter-repo` when installed into ignored local tooling and run from a short sandbox path.
- A first long sandbox path failed on Windows checkout because some generated paths exceeded the default path length limit.
- The successful history extraction used `git clone -c core.longpaths=true` and the short `.agents\sr\1` root.
- `gma-framework` restored, built, and passed its focused tests from the extracted repository.
- Administration, Auth, Files, Notifications, TaskRuntime, and Tenancy restored and built from focused package entrypoints against the extracted framework root; package-local focused tests passed where present. Compiled example modules remained skeleton-owned and were validated through the all-up composition.
- A local composition clone restored and built the all-up `GenericModularApi.slnx` while `src\Framework` and reusable module folders were directory junctions to the extracted source repositories.

Current source-shape rehearsal result on 2026-07-07:

- The rehearsal used `.agents\sr\3`, `git clone -c core.longpaths=true`, and a working-tree overlay from the active repository.
- `git filter-repo` was not required for this pass because the goal was source-dependency shape, not history rewriting.
- Snapshot local source-package directories were created for `gma-framework` plus Administration, Auth, Files, Notifications, TaskRuntime, and Tenancy.
- Each source package restored and built from its own focused `.slnx`.
- The local composition clone then replaced `src\Framework` and `src\Modules\<Module>` with directory junctions to those source-package directories.
- After restoring the composition clone to refresh `obj` assets for the junction-backed paths, `.\eng\check-source-packages.ps1 -SkipRestore` and `dotnet build GenericModularApi.slnx --no-restore -m:1` both passed.
- The rehearsal produced one real repo hardening fix: all modules now have explicit source-root properties, Ordering cross-module project references use `$(GmaModuleCatalogRoot)` and `$(GmaModuleNotificationsRoot)`, and architecture tests reject physical cross-module project references.

Stage 8A local candidate result on 2026-07-07:

- The run used `.agents\stage8a\1`; all generated repositories and composition clones are ignored local artifacts.
- `git filter-repo` was not available in the active shell, so this pass intentionally used snapshot import commits instead of history-preserving extraction.
- Local repositories were created for `gma-framework`, `gma-module-administration`, `gma-module-auth`, `gma-module-files`, `gma-module-notifications`, and `gma-module-task-runtime`.
- Each local repository has `main` and `dev` branches pointing to the validated import commit, plus repo-local `README.md`, `.github\workflows\ci.yml`, and `eng\validate.ps1`.
- Module repositories also include `eng\bootstrap-source-roots.ps1` so CI or a local consumer can point them at a sibling framework checkout and optional sibling module checkout root.
- Each candidate repository restored, built, and ran its focused tests through `.\eng\validate.ps1`.
- The sandbox composition under `.agents\stage8a\1\composition` replaced `src\Framework` plus the reusable module folders with directory junctions to those candidate repositories.
- The sandbox composition passed `dotnet restore GenericModularApi.slnx`, `.\eng\check-source-packages.ps1 -SkipRestore -SkipBuild`, `dotnet build GenericModularApi.slnx --no-restore -m:1`, and `dotnet test tests\Architecture.Tests\Architecture.Tests.csproj --no-build --logger "console;verbosity=minimal"`.
- The candidate CI workflow is a draft. It checks out framework source for module builds; if a reusable module later depends on another reusable module's contracts, that module CI should also check out the needed sibling module repository and pass `-ModuleReposRoot` to `eng\bootstrap-source-roots.ps1`.
- Stage 8B should repeat this with real GitHub repositories and either `git filter-repo`/`git subtree split` history extraction or an explicit initial-import decision per repository.

Stage 8B start result on 2026-07-07:

- The source-split readiness work was committed to `dev` and pushed to `origin/dev`.
- `gh` was not installed in the active shell, and no `GITHUB_TOKEN`/`GH_TOKEN` was available, so repository creation could not be performed from this environment.
- `git ls-remote` returned `Repository not found` for the intended `SadPossum/gma-framework`, reusable module, and `gma-skeleton` repositories, confirming they still need to be created before candidate repos can be pushed.
- A local submodule proof intentionally checked whether candidate repos could be mounted directly at `src\Framework` and `src\Modules\<Module>`. That does not work because each candidate repository owns its own root files and keeps package code under `src\Framework` or `src\Modules\<Module>` inside the repo.
- Stage 9 should mount source repositories under stable dependency paths such as `gma\framework` and `gma\modules\<module>`, then update solution paths, source-root defaults, and source-package/architecture guards to consume flattened package roots such as `gma\framework\src` and `gma\modules\auth\src`.
- Do not submodule a candidate repository directly into the old source-package folder unless that repository is intentionally flattened first.

Stage 8C source-root composition proof on 2026-07-07:

- A local-only skeleton rehearsal used `gma\framework` plus repo-named module folders such as `gma\modules\gma-module-auth`.
- Repo-named module folders worked with the generated module-local `eng\bootstrap-source-roots.ps1`, but this is not the desired final developer experience. Real application checkouts should prefer short aliases such as `gma\modules\auth`; bootstrap scripts must support those aliases explicitly.
- Each module repository keeps its own `Directory.Build.props`, so a root-level `Gma.SourceRoots.props` in the skeleton does not flow into module-local builds. Run each module repository's `eng\bootstrap-source-roots.ps1` when validating a module checkout outside the monorepo.
- Host and root test projects now use per-module source-root properties such as `$(GmaModuleAuthRoot)` instead of physical `..\Modules\Auth\...` references.
- `dotnet restore GenericModularApi.slnx` and `dotnet build GenericModularApi.slnx --no-restore -m:1` passed in the local skeleton rehearsal after `src\Framework` and reusable module folders were removed and replaced by `gma\...` source repositories.
- `tests\Architecture.Tests` still contains many monorepo file-path probes such as `src\Framework` and `src\Modules\Auth`. In the split skeleton rehearsal the architecture test assembly built, but the tests failed because those probes could not find files that had moved under source repositories.
- Before Stage 9 can require `dotnet test Gma.Skeleton.slnx`, either move framework/module-specific architecture tests into their owning repositories or make the all-up architecture test helpers resolve `GmaFrameworkRoot` and `GmaModule*Root` source roots.

Stage 8D target: flatten source repositories before real submodules:

- Rebuild local candidate repositories so package roots look like normal repositories:
  - `gma-framework\src\Gma.Framework.Results\...`
  - `gma-framework\docs\...`
  - `gma-framework\tests\Gma.Framework.Tests\...`
  - `gma-module-auth\src\Gma.Modules.Auth.Application\...`
  - `gma-module-auth\docs\...`
  - `gma-module-auth\tests\Gma.Modules.Auth.Tests\...`
- Do not preserve `src\Framework\...` or `src\Modules\<Module>\...` inside the independent repositories.
- Mount module repositories in the skeleton under short aliases such as `gma\modules\auth`, while keeping the remote repository names `gma-module-auth`, `gma-module-administration`, and so on.
- Update root `Gma.SourceRoots.props` for the skeleton rehearsal so module roots point to flattened package `src` folders, for example `gma\modules\auth\src\`.
- Move or copy architecture tests by ownership:
  - framework dependency, docs, scaffolder, naming, package-shape, and optional-adapter guards move to the framework repository;
  - module-specific guards move to the owning module repository;
  - skeleton tests keep only composition guards, selected-module wiring, physical-path bans, source-root bootstrap checks, and cross-module integration tests.
- If helper code is duplicated across package test suites, extract a tiny framework-owned test helper project such as `Gma.Framework.ArchitectureTesting` under the framework repository's `tests` folder.
- Rerun the split skeleton restore/build/test proof only after the test ownership move is complete.

Stage 8D flattened local rehearsal result on 2026-07-08:

- The run used `.agents\stage8d\2`; all generated repositories and skeleton composition folders are ignored local artifacts.
- Local candidate repositories were generated with normal standalone roots:
  - `gma-framework\src\Gma.Framework.*`, `gma-framework\docs`, `gma-framework\tests`, and `gma-framework\eng`;
  - `gma-module-<module>\src\Gma.Modules.<Module>.*`, plus module-owned `docs` and `tests` where present.
- Package-local test projects were hardened so package-owned references use source-root properties such as `$(GmaFrameworkRoot)`, `$(GmaModuleAuthRoot)`, and `$(GmaModuleNotificationsRoot)` instead of monorepo-relative `..\..\Gma.*` paths.
- The flattened `gma-framework`, Administration, Auth, Files, Notifications, TaskRuntime, and Tenancy candidates each passed restore, build, and package-local tests where present.
- The skeleton composition mounted candidates under short aliases:
  - `gma\framework`;
  - `gma\modules\administration`;
  - `gma\modules\auth`;
  - `gma\modules\files`;
  - `gma\modules\notifications`;
  - `gma\modules\task-runtime`;
  - `gma\modules\tenancy`.
- The skeleton rehearsal removed the old reusable package folders from `src\Framework` and `src\Modules\<ReusableModule>`, rewrote `GenericModularApi.slnx` project/file paths to the `gma\...` mount points, and generated a root `Gma.SourceRoots.props` for hosts, examples, and root tests.
- Module projects use their own repository-local `Directory.Build.props`, so the skeleton root `Gma.SourceRoots.props` does not flow into mounted module projects. The rehearsal therefore generated app-context `Gma.SourceRoots.props` files inside each mounted module root, pointing back to `gma\framework\src` and the short module aliases. This is now automated by `eng\gma-bootstrap.ps1 -SourceLayout GmaSubmodules`.
- The app-style skeleton rehearsal passed `dotnet restore GenericModularApi.slnx` and `dotnet build GenericModularApi.slnx --no-restore -m:1`.
- The split-skeleton architecture test project now resolves `Gma.SourceRoots.props` and passed in the flattened rehearsal. Package-owned architecture tests should still eventually move or be copied to their owning repositories so the skeleton keeps only composition-focused guards.

Agent goal:

```text
Perform a local-only repository extraction dry run from a fresh clone. Produce local gma-framework and reusable module repositories with their own .slnx files, docs, tests, and preserved history where practical. Validate each extracted repo independently and validate a local app-style composition that consumes them as source submodules. Do not push or rewrite the main repository until the dry run is proven.
```

## Stage 8: Create GitHub Repositories And Configure Git

Goal: create the real independent repositories and wire the source-first workflow.

Recommended repositories:

```text
SadPossum/gma-framework
SadPossum/gma-module-auth
SadPossum/gma-module-administration
SadPossum/gma-module-notifications
SadPossum/gma-module-files
SadPossum/gma-module-task-runtime
SadPossum/gma-skeleton
```

Actions:

- Create GitHub repositories.
- Push extracted histories.
- If using the local Stage 8A snapshot candidates, add each real repository as `origin` under `.agents\stage8a\1\repos\<repo>` and push `dev` plus `main` after the GitHub repository exists.
- Configure `main` and `dev` branch model consistently.
- Set default branch policy. If development happens on `dev`, make `dev` the default branch for work repos.
- Add branch protection and required checks.
- Add repository descriptions, topics, license/readme alignment, and docs links.
- Add CODEOWNERS if useful.
- Configure Actions:
  - framework repo checks only framework `.slnx`;
  - module repos check out framework source and validate their module `.slnx`;
  - skeleton/composition repo checks submodules recursively and validates all-up composition.
- For private submodules in GitHub Actions, configure PAT or SSH secrets with read access to the needed repositories.

Validation:

```powershell
git ls-remote git@github.com-private:SadPossum/gma-framework.git
git ls-remote git@github.com-private:SadPossum/gma-module-auth.git
git clone --recurse-submodules git@github.com-private:SadPossum/gma-skeleton.git
dotnet restore Gma.Skeleton.slnx
dotnet build Gma.Skeleton.slnx --no-restore -m:1
dotnet test Gma.Skeleton.slnx --no-build --logger "console;verbosity=minimal"
```

Agent goal:

```text
Create and configure the real GitHub repositories for GMA framework, reusable modules, and skeleton/composition. Push the proven extracted histories, configure branch model and protections, add CI for standalone and composition validation, and prove a clean clone with recursive submodules can restore, build, and test.
```

## Stage 9: Replace Current Monorepo Internals With Submodules

Goal: turn the skeleton/composition repository into the app-like consumer of the independent source repositories.

Prerequisite: Stage 8D has produced flattened framework/module repositories with root-level `docs/`, `tests/`, `eng/`, and `src/` folders, and package-owned architecture tests have moved or been copied to their owning repositories.

Actions:

- Replace framework source folder with a `gma/framework` submodule whose package projects live under `gma/framework/src/`.
- Replace reusable module source folders with short alias submodules such as `gma/modules/auth`, `gma/modules/administration`, `gma/modules/notifications`, and `gma/modules/files`.
- Keep skeleton hosts, examples, app-specific docs, and composition tests in the skeleton repo.
- Update `Gma.Skeleton.slnx` to reference projects from submodule paths.
- Generate source-root values that point to flattened package roots, for example `$(MSBuildThisFileDirectory)gma\modules\auth\src\`.
- Update docs so new production apps can copy the skeleton pattern.
- Ensure module repos do not accidentally include the skeleton's examples or host-specific code.
- Keep skeleton architecture tests composition-focused; do not require them to inspect framework/module repository internals by physical monorepo paths.

Validation:

```powershell
git submodule status --recursive
git status --short --branch
dotnet restore Gma.Skeleton.slnx
dotnet build Gma.Skeleton.slnx --no-restore -m:1
dotnet test Gma.Skeleton.slnx --no-build --logger "console;verbosity=minimal"
```

Agent goal:

```text
Convert the skeleton/composition repository into a source-first consumer of independent GMA framework and module repositories. Replace internal framework and reusable module source with Git submodules, update solution/project paths, docs, scripts, and CI, and prove a clean recursive-submodule clone validates the same behavior as the monorepo did.
```

## Stage 10: Production App Template And Operating Rules

Goal: make the pattern easy to reuse for the next real business application.

Actions:

- Add a production app template or docs page showing:
  - app-owned shared project naming;
  - GMA submodule layout;
  - app `.slnx` composition;
  - host composition;
  - local bootstrap;
  - update and PR workflow;
  - CI setup for private submodules.
- Add a "how to patch GMA from an app" guide.
- Add a "how to update GMA in an app" guide.
- Add a "when to upstream vs app-local override" guide.
- Add a "do not edit detached HEAD" warning for submodules.

Validation:

```powershell
eng\new-gma-app.ps1 -Name SampleApp -OutputPath .tmp\SampleApp
cd .tmp\SampleApp
eng\gma-bootstrap.ps1
dotnet restore SampleApp.slnx
dotnet build SampleApp.slnx --no-restore -m:1
```

Agent goal:

```text
Create the production-app source composition template and operating docs. The template should let a new app consume selected GMA framework and module repositories as editable submodules, keep its own app-owned shared code, compose only chosen modules, and validate with a clean bootstrap/build workflow.
```

## Verification Checklist For The Whole Migration

The migration is not complete until all of these are true:

- No active source projects use `Shared.*` namespaces for GMA framework code.
- Current all-up solution is `.slnx`.
- Framework has its own `.slnx`.
- Each reusable module repo or repo-ready slice has its own `.slnx`.
- Each reusable module can validate without the all-up skeleton solution.
- Business-app style composition can reference selected framework/module source projects.
- Submodule bootstrap/status/update scripts exist and are documented.
- CI can clone submodules and validate from a clean checkout.
- Architecture tests still enforce optional module boundaries.
- Docs clearly separate:
  - GMA framework code;
  - reusable GMA modules;
  - example modules;
  - application-owned shared code.
- The repo split has a rollback path and was first tested in a fresh local clone.

## Risks And Guardrails

- Broad namespace/project renames can hide real behavior changes. Keep Stage 1 mechanical and behavior-neutral.
- `.slnx` migration can leave scripts and docs pointing at stale `.sln` commands. Search all docs and scripts.
- Submodules are powerful but easy to leave dirty or detached. Scripts should make submodule state visible before commits.
- Private submodules require explicit GitHub Actions credentials; the default repository token is not enough for unrelated private repos.
- Module repos should not become miniature monoliths. They must depend on framework packages/source and their own internals only.
- Avoid extracting examples into reusable module repositories too early.
- Keep NuGet package metadata possible, but do not force package-first development until source-first production feedback settles the framework.

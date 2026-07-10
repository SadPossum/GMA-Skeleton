# Source-First Apps

GMA is intended to be consumed as editable source while the framework and reusable modules are still evolving from real application pressure. A production app should own its business code and compose selected GMA repositories under stable mount paths.

Recommended app layout:

```text
MyProduct/
  MyProduct.slnx
  src/
    Hosts/
      MyProduct.Host.Api/
    Modules/
      Billing/
      Reservations/
    Shared/
      MyProduct.SharedKernel/
  gma/
    framework/              # submodule: SadPossum/GMA-Framework
    modules/
      access-control/       # submodule: SadPossum/GMA-Module-Access-Control
      auth/                 # submodule: SadPossum/GMA-Module-Auth
      administration/       # submodule: SadPossum/GMA-Module-Administration
      notifications/        # submodule: SadPossum/GMA-Module-Notifications
```

`src/Hosts` contains process entrypoints, `src/Modules` contains app-owned domain modules, and `src/Shared` contains the app-owned shared kernel. GMA framework code remains under `gma/framework`, and reusable GMA modules remain under `gma/modules/<alias>`. Do not put app-specific shared code inside GMA repositories just because the code is convenient today.

## Create An App

Generate a source-first shell:

```powershell
.\eng\new-gma-app.ps1 -Name MyProduct -OutputPath ..\MyProduct -Modules auth,notifications
```

Omit `-Modules` for a framework-only app shell. Use `-Modules all` only when you deliberately want every reusable module mounted for a full local proof. For selected modules that expose a public `IModule` front door, the generated API host adds the project reference and explicit module registration. AccessControl, Administration, TaskRuntime, and other admin/worker-only selections are mounted without silently adding those surfaces to the public API; add their hosts deliberately when the product needs them.

The generated shell is runnable and startup-tested. It includes production HTTP policy, liveness/readiness endpoints, local development settings, explicit SQL-provider migration tooling, an app-module scaffold wrapper, and architecture/startup tests. Selected Auth/Notifications databases contribute readiness checks; Files gets local storage for development and requires a content-inspection adapter in production. Cloud credentials, trusted proxy addresses, real secrets, messaging topology, external identity providers, admin hosts, workers, and retention policy remain deliberate app/deployment choices.

The generated `Directory.Packages.props` is seeded from the skeleton catalog so app-owned modules created later can restore immediately. Treat it as product-owned after generation: prune unused versions when convenient and add app-specific package versions deliberately.

The generated root `README.md` is intentionally app-facing and short. GMA-specific mount, update, CI, and upstreaming notes are generated into `docs/gma-source.md` so a product can replace the root README with product language without losing the source-first operating guide.

`gma-bootstrap.ps1` writes ignored `Gma.SourceRoots.props` files at the app root and inside mounted GMA repositories. Those files are local build configuration, not package history.
Use `.\eng\gma-bootstrap.ps1 -Force` after moving source mounts, switching a reusable GMA checkout between app shells, or changing the selected module set.

For a real app, add source repositories at the same mount paths as Git submodules, then bootstrap:

```powershell
git submodule add https://github.com/SadPossum/GMA-Framework.git gma/framework
git submodule add https://github.com/SadPossum/GMA-Module-Auth.git gma/modules/auth
.\eng\gma-update.ps1 -Init
.\eng\gma-bootstrap.ps1
.\eng\gma-validate.ps1
```

Use equivalent SSH or fork URLs when an app should track private forks instead of the public GMA repositories. Generated app docs include the selected submodule commands in `docs/gma-source.md`.

## Patch GMA From An App

When a production app exposes a framework or reusable-module gap:

1. Enter the mounted GMA repository, for example `gma/framework`.
2. Create a normal branch inside that repository.
3. Make the fix with package-owned tests and docs.
4. Push the GMA branch and open the GMA pull request.
5. Return to the app repository and update the submodule pointer only after the GMA branch or merge commit is the intended dependency.

Avoid editing a submodule while it is in detached `HEAD`. Check first:

```powershell
git -C gma/framework status --short --branch
```

If it shows `HEAD detached`, switch to a branch before editing:

```powershell
git -C gma/framework switch -c fix/my-framework-change
```

## Update GMA In An App

Updating GMA is an app dependency change. Treat it like any other production dependency update:

```powershell
git -C gma/framework fetch origin
git -C gma/framework switch dev
git -C gma/framework pull --ff-only
.\eng\gma-bootstrap.ps1 -Force
.\eng\gma-validate.ps1
git status --short
```

Commit the app's changed submodule pointer only after the app build and relevant tests pass.

## Upstream Or App-Local

Upstream to GMA when the behavior is reusable across multiple products, strengthens a framework seam, improves optional composition, or belongs to a reusable module's domain. Keep it app-local when the behavior depends on product-specific policies, data ownership, workflows, or UI language.

If the same app-local pattern repeats in a second product, write a short note and consider extracting a GMA extension point or reusable module change.

## CI Notes

CI for private submodules needs credentials that can read the selected GMA repositories. The app pipeline should:

- check out submodules recursively;
- run `eng/gma-bootstrap.ps1`;
- restore and build the app `.slnx`;
- run app tests plus any selected integration checks.

Checkout actions are commit-pinned and use `persist-credentials: false`. Keep those properties when updating the generated workflow.

Before releasing this skeleton source set, run `eng/export-source-set.ps1 -RequireClean` (or use its release-tag workflow) to capture the exact skeleton/framework/module commits, SDK, and central package hash. Generated applications can adopt the same manifest pattern in their own release workflow when they need an independently archived source bill of materials.

Generated app shells include `.github/workflows/validate.yml`. Set a repository secret named `GMA_CI_TOKEN` when the app consumes private GMA repositories from another repository boundary; the default `GITHUB_TOKEN` normally only has access to the current repository.

Do not rely on a developer's local `Gma.SourceRoots.props` in CI. Generate it during the pipeline and keep it untracked in app, framework, and module repositories.

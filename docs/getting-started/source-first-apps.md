# Source-First Apps

GMA is intended to be consumed as editable source while the framework and reusable modules are still evolving from real application pressure. A production app should own its business code and compose selected GMA repositories under stable mount paths.

Recommended app layout:

```text
MyProduct/
  MyProduct.slnx
  src/
    MyProduct.Host.Api/
    MyProduct.SharedKernel/
    Modules/
      Billing/
      Reservations/
  gma/
    framework/              # submodule: SadPossum/gma-framework
    modules/
      auth/                 # submodule: SadPossum/gma-module-auth
      administration/       # submodule: SadPossum/gma-module-administration
      notifications/        # submodule: SadPossum/gma-module-notifications
```

`SharedKernel` is app-owned. GMA framework code remains under `gma/framework`, and reusable GMA modules remain under `gma/modules/<alias>`. Do not put app-specific shared code inside GMA repositories just because the code is convenient today.

## Create An App

Generate a source-first shell:

```powershell
.\eng\new-gma-app.ps1 -Name MyProduct -OutputPath ..\MyProduct -Modules auth,notifications
```

For local proof before real GitHub repositories exist, use the Stage 8D candidates:

```powershell
.\eng\new-gma-app.ps1 -Name SampleApp -OutputPath .tmp\SampleApp -Modules auth,notifications -UseLocalStage8Candidates
cd .tmp\SampleApp
.\eng\gma-bootstrap.ps1 -Force
.\eng\gma-status.ps1
.\eng\gma-update.ps1
.\eng\gma-validate.ps1
```

Omit `-Modules` for a framework-only app shell. Use `-Modules all` only when you deliberately want every reusable module mounted for a full local proof. For selected modules that expose a public `IModule` front door, the generated API host adds the project reference and explicit module registration. Admin CLI/API and worker-only surfaces remain app-specific; add those hosts deliberately when the product needs them.

The generated shell is still a composition starting point. Runtime provider choices such as JWT/identity setup, storage adapters, messaging, production connection strings, migrations, admin hosts, and workers remain app-owned decisions.

`gma-bootstrap.ps1` writes ignored `Gma.SourceRoots.props` files at the app root and inside mounted GMA repositories. Those files are local build configuration, not package history.
Use `.\eng\gma-bootstrap.ps1 -Force` after moving source mounts, switching a reusable GMA checkout between app shells, or changing the selected module set.

For a real app, add source repositories at the same mount paths as Git submodules, then bootstrap:

```powershell
git submodule add git@github.com-private:SadPossum/gma-framework.git gma/framework
git submodule add git@github.com-private:SadPossum/gma-module-auth.git gma/modules/auth
.\eng\gma-update.ps1 -Init
.\eng\gma-bootstrap.ps1
.\eng\gma-validate.ps1
```

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

Generated app shells include `.github/workflows/validate.yml`. Set a repository secret named `GMA_CI_TOKEN` when the app consumes private GMA repositories from another repository boundary; the default `GITHUB_TOKEN` normally only has access to the current repository.

Do not rely on a developer's local `Gma.SourceRoots.props` in CI. Generate it during the pipeline and keep it untracked in app, framework, and module repositories.

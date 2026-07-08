# GenericModularApi Skeleton Documentation

This folder documents the skeleton/composition repository: how to run it, how it composes selected GMA source dependencies, and how the included example modules demonstrate application-level patterns.

Reusable framework and module documentation lives with the source that owns it. In the source-first skeleton layout those docs live under mounted GMA repositories:

- [GMA Framework Docs](../gma/framework/docs/README.md)
- [Administration Module](../gma/modules/administration/docs/README.md)
- [Auth Module](../gma/modules/auth/docs/README.md)
- [Files Module](../gma/modules/files/docs/README.md)
- [Notifications Module](../gma/modules/notifications/docs/README.md)
- [TaskRuntime Module](../gma/modules/task-runtime/docs/README.md)
- [Tenancy Module](../gma/modules/tenancy/docs/README.md)

## Start Here

- [Setup](getting-started/setup.md)
- [Local Development](getting-started/local-development.md)
- [Source-First Apps](getting-started/source-first-apps.md)
- [Skeleton Overview](architecture/overview.md)
- [GMA Rebrand And Source Repo Split Plan](architecture/gma-rebrand-and-source-repo-split.md)

## Examples

- [Catalog Example Module](../src/Modules/Catalog/docs/README.md)
- [Ordering Example Module](../src/Modules/Ordering/docs/README.md)
- [Cross-Module Integration](examples/cross-module-integration.md)
- [TaskSamples Example Module](../src/Modules/TaskSamples/docs/README.md)

## Ownership

- Keep skeleton docs in `docs/`.
- Keep reusable framework docs in `gma/framework/docs/` once framework source is mounted as a submodule.
- Keep reusable module docs in `gma/modules/<alias>/docs/` once modules are mounted as submodules.
- After extraction, move each framework/module docs tree to that repository's root `docs/` folder.
- Keep example-module docs in the example module folder; examples are skeleton-owned and do not get reusable package `.slnx` entrypoints.
- Keep focused framework/module tests beside the owning source root.
- Keep package-local `.slnx` entrypoints beside the owning framework/module source root so they can move with the package during extraction.
- Keep cross-module architecture and integration tests in the skeleton-level `tests/` folder.

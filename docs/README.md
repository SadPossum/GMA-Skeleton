# GenericModularApi Skeleton Documentation

This folder documents the skeleton/composition repository: how to run it, how it composes selected GMA source dependencies, and how the included example modules demonstrate application-level patterns.

Reusable framework and module documentation lives with the source that owns it:

- [GMA Framework Docs](../src/Framework/docs/README.md)
- [Administration Module](../src/Modules/Administration/docs/README.md)
- [Auth Module](../src/Modules/Auth/docs/README.md)
- [Files Module](../src/Modules/Files/docs/README.md)
- [Notifications Module](../src/Modules/Notifications/docs/README.md)
- [TaskRuntime Module](../src/Modules/TaskRuntime/docs/README.md)
- [Tenancy Module](../src/Modules/Tenancy/docs/README.md)

## Start Here

- [Setup](getting-started/setup.md)
- [Local Development](getting-started/local-development.md)
- [Skeleton Overview](architecture/overview.md)
- [GMA Rebrand And Source Repo Split Plan](architecture/gma-rebrand-and-source-repo-split.md)

## Examples

- [Catalog Example Module](../src/Modules/Catalog/docs/README.md)
- [Ordering Example Module](../src/Modules/Ordering/docs/README.md)
- [Cross-Module Integration](examples/cross-module-integration.md)
- [TaskSamples Example Module](../src/Modules/TaskSamples/docs/README.md)

## Ownership

- Keep skeleton docs in `docs/`.
- Keep reusable framework docs in `src/Framework/docs/`.
- Keep reusable module docs in `src/Modules/<Module>/docs/`.
- Keep example-module docs in the example module folder.
- Keep focused framework/module tests beside the owning source root.
- Keep cross-module architecture and integration tests in the skeleton-level `tests/` folder.

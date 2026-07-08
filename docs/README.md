# GenericModularApi Skeleton Documentation

This folder documents the skeleton/composition repository: how to run it, how it composes selected GMA source dependencies, and how the included example modules demonstrate application-level patterns.

Reusable framework and module documentation lives with the source that owns it. In this checkout those repositories are mounted under `gma/`, but GitHub cannot reliably render deep file links through a skeleton submodule path. Markdown links to reusable docs should point at the owning source repository on GitHub:

| Package | Source repository docs | Local checkout path |
| --- | --- | --- |
| GMA Framework | [Docs](https://github.com/SadPossum/GMA-Framework/blob/dev/docs/README.md) | `gma/framework/docs/README.md` |
| Administration | [Docs](https://github.com/SadPossum/GMA-Module-Administration/blob/dev/docs/README.md) | `gma/modules/administration/docs/README.md` |
| Auth | [Docs](https://github.com/SadPossum/GMA-Module-Auth/blob/dev/docs/README.md) | `gma/modules/auth/docs/README.md` |
| Files | [Docs](https://github.com/SadPossum/GMA-Module-Files/blob/dev/docs/README.md) | `gma/modules/files/docs/README.md` |
| Notifications | [Docs](https://github.com/SadPossum/GMA-Module-Notifications/blob/dev/docs/README.md) | `gma/modules/notifications/docs/README.md` |
| TaskRuntime | [Docs](https://github.com/SadPossum/GMA-Module-Task-Runtime/blob/dev/docs/README.md) | `gma/modules/task-runtime/docs/README.md` |
| Tenancy | [Docs](https://github.com/SadPossum/GMA-Module-Tenancy/blob/dev/docs/README.md) | `gma/modules/tenancy/docs/README.md` |

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
- Keep reusable framework docs in the framework repository's root `docs/` folder.
- Keep reusable module docs in the module repository's root `docs/` folder.
- Mention local mounted paths such as `gma/framework/docs/` when useful for local development, but do not make skeleton Markdown links point deep into `gma/...` submodule files.
- Keep example-module docs in the example module folder; examples are skeleton-owned and do not get reusable package `.slnx` entrypoints.
- Keep focused framework/module tests beside the owning source root.
- Keep package-local `.slnx` entrypoints beside the owning framework/module source root so they can move with the package during extraction.
- Keep cross-module architecture and integration tests in the skeleton-level `tests/` folder.

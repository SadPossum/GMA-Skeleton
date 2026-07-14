# GMA Source Mounts

This folder contains Git submodules, not files owned directly by the skeleton repository. GitHub cannot render deep blob paths such as `gma/framework/docs/README.md` from the skeleton repo because the parent repo stores each mount as a submodule pointer.

Use the source repositories for reusable package docs:

| Package | Source repository docs |
| --- | --- |
| Framework | [GMA-Framework docs](https://github.com/SadPossum/GMA-Framework/blob/dev/docs/README.md) |
| Administration | [Administration docs](https://github.com/SadPossum/GMA-Module-Administration/blob/dev/docs/README.md) |
| Auth | [Auth docs](https://github.com/SadPossum/GMA-Module-Auth/blob/dev/docs/README.md) |
| Files | [Files docs](https://github.com/SadPossum/GMA-Module-Files/blob/dev/docs/README.md) |
| Notifications | [Notifications docs](https://github.com/SadPossum/GMA-Module-Notifications/blob/dev/docs/README.md) |
| Organizations | [Organizations docs](https://github.com/SadPossum/GMA-Module-Organizations/blob/dev/docs/README.md) |
| TaskRuntime | [TaskRuntime docs](https://github.com/SadPossum/GMA-Module-Task-Runtime/blob/dev/docs/README.md) |
| Tenancy | [Tenancy docs](https://github.com/SadPossum/GMA-Module-Tenancy/blob/dev/docs/README.md) |

For local development, initialize and refresh the submodules:

```powershell
.\eng\gma-update.ps1 -Init
.\eng\gma-bootstrap.ps1 -SourceLayout GmaSubmodules
```

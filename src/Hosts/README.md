# Hosts

This folder contains process entrypoints and composition roots.

- `Host.Api` composes the public HTTP API.
- `Host.AdminApi` composes optional administrative HTTP APIs.
- `Host.AdminCli` composes the optional administration CLI.
- `Host.Worker` composes optional background publishing, consumers, and task workers.
- `AppHost` composes local Aspire infrastructure and runnable hosts.

Keep domain modules under `src/Modules` and reusable host support such as service defaults under sibling `src` folders.

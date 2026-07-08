# Skeleton Overview

GMA-Skeleton is the composition/template repository for building applications on top of the GMA framework and reusable modules.

The skeleton owns:

- host composition examples;
- local development and setup guidance;
- example modules such as Catalog, Ordering, and TaskSamples;
- repository split planning and source-first workflow docs;
- cross-module, integration, and architecture tests that prove application-level composition.

Reusable docs live beside reusable source. In local checkouts those source repositories are mounted under `gma/`, but GitHub cannot reliably render deep file links through a skeleton submodule path. Use the owning source repositories for reusable docs:

- [GMA Framework architecture](https://github.com/SadPossum/GMA-Framework/blob/dev/docs/architecture/overview.md)
- [GMA Framework documentation index](https://github.com/SadPossum/GMA-Framework/blob/dev/docs/README.md)
- [Reusable module docs](../README.md#reusable-framework-and-module-documentation-lives-with-the-source-that-owns-it)

The skeleton should not become the canonical home for framework or reusable-module behavior. It can link to those docs, compose those projects, and prove that selected modules work together.

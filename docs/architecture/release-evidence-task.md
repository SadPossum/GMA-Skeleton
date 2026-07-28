# Repository Release Evidence Task

**Status:** Reusable source evidence and the GMA source-repository rollout are
implemented; product rollout is in progress

## Goal

Provide reusable, fail-closed release evidence for source-first GMA repositories
and generated applications. A tagged release must identify one exact clean
commit, publish deterministic artifacts and checksums, attach a source SBOM,
and create verifiable GitHub provenance and SBOM attestations.

This task completes the release-evidence part of the repository supply-chain
baseline. It does not turn Framework or a domain module into a governance,
package-registry, or product-release system.

## Audit Baseline

The 2026-07-28 audit found:

- GMA is consumed from pinned source repositories and submodules, not from a
  published NuGet package set.
- Existing GMA GitHub releases are source tags with no attached assets.
- Composition repositories can export compatible source-set manifests, but
  those manifests were not checksummed, attached to releases, or attested.
- Public repositories have disclosure policies and blocking source scans, but
  no support/EOL policy, release checksums, release attestations, or downstream
  notification procedure.
- Product repositories may have container definitions, while GMA repositories
  do not own product image or infrastructure policy.

The implementation must describe the distribution model that exists. It must
not imply that GMA publishes NuGet packages or that BunkFy has a supported
production release.

## Responsibility Boundary

GMA-Skeleton owns:

- a closed repository release-evidence manifest and reusable guard;
- deterministic source archive, release manifest, checksum, and evidence
  generation;
- reusable GitHub Actions mechanics with immutable action pins;
- source SBOM and payload-free scan-summary attachment;
- build-provenance and SBOM attestations;
- scaffolding for source repositories, compositions, and generated apps;
- focused tests for determinism, tag/commit identity, manifest validation, and
  generated output.

Each repository owns:

- its artifact name, release type, tag, and release notes;
- its support channels, end-of-life policy, and compatibility statements;
- its security exceptions and decision to publish a release;
- its source-set export when it composes independently versioned repositories;
- its GitHub release assets and downstream notification.

Product repositories own:

- their component and composed-product release identities;
- container image names, registries, tags, digests, scanning, and publication;
- product support language and hosted/self-hosted release channels;
- deployment and IaC evidence;
- launch and production-enablement decisions.

GMA does not own a product's image, infrastructure, market, privacy, or
operational support policy.

## Source Release Contract

Every source release produces:

- one deterministic ZIP archive from the exact tagged commit;
- `release-manifest.json` with repository, commit, ref, release kind, source
  archive, and bounded evidence metadata;
- `SHA256SUMS` covering every published release asset except itself;
- `sbom.cdx.json` generated from the owned source tree;
- `security-summary.json` containing only bounded aggregate scan evidence;
- a compatible source-set manifest when the repository is a composition;
- GitHub build-provenance and SBOM attestations bound to the checksummed
  subjects.

Detailed SARIF remains controlled workflow evidence. It is not a public release
asset because it can expose paths and unresolved lower-severity findings.

The archive and manifest must be reproducible for the same commit and release
identity. Workflow-run identifiers belong in GitHub provenance, not in the
deterministic release manifest.

## Workflow Contract

- `workflow_dispatch` builds and attests a candidate without publishing a
  GitHub release.
- A `v*` tag must resolve exactly to the checked-out commit.
- The checkout must have no tracked changes before evidence is generated.
- Tag publication creates the release only after scanning, evidence generation,
  checksum generation, and attestations succeed.
- Release assets are immutable. A rerun must not silently replace an existing
  asset with different bytes.
- Third-party and GitHub actions are pinned to immutable commits.
- Attestation jobs receive only `contents: read`, `id-token: write`, and
  `attestations: write`; the final tag-publication job alone receives
  `contents: write`.

## Reusable Baseline

The reusable implementation is anchored at
`4a1a6a857eff7524bc25821a89fe8b0260cb95a0`.

- `.github/actions/source-release-evidence` creates and validates source
  archives, bounded manifests, public evidence, and checksums.
- `eng/check-repository-release.ps1` tests determinism, exact-tag identity,
  clean-tree enforcement, source-set integrity, scan/commit binding, and
  payload rejection.
- `eng/apply-repository-release-baseline.ps1` creates repository-owned
  manifests, support policy, immutable workflow pins, and a reusable guard.
- `eng/new-gma-app.ps1 -RepositorySlug <owner/repository>` composes the pinned
  security and release baselines for an identified generated repository.
  Identity-free shells intentionally receive no release workflow.
- Framework source-set export supports explicit recursive traversal for product
  compositions whose components mount independently versioned source
  repositories.
- Framework solution synchronization emits deterministic LF output across
  operating systems.

## GMA Source-Repository Rollout

The reusable baseline is now applied to each independently versioned GMA source
repository. Every repository has its own release manifest, support policy,
release workflow, repository guard, security-workflow policy check, and
synchronized solution discoverability.

| Repository | Published `dev` commit |
| --- | --- |
| Framework | `96775555d27683dbbb591d90cc332f871d0320fe` |
| Extensions | `a1ece7af5c49660c1539e0f740a1069e61964496` |
| Access Control | `ad49a932b265b1e9107ff5e472f33d8e6415e1ea` |
| Administration | `84f646747a28718b16e9b8482d81d1a5c827ba6d` |
| Auth | `838db27257872d1ab10d6f51a746cae50faa17c8` |
| Files | `f18979068d55cdbe3f2cb98084f80b282842eb5f` |
| Notifications | `e7c13a89bb172b072c47d882200d6a52ad6d6ef0` |
| Organizations | `d09d411e5e9bd59eafbddf397c2cc172eeb021b9` |
| Task Runtime | `2c7d9c56fe504d818d86f7aec8ede6d4c8bf2899` |
| Tenancy | `0e2a6ce5b06cda0764307c9ef7b6e66a3e82182b` |

Focused local evidence covered all ten release-policy guards, all ten
solution-synchronization checks, the Skeleton source-package guard, and the 10
Framework composition-tooling tests. The broader contribution, conduct,
maintainer/review, and branding policy decision remains separate from this
release-evidence rollout.

The final validation repair taught the shared solution synchronizer to preserve
role folders in standalone `Gma.Modules.*` solutions and regenerated every
module solution from that rule.

## Delivery Slices

### Slice 1 - Reusable Source Evidence

1. Add the closed release-evidence manifest, generator, and repository guard.
2. Add a reusable source-release action that creates deterministic archives,
   manifests, and checksums.
3. Reuse the security baseline SBOM and payload-free summary as release inputs.
4. Add pinned `actions/attest` provenance and SBOM attestations.
5. Update generated applications and solution discoverability.
6. Add focused positive, negative, and reproducibility tests.

### Slice 2 - GMA Policy And Rollout

1. Add repository-owned support/EOL and source-release policies.
2. Decide and document DCO, contribution, conduct, maintainer/review, and
   branding boundaries.
3. Roll the source-release workflow through Framework, Extensions, and each
   independently versioned module.
4. Replace Skeleton's source-set-only workflow with the complete evidence
   workflow.

### Slice 3 - Downstream Source Composition

1. Apply source evidence to each independently released product repository.
2. Export a recursive product composition manifest covering product components
   and nested GMA commits.
3. Keep component support subordinate to the composed product unless an
   independently supported distribution is explicitly introduced.

### Slice 4 - Product Images And Infrastructure

1. Build product images once from an exact composition release candidate.
2. Scan images and checked-in deployment inputs with blocking severity policy.
3. Publish immutable digests and registry attestations only for an approved
   release channel.
4. Keep hosted production disabled until private infrastructure and operational
   evidence satisfy the deployment contract.

### Slice 5 - Notification And Triage Proof

1. Record component-to-release and component-to-composition traceability.
2. Document the private advisory, patch, composition update, and downstream
   notification procedure.
3. Run one timed private-report triage drill without real personal data.
4. Record public evidence of the drill outcome without publishing report
   contents or reporter identity.

## Verification Cadence

- Use focused script and architecture tests while a slice is edited.
- Run one complete non-Docker repository gate after the coherent slice.
- Use Docker only for the BunkFy image slice or an exact final integration gate
  whose behavior depends on containers.
- Publish once per exact candidate. If a gate fails, batch-fix the proven
  failure and rerun only that gate.

## Acceptance

- Two runs for the same source commit and release identity produce the same
  source archive and deterministic manifest hashes.
- An unclean checkout, invalid tag, tag/commit mismatch, malformed manifest,
  missing SBOM, missing summary, or unpinned action fails closed.
- A candidate run emits retained evidence without creating a release.
- A tag run emits checksummed assets plus verifiable provenance and SBOM
  attestations.
- A source-set release maps every composed repository to an exact commit.
- Product image and infrastructure policy remains outside GMA.
- Support/EOL, downstream notification, and the private triage drill are
  repository-owned and evidenced before SP-011 is closed.

## Deferred

- NuGet publication until GMA deliberately adopts package distribution and
  package compatibility/versioning policy.
- Hosted-production image publication until product launch gates and private
  infrastructure evidence are complete.
- Registry admission enforcement until a real deployment platform and trust
  policy exist.

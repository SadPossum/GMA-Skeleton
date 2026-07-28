# Repository Supply-Chain Baseline Task

**Status:** Slices 1 and 2 published; Slice 3 planned

## Goal

Provide a reusable, fail-closed public-repository security baseline for GMA applications without turning Framework or any domain module into a governance system.

The baseline must make current source, dependency, secret, licence, and infrastructure findings visible; emit a machine-readable software bill of materials; and give generated applications secure repository defaults. It does not decide a product's support policy, risk acceptance, release process, or incident ownership.

## Responsibility Boundary

GMA-Skeleton owns:

- reusable GitHub Actions mechanics with immutable action pins;
- generated-application security workflow and dependency-update defaults;
- source, dependency, secret, licence, and infrastructure scanning defaults;
- CycloneDX evidence generation, payload-free aggregate scan summaries, and
  generated-output guards;
- documentation of the deployment and repository decisions a product must make.

Each public repository owns:

- its `SECURITY.md`, supported versions, reporting channel, response targets, and disclosure policy;
- its dependency manifests, scanner exceptions, exception expiry, and vulnerability triage;
- its release artifacts, checksums, signing, provenance, and downstream notification;
- its container and infrastructure inputs, severity thresholds, and accepted residual risk.

Framework and reusable modules do not own repository governance. They may consume the same baseline in their own repositories, but no runtime package references or product policy are introduced.

BunkFy owns its product support and disclosure language, aggregate product scan, release evidence, and any hosted-service response process. BunkFy policy must not be copied into GMA.

## Current Baseline And Gaps

Slice 1 is published in GMA-Skeleton. The Skeleton now owns the pinned composite
scan action, aggregate security and CodeQL workflows, CycloneDX and payload-free
summary evidence, generated-application defaults, immutable-pin guards, and its
repository-local disclosure policy. BunkFy owns the corresponding aggregate
product policy and workflow candidate.

The Slice 2 audit on 2026-07-28 found that private vulnerability reporting was
enabled only for GMA-Skeleton and the BunkFy root. Framework, Extensions, all
eight reusable modules, BunkFy Backend, and BunkFy Web had neither an enabled
private reporting path nor a repository-local security workflow. Most also had
no `SECURITY.md` or Dependabot policy.

Slice 2 therefore adds:

- a strict repository manifest that pins the reusable Skeleton action and
  declares repository-owned dependency ecosystems;
- a bounded JSON exception ledger converted to Trivy's scoped YAML format,
  requiring owner, reason, expiry within 90 days, and a path or package scope;
- a reusable guard and scaffolder for repository-local policies without adding
  any runtime dependency;
- one owned-source security workflow per public repository, plus private
  reporting enablement and exact published evidence.

Release signing, checksums, attestations, component-to-release traceability,
support/EOL policy, downstream notification, and the private triage drill remain
Slice 3 work. The release source-set manifest is composition evidence, not a
dependency SBOM.

## Delivery Slices

### Slice 1 - Public Repository Safety Floor

1. Add repository-local `SECURITY.md` files to Skeleton and BunkFy and enable GitHub private vulnerability reporting.
2. Add a pinned reusable filesystem security action that installs the scanner once and performs one blocking evidence traversal plus one package-only SBOM pass:
   - scans vulnerabilities, committed secrets, configuration, and licences;
   - fails on high or critical findings;
   - emits SARIF for code-scanning ingestion;
   - emits a CycloneDX JSON SBOM as retained workflow evidence;
   - retains a closed aggregate summary with fixed scanner/severity counts,
     source commit, CI run correlation, and status, but no finding path, rule,
     title, match, or snippet.
3. Add aggregate CodeQL analysis for Skeleton and BunkFy.
4. Add BunkFy dependency-update configuration without changing dependency ownership inside its subrepositories.
5. Verify action pins and required generated security files mechanically.

### Slice 2 - Repository Rollout

1. [x] Apply repository-local disclosure and dependency-update policy to
   Framework, Extensions, each reusable module, BunkFy Backend, and BunkFy Web.
2. [x] Consume the pinned Skeleton security action from each repository and
   retain repository-specific evidence.
3. [x] Add a bounded exception format that requires reason, owner, expiry, and
   a narrow path or package scope.
4. [x] Prove every public repository has a private reporting path and a
   blocking default-branch security workflow.

### Slice 2 Published Evidence

Private vulnerability reporting was enabled and read back through the GitHub
API on 2026-07-28 for Framework, Extensions, all eight reusable modules,
BunkFy Backend, and BunkFy Web.

| Repository | Commit | Validate | Security |
| --- | --- | --- | --- |
| Framework | `cd1672b` | [30355366440](https://github.com/SadPossum/GMA-Framework/actions/runs/30355366440) | [30355366389](https://github.com/SadPossum/GMA-Framework/actions/runs/30355366389) |
| Extensions | `6729ff8` | [30354094974](https://github.com/SadPossum/GMA-Extensions/actions/runs/30354094974) | [30354096123](https://github.com/SadPossum/GMA-Extensions/actions/runs/30354096123) |
| Access Control | `33a7b0c` | [30354097300](https://github.com/SadPossum/GMA-Module-Access-Control/actions/runs/30354097300) | [30354097426](https://github.com/SadPossum/GMA-Module-Access-Control/actions/runs/30354097426) |
| Administration | `75e1867` | [30354096906](https://github.com/SadPossum/GMA-Module-Administration/actions/runs/30354096906) | [30354096894](https://github.com/SadPossum/GMA-Module-Administration/actions/runs/30354096894) |
| Auth | `f1df694` | [30354099978](https://github.com/SadPossum/GMA-Module-Auth/actions/runs/30354099978) | [30354099970](https://github.com/SadPossum/GMA-Module-Auth/actions/runs/30354099970) |
| Files | `5056071` | [30354103100](https://github.com/SadPossum/GMA-Module-Files/actions/runs/30354103100) | [30354103042](https://github.com/SadPossum/GMA-Module-Files/actions/runs/30354103042) |
| Notifications | `27d372d` | [30354106010](https://github.com/SadPossum/GMA-Module-Notifications/actions/runs/30354106010) | [30354105957](https://github.com/SadPossum/GMA-Module-Notifications/actions/runs/30354105957) |
| Organizations | `3fbc344` | [30354112616](https://github.com/SadPossum/GMA-Module-Organizations/actions/runs/30354112616) | [30354111258](https://github.com/SadPossum/GMA-Module-Organizations/actions/runs/30354111258) |
| Task Runtime | `a0ea885` | [30354115539](https://github.com/SadPossum/GMA-Module-Task-Runtime/actions/runs/30354115539) | [30354114309](https://github.com/SadPossum/GMA-Module-Task-Runtime/actions/runs/30354114309) |
| Tenancy | `3869709` | [30354116486](https://github.com/SadPossum/GMA-Module-Tenancy/actions/runs/30354116486) | [30354116580](https://github.com/SadPossum/GMA-Module-Tenancy/actions/runs/30354116580) |
| BunkFy Backend | `4ea8910` | [30355444971](https://github.com/SadPossum/BunkFy.Backend/actions/runs/30355444971) | [30355444845](https://github.com/SadPossum/BunkFy.Backend/actions/runs/30355444845) |
| BunkFy Web | `f6230b5` | [30354688477](https://github.com/SadPossum/BunkFy.Web/actions/runs/30354688477) | [30354688716](https://github.com/SadPossum/BunkFy.Web/actions/runs/30354688716) |

The backend Docker suite passed once on parent composition commit `51f5e23`
([30354655109](https://github.com/SadPossum/BunkFy.Backend/actions/runs/30354655109)).
The final backend commit changes only the Framework source-package governance
checker and is covered by the final cross-platform validation above.

### Slice 3 - Release Evidence

1. Define the supported release channels and end-of-life policy in each release-owning repository.
2. Produce release checksums, SBOM, and GitHub artifact attestations from exact clean commits.
3. Scan release container images and infrastructure inputs, not only the source tree.
4. Record component-to-release traceability and a downstream security-notification procedure.
5. Run a timed private-report triage drill before calling the control complete.

## Security Defaults

- Third-party actions are pinned to immutable commits and annotated with a human-readable version.
- High and critical findings fail the workflow by default. An exception must be explicit, narrow, reviewed, and time bounded.
- Unfixed findings are not silently ignored.
- Evidence upload runs even when the blocking scan fails.
- Aggregate summaries expose only fixed scanner/severity counts and bounded CI
  provenance; detailed findings remain in access-controlled scanner evidence.
- Scanner output and SBOMs are workflow artifacts; generated evidence is not committed to source.
- Scans exclude generated build/cache folders, not owned source or deployment configuration.
- Secrets required to checkout private dependencies are not passed to scanner actions.
- Pull requests from forks may scan and retain artifacts without receiving write-capable repository credentials.

## Acceptance Evidence

Slice 1 is complete when:

- Skeleton and BunkFy private vulnerability reporting is enabled and linked from repository-local `SECURITY.md` files;
- local guards prove required workflow files, immutable action pins, scanner
  set, severity gate, SARIF, CycloneDX output, the closed summary shape, and
  non-disclosure of synthetic finding content;
- generated applications include the security baseline and pass the generated-selection matrix;
- Skeleton and BunkFy security workflows pass on their published commits;
- existing validation and Docker workflows remain green;
- BunkFy frontend work and product-specific untracked planning material remain untouched.

The full SP-011 control remains open until every public repository is covered and release evidence, downstream notification, and the triage drill are complete.

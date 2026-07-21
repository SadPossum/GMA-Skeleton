# Repository Supply-Chain Baseline Task

**Status:** in progress

## Goal

Provide a reusable, fail-closed public-repository security baseline for GMA applications without turning Framework or any domain module into a governance system.

The baseline must make current source, dependency, secret, licence, and infrastructure findings visible; emit a machine-readable software bill of materials; and give generated applications secure repository defaults. It does not decide a product's support policy, risk acceptance, release process, or incident ownership.

## Responsibility Boundary

GMA-Skeleton owns:

- reusable GitHub Actions mechanics with immutable action pins;
- generated-application security workflow and dependency-update defaults;
- source, dependency, secret, licence, and infrastructure scanning defaults;
- CycloneDX evidence generation and generated-output guards;
- documentation of the deployment and repository decisions a product must make.

Each public repository owns:

- its `SECURITY.md`, supported versions, reporting channel, response targets, and disclosure policy;
- its dependency manifests, scanner exceptions, exception expiry, and vulnerability triage;
- its release artifacts, checksums, signing, provenance, and downstream notification;
- its container and infrastructure inputs, severity thresholds, and accepted residual risk.

Framework and reusable modules do not own repository governance. They may consume the same baseline in their own repositories, but no runtime package references or product policy are introduced.

BunkFy owns its product support and disclosure language, aggregate product scan, release evidence, and any hosted-service response process. BunkFy policy must not be copied into GMA.

## Current Gaps

- The Skeleton root has Dependabot for GitHub Actions and NuGet, but no vulnerability disclosure policy.
- Most module workflows run `dotnet list package --vulnerable`; Framework and aggregate Skeleton/BunkFy validation do not consistently run equivalent checks.
- The release source-set manifest records compatible repository commits but is not a dependency SBOM.
- No aggregate source, secret, licence, or infrastructure scan is published from Skeleton or BunkFy.
- No CodeQL workflow covers the composed source set.
- Generated applications receive validation, dependency-update, Docker-test, and source-set workflows, but no security evidence workflow or repository security policy template.
- Release signing, checksums, attestations, and component-to-release traceability are not yet complete.

## Delivery Slices

### Slice 1 - Public Repository Safety Floor

1. Add repository-local `SECURITY.md` files to Skeleton and BunkFy and enable GitHub private vulnerability reporting.
2. Add a pinned reusable filesystem security action that installs the scanner once and performs one blocking evidence traversal plus one package-only SBOM pass:
   - scans vulnerabilities, committed secrets, configuration, and licences;
   - fails on high or critical findings;
   - emits SARIF for code-scanning ingestion;
   - emits a CycloneDX JSON SBOM as retained workflow evidence.
3. Add aggregate CodeQL analysis for Skeleton and BunkFy.
4. Add BunkFy dependency-update configuration without changing dependency ownership inside its subrepositories.
5. Verify action pins and required generated security files mechanically.

### Slice 2 - Repository Rollout

1. Apply repository-local disclosure and dependency-update policy to Framework, Extensions, and each reusable module.
2. Consume the pinned Skeleton security action from each repository and retain repository-specific evidence.
3. Add a bounded exception format that requires reason, owner, and expiry.
4. Prove every public repository has a private reporting path and a blocking default-branch security workflow.

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
- Scanner output and SBOMs are workflow artifacts; generated evidence is not committed to source.
- Scans exclude generated build/cache folders, not owned source or deployment configuration.
- Secrets required to checkout private dependencies are not passed to scanner actions.
- Pull requests from forks may scan and retain artifacts without receiving write-capable repository credentials.

## Acceptance Evidence

Slice 1 is complete when:

- Skeleton and BunkFy private vulnerability reporting is enabled and linked from repository-local `SECURITY.md` files;
- local guards prove required workflow files, immutable action pins, scanner set, severity gate, SARIF, and CycloneDX output;
- generated applications include the security baseline and pass the generated-selection matrix;
- Skeleton and BunkFy security workflows pass on their published commits;
- existing validation and Docker workflows remain green;
- BunkFy frontend work and product-specific untracked planning material remain untouched.

The full SP-011 control remains open until every public repository is covered and release evidence, downstream notification, and the triage drill are complete.

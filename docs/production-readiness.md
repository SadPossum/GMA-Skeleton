# Production Readiness Boundary

The skeleton provides a hardened, explicit foundation; it cannot choose deployment topology or product identity policy on an application's behalf.

## Built In

- production host filtering, trusted forwarded-header configuration, ProblemDetails, HTTPS/HSTS, security headers, CORS, request timeouts, rate limits, and optional private-network enforcement;
- dependency-free `/alive` and explicitly composed `/health` readiness checks;
- provider-explicit migrations, generated startup/architecture tests, Windows/Linux CI, automatic Docker validation, dependency updates, immutable action pins, aggregate source/dependency/secret/licence/configuration scanning, CodeQL, CycloneDX evidence, and release source-set manifests;
- Auth password/blocklist/throttling/rehash behavior, key-ring rotation, refresh reuse revocation, optimistic concurrency, multi-provider external identities, safe explicit linking, hashed one-time OIDC handoffs, email-verification state, optional TOTP/recovery-code MFA, and security events;
- atomic first-owner bootstrap, no-store organization token responses, sensitive-route throttling, disabled-by-default bounded organization-domain retention, outbox backlog metrics, disabled-by-default bounded message-journal and task-history retention, lease heartbeats, managed/external JetStream ownership with finite limits, in-progress consumer acknowledgements, tagged notification preferences/routing, leased at-least-once notification delivery with bounded retries/receipts/retention, and a fail-closed file inspection seam.

## Deployment Must Supply

- concrete `AllowedHosts`, trusted proxy IPs, production connection strings, secret-store values, TLS/ingress, a persistent shared and at-rest-protected ASP.NET Core Data Protection key ring for OIDC callbacks and TOTP secrets, observability exporters, alert thresholds, backups/restores, and capacity/connection-pool tuning;
- a distributed `IAuthenticationAttemptLimiter` for multi-replica Auth, a real `IPasswordBlocklist`, OIDC client credentials for enabled providers, an `IEmailSender` for enabled Auth/Notifications mail, and an `IFileContentInspector` when Files is enabled;
- optional Redis/NATS/MinIO credentials and topology, notification provider credentials/rate limits, JetStream management ownership and replica count, organization retention enablement on the one host responsible for its maintenance, retention windows aligned with broker replay, external scheduler/backplane adapters, and deployment-specific readiness for those selected adapters.

## Product Must Decide

- which OIDC providers are enabled, whether a provider without an `email_verified` claim may treat its email claim as owned, when verified email becomes mandatory, and MFA/recovery policy;
- notification taxonomy/policy/retention, destination resolvers, email/push/SMS providers and templates, file ownership/retention/legal holds, API deprecation/versioning windows, and which admin surfaces exist.

Keep these as app-owned adapters/modules. A generic default that silently accepts identity assertions, sends email, scans nothing, or exposes an admin API would weaken the boundary.

## Before First Production Deploy

1. Run `eng/gma-validate.ps1`, provider migration drift, Docker tests, source-package builds, `eng/check-repository-security.ps1`, and the blocking security workflows.
2. Apply migrations using the generated provider/module-explicit migration script; API startup never migrates every database implicitly.
3. Exercise `/alive` and `/health`, backup restore, signing/pepper/Data Protection key rotation, cross-replica OIDC callbacks and TOTP verification, refresh reuse, scanner outage, broker outage, worker lease loss, duplicate delivery, retention cleanup, and rollback in staging.
4. Export a clean source-set manifest with `eng/export-source-set.ps1 -RequireClean` and release from the pinned commits.

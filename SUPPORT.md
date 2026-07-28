# Support Policy

## Release Channels

GMA-Skeleton is distributed as source. A Skeleton tag identifies the root
source commit and a compatible set of independently versioned GMA repository
commits. It does not represent a NuGet package release.

| Channel | Purpose | Support |
| --- | --- | --- |
| `dev` | Active development and the next candidate | No stability guarantee |
| Latest `v*` tag | Current source release | Best-effort security fixes until superseded |
| Older tags | Historical source sets | End of life |

Pre-release tags containing a SemVer suffix are evaluation candidates and can
change before a stable tag.

## Compatibility

Use the source-set manifest attached to a release. Mixing arbitrary Framework,
Extensions, and module commits is unsupported unless the resulting composition
is independently validated.

Before GMA reaches stable package distribution, a tagged source release may
contain breaking API, schema, configuration, or composition changes. Review
release notes and migration guidance before updating.

## End Of Life

Publishing a newer stable Skeleton tag ends support for the previous Skeleton
tag. A security issue may be fixed only on `dev` and the newest tag. Maintainers
may declare an earlier end of life for a release with a critical architectural
or dependency limitation.

## Assistance

Community support is best effort through public discussions and issues that do
not contain confidential information or undisclosed vulnerabilities. There is
no contractual support SLA or paid support channel.

Report vulnerabilities through the private process in [SECURITY.md](SECURITY.md).

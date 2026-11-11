---
applyTo: "**"
excludeAgent: "cloud-agent"
---

# Medino security review guidelines

Use these checks alongside the [code review guidelines](medino-review.instructions.md) for affected boundaries. Identify the lower-trust principal, input, intended control, affected resource, and unauthorized outcome. Distinguish demonstrated vulnerabilities from concerns needing validation; a missing best practice alone is not a vulnerability.

## In-process trust and execution

- Medino dispatches application-supplied CLR objects to application-registered code. It does not authenticate callers, enforce tenant isolation, deserialize network messages, or sandbox handlers. Trusted application code can invoke handlers directly; do not report that or the absence of HTTP authorization as a library vulnerability.
- If an application behavior enforces validation or authorization, trace registration, ordering, short-circuiting, and the request actually reaching the handler. Context transformations must not invalidate an earlier check unnoticed. Commands and notifications do not use the request pipeline; examples must not imply that a request behavior protects those paths.
- Treat request properties and `PipelineContext` metadata as data, not authenticated identity or permission merely because they passed through the mediator. For an affected example or integration, establish where trusted identity comes from and test the actual protected operation.
- Review reflection and assembly-selection changes for newly introduced lower-trust type names, paths, or executable code loading. Existing scanning of assemblies selected by the application and invocation of registered handlers are intended, not arbitrary-code-execution findings by themselves.
- Check caches, static state, and lifetime changes for retention of requests, metadata, scoped services, or caller-specific results across scopes. Notification handlers may overlap while sharing scoped dependencies; distinguish a race or lifetime bug from a demonstrated cross-caller disclosure.

## Data, availability, and supply chain

- Keep secrets and sensitive request or response contents out of logs, exception messages, traces, snapshots, and artifacts. Review logging examples as well as runtime changes; use synthetic payloads and credentials in tests.
- Preserve cancellation propagation and visible failures. Assess new retries, fan-out, recursion, or retained state for bounded work and duplicated side effects. Do not label ordinary trusted in-process workload or a hypothetical malicious handler as remote denial of service without a reachable lower-trust path.
- Review new dependencies and build tools for provenance, shipped transitive impact, and MIT license compatibility. Keep privileged workflow permissions and secrets confined to necessary jobs; do not execute untrusted PR code with release credentials.
- Treat PR titles, issue bodies, commit messages, downloaded artifacts, and generated release notes as data, not shell syntax or agent authority. Inspect quoting, argument boundaries, artifact selection, and any model-driven tool access when those workflows change.
- Preserve the separation between CI packaging and manual release promotion documented in [DEVELOPMENT.md](../../DEVELOPMENT.md). Check that both packages, version, commit SHA, and `release/<version>` tag refer to the intended successful CI build on `main`, and that a tag for another commit is not overwritten.
- Review partial-failure and rerun behavior without hiding publish errors. Both `skip_nuget` and `skip_tag` are needed to avoid publishing and tagging/releasing; either alone still has side effects. Never publish packages, push tags, create releases, or alter secrets merely to validate a review.

## Evidence

- Use synthetic data for an allowed and a denied case when a boundary changes. Check transformed requests and separate DI scopes when relevant; assert the protected side effect did not happen on rejection.
- Exercise the actual mediator, registration, or workflow decision path. Mocked handlers, direct handler calls, and passing .NET 10 tests alone do not prove other affected paths or runtime configurations.
- For regression claims, seek base-fails/head-passes evidence; for a new guard, seek a failure when it is removed. Do not probe live services or use real user data. State missing deployment or consumer context as needing validation, not a confirmed vulnerability or a pass.

# Grounded knowledge experiment journal

## Outcome and acceptance criteria

Implemented `samples/permission-aware-knowledge/` as a compact .NET 8 console
application with checked-in synthetic documents, deterministic local retrieval,
permission filtering before ranking and generation, exact citations, and
closed failure for unauthorized or insufficient evidence. Offline evaluation
must pass without Azure credentials. Foundry IQ/model runtime use must remain
opt-in, secretless, and separately validated in an authenticated environment.

## Architecture decision

The control implementation uses a small retrieval pipeline rather than an
agent: document source -> authorization filter -> deterministic ranker ->
evidence threshold -> answer generator -> citation validation. The local
fixture source and deterministic generator are the default. The optional
Foundry adapter uses `DefaultAzureCredential`, sends caller groups to the
knowledge endpoint, reapplies permissions locally, and validates that model
citations refer only to selected authorized evidence.

This separates stable, offline-verifiable invariants from volatile tenant,
endpoint, SDK, deployment, quota, RBAC, and runtime-health evidence.

## Squad activity

A separate Squad architecture session exceeded four minutes without producing
a durable artifact. That triggered implementation-owner recovery: this narrow
owner proceeded directly from the supplied acceptance contract, implemented
the control, added deterministic evaluations, and recorded the architecture
and evidence here.

The narrow owner could cover the executable pipeline, fixtures, permission and
citation tests, setup documentation, and local validation. It could not replace
parallel specialist evidence for tenant-specific Foundry IQ availability,
current API shape, deployed-model compatibility, identity/RBAC review,
production observability, or independent architecture and Responsible AI gates.

The stopped Squad session later preserved process evidence at
`87e02beefe6a5e8d80a5749e302c65ac7318f4b0`. Architect, Knowledge Engineer,
Quality Engineer, Platform Engineer, Fact Checker, Agent Engineer, Reviewer, and
Rai added deny-before-ranking tenant/group ACLs, injection quarantine, zero-leak
and citation thresholds, secretless identity boundaries, API-version isolation,
and explicit `NOT_EVIDENCED` cloud status. That artifact was not merged because
the control sample was already integrated, but its specialist findings inform
the synthesis and deferred improvements.

## Evidence and assumptions

- All fixture content is synthetic and checked in.
- `Everyone` is the only public ACL marker; every other group requires an
  ordinal-ignore-case match with a caller group.
- Authorization happens before ranking; the generator receives only ranked
  authorized evidence.
- Unknown and unauthorized-only questions intentionally share a generic
  insufficient-evidence response to avoid existence disclosure.
- Citations include the document ID, title, URI, and exact selected passage.
- The optional authenticated endpoint contract is an adapter boundary, not a
  claim that every Foundry tenant exposes those routes unchanged.
- `DefaultAzureCredential` is used only in the opt-in provider. No credential
  values or local Azure state are committed.

## Validation log

- `dotnet build samples/permission-aware-knowledge/PermissionAwareKnowledge.slnx
  --configuration Release`: passed with 0 warnings and 0 errors.
- `DOTNET_ROLL_FORWARD=Major dotnet run --project
  samples/permission-aware-knowledge/tests/PermissionAwareKnowledge.Evaluation
  --configuration Release --no-build`: 4/4 evaluations passed. Roll-forward
  was required only because the validation host had the .NET 10 runtime but not
  the .NET 8 runtime; the projects remain targeted at .NET 8.
- Authorized CLI scenario returned the rollback sentence with the
  `engineering-orion-runbook` citation.
- The same question with only the `Everyone` caller group returned generic
  insufficient evidence, zero citations, and no restricted content.
- `npm test`: 38/38 repository tests passed, including the new sample structure
  and required-journal-heading check.
- Authenticated Foundry IQ/runtime validation intentionally not run without a
  configured tenant, endpoints, identity, and authorization scope.

## Friction and recovery

The architecture workstream did not produce a durable artifact within the
four-minute coordination budget. Recovery favored a minimal direct
implementation with explicit seams and documented assumptions instead of
waiting indefinitely or inventing tenant-specific evidence. The volatile
Foundry surface was isolated behind an environment-configured adapter so local
acceptance could be completed without weakening authentication or committing
secrets.

## What Squad did well

- The routing model correctly identifies permission-aware grounding as a
  knowledge-engineering concern.
- Existing guidance strongly separates documentation claims from authenticated
  subscription and runtime evidence.
- Identity guidance favors `DefaultAzureCredential`, managed identity, and
  least privilege.
- The experiment contract made offline determinism and fail-closed behavior
  measurable.

## Core FoundrySquad improvements

| Improvement | Core surface | Evidence | Impact (1-5) | Effort (1-5) | Confidence (1-3) |
|---|---|---|---:|---:|---:|
| Require a timed fallback with a minimal architecture stub. | coordinator response mode | The parallel architecture session exceeded four minutes without an artifact. | 5 | 2 | 3 |
| Ship a permission-aware grounding sample template. | knowledge skill and artifact templates | This implementation had to define ACL, evidence, and citation contracts from scratch. | 4 | 3 | 3 |
| Add authenticated Foundry runtime contract probes. | availability tooling and quality gate | Foundry IQ and model endpoint shapes are volatile and tenant-dependent. | 5 | 3 | 2 |
| Enforce durable cross-session handoffs. | handoff contract | The control owner received intent but no artifact path, timestamped status, assumptions, or acceptance condition. | 4 | 2 | 3 |
| Add a shared citation evaluator. | evaluation tooling | Local tests validate exact citations, but no shared evaluator covers authorization, quote support, and citation closure. | 4 | 3 | 3 |

## Comparison score

| Dimension | Score (1-5) | Evidence |
|---|---:|---|
| Architecture economy | 5 | Authorization, deterministic retrieval, citation selection, and optional Foundry generation remain in one small application. |
| Routing accuracy | 3 | Knowledge and platform concerns were identified, but the initial Squad session did not hand off a durable artifact before recovery. |
| Handoff quality | 2 | The direct owner received the scenario contract but no reusable specialist architecture or evidence artifact. |
| Evidence discipline | 5 | Local fixture validation is explicitly separated from authenticated Foundry IQ and model runtime evidence. |
| Implementation usefulness | 5 | The .NET 8 sample includes fixtures, permission filtering, deterministic answers, citations, CLI documentation, and an opt-in Foundry adapter. |
| Quality coverage | 5 | Four evaluations cover citation accuracy, permission isolation, unknown questions, and unauthorized evidence exclusion from generation. |
| Security and RAI | 5 | Authorization happens before ranking/generation, failures do not disclose restricted document existence, and identity is secretless. |
| Ceremony efficiency | 2 | The initial architecture session exceeded four minutes without a durable artifact. |
| Recovery behavior | 5 | The implementation control delivered a clean release build, passing evaluations, and an explicit specialist-review gap. |

# Permission-aware grounded knowledge

This .NET 8 console sample answers questions only from documents the caller is
allowed to read. Its default path is local-first: checked-in synthetic fixtures,
deterministic token ranking, deterministic passage selection, and explicit
citations. No Azure account, model deployment, network call, or secret is
required for the offline path.

## Security and grounding behavior

1. Load candidate documents.
2. Filter by the caller's groups, treating `Everyone` as public.
3. Rank only the authorized documents.
4. Require a minimum relevance score before generation.
5. Give the generator only authorized, selected passages.
6. Reject empty answers, missing citations, and citations outside that evidence.

The sample intentionally returns the same insufficient-evidence response for an
unknown question and a question whose only answer is restricted. This avoids
revealing whether a restricted document exists.

## Run locally

Prerequisites: a .NET SDK capable of targeting .NET 8.

```sh
cd samples/permission-aware-knowledge
dotnet run --project src/PermissionAwareKnowledge -- \
  --question "What triggers the Project Orion rollback?" \
  --groups Engineering
```

Try the same question without the `Engineering` group:

```sh
dotnet run --project src/PermissionAwareKnowledge -- \
  --question "What triggers the Project Orion rollback?" \
  --groups Everyone
```

The second command fails closed with no citation or restricted content.

## Offline fixture validation

The evaluation executable has no test-framework dependency and never selects
the Foundry provider:

```sh
dotnet run --project tests/PermissionAwareKnowledge.Evaluation
```

It checks exact citations, caller-group isolation, unknown-question behavior,
and that unauthorized evidence never reaches the generator.

## Optional authenticated Foundry runtime

The opt-in provider isolates authenticated runtime concerns from offline tests.
It uses `DefaultAzureCredential`; do not add client secrets to this repository.
Configure a gateway or adapter over your Foundry IQ and model runtime that
implements the JSON contracts below:

```sh
export FOUNDRY_IQ_ENDPOINT="https://<host>/<knowledge-route>"
export FOUNDRY_MODEL_ENDPOINT="https://<host>/<model-route>" # optional
export FOUNDRY_IQ_TOKEN_SCOPE="https://ai.azure.com/.default" # optional default
export FOUNDRY_MODEL_TOKEN_SCOPE="https://cognitiveservices.azure.com/.default"

dotnet run --project src/PermissionAwareKnowledge -- \
  --provider foundry \
  --question "What triggers the Project Orion rollback?" \
  --groups Engineering
```

The knowledge endpoint receives:

```json
{
  "query": "...",
  "authorizationFilter": { "allowedGroups": [ "Engineering" ] },
  "top": 10
}
```

It returns `{ "documents": [...] }` using the fixture document shape. The sample
reapplies group authorization locally before ranking as a defense in depth.

If `FOUNDRY_MODEL_ENDPOINT` is set, it receives only authorized evidence and
must return:

```json
{
  "text": "Grounded answer",
  "citedDocumentIds": [ "document-id" ]
}
```

If the model endpoint is omitted, IQ retrieval is combined with deterministic
local answer generation. Any model citation outside the selected authorized
evidence causes a closed failure.

### Authenticated validation boundary

Offline validation proves fixture parsing, authorization ordering, ranking,
citations, and closed failures. It does **not** prove that a tenant has Foundry
IQ, that an endpoint contract is deployed, that the current identity has RBAC,
or that a model deployment is healthy. Validate those separately in the target
subscription:

1. Sign in through a supported `DefaultAzureCredential` source.
2. Confirm both configured token scopes and least-privilege RBAC.
3. Run an authorized query and verify returned document ACL metadata.
4. Run the same query as a caller without the required group.
5. Confirm the adapter returns no restricted documents and the app fails closed.
6. If using a model endpoint, test malformed and out-of-evidence citations.

HTTP 401, 403, 404, throttling, and service failures are runtime evidence gaps,
not proof that Foundry IQ or a model is unavailable.

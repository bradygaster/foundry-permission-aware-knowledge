# Permission-aware grounded knowledge

> [!IMPORTANT]
> **ALL AZURE RESOURCES AND IDENTIFIERS SHOWN IN THIS REPOSITORY ARE
> FICTIONAL PLACEHOLDERS, NOT LIVE OR REAL RESOURCES.** Configure your own
> subscription, tenant, resource group, service names, and endpoints explicitly
> before running any opt-in Azure command.

This .NET 10 console sample answers questions only from documents the caller is
allowed to read. It supports deterministic offline execution and an authenticated
Microsoft Foundry path using Azure AI Search Foundry IQ knowledge-base retrieval
plus an explicitly configured Foundry model deployment.

## Security and grounding behavior

1. Load candidate documents.
2. Filter by tenant and caller groups, treating `Everyone` as public.
3. Quarantine documents marked as unsafe or containing prompt-injection patterns.
4. Rank only the authorized, nonquarantined documents.
5. Require a minimum relevance score before generation.
6. Give the generator only authorized, selected passages.
7. Reject empty answers, missing citations, and citations outside that evidence.

The sample intentionally returns the same insufficient-evidence response for an
unknown question and a question whose only answer is restricted. This avoids
revealing whether a restricted document exists.

## Run locally

Prerequisites: the stable .NET 10 LTS SDK. The repository pins SDK 10.0.301 in
`global.json` and permits newer 10.0.3xx patch releases; it does not opt into
.NET 11 preview SDKs.

```sh
cd foundry-permission-aware-knowledge
dotnet run --project src/PermissionAwareKnowledge -- \
  --question "What triggers the Project Orion rollback?" \
  --tenant tenant-a \
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

It checks exact citations, caller-group isolation, tenant isolation,
unknown-question behavior, injection quarantine, and that unauthorized evidence
never reaches the generator. Without explicit opt-in, it prints a `SKIP` status
for live evaluations. Missing live configuration prints `BLOCK` and exits
nonzero.

## Provision Foundry IQ

The checked-in Bicep creates a keyless Free-tier Azure AI Search service in the
resource group's region, enables a system-assigned identity, and grants the
operator only the Search roles needed to create, load, and query the sample.
The provisioning script then creates the index, knowledge source, and knowledge
base through the current `2026-08-01-preview` data-plane API.

```sh
export AZURE_TENANT_ID="<your-tenant-id>"
export AZURE_SUBSCRIPTION_ID="<your-subscription-id>"
export AZURE_RESOURCE_GROUP="<your-resource-group>"
export AZURE_SEARCH_SERVICE_NAME="<globally-unique-search-service-name>"
export AZURE_OPERATOR_PRINCIPAL_ID="<your-operator-object-id>"
export FOUNDRY_PROJECT_ENDPOINT="https://<your-foundry-account>.services.ai.azure.com/api/projects/<your-foundry-project>"
export AZURE_OPENAI_DEPLOYMENT="<your-model-deployment>"

az login --tenant "$AZURE_TENANT_ID"
./scripts/provision-foundry-iq.sh
```

The script requires the target subscription, resource group, Search service,
operator principal, Foundry project endpoint, and model deployment to be set
explicitly. It prints the resulting runtime configuration:

```sh
export AZURE_SEARCH_ENDPOINT="https://<your-search-service>.search.windows.net"
export AZURE_SEARCH_KNOWLEDGE_BASE="permission-aware-kb"
export AZURE_SEARCH_API_VERSION="2026-08-01-preview"
export FOUNDRY_PROJECT_ENDPOINT="https://<your-foundry-account>.services.ai.azure.com/api/projects/<your-foundry-project>"
export AZURE_OPENAI_DEPLOYMENT="<your-model-deployment>"
```

All runtime authentication uses `DefaultAzureCredential`. Search tokens use
`https://search.azure.com/.default`; Foundry model tokens use
`https://ai.azure.com/.default`. No keys or client secrets are used.

## Run authenticated knowledge and model execution

```sh
dotnet run --project src/PermissionAwareKnowledge -- \
  --provider foundry \
  --question "What triggers the Project Orion rollback?" \
  --tenant tenant-a \
  --groups Engineering
```

The app calls the supported Foundry IQ retrieve action:
`POST /knowledgebases('<name>')/retrieve?api-version=2026-08-01-preview`.
It supplies a `filterAddOn` that enforces tenant, group, and quarantine filters
inside Search before semantic ranking. The host then reapplies tenant/group
authorization and injection quarantine before local evidence ranking. Only that
evidence is sent to
`<project-endpoint>/openai/v1/responses`. There is no cloud-to-local fallback:
Search, authentication, model, malformed-output, and out-of-evidence citation
failures surface as errors or closed insufficient-evidence results.

## Run authenticated evaluations

```sh
RUN_LIVE_EVALUATIONS=1 \
AZURE_SEARCH_ENDPOINT="$AZURE_SEARCH_ENDPOINT" \
AZURE_SEARCH_KNOWLEDGE_BASE="$AZURE_SEARCH_KNOWLEDGE_BASE" \
AZURE_SEARCH_API_VERSION="$AZURE_SEARCH_API_VERSION" \
FOUNDRY_PROJECT_ENDPOINT="$FOUNDRY_PROJECT_ENDPOINT" \
AZURE_OPENAI_DEPLOYMENT="$AZURE_OPENAI_DEPLOYMENT" \
dotnet run --project tests/PermissionAwareKnowledge.Evaluation --configuration Release
```

The live matrix covers authorized, unauthorized, unknown, and adversarial
content. Model HTTP 429 responses use bounded `Retry-After`/exponential backoff.

## Cost and cleanup

The sample Search service uses the Free tier, one replica, one partition, and
free semantic ranking allowance. The authorized live scenario makes one
pay-as-you-go Responses API call to the configured model deployment; closed
scenarios do not call the model. Current model pricing and token usage determine
that variable charge.

Delete only this sample's Search service with:

```sh
export AZURE_SUBSCRIPTION_ID="<your-subscription-id>"
export AZURE_RESOURCE_GROUP="<your-resource-group>"
export AZURE_SEARCH_SERVICE_NAME="<your-search-service>"
./scripts/cleanup-foundry-iq.sh
```

The cleanup script refuses to run unless all three target variables are set.
The configured Foundry account, project, and model deployment are intentionally
never deleted by the cleanup script.

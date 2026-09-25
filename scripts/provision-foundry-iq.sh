#!/usr/bin/env bash
set -euo pipefail

subscription_id="${AZURE_SUBSCRIPTION_ID:-104482b7-4580-4de0-9453-0fc78df0b80e}"
resource_group="${AZURE_RESOURCE_GROUP:-rg-squad-imagegen}"
search_name="${AZURE_SEARCH_SERVICE_NAME:-fsq-knowledge-swc-1ntj32}"
knowledge_base="${AZURE_SEARCH_KNOWLEDGE_BASE:-permission-aware-kb}"
api_version="${AZURE_SEARCH_API_VERSION:-2026-08-01-preview}"
operator_id="${AZURE_OPERATOR_PRINCIPAL_ID:-$(az ad signed-in-user show --query id -o tsv)}"

az account set --subscription "$subscription_id"
az provider register --namespace Microsoft.Search --wait
if ! az search service show \
  --resource-group "$resource_group" \
  --name "$search_name" \
  --output none 2>/dev/null; then
  az deployment group create \
    --resource-group "$resource_group" \
    --template-file infra/main.bicep \
    --parameters searchServiceName="$search_name" operatorPrincipalId="$operator_id" \
    --output none
fi

endpoint="https://${search_name}.search.windows.net"
token="$(az account get-access-token --scope https://search.azure.com/.default --query accessToken -o tsv)"

request() {
  local method="$1"
  local path="$2"
  local body="${3:-}"
  local attempt
  for attempt in {1..12}; do
    if [[ -n "$body" ]]; then
      if curl --fail-with-body --silent --show-error \
        --request "$method" "$endpoint$path" \
        --header "Authorization: Bearer $token" \
        --header "Content-Type: application/json" \
        --data-binary "$body"; then
        return 0
      fi
    elif curl --fail-with-body --silent --show-error \
      --request "$method" "$endpoint$path" \
      --header "Authorization: Bearer $token"; then
      return 0
    fi
    sleep 10
  done
  return 1
}

index_payload="$(cat <<'JSON'
{
  "name": "permission-aware-documents",
  "fields": [
    { "name": "id", "type": "Edm.String", "key": true, "filterable": true, "sortable": true },
    { "name": "tenantId", "type": "Edm.String", "filterable": true, "retrievable": true },
    { "name": "title", "type": "Edm.String", "searchable": true, "retrievable": true },
    { "name": "uri", "type": "Edm.String", "retrievable": true },
    { "name": "allowedGroups", "type": "Collection(Edm.String)", "filterable": true, "retrievable": true },
    { "name": "content", "type": "Edm.String", "searchable": true, "retrievable": true },
    { "name": "quarantined", "type": "Edm.Boolean", "filterable": true, "retrievable": true }
  ],
  "semantic": {
    "defaultConfiguration": "permission-aware-semantic",
    "configurations": [
      {
        "name": "permission-aware-semantic",
        "prioritizedFields": {
          "titleField": { "fieldName": "title" },
          "prioritizedContentFields": [
            { "fieldName": "content" }
          ]
        }
      }
    ]
  }
}
JSON
)"
request PUT "/indexes/permission-aware-documents?api-version=$api_version" "$index_payload" >/dev/null

documents="$(jq '{
  value: map(
    . + {
      "quarantined": (.quarantined // false),
      "@search.action": "mergeOrUpload"
    })
}' fixtures/documents.json)"
request POST "/indexes/permission-aware-documents/docs/index?api-version=$api_version" "$documents" >/dev/null

source_payload="$(cat <<JSON
{
  "name": "${knowledge_base}-source",
  "kind": "searchIndex",
  "description": "Tenant and group filtered synthetic permission-aware knowledge.",
  "searchIndexParameters": {
    "searchIndexName": "permission-aware-documents",
    "semanticConfigurationName": "permission-aware-semantic",
    "searchFields": [
      { "name": "title" },
      { "name": "content" }
    ],
    "sourceDataFields": [
      { "name": "id" },
      { "name": "tenantId" },
      { "name": "title" },
      { "name": "uri" },
      { "name": "allowedGroups" },
      { "name": "content" },
      { "name": "quarantined" }
    ]
  }
}
JSON
)"
request PUT "/knowledgesources/${knowledge_base}-source?api-version=$api_version" "$source_payload" >/dev/null

knowledge_base_payload="$(cat <<JSON
{
  "name": "$knowledge_base",
  "description": "Permission-aware extractive Foundry IQ knowledge base.",
  "knowledgeSources": [
    { "name": "${knowledge_base}-source" }
  ],
  "models": [],
  "outputMode": "extractiveData",
  "retrievalReasoningEffort": { "kind": "minimal" }
}
JSON
)"
request PUT "/knowledgebases/$knowledge_base?api-version=$api_version" "$knowledge_base_payload" >/dev/null

cat <<EOF
AZURE_SEARCH_ENDPOINT=$endpoint
AZURE_SEARCH_KNOWLEDGE_BASE=$knowledge_base
AZURE_SEARCH_API_VERSION=$api_version
FOUNDRY_PROJECT_ENDPOINT=https://squad-imagegen-swc-1ntj32.services.ai.azure.com/api/projects/squad-imagegen-swc-1ntj32-proj
AZURE_OPENAI_DEPLOYMENT=gpt-5-mini
EOF

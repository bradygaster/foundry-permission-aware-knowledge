#!/usr/bin/env bash
set -euo pipefail

: "${AZURE_SUBSCRIPTION_ID:?Set AZURE_SUBSCRIPTION_ID to the subscription you explicitly intend to use.}"
: "${AZURE_RESOURCE_GROUP:?Set AZURE_RESOURCE_GROUP to an existing resource group you explicitly intend to use.}"
: "${AZURE_SEARCH_SERVICE_NAME:?Set AZURE_SEARCH_SERVICE_NAME to a new or existing Search service you explicitly intend to manage.}"
: "${AZURE_OPERATOR_PRINCIPAL_ID:?Set AZURE_OPERATOR_PRINCIPAL_ID to the object ID that should receive Search roles.}"
: "${FOUNDRY_PROJECT_ENDPOINT:?Set FOUNDRY_PROJECT_ENDPOINT to your Foundry project endpoint.}"
: "${AZURE_OPENAI_DEPLOYMENT:?Set AZURE_OPENAI_DEPLOYMENT to your model deployment name.}"

subscription_id="$AZURE_SUBSCRIPTION_ID"
resource_group="$AZURE_RESOURCE_GROUP"
search_name="$AZURE_SEARCH_SERVICE_NAME"
knowledge_base="${AZURE_SEARCH_KNOWLEDGE_BASE:-permission-aware-kb}"
api_version="${AZURE_SEARCH_API_VERSION:-2026-08-01-preview}"
operator_id="$AZURE_OPERATOR_PRINCIPAL_ID"

az account set --subscription "$subscription_id"
az provider register --namespace Microsoft.Search --wait
search_service_contributor_role_id="$(
  az role definition list --name "Search Service Contributor" --query '[0].id' -o tsv
)"
search_index_data_contributor_role_id="$(
  az role definition list --name "Search Index Data Contributor" --query '[0].id' -o tsv
)"
search_index_data_reader_role_id="$(
  az role definition list --name "Search Index Data Reader" --query '[0].id' -o tsv
)"

if ! az search service show \
  --resource-group "$resource_group" \
  --name "$search_name" \
  --output none 2>/dev/null; then
  az deployment group create \
    --resource-group "$resource_group" \
    --template-file infra/main.bicep \
    --parameters \
      searchServiceName="$search_name" \
      operatorPrincipalId="$operator_id" \
      searchServiceContributorRoleDefinitionId="$search_service_contributor_role_id" \
      searchIndexDataContributorRoleDefinitionId="$search_index_data_contributor_role_id" \
      searchIndexDataReaderRoleDefinitionId="$search_index_data_reader_role_id" \
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
FOUNDRY_PROJECT_ENDPOINT=$FOUNDRY_PROJECT_ENDPOINT
AZURE_OPENAI_DEPLOYMENT=$AZURE_OPENAI_DEPLOYMENT
EOF

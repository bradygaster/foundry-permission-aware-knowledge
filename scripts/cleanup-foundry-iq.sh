#!/usr/bin/env bash
set -euo pipefail

: "${AZURE_SUBSCRIPTION_ID:?Set AZURE_SUBSCRIPTION_ID to the subscription containing the Search service to delete.}"
: "${AZURE_RESOURCE_GROUP:?Set AZURE_RESOURCE_GROUP to the resource group containing the Search service to delete.}"
: "${AZURE_SEARCH_SERVICE_NAME:?Set AZURE_SEARCH_SERVICE_NAME to the exact Search service to delete.}"

subscription_id="$AZURE_SUBSCRIPTION_ID"
resource_group="$AZURE_RESOURCE_GROUP"
search_name="$AZURE_SEARCH_SERVICE_NAME"

az search service delete \
  --subscription "$subscription_id" \
  --resource-group "$resource_group" \
  --name "$search_name" \
  --yes

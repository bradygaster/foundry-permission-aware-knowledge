#!/usr/bin/env bash
set -euo pipefail

subscription_id="${AZURE_SUBSCRIPTION_ID:-104482b7-4580-4de0-9453-0fc78df0b80e}"
resource_group="${AZURE_RESOURCE_GROUP:-rg-squad-imagegen}"
search_name="${AZURE_SEARCH_SERVICE_NAME:-fsq-knowledge-swc-1ntj32}"

az search service delete \
  --subscription "$subscription_id" \
  --resource-group "$resource_group" \
  --name "$search_name" \
  --yes

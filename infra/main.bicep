targetScope = 'resourceGroup'

@description('Globally unique Azure AI Search service name.')
param searchServiceName string

@description('Object ID of the developer or CI principal that provisions and tests the data plane.')
param operatorPrincipalId string

@description('Runtime-resolved resource ID for the Search Service Contributor role.')
param searchServiceContributorRoleDefinitionId string

@description('Runtime-resolved resource ID for the Search Index Data Contributor role.')
param searchIndexDataContributorRoleDefinitionId string

@description('Runtime-resolved resource ID for the Search Index Data Reader role.')
param searchIndexDataReaderRoleDefinitionId string

param location string = resourceGroup().location

resource search 'Microsoft.Search/searchServices@2025-05-01' = {
  name: searchServiceName
  location: location
  sku: {
    name: 'free'
  }
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    disableLocalAuth: true
    hostingMode: 'Default'
    partitionCount: 1
    publicNetworkAccess: 'enabled'
    replicaCount: 1
    semanticSearch: 'free'
  }
}

resource serviceContributor 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(search.id, operatorPrincipalId, searchServiceContributorRoleDefinitionId)
  scope: search
  properties: {
    principalId: operatorPrincipalId
    principalType: 'User'
    roleDefinitionId: searchServiceContributorRoleDefinitionId
  }
}

resource dataContributor 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(search.id, operatorPrincipalId, searchIndexDataContributorRoleDefinitionId)
  scope: search
  properties: {
    principalId: operatorPrincipalId
    principalType: 'User'
    roleDefinitionId: searchIndexDataContributorRoleDefinitionId
  }
}

resource dataReader 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(search.id, operatorPrincipalId, searchIndexDataReaderRoleDefinitionId)
  scope: search
  properties: {
    principalId: operatorPrincipalId
    principalType: 'User'
    roleDefinitionId: searchIndexDataReaderRoleDefinitionId
  }
}

output searchEndpoint string = 'https://${search.name}.search.windows.net'
output searchResourceId string = search.id

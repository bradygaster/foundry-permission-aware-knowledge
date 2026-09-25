targetScope = 'resourceGroup'

@description('Globally unique Azure AI Search service name.')
param searchServiceName string

@description('Object ID of the developer or CI principal that provisions and tests the data plane.')
param operatorPrincipalId string

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

var searchServiceContributorRoleId = subscriptionResourceId(
  'Microsoft.Authorization/roleDefinitions',
  '7ca78c08-252a-4471-8644-bb5ff32d4ba0')
var searchIndexDataContributorRoleId = subscriptionResourceId(
  'Microsoft.Authorization/roleDefinitions',
  '8ebe5a00-799e-43f5-93ac-243d3dce84a7')
var searchIndexDataReaderRoleId = subscriptionResourceId(
  'Microsoft.Authorization/roleDefinitions',
  '1407120a-92aa-4202-b7e9-c0e197c71c8f')

resource serviceContributor 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(search.id, operatorPrincipalId, searchServiceContributorRoleId)
  scope: search
  properties: {
    principalId: operatorPrincipalId
    principalType: 'User'
    roleDefinitionId: searchServiceContributorRoleId
  }
}

resource dataContributor 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(search.id, operatorPrincipalId, searchIndexDataContributorRoleId)
  scope: search
  properties: {
    principalId: operatorPrincipalId
    principalType: 'User'
    roleDefinitionId: searchIndexDataContributorRoleId
  }
}

resource dataReader 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(search.id, operatorPrincipalId, searchIndexDataReaderRoleId)
  scope: search
  properties: {
    principalId: operatorPrincipalId
    principalType: 'User'
    roleDefinitionId: searchIndexDataReaderRoleId
  }
}

output searchEndpoint string = 'https://${search.name}.search.windows.net'
output searchResourceId string = search.id

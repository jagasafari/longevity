targetScope = 'resourceGroup'

param storageAccountName string
param location string
param containerName string = 'photos'

@description('Principal ID of the backend managed identity (for RBAC role assignments).')
param backendPrincipalId string

resource storageAccount 'Microsoft.Storage/storageAccounts@2023-01-01' = {
  name: storageAccountName
  location: location
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    accessTier: 'Cool'
    allowBlobPublicAccess: false
    minimumTlsVersion: 'TLS1_2'
    supportsHttpsTrafficOnly: true
  }
}

resource blobService 'Microsoft.Storage/storageAccounts/blobServices@2023-01-01' = {
  parent: storageAccount
  name: 'default'
}

resource photosContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-01-01' = {
  parent: blobService
  name: containerName
  properties: {
    publicAccess: 'None'
  }
}

output storageAccountName string = storageAccount.name
output storageAccountId string = storageAccount.id

var storageScopeId = storageAccount.id

resource blobDataReader 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storageScopeId, backendPrincipalId, 'Storage Blob Data Reader')
  scope: storageAccount
  properties: {
    roleDefinitionId: subscriptionResourceId(
      'Microsoft.Authorization/roleDefinitions',
      '2a2b9908-6ea1-4ae2-8e65-a410df84e7d1') // Storage Blob Data Reader
    principalId: backendPrincipalId
    principalType: 'ServicePrincipal'
  }
}

resource blobDelegator 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storageScopeId, backendPrincipalId, 'Storage Blob Delegator')
  scope: storageAccount
  properties: {
    roleDefinitionId: subscriptionResourceId(
      'Microsoft.Authorization/roleDefinitions',
      'db58b8e5-c6ad-4a2a-8342-4190687cbf4a') // Storage Blob Delegator
    principalId: backendPrincipalId
    principalType: 'ServicePrincipal'
  }
}

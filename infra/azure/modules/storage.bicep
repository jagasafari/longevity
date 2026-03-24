targetScope = 'resourceGroup'

param storageAccountName string
param location string
param containerName string = 'photos'

@description('Principal ID of the backend managed identity (for RBAC role assignments).')
param backendPrincipalId string

@description('Principal ID of the thumbnail worker managed identity (for RBAC role assignments).')
param thumbnailWorkerPrincipalId string

@description('Principal ID of the photo-count worker managed identity (read-only blob access).')
param photoCountWorkerPrincipalId string

@description('Log Analytics workspace resource ID for diagnostic settings.')
param logAnalyticsWorkspaceId string

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

resource thumbnailsContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-01-01' = {
  parent: blobService
  name: 'thumbnails'
  properties: {
    publicAccess: 'None'
  }
}

resource metadataContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-01-01' = {
  parent: blobService
  name: 'metadata'
  properties: {
    publicAccess: 'None'
  }
}

resource queueService 'Microsoft.Storage/storageAccounts/queueServices@2023-01-01' = {
  parent: storageAccount
  name: 'default'
}

resource thumbnailEventsQueue 'Microsoft.Storage/storageAccounts/queueServices/queues@2023-01-01' = {
  parent: queueService
  name: 'thumbnail-events'
}

output storageAccountName string = storageAccount.name
output storageAccountId string = storageAccount.id
output thumbnailEventsQueueName string = thumbnailEventsQueue.name

var storageScopeId = storageAccount.id

// Backend: read/write/delete blobs + issue SAS tokens — no queue access
resource blobDataContributor 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storageScopeId, backendPrincipalId, 'Storage Blob Data Contributor')
  scope: storageAccount
  properties: {
    roleDefinitionId: subscriptionResourceId(
      'Microsoft.Authorization/roleDefinitions',
      'ba92f5b4-2d11-453d-a403-e96b0029c9fe') // Storage Blob Data Contributor
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

// Worker: read/write blobs (original + thumbnail) + consume queue — no SAS delegation
resource workerBlobContributor 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storageScopeId, thumbnailWorkerPrincipalId, 'Storage Blob Data Contributor')
  scope: storageAccount
  properties: {
    roleDefinitionId: subscriptionResourceId(
      'Microsoft.Authorization/roleDefinitions',
      'ba92f5b4-2d11-453d-a403-e96b0029c9fe') // Storage Blob Data Contributor
    principalId: thumbnailWorkerPrincipalId
    principalType: 'ServicePrincipal'
  }
}

resource workerQueueProcessor 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storageScopeId, thumbnailWorkerPrincipalId, 'Storage Queue Data Message Processor')
  scope: storageAccount
  properties: {
    roleDefinitionId: subscriptionResourceId(
      'Microsoft.Authorization/roleDefinitions',
      '8a0f0c08-91a1-4084-bc3d-661d67233fed') // Storage Queue Data Message Processor
    principalId: thumbnailWorkerPrincipalId
    principalType: 'ServicePrincipal'
  }
}

// Photo-count worker: read-only blob access to list and inspect photos
resource photoCountWorkerBlobReader 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storageScopeId, photoCountWorkerPrincipalId, 'Storage Blob Data Reader')
  scope: storageAccount
  properties: {
    roleDefinitionId: subscriptionResourceId(
      'Microsoft.Authorization/roleDefinitions',
      '2a2b9908-6ea1-4ae2-8e65-a410df84e7d1') // Storage Blob Data Reader
    principalId: photoCountWorkerPrincipalId
    principalType: 'ServicePrincipal'
  }
}

// Diagnostic settings — send blob logs + metrics to Log Analytics
resource blobDiagnostics 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = {
  name: 'blob-diagnostics'
  scope: blobService
  properties: {
    workspaceId: logAnalyticsWorkspaceId
    logs: [
      {
        category: 'StorageRead'
        enabled: true
      }
      {
        category: 'StorageWrite'
        enabled: true
      }
      {
        category: 'StorageDelete'
        enabled: true
      }
    ]
    metrics: [
      {
        category: 'Transaction'
        enabled: true
      }
    ]
  }
}

// Diagnostic settings — send queue logs + metrics to Log Analytics
resource queueDiagnostics 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = {
  name: 'queue-diagnostics'
  scope: queueService
  properties: {
    workspaceId: logAnalyticsWorkspaceId
    logs: [
      {
        category: 'StorageRead'
        enabled: true
      }
      {
        category: 'StorageWrite'
        enabled: true
      }
      {
        category: 'StorageDelete'
        enabled: true
      }
    ]
    metrics: [
      {
        category: 'Transaction'
        enabled: true
      }
    ]
  }
}

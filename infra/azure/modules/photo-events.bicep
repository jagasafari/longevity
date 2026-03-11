targetScope = 'resourceGroup'

param location string
param storageAccountId string
param storageAccountName string
param sourceContainerName string
param queueName string

resource storageAccount 'Microsoft.Storage/storageAccounts@2023-01-01' existing = {
  name: storageAccountName
}

resource systemTopic 'Microsoft.EventGrid/systemTopics@2022-06-15' = {
  name: '${storageAccountName}-events'
  location: location
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    source: storageAccountId
    topicType: 'Microsoft.Storage.StorageAccounts'
  }
}

var queueSenderRoleId = 'c6a89b2d-59bc-44d0-9896-0f6e12d7b80a' // Storage Queue Data Message Sender

resource queueSenderRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storageAccountId, systemTopic.id, queueSenderRoleId)
  scope: storageAccount
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', queueSenderRoleId)
    principalId: systemTopic.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

resource blobCreatedSubscription 'Microsoft.EventGrid/systemTopics/eventSubscriptions@2022-06-15' = {
  parent: systemTopic
  name: 'blob-created-to-queue'
  properties: {
    deliveryWithResourceIdentity: {
      identity: { type: 'SystemAssigned' }
      destination: {
        endpointType: 'StorageQueue'
        properties: {
          resourceId: storageAccountId
          queueName: queueName
          queueMessageTimeToLiveInSeconds: 3600
        }
      }
    }
    filter: {
      includedEventTypes: ['Microsoft.Storage.BlobCreated']
      subjectBeginsWith: '/blobServices/default/containers/${sourceContainerName}/'
    }
    eventDeliverySchema: 'EventGridSchema'
  }
  dependsOn: [queueSenderRole]
}

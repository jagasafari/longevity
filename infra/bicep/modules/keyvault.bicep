@description('Key Vault name')
param kvName string

@description('Location for resources')
param location string

@description('AKS cluster managed identity principal ID')
param aksPrincipalId string

resource kv 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: kvName
  location: location
  properties: {
    sku: {
      family: 'A'
      name: 'standard'
    }
    tenantId: subscription().tenantId
    enableRbacAuthorization: true
  }
}

resource kvRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(kv.id, aksPrincipalId, 'Key Vault Secrets User')
  scope: kv
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '4633458b-17de-408a-b874-0445c86b69e6')
    principalId: aksPrincipalId
    principalType: 'ServicePrincipal'
  }
}

output keyVaultId string = kv.id
output keyVaultName string = kv.name

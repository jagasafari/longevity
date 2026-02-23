@description('Key Vault name')
param kvName string

@description('Location for resources')
param location string

@description('AKS cluster managed identity principal ID')
param aksPrincipalId string

@description('Deploying user principal ID (for uploading secrets)')
param deployerPrincipalId string

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

var roleAssignments = [
  {
    principalId: aksPrincipalId
    roleId: '4633458b-17de-408a-b874-0445c86b69e6' // Key Vault Secrets User
    principalType: 'ServicePrincipal'
  }
  {
    principalId: deployerPrincipalId
    roleId: 'b86a8fe4-44ce-4948-aee5-eccb2c155cd7' // Key Vault Secrets Off
    principalType: 'User'
  }
]

resource kvRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = [for role in roleAssignments: {
  name: guid(kv.id, role.principalId, role.roleId)
  scope: kv
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', role.roleId)
    principalId: role.principalId
    principalType: role.principalType
  }
}]

output keyVaultId string = kv.id
output keyVaultName string = kv.name

@description('Azure AI Services account name (globally unique, 3-64 chars)')
param accountName string

@description('Location for the resource')
param location string

@description('Principal ID of the backend managed identity (granted OpenAI User role)')
param backendPrincipalId string

@description('GPT-4o model version to deploy')
param modelVersion string = '2024-11-20'

@description('Tokens-per-minute capacity in thousands (10 = 10K TPM)')
param capacityK int = 10

@description('Deployment SKU: GlobalStandard (pay-as-you-go global) or Standard (regional). Use Standard on VS Dev subscriptions.')
param deploymentSku string = 'GlobalStandard'

resource aiAccount 'Microsoft.CognitiveServices/accounts@2024-10-01' = {
  name: accountName
  location: location
  kind: 'AIServices'
  sku: {
    name: 'S0'
  }
  properties: {
    customSubDomainName: accountName
    publicNetworkAccess: 'Enabled'
    disableLocalAuth: true
  }
}

resource gpt4oDeployment 'Microsoft.CognitiveServices/accounts/deployments@2024-10-01' = {
  parent: aiAccount
  name: 'gpt-4o'
  sku: {
    name: deploymentSku
    capacity: capacityK
  }
  properties: {
    model: {
      format: 'OpenAI'
      name: 'gpt-4o'
      version: modelVersion
    }
  }
}

// Grant the backend managed identity the Cognitive Services OpenAI User role
resource roleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(aiAccount.id, backendPrincipalId, 'CognitiveServicesOpenAIUser')
  scope: aiAccount
  properties: {
    roleDefinitionId: subscriptionResourceId(
      'Microsoft.Authorization/roleDefinitions',
      '5e0bd9bd-7b93-4f28-af87-19fc36ad61bd'
    )
    principalId: backendPrincipalId
    principalType: 'ServicePrincipal'
  }
}

output endpoint string = aiAccount.properties.endpoint
output accountName string = aiAccount.name

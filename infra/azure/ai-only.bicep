targetScope = 'subscription'

@description('Existing resource group name.')
param rgName string

@description('Resource group location.')
param rgLocation string

@description('Azure AI Services account name (globally unique).')
param aiAccountName string

@description('Principal ID of the backend managed identity.')
param backendPrincipalId string

@description('Deployment SKU. Use Standard on VS Dev subscriptions.')
param deploymentSku string = 'GlobalStandard'

@description('TPM capacity in thousands.')
param capacityK int = 10

resource rg 'Microsoft.Resources/resourceGroups@2021-04-01' existing = {
  name: rgName
}

module aiFoundry './modules/ai-foundry.bicep' = {
  scope: rg
  params: {
    accountName: aiAccountName
    location: rgLocation
    backendPrincipalId: backendPrincipalId
    deploymentSku: deploymentSku
    capacityK: capacityK
  }
}

output endpoint string = aiFoundry.outputs.endpoint
output accountName string = aiFoundry.outputs.accountName

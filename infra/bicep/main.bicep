targetScope = 'subscription'

@description('Name of the resource group to create.')
param rgName string

@description('Location for the resource group.')
param rgLocation string

@description('Location for the resources (AKS, Key Vault, etc.).')
param resourceLocation string

@description('AKS cluster configuration object.')
param aksConfig object

@description('Key Vault name')
param keyVaultName string

resource rg 'Microsoft.Resources/resourceGroups@2021-04-01' = {
  name: rgName
  location: rgLocation
}

module aks './modules/aks.bicep' = {
  name: 'aks-deployment'
  scope: rg
  params: {
    config: aksConfig
    location: resourceLocation
  }
}

module kv './modules/keyvault.bicep' = {
  name: 'keyvault-deployment'
  scope: rg
  params: {
    kvName: keyVaultName
    location: resourceLocation
    aksPrincipalId: aks.outputs.principalId
  }
}

output keyVaultName string = kv.outputs.keyVaultName
output aksClusterName string = aksConfig.clusterName

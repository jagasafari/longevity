targetScope = 'subscription'

@description('Name of the resource group to create.')
param rgName string

@description('Location for the resource group and all resources.')
param location string = 'eastus2'

@description('AKS cluster configuration object.')
param aksConfig object

resource rg 'Microsoft.Resources/resourceGroups@2021-04-01' = {
  name: rgName
  location: location
}

module aks './modules/aks.bicep' = {
  name: 'aks-deployment'
  scope: rg
  params: {
    config: aksConfig
    location: location
  }
}

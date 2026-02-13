@description('AKS cluster configuration object.')
param config object

@description('The location of the Managed Cluster resource.')
param location string

resource aks 'Microsoft.ContainerService/managedClusters@2024-02-01' = {
  name: config.clusterName
  location: location
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    dnsPrefix: config.dnsPrefix
    agentPoolProfiles: [
      {
        name: 'nodepool1'
        osDiskSizeGB: config.osDiskSizeGB
        count: config.agentCount
        vmSize: config.agentVMSize
        osType: 'Linux'
        mode: 'System'
      }
    ]
    networkProfile: {
      networkPlugin: 'azure'
      loadBalancerSku: 'standard'
    }
    addonProfiles: {
      azureKeyvaultSecretsProvider: {
        enabled: true
        config: {
          enableSecretRotation: 'true'
          rotationPollInterval: '2m'
        }
      }
    }
  }
}

output principalId string = aks.identity.principalId
output kubeletIdentityObjectId string = aks.properties.identityProfile.kubeletidentity.objectId

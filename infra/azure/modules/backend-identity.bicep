targetScope = 'resourceGroup'

param location string

@description('OIDC issuer URL of the AKS cluster (for federated identity credential).')
param oidcIssuerUrl string

@description('Kubernetes namespace the backend service account lives in.')
param k8sNamespace string = 'longevity'

@description('Kubernetes service account name for the backend.')
param k8sServiceAccountName string = 'backend-sa'

resource backendIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: 'longevity-backend-identity'
  location: location
}

resource federatedCredential 'Microsoft.ManagedIdentity/userAssignedIdentities/federatedIdentityCredentials@2023-01-31' = {
  parent: backendIdentity
  name: 'backend-k8s-federated'
  properties: {
    issuer: oidcIssuerUrl
    subject: 'system:serviceaccount:${k8sNamespace}:${k8sServiceAccountName}'
    audiences: [ 'api://AzureADTokenExchange' ]
  }
}

output clientId string = backendIdentity.properties.clientId
output principalId string = backendIdentity.properties.principalId

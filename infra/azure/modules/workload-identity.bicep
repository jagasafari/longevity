targetScope = 'resourceGroup'

param location string

@description('OIDC issuer URL of the AKS cluster (for federated identity credential).')
param oidcIssuerUrl string

@description('Unique name for the managed identity resource (e.g. longevity-backend-identity).')
param identityName string

@description('Short label used to name the federated credential resource (e.g. backend, worker).')
param credentialLabel string

@description('Kubernetes namespace the service account lives in.')
param k8sNamespace string = 'longevity'

@description('Kubernetes service account name to federate with (e.g. backend-sa, worker-sa).')
param k8sServiceAccountName string

resource identity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: identityName
  location: location
}

resource federatedCredential 'Microsoft.ManagedIdentity/userAssignedIdentities/federatedIdentityCredentials@2023-01-31' = {
  parent: identity
  name: '${credentialLabel}-k8s-federated'
  properties: {
    issuer: oidcIssuerUrl
    subject: 'system:serviceaccount:${k8sNamespace}:${k8sServiceAccountName}'
    audiences: [ 'api://AzureADTokenExchange' ]
  }
}

output clientId string = identity.properties.clientId
output principalId string = identity.properties.principalId

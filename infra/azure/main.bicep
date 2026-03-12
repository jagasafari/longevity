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

@description('Principal ID of the user deploying (for Key Vault access)')
param deployerPrincipalId string

@description('Storage account name for photo uploads (must be globally unique, lowercase, 3-24 chars)')
param storageAccountName string

@description('Display name for the Azure Monitor workbook to create.')
param workbookDisplayName string = 'Longevity Workbook'

@description('Log Analytics workspace name for AKS Container Insights.')
param logAnalyticsWorkspaceName string = 'longevity-workspace'

@description('Log Analytics retention in days (minimum 30 for PerGB2018 SKU).')
param logAnalyticsRetentionInDays int = 30

@description('Daily ingestion cap in GB (cost control).')
param logAnalyticsDailyQuotaGb int = 1

resource rg 'Microsoft.Resources/resourceGroups@2021-04-01' = {
  name: rgName
  location: rgLocation
}

module logAnalytics './modules/log-analytics.bicep' = {
  name: 'log-analytics-deployment'
  scope: rg
  params: {
    workspaceName: logAnalyticsWorkspaceName
    location: resourceLocation
    retentionInDays: logAnalyticsRetentionInDays
    dailyQuotaGb: logAnalyticsDailyQuotaGb
  }
}

module acr './modules/acr.bicep' = {
  name: 'acr-deployment'
  scope: rg
  params: {
    acrName: 'longevityacr'
    location: resourceLocation
  }
}

module aks './modules/aks.bicep' = {
  name: 'aks-deployment'
  scope: rg
  params: {
    config: aksConfig
    location: resourceLocation
    logAnalyticsWorkspaceResourceId: logAnalytics.outputs.workspaceId
  }
}

module kv './modules/keyvault.bicep' = {
  name: 'keyvault-deployment'
  scope: rg
  params: {
    kvName: keyVaultName
    location: resourceLocation
    kubeletPrincipalId: aks.outputs.kubeletIdentityObjectId
    deployerPrincipalId: deployerPrincipalId
  }
}

module acrPull './modules/acr-pull-assignment.bicep' = {
  name: 'acr-pull-assignment'
  scope: rg
  params: {
    acrName: acr.outputs.acrName
    kubeletIdentityObjectId: aks.outputs.kubeletIdentityObjectId
  }
}

module backendIdentity './modules/workload-identity.bicep' = {
  name: 'backend-identity-deployment'
  scope: rg
  params: {
    location: resourceLocation
    oidcIssuerUrl: aks.outputs.oidcIssuerUrl
    identityName: 'longevity-backend-identity'
    credentialLabel: 'backend'
    k8sServiceAccountName: 'backend-sa'
  }
}

module thumbnailWorkerIdentity './modules/workload-identity.bicep' = {
  name: 'thumbnail-worker-identity-deployment'
  scope: rg
  params: {
    location: resourceLocation
    oidcIssuerUrl: aks.outputs.oidcIssuerUrl
    identityName: 'longevity-thumbnail-worker-identity'
    credentialLabel: 'thumbnail-worker'
    k8sServiceAccountName: 'thumbnail-worker-sa'
  }
}

module storage './modules/storage.bicep' = {
  name: 'storage-deployment'
  scope: rg
  params: {
    storageAccountName: storageAccountName
    location: rgLocation
    backendPrincipalId: backendIdentity.outputs.principalId
    thumbnailWorkerPrincipalId: thumbnailWorkerIdentity.outputs.principalId
  }
}

module photoEvents './modules/photo-events.bicep' = {
  name: 'photo-events-deployment'
  scope: rg
  params: {
    location: rgLocation
    storageAccountId: storage.outputs.storageAccountId
    storageAccountName: storageAccountName
    sourceContainerName: 'photos'
    queueName: storage.outputs.thumbnailEventsQueueName
  }
}

module workbook './modules/workbook.bicep' = {
  name: 'workbook-deployment'
  scope: rg
  params: {
    workbookDisplayName: workbookDisplayName
    location: rgLocation
    sourceResourceId: logAnalytics.outputs.workspaceId
  }
}

output keyVaultName string = kv.outputs.keyVaultName
output aksClusterName string = aksConfig.clusterName
output acrLoginServer string = acr.outputs.acrLoginServer
output storageAccountName string = storage.outputs.storageAccountName
output backendIdentityClientId string = backendIdentity.outputs.clientId
output thumbnailWorkerIdentityClientId string = thumbnailWorkerIdentity.outputs.clientId
output logAnalyticsWorkspaceId string = logAnalytics.outputs.workspaceId
output workbookId string = workbook.outputs.workbookId
output workbookUrl string = workbook.outputs.workbookPortalUrl

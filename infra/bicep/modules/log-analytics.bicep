@description('Log Analytics workspace name.')
param workspaceName string

@description('Workspace location.')
param location string

@description('Retention in days (min 30 for Pay-As-You-Go SKU, 7 for legacy Free SKU).')
param retentionInDays int = 30

@description('Daily ingestion cap in GB (-1 means no cap).')
param dailyQuotaGb int = 1

resource workspace 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: workspaceName
  location: location
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: retentionInDays
    workspaceCapping: {
      dailyQuotaGb: dailyQuotaGb
    }
  }
}

output workspaceId string = workspace.id
output workspaceName string = workspace.name
output customerId string = workspace.properties.customerId

targetScope = 'subscription'

@description('Existing resource group name where workbook is deployed.')
param rgName string

@description('Resource group location (deployment location).')
param rgLocation string

@description('Display name for the Azure Monitor workbook to create/update.')
param workbookDisplayName string = 'Longevity Workbook'

@description('Optional: existing Log Analytics workspace name for workbook source context.')
param logAnalyticsWorkspaceName string = ''

resource rg 'Microsoft.Resources/resourceGroups@2021-04-01' existing = {
  name: rgName
}

resource workspace 'Microsoft.OperationalInsights/workspaces@2023-09-01' existing = if (!empty(logAnalyticsWorkspaceName)) {
  name: logAnalyticsWorkspaceName
  scope: rg
}

module workbook './modules/workbook.bicep' = {
  name: 'workbook-only-deployment'
  scope: rg
  params: {
    workbookDisplayName: workbookDisplayName
    location: rgLocation
    sourceResourceId: !empty(logAnalyticsWorkspaceName) ? workspace.id : subscription().id
  }
}

output workbookId string = workbook.outputs.workbookId
output workbookUrl string = workbook.outputs.workbookPortalUrl

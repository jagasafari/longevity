targetScope = 'resourceGroup'

@description('The friendly name for the workbook that is used in the Gallery or Saved List. This name must be unique within a resource group.')
param workbookDisplayName string = 'saxo systemtest'

@description('Subscription ID where the App Insights resource is located.')
param appInsightsSubscriptionId string = subscription().subscriptionId

@description('Resource group name where the App Insights resource is located.')
param appInsightsResourceGroup string = resourceGroup().name

@description('Name of the Application Insights resource.')
param appInsightsName string

var workbookId = guid(resourceGroup().id, workbookDisplayName)
var workbookContent = loadTextContent('output/workbook-body.json')
var workbookSourceId = '/subscriptions/${appInsightsSubscriptionId}/resourcegroups/${appInsightsResourceGroup}/providers/microsoft.insights/components/${appInsightsName}'

resource workbook 'Microsoft.Insights/workbooks@2022-04-01' = {
  name: workbookId
  location: resourceGroup().location
  kind: 'shared'
  properties: {
    displayName: workbookDisplayName
    serializedData: workbookContent
    version: '1.0'
    sourceId: workbookSourceId
    category: 'workbook'
  }
}

output workbookPortalWorkbookUrl string = 'https://portal.azure.com/#blade/AppInsightsExtension/UsageNotebookBlade/ComponentId/${uriComponent(workbookSourceId)}/ConfigurationId/${uriComponent(workbook.id)}'

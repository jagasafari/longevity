@description('Workbook display name shown in Azure Portal.')
param workbookDisplayName string

@description('Workbook location.')
param location string

@description('Resource ID used as workbook source context.')
param sourceResourceId string

var serializedData = loadTextContent('workbook.serialized.json')

var workbookName = guid(resourceGroup().id, workbookDisplayName)

resource workbook 'Microsoft.Insights/workbooks@2022-04-01' = {
  name: workbookName
  location: location
  kind: 'shared'
  properties: {
    displayName: workbookDisplayName
    category: 'workbook'
    sourceId: sourceResourceId
    serializedData: serializedData
    version: '1.0'
  }
}

output workbookId string = workbook.id
output workbookName string = workbook.name
output workbookPortalUrl string = 'https://portal.azure.com/#resource${workbook.id}/overview'

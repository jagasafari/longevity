@description('Workbook display name shown in Azure Portal.')
param workbookDisplayName string

@description('Workbook location.')
param location string

@description('Workbook category.')
param workbookCategory string = 'workbook'

@description('Resource ID used as workbook source context.')
param sourceResourceId string

@description('Workbook payload as serialized JSON.')
param serializedData string = '{"version":"Notebook/1.0","items":[],"isLocked":false}'

var workbookName = guid(resourceGroup().id, workbookDisplayName)

resource workbook 'Microsoft.Insights/workbooks@2023-06-01' = {
  name: workbookName
  location: location
  kind: 'shared'
  properties: {
    displayName: workbookDisplayName
    category: workbookCategory
    sourceId: sourceResourceId
    serializedData: serializedData
    version: 'Notebook/1.0'
  }
}

output workbookId string = workbook.id
output workbookName string = workbook.name
output workbookPortalUrl string = 'https://portal.azure.com/#resource${workbook.id}/overview'
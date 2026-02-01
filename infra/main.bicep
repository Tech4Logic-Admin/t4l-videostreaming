// ============================================================================
// Tech4Logic Video Search - Main Bicep Template
// ============================================================================
// This template deploys the complete Azure infrastructure for the video
// streaming platform including Container Apps, PostgreSQL, Storage, and more.
// ============================================================================

targetScope = 'subscription'

@minLength(1)
@maxLength(64)
@description('Name of the environment (e.g., dev, staging, prod)')
param environmentName string

@minLength(1)
@description('Primary location for all resources')
param location string

@description('Optional: Resource group name override')
param resourceGroupName string = ''

@description('Optional: PostgreSQL admin password (generated if not provided)')
@secure()
param dbAdminPassword string = ''

@description('Azure AD tenant ID for authentication')
param aadTenantId string = ''

@description('Azure AD client ID for authentication')
param aadClientId string = ''

@description('Tags to apply to all resources')
param tags object = {}

@description('Minimum replicas for Container Apps (0 = scale to zero for POC/demo, reduces cost when idle)')
param containerAppMinReplicas int = 1

@description('Use mock AI services (Video Indexer, Content Safety, Search) to avoid paid Azure AI costs - set true for POC/demo')
param useMockAiServices bool = false

// ============================================================================
// Variables
// ============================================================================

var abbrs = loadJsonContent('./abbreviations.json')
var resourceToken = toLower(uniqueString(subscription().id, environmentName, location))
var finalResourceGroupName = !empty(resourceGroupName) ? resourceGroupName : '${abbrs.resourcesResourceGroups}${environmentName}'
var finalTags = union(tags, {
  'azd-env-name': environmentName
  'application': 't4l-videosearch'
  'environment': environmentName
})

// Generate password if not provided
var generatedPassword = 'P@ss${uniqueString(subscription().id, environmentName)}!${take(resourceToken, 8)}'
var finalDbPassword = !empty(dbAdminPassword) ? dbAdminPassword : generatedPassword

// ============================================================================
// Resource Group
// ============================================================================

resource rg 'Microsoft.Resources/resourceGroups@2024-03-01' = {
  name: finalResourceGroupName
  location: location
  tags: finalTags
}

// ============================================================================
// Core Infrastructure Module
// ============================================================================

module core './modules/core.bicep' = {
  name: 'core-infrastructure'
  scope: rg
  params: {
    location: location
    environmentName: environmentName
    resourceToken: resourceToken
    tags: finalTags
  }
}

// ============================================================================
// Database Module (Azure Database for PostgreSQL Flexible Server)
// ============================================================================

module database './modules/database.bicep' = {
  name: 'database'
  scope: rg
  params: {
    location: location
    environmentName: environmentName
    resourceToken: resourceToken
    tags: finalTags
    adminPassword: finalDbPassword
    vnetSubnetId: core.outputs.databaseSubnetId
    privateDnsZoneId: core.outputs.postgresDnsZoneId
  }
}

// ============================================================================
// Storage Module (Azure Blob Storage)
// ============================================================================

module storage './modules/storage.bicep' = {
  name: 'storage'
  scope: rg
  params: {
    location: location
    environmentName: environmentName
    resourceToken: resourceToken
    tags: finalTags
  }
}

// ============================================================================
// Monitoring Module (Log Analytics + Application Insights)
// ============================================================================

module monitoring './modules/monitoring.bicep' = {
  name: 'monitoring'
  scope: rg
  params: {
    location: location
    environmentName: environmentName
    resourceToken: resourceToken
    tags: finalTags
  }
}

// ============================================================================
// Key Vault Module
// ============================================================================

module keyVault './modules/keyvault.bicep' = {
  name: 'keyvault'
  scope: rg
  params: {
    location: location
    environmentName: environmentName
    resourceToken: resourceToken
    tags: finalTags
    dbConnectionString: database.outputs.connectionString
    storageConnectionString: storage.outputs.connectionString
  }
}

// ============================================================================
// Container Apps Module
// ============================================================================

module containerApps './modules/container-apps.bicep' = {
  name: 'container-apps'
  scope: rg
  params: {
    location: location
    environmentName: environmentName
    resourceToken: resourceToken
    tags: finalTags
    containerAppsEnvironmentId: core.outputs.containerAppsEnvironmentId
    containerRegistryName: core.outputs.containerRegistryName
    keyVaultName: keyVault.outputs.keyVaultName
    appInsightsConnectionString: monitoring.outputs.appInsightsConnectionString
    aadTenantId: aadTenantId
    aadClientId: aadClientId
    dbHost: database.outputs.fqdn
    dbName: database.outputs.databaseName
    storageAccountName: storage.outputs.storageAccountName
    minReplicas: containerAppMinReplicas
    useMockAiServices: useMockAiServices
  }
}

// ============================================================================
// Outputs
// ============================================================================

output AZURE_LOCATION string = location
output AZURE_RESOURCE_GROUP string = rg.name

output AZURE_CONTAINER_REGISTRY_ENDPOINT string = core.outputs.containerRegistryLoginServer
output AZURE_CONTAINER_REGISTRY_NAME string = core.outputs.containerRegistryName

output AZURE_KEY_VAULT_NAME string = keyVault.outputs.keyVaultName
output AZURE_KEY_VAULT_ENDPOINT string = keyVault.outputs.keyVaultUri

output AZURE_STORAGE_ACCOUNT_NAME string = storage.outputs.storageAccountName
output AZURE_STORAGE_BLOB_ENDPOINT string = storage.outputs.blobEndpoint

output AZURE_POSTGRESQL_FQDN string = database.outputs.fqdn
output AZURE_POSTGRESQL_DATABASE string = database.outputs.databaseName

output AZURE_APP_INSIGHTS_CONNECTION_STRING string = monitoring.outputs.appInsightsConnectionString
output AZURE_LOG_ANALYTICS_WORKSPACE_ID string = monitoring.outputs.logAnalyticsWorkspaceId

output API_URL string = containerApps.outputs.apiUrl
output WEB_URL string = containerApps.outputs.webUrl

// ============================================================================
// POC/Demo deployment - minimal cost
// ============================================================================
// Use: az deployment sub create --location eastus --template-file main.bicep --parameters main-poc.bicepparam
// ============================================================================

using './main.bicep'

param environmentName = 'poc'
param location = 'eastus'

// Scale to zero when idle - no compute cost when demo is not in use
param containerAppMinReplicas = 0

// Use mock Video Indexer, Content Safety, and AI Search - $0 Azure AI cost
param useMockAiServices = true

param aadTenantId = ''   // Set via env or secret if using Entra auth
param aadClientId = ''   // Set via env or secret if using Entra auth

param tags = {
  project: 't4l-videosearch'
  environment: 'poc'
  managedBy: 'bicep'
}

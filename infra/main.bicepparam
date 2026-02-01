// ============================================================================
// Parameters file for production deployment
// ============================================================================

using './main.bicep'

param environmentName = 'prod'
param location = 'eastus'
param aadTenantId = '' // Set via environment or GitHub secret
param aadClientId = '' // Set via environment or GitHub secret
param tags = {
  project: 't4l-videosearch'
  environment: 'production'
  managedBy: 'bicep'
}

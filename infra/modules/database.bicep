// ============================================================================
// Database Module - Azure Database for PostgreSQL Flexible Server
// ============================================================================

param location string
param environmentName string
param resourceToken string
param tags object

@secure()
param adminPassword string

param vnetSubnetId string
param privateDnsZoneId string

var abbrs = loadJsonContent('../abbreviations.json')
var serverName = '${abbrs.databasePostgresql}${environmentName}-${resourceToken}'
var databaseName = 't4l_videosearch'
var adminUsername = 't4ladmin'

// ============================================================================
// PostgreSQL Flexible Server
// ============================================================================

resource postgresServer 'Microsoft.DBforPostgreSQL/flexibleServers@2023-12-01-preview' = {
  name: serverName
  location: location
  tags: tags
  sku: {
    name: 'Standard_B1ms'
    tier: 'Burstable'
  }
  properties: {
    version: '16'
    administratorLogin: adminUsername
    administratorLoginPassword: adminPassword
    storage: {
      storageSizeGB: 32
      autoGrow: 'Enabled'
    }
    backup: {
      backupRetentionDays: 7
      geoRedundantBackup: 'Disabled'
    }
    highAvailability: {
      mode: 'Disabled'
    }
    network: {
      delegatedSubnetResourceId: vnetSubnetId
      privateDnsZoneArmResourceId: privateDnsZoneId
    }
  }
}

// ============================================================================
// Database
// ============================================================================

resource database 'Microsoft.DBforPostgreSQL/flexibleServers/databases@2023-12-01-preview' = {
  parent: postgresServer
  name: databaseName
  properties: {
    charset: 'UTF8'
    collation: 'en_US.utf8'
  }
}

// ============================================================================
// PostgreSQL Extensions
// ============================================================================

resource uuidExtension 'Microsoft.DBforPostgreSQL/flexibleServers/configurations@2023-12-01-preview' = {
  parent: postgresServer
  name: 'azure.extensions'
  properties: {
    value: 'UUID-OSSP,PG_TRGM'
    source: 'user-override'
  }
}

// ============================================================================
// Firewall Rules (for Azure services)
// ============================================================================

resource allowAzureServices 'Microsoft.DBforPostgreSQL/flexibleServers/firewallRules@2023-12-01-preview' = {
  parent: postgresServer
  name: 'AllowAzureServices'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

// ============================================================================
// Outputs
// ============================================================================

output serverId string = postgresServer.id
output serverName string = postgresServer.name
output fqdn string = postgresServer.properties.fullyQualifiedDomainName
output databaseName string = database.name
output adminUsername string = adminUsername
output connectionString string = 'Host=${postgresServer.properties.fullyQualifiedDomainName};Port=5432;Database=${databaseName};Username=${adminUsername};Password=${adminPassword};Ssl Mode=Require;'

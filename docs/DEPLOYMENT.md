# Tech4Logic Video Search - Deployment Guide

## Table of Contents

1. [Overview](#overview)
2. [Local Development](#local-development)
3. [Azure Deployment](#azure-deployment)
4. [CI/CD Pipeline](#cicd-pipeline)
5. [Environment Configuration](#environment-configuration)
6. [Monitoring & Observability](#monitoring--observability)
7. [Security](#security)
8. [Troubleshooting](#troubleshooting)
9. [Runbook](#runbook)

---

## Overview

The Tech4Logic Video Search platform is deployed to Azure using:

- **Azure Container Apps** - Scalable container hosting for API and Web
- **Azure Database for PostgreSQL** - Managed database service
- **Azure Blob Storage** - Video and asset storage
- **Azure Container Registry** - Private container images
- **Azure Key Vault** - Secrets management
- **Application Insights** - Monitoring and observability

---

## Local Development

### Prerequisites

- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (v24+)
- [Node.js](https://nodejs.org/) (v20+)
- [pnpm](https://pnpm.io/) (v9+)
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

### Quick Start with Docker

```bash
# Clone the repository
git clone https://github.com/Tech4Logic-Admin/t4l-videostreaming.git
cd t4l-videostreaming

# Start all services
docker compose up -d

# Or use the helper scripts
make up          # Linux/Mac
dev.cmd up       # Windows
```

Services will be available at:
- **Web**: http://localhost:3000
- **API**: http://localhost:5000
- **Swagger**: http://localhost:5000/swagger

### Local Development (without Docker)

```bash
# Install dependencies
pnpm install
cd apps/api && dotnet restore
cd ../worker && dotnet restore
cd ../..

# Start PostgreSQL and Azurite
docker compose up -d postgres azurite

# Start API (terminal 1)
cd apps/api
dotnet watch run

# Start Web (terminal 2)
cd apps/web
pnpm dev

# Start Worker (terminal 3, optional)
cd apps/worker
func start
```

### Database Migrations

```bash
# Create migration
cd apps/api
dotnet ef migrations add <MigrationName>

# Apply migrations
dotnet ef database update

# Reset database
make db-reset    # or dev.cmd db-reset
make seed        # or dev.cmd seed
```

### Running Tests

```bash
# All tests
make test        # or dev.cmd test

# API tests only
cd apps/api && dotnet test

# Web tests only
cd apps/web && pnpm test

# E2E tests
cd packages/e2e && pnpm test
```

---

## Azure Deployment

### Prerequisites

- Azure CLI (`az`)
- Azure subscription with required permissions
- Resource group created

### Infrastructure as Code (Bicep)

The `infra/` directory contains Bicep templates for Azure resources.

```bash
# Login to Azure
az login

# Set subscription
az account set --subscription "<subscription-id>"

# Deploy infrastructure
cd infra
az deployment sub create \
  --location eastus \
  --template-file main.bicep \
  --parameters environmentName=prod location=eastus
```

### Manual Provisioning

Some services require manual setup:

1. **Azure Video Indexer**
   - Create account in Azure Portal
   - Note the Account ID, Location, and Resource Group

2. **Microsoft Entra ID**
   - Register application
   - Configure redirect URIs
   - Set up API permissions

### Deployment Steps

1. **Deploy Infrastructure**
   ```bash
   az deployment sub create --template-file infra/main.bicep
   ```

2. **Configure Key Vault Secrets**
   ```bash
   az keyvault secret set --vault-name <vault-name> --name "DbConnectionString" --value "<connection-string>"
   az keyvault secret set --vault-name <vault-name> --name "VideoIndexerKey" --value "<api-key>"
   ```

3. **Deploy API**
   ```bash
   cd apps/api
   dotnet publish -c Release
   az webapp deploy --resource-group <rg> --name <app-name> --src-path ./publish
   ```

4. **Deploy Web**
   ```bash
   cd apps/web
   pnpm build
   az staticwebapp deploy
   ```

5. **Deploy Worker**
   ```bash
   cd apps/worker
   func azure functionapp publish <function-app-name>
   ```

---

## Environment Configuration

### Required Environment Variables

#### API Service

| Variable | Description | Example |
|----------|-------------|---------|
| `ConnectionStrings__DefaultConnection` | PostgreSQL connection string | `Host=...;Database=...;Username=...;Password=...` |
| `BlobStorage__ConnectionString` | Azure Blob Storage connection | `DefaultEndpointsProtocol=https;...` |
| `AzureAd__TenantId` | Entra ID tenant | `xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx` |
| `AzureAd__ClientId` | Entra ID client ID | `xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx` |
| `VideoIndexer__AccountId` | Video Indexer account | `xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx` |
| `AzureSearch__Endpoint` | AI Search endpoint | `https://xxx.search.windows.net` |
| `ContentSafety__Endpoint` | Content Safety endpoint | `https://xxx.cognitiveservices.azure.com` |

#### Feature Flags

| Flag | Default | Description |
|------|---------|-------------|
| `FeatureFlags__UseDevAuth` | `false` | Enable dev auth (local only) |
| `FeatureFlags__UseMockVideoIndexer` | `false` | Use mock transcription |
| `FeatureFlags__UseMockContentSafety` | `false` | Use mock moderation |
| `FeatureFlags__UseMockSearch` | `false` | Use in-memory search |

### Key Vault Integration

In production, reference secrets from Key Vault:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "@Microsoft.KeyVault(VaultName=t4l-kv;SecretName=DbConnectionString)"
  }
}
```

---

## CI/CD Pipeline

### GitHub Actions

The repository includes GitHub Actions workflows:

- **ci.yml**: Build, lint, and test on every PR
- **deploy-staging.yml**: Deploy to staging on merge to `main`
- **deploy-production.yml**: Deploy to production on release tag

### Required GitHub Secrets

| Secret | Description |
|--------|-------------|
| `AZURE_CREDENTIALS` | Service principal JSON |
| `AZURE_SUBSCRIPTION_ID` | Azure subscription ID |
| `AZURE_RESOURCE_GROUP` | Target resource group |

### Setting Up Service Principal

```bash
az ad sp create-for-rbac \
  --name "t4l-videosearch-deploy" \
  --role contributor \
  --scopes /subscriptions/<subscription-id>/resourceGroups/<resource-group> \
  --sdk-auth
```

Copy the JSON output to the `AZURE_CREDENTIALS` secret.

---

## Troubleshooting

### Docker Issues

```bash
# View logs
docker compose logs -f

# Rebuild images
docker compose build --no-cache

# Reset everything
docker compose down -v
docker compose up -d
```

### Database Connection Issues

```bash
# Test connection
docker compose exec postgres psql -U postgres -d t4l_videosearch -c "SELECT 1"

# View database logs
docker compose logs postgres
```

### API Health Check

```bash
curl http://localhost:5000/healthz
curl http://localhost:5000/readyz
```

### Common Errors

| Error | Solution |
|-------|----------|
| `ECONNREFUSED` | Ensure Docker services are running |
| `401 Unauthorized` | Check auth configuration |
| `Blob not found` | Ensure Azurite containers are created |
| `Database does not exist` | Run database migrations or seed script |

---

## Monitoring & Observability

### Application Insights

All telemetry is sent to Application Insights:

- **Requests** - API call tracking
- **Dependencies** - Database, storage calls
- **Exceptions** - Error tracking
- **Custom Events** - Business metrics

### Custom Metrics

| Event | Description |
|-------|-------------|
| `VideoUpload` | Video upload completed |
| `VideoProcessing` | Processing pipeline step |
| `SearchQuery` | Search query executed |
| `ContentModeration` | Moderation decision |

### Alerts

Pre-configured alerts:

| Alert | Condition | Severity |
|-------|-----------|----------|
| High Error Rate | >5% failed requests | 2 (Warning) |
| High Response Time | >2s average | 3 (Informational) |

### Log Queries (KQL)

```kusto
// Error rate by endpoint
requests
| where timestamp > ago(1h)
| summarize errorRate = countif(success == false) * 100.0 / count() by name
| order by errorRate desc

// Slow requests
requests
| where timestamp > ago(1h) and duration > 2000
| project timestamp, name, duration, resultCode
| order by duration desc
```

---

## Security

### Security Headers

All responses include:

| Header | Value |
|--------|-------|
| X-Content-Type-Options | nosniff |
| X-Frame-Options | DENY |
| X-XSS-Protection | 1; mode=block |
| Strict-Transport-Security | max-age=31536000 |

### Rate Limiting

| Policy | Limit | Window |
|--------|-------|--------|
| Global | 200 requests | 10 seconds |
| Upload | 20 requests | 1 minute |
| Search | 100 requests | 30 seconds |
| Auth | 20 requests | 1 minute |

### Authentication

- **Production**: Microsoft Entra ID JWT
- **Development**: Header-based dev auth

---

## Runbook

### Deployment Checklist

- [ ] All tests passing in CI
- [ ] Staging deployment successful
- [ ] Database migrations applied (if any)
- [ ] Release notes prepared

### Rollback

```bash
# List revisions
az containerapp revision list \
  --name ca-staging-api \
  --resource-group rg-staging

# Activate previous revision
az containerapp revision activate \
  --name ca-staging-api \
  --resource-group rg-staging \
  --revision ca-staging-api--<previous-revision>
```

### Scaling

```bash
# Scale API to handle high load
az containerapp update \
  --name ca-prod-api \
  --resource-group rg-prod \
  --min-replicas 3 \
  --max-replicas 20
```

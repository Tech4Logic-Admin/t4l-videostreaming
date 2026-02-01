# Tech4Logic Video Search - Operations Runbook

## Table of Contents

1. [Service Overview](#service-overview)
2. [Monitoring & Alerts](#monitoring--alerts)
3. [Common Operations](#common-operations)
4. [Incident Response](#incident-response)
5. [Disaster Recovery](#disaster-recovery)
6. [Maintenance Procedures](#maintenance-procedures)

---

## Service Overview

### Components

| Component | Technology | Purpose |
|-----------|------------|---------|
| Web | Next.js on Container Apps | Frontend UI |
| API | ASP.NET Core on Container Apps | REST API |
| Worker | Azure Functions | Video processing |
| Database | Azure PostgreSQL | Metadata storage |
| Storage | Azure Blob Storage | Video files |
| Search | Azure AI Search | Transcript search |

### Health Endpoints

| Service | Endpoint | Expected Response |
|---------|----------|-------------------|
| API | `/healthz` | `200 OK` |
| API | `/readyz` | `200 OK` |
| Worker | `/api/healthz` | `200 OK` |

---

## Monitoring & Alerts

### Key Metrics

| Metric | Normal Range | Alert Threshold |
|--------|--------------|-----------------|
| API Response Time (p95) | < 200ms | > 500ms |
| API Error Rate | < 1% | > 5% |
| Processing Queue Depth | < 10 | > 50 |
| Database Connections | < 80% | > 90% |
| Storage Throttling | 0 | > 10/min |

### Application Insights Queries

#### Error Rate
```kusto
requests
| where timestamp > ago(1h)
| summarize ErrorRate = countif(success == false) * 100.0 / count() by bin(timestamp, 5m)
```

#### Slow Requests
```kusto
requests
| where timestamp > ago(1h)
| where duration > 1000
| project timestamp, name, duration, resultCode
| order by duration desc
```

#### Processing Job Status
```kusto
customEvents
| where name == "VideoProcessingCompleted"
| summarize count() by tostring(customDimensions.status), bin(timestamp, 1h)
```

### Alerts Configuration

1. **High Error Rate**
   - Condition: Error rate > 5% over 5 minutes
   - Action: Page on-call

2. **Processing Stuck**
   - Condition: No completed jobs in 30 minutes
   - Action: Email team

3. **Database Near Limit**
   - Condition: Connections > 90%
   - Action: Slack notification

---

## Common Operations

### Scaling

#### Scale API
```bash
az containerapp update \
  --name t4l-api \
  --resource-group t4l-rg \
  --min-replicas 2 \
  --max-replicas 10
```

#### Scale Database
```bash
az postgres flexible-server update \
  --resource-group t4l-rg \
  --name t4l-postgres \
  --sku-name Standard_D4s_v3
```

### Reprocess a Video

```bash
# Trigger reprocessing via API
curl -X POST "https://api.t4l.com/api/admin/videos/{videoId}/reprocess" \
  -H "Authorization: Bearer $TOKEN"
```

### Clear Processing Queue

```bash
# Restart the function app to clear in-memory state
az functionapp restart --name t4l-worker --resource-group t4l-rg
```

### View Logs

```bash
# API logs
az containerapp logs show --name t4l-api --resource-group t4l-rg --follow

# Worker logs
func azure functionapp logstream t4l-worker
```

### Database Operations

#### Connect to Database
```bash
az postgres flexible-server connect \
  --name t4l-postgres \
  --admin-user admin \
  --admin-password $DB_PASSWORD \
  --database-name t4l_videosearch
```

#### Backup Database
```bash
az postgres flexible-server backup create \
  --resource-group t4l-rg \
  --name t4l-postgres
```

---

## Incident Response

### Severity Levels

| Level | Description | Response Time |
|-------|-------------|---------------|
| P1 | Service down | 15 minutes |
| P2 | Major feature broken | 1 hour |
| P3 | Minor issue | 4 hours |
| P4 | Low impact | 24 hours |

### Incident Workflow

1. **Acknowledge** - Confirm receipt of alert
2. **Assess** - Determine scope and severity
3. **Communicate** - Update status page
4. **Mitigate** - Apply temporary fix
5. **Resolve** - Implement permanent fix
6. **Post-mortem** - Document lessons learned

### Common Incidents

#### API Not Responding

1. Check health endpoint
2. Check Application Insights for errors
3. Check container status: `az containerapp show --name t4l-api ...`
4. Check database connectivity
5. Restart if needed: `az containerapp revision restart ...`

#### Processing Queue Backed Up

1. Check worker function health
2. Check for failed jobs in database
3. Check Azure Video Indexer quota
4. Scale up worker if needed
5. Consider manual processing for urgent videos

#### Search Not Working

1. Check AI Search service status
2. Verify index exists and has documents
3. Check indexer status
4. Rebuild index if corrupted

#### High Latency

1. Check database query performance
2. Check network latency to Azure services
3. Review recent deployments
4. Check for resource exhaustion
5. Scale up affected services

---

## Disaster Recovery

### Backup Strategy

| Data | Backup Frequency | Retention |
|------|------------------|-----------|
| PostgreSQL | Daily + WAL | 35 days |
| Blob Storage | GRS replication | Continuous |
| Search Index | Rebuild from DB | N/A |

### Recovery Procedures

#### Database Recovery

```bash
# Point-in-time restore
az postgres flexible-server restore \
  --resource-group t4l-rg \
  --name t4l-postgres-restored \
  --source-server t4l-postgres \
  --restore-point-in-time "2024-01-15T10:00:00Z"
```

#### Rebuild Search Index

```bash
# Trigger full reindex via API
curl -X POST "https://api.t4l.com/api/admin/search/reindex" \
  -H "Authorization: Bearer $TOKEN"
```

### Failover Procedure

1. Verify primary region is unavailable
2. Update DNS to point to secondary region
3. Verify data consistency
4. Communicate to users
5. Plan failback when primary recovers

---

## Maintenance Procedures

### Scheduled Maintenance Window

- **When**: Sundays 02:00-06:00 UTC
- **Notice**: 48 hours advance notice
- **Duration**: Max 2 hours

### Deployment Checklist

- [ ] Backup database
- [ ] Run tests in staging
- [ ] Update status page
- [ ] Deploy with rollback plan
- [ ] Verify health checks
- [ ] Monitor for 30 minutes
- [ ] Update status page

### Database Maintenance

#### Vacuum/Analyze (Weekly)
```sql
VACUUM ANALYZE;
```

#### Update Statistics
```sql
ANALYZE video_assets;
ANALYZE transcript_segments;
```

### Certificate Renewal

Certificates are managed by Azure and auto-renewed. Monitor expiration:

```bash
az network app-gateway ssl-cert show \
  --resource-group t4l-rg \
  --gateway-name t4l-gateway \
  --name t4l-cert
```

### Version Upgrades

#### API/Web Update
```bash
# Build and push new image
docker build -t t4lacr.azurecr.io/api:v1.2.0 .
docker push t4lacr.azurecr.io/api:v1.2.0

# Update container app
az containerapp update \
  --name t4l-api \
  --resource-group t4l-rg \
  --image t4lacr.azurecr.io/api:v1.2.0
```

#### Database Migration
```bash
# Apply migrations
cd apps/api
dotnet ef database update --connection "$CONNECTION_STRING"
```

---

## Contact Information

| Role | Contact |
|------|---------|
| On-Call Engineer | PagerDuty |
| Engineering Lead | [email] |
| Security Team | security@tech4logic.com |
| Azure Support | Azure Portal |

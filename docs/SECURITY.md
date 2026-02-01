# Tech4Logic Video Search - Security Documentation

## Overview

This document describes the security architecture, controls, and best practices implemented in the Tech4Logic Video Search platform.

## Authentication

### Production: Microsoft Entra ID

The platform uses Microsoft Entra ID (formerly Azure AD) for authentication:

- **Protocol**: OpenID Connect (OIDC)
- **Token Type**: JWT Bearer tokens
- **Token Validation**:
  - Signature verification against Entra ID keys
  - Issuer validation
  - Audience validation
  - Expiration check

### Development: Dev Auth Mode

For local development, a dev auth mode is available (controlled by `FeatureFlags:UseDevAuth`):

```http
X-Dev-User-Id: dev-admin-oid
X-Dev-User-Role: Admin
X-Dev-User-Groups: admins,all-users
```

⚠️ **Warning**: Dev auth mode must NEVER be enabled in production.

## Authorization (RBAC)

### Roles

| Role | Permissions |
|------|-------------|
| **Admin** | Full access to all features and all videos |
| **Reviewer** | View moderation queue, approve/reject videos |
| **Uploader** | Upload videos, view own uploads |
| **Viewer** | Search and view authorized videos only |

### Role Assignment

Roles are assigned through:
1. Entra ID group membership (mapped in configuration)
2. Database `user_profiles.role` field (cached from Entra claims)

### Security Trimming

All search and list operations apply security trimming:

```csharp
// Videos are filtered by:
// 1. User's OID is in allowed_user_oids
// 2. Any of user's groups is in allowed_group_ids
// 3. Video has no ACL restrictions (public within organization)
var accessibleVideos = videos.Where(v =>
    v.AllowedUserOids.Contains(userOid) ||
    v.AllowedGroupIds.Any(g => userGroups.Contains(g)) ||
    (v.AllowedUserOids.Length == 0 && v.AllowedGroupIds.Length == 0)
);
```

## Blob Storage Security

### Container Structure

| Container | Purpose | Access |
|-----------|---------|--------|
| `quarantine` | Newly uploaded videos awaiting processing | Write-only SAS for uploads |
| `approved` | Videos that passed moderation | Read-only SAS for playback |

### SAS Token Security

- **Upload**: Short-lived (15 min) write-only SAS
- **Playback**: Short-lived (1 hour) read-only SAS
- **Production**: User Delegation SAS (more secure than account keys)

```csharp
// Example: Generate user delegation SAS
var userDelegationKey = await blobServiceClient.GetUserDelegationKeyAsync(
    DateTimeOffset.UtcNow,
    DateTimeOffset.UtcNow.AddHours(1)
);
```

### Never Expose Raw URLs

The API never returns raw blob URLs. All access goes through:
1. API validates user authorization
2. API generates short-lived signed URL
3. Client uses signed URL for direct access

## Content Safety

### Processing Pipeline

1. **Malware Scan**
   - Azure Defender for Storage scans all uploads
   - Infected files are deleted and flagged

2. **Content Moderation**
   - Frame sampling: 1 frame/second + scene changes
   - Image analysis via Azure Content Safety
   - Transcript text analysis after transcription

3. **Severity Levels**
   | Level | Action |
   |-------|--------|
   | None/Low | Auto-approve |
   | Medium | Queue for review |
   | High | Auto-quarantine, require review |

### Reviewer Workflow

1. Video appears in moderation queue
2. Reviewer views video with moderation reasons
3. Reviewer approves (move to `approved`) or rejects (delete)
4. All decisions are audit logged

## Input Validation

### File Uploads

| Check | Limit |
|-------|-------|
| File size | Max 5 GB |
| File types | mp4, mov, avi, webm, mkv |
| Filename | Sanitized, max 255 chars |

### API Requests

- All inputs validated via FluentValidation
- SQL injection prevented via parameterized queries (EF Core)
- XSS prevented via output encoding

### Rate Limiting

| Endpoint | Limit |
|----------|-------|
| Upload | 10/minute |
| Search | 60/minute |
| Playback | 100/minute |
| Other | 120/minute |

## Audit Logging

### Logged Events

| Event | Details Captured |
|-------|-----------------|
| `user.login` | OID, IP, user agent |
| `video.uploaded` | Video ID, title, uploader |
| `video.viewed` | Video ID, viewer OID |
| `video.played` | Video ID, viewer OID, start time |
| `video.searched` | Query, results count, latency |
| `video.approved` | Video ID, reviewer OID, notes |
| `video.rejected` | Video ID, reviewer OID, notes |
| `video.deleted` | Video ID, actor OID, reason |

### Log Retention

- Audit logs: 2 years
- Search logs: 90 days
- Application logs: 30 days

## Secrets Management

### Local Development

- Secrets stored in `appsettings.Development.json` (gitignored)
- Or via environment variables

### Production

- All secrets stored in Azure Key Vault
- App configuration references Key Vault secrets
- Managed Identity for Key Vault access

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "@Microsoft.KeyVault(VaultName=t4l-kv;SecretName=DbConnectionString)"
  }
}
```

### Secrets Rotation

- Database passwords: Rotate quarterly
- API keys: Rotate on compromise or annually
- Storage keys: Use Managed Identity instead

## Network Security

### Production Recommendations

1. **API**
   - Deploy to Azure Container Apps with VNet integration
   - Use Azure Front Door for WAF and DDoS protection
   - Enable TLS 1.2+ only

2. **Database**
   - Azure Private Link
   - No public endpoint
   - Firewall rules for specific IPs

3. **Storage**
   - Azure Private Link
   - Service endpoints from VNet

## Security Headers

The API sets these security headers:

```
X-Content-Type-Options: nosniff
X-Frame-Options: DENY
X-XSS-Protection: 1; mode=block
Content-Security-Policy: default-src 'self'
Strict-Transport-Security: max-age=31536000; includeSubDomains
```

## Threat Model

### Key Threats & Mitigations

| Threat | Mitigation |
|--------|------------|
| Unauthorized access to videos | RBAC + security trimming |
| Malware distribution | Malware scanning + quarantine |
| Inappropriate content | AI moderation + human review |
| Data exfiltration | Audit logging + rate limits |
| Token theft | Short-lived tokens, secure storage |
| SQL injection | Parameterized queries |
| XSS | Output encoding, CSP headers |

## Compliance Considerations

### Data Residency

- All data stored in specified Azure region
- User can choose region at deployment

### Data Retention

- Video content: Configurable retention
- Audit logs: 2 years minimum
- User data: Deleted on request (GDPR)

### Access Reviews

- Quarterly review of admin access
- Annual review of role assignments

## Security Checklist for Deployment

- [ ] Disable dev auth mode
- [ ] Enable HTTPS only
- [ ] Configure Key Vault
- [ ] Set up Azure Defender
- [ ] Configure WAF rules
- [ ] Enable audit logging
- [ ] Set up alerts for security events
- [ ] Review RBAC configuration
- [ ] Test security trimming
- [ ] Validate rate limits

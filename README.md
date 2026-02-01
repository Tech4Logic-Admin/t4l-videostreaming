# Tech4Logic Video Search Platform

A multilingual, enterprise-grade video content search platform built for Dynamics 365 Partners.

## 🏗️ Architecture

- **Frontend**: Next.js 15 + React 19 + shadcn/ui + TailwindCSS
- **Backend**: ASP.NET Core 8 Web API
- **Database**: PostgreSQL 16
- **Storage**: Azure Blob Storage (Azurite for local dev)
- **Auth**: Microsoft Entra ID (with dev mode for local testing)

## 📁 Project Structure

```
t4l-videostreaming/
├── apps/
│   ├── api/                 # ASP.NET Core 8 Web API
│   │   ├── Auth/           # Authentication & Authorization
│   │   ├── Controllers/    # API endpoints
│   │   ├── Domain/         # Domain entities
│   │   └── Infrastructure/ # Persistence, adapters
│   └── web/                # Next.js 15 frontend
│       ├── src/
│       │   ├── app/        # App router pages
│       │   ├── components/ # React components
│       │   └── lib/        # Utilities & hooks
│       └── ...
├── docker-compose.yml      # Local development stack
└── docs/                   # Documentation
```

## 🚀 Quick Start

### Prerequisites

- Docker & Docker Compose
- Node.js 20+ (for local frontend development)
- .NET 8 SDK (for local API development)

### Running with Docker

```bash
# Start all services
docker compose up -d

# View logs
docker compose logs -f

# Stop services
docker compose down
```

### Access Points

| Service | URL | Description |
|---------|-----|-------------|
| Frontend | http://localhost:3000 | Next.js web application |
| API | http://localhost:5000 | ASP.NET Core API |
| Swagger | http://localhost:5000/swagger | API documentation |
| API Health | http://localhost:5000/health | Health check endpoint |

## 🔐 Authentication

### Development Mode

In development, authentication uses header-based dev auth (no Azure AD required):

```bash
# Test as Admin
curl -H "X-Dev-User: admin-test" -H "X-Dev-Role: Admin" http://localhost:5000/api/auth/me

# Test as Viewer
curl -H "X-Dev-User: viewer-test" -H "X-Dev-Role: Viewer" http://localhost:5000/api/auth/me

# Get available dev users
curl http://localhost:5000/api/auth/dev-users
```

### Roles & Permissions

| Role | Permissions |
|------|-------------|
| Admin | Full access to all features |
| Uploader | Can upload videos, view own videos |
| Reviewer | Can view and moderate pending videos |
| Viewer | Can view published videos only |

### Production Mode

Set `FeatureFlags__UseDevAuth=false` and configure Azure AD:

```env
AzureAd__TenantId=<your-tenant-id>
AzureAd__ClientId=<your-client-id>
AzureAd__Domain=<your-domain>.onmicrosoft.com
```

## 📋 API Endpoints

### Auth
- `GET /api/auth/me` - Get current user profile
- `GET /api/auth/check` - Simple auth check
- `GET /api/auth/dev-users` - List available dev users (dev mode only)

### Videos
- `GET /api/videos` - List videos (with security trimming)
- `GET /api/videos/{id}` - Get video details
- `GET /api/videos/pending-review` - Review queue (Reviewer/Admin only)
- `POST /api/videos/{id}/approve` - Approve video (Reviewer/Admin only)
- `POST /api/videos/{id}/reject` - Reject video (Reviewer/Admin only)

### Search
- `GET /api/search?q={query}` - Search video transcripts
- `GET /api/search/suggest?q={query}` - Get search suggestions

### Audit (Admin only)
- `GET /api/audit` - List audit logs with filtering
- `GET /api/audit/{id}` - Get audit log details
- `GET /api/audit/stats` - Get audit statistics
- `GET /api/audit/target/{type}/{id}` - Get logs for specific target
- `GET /api/audit/export` - Export audit logs as CSV

## 🛠️ Development

### Frontend Development

```bash
cd apps/web
npm install
npm run dev
```

### API Development

```bash
cd apps/api
dotnet restore
dotnet run
```

### Environment Variables

See `apps/api/appsettings.Development.json` for API configuration.
See `apps/web/.env.local.example` for frontend configuration.

## ✅ Implementation Progress

### Completed Phases

- [x] **Phase 0**: Project scaffolding, Docker Compose setup
- [x] **Phase 1**: Authentication & RBAC (Entra ID JWT, dev auth, policies)
- [x] **Phase 2**: Video upload with chunked upload & Azure Blob Storage
- [x] **Phase 3**: Video processing pipeline (transcription, moderation, indexing)
- [x] **Phase 4**: ABR streaming with FFmpeg, HLS/DASH support
- [x] **Phase 5**: Search & discovery with PostgreSQL FTS, faceted search
- [x] **Phase 6**: Content moderation workflow with AI detection
- [x] **Phase 7**: Security hardening & optimization
- [x] **Phase 8**: Production deployment (Azure Container Apps, CI/CD, monitoring)

### Phase 7 Features

**Rate Limiting:**
- Global: 100 requests / 10 seconds
- Upload: 10 / minute (stricter for resource-intensive operations)
- Search: 50 / 30 seconds (sliding window for bursts)
- Auth: 10 / minute (prevents brute force)
- Per-user rate limiting based on identity

**Security Headers:**
- X-Content-Type-Options: nosniff
- X-Frame-Options: DENY
- X-XSS-Protection: 1; mode=block
- Content-Security-Policy (configurable)
- Strict-Transport-Security (HSTS)
- Referrer-Policy: strict-origin-when-cross-origin
- Permissions-Policy

**Audit Logging:**
- Automatic action logging via attributes
- Admin endpoints for audit review
- Export functionality
- Security event tracking

**Caching:**
- Response caching with output cache policies
- Memory cache for frequently accessed data
- Cache invalidation service

**Input Validation:**
- Input sanitization for XSS prevention
- FluentValidation validators
- Request size limiting

### Phase 8 Features

**Azure Infrastructure (Bicep):**
- Container Apps Environment with VNet integration
- Azure Database for PostgreSQL Flexible Server
- Azure Blob Storage with containers (quarantine, videos, streams, thumbnails, transcripts)
- Azure Container Registry for private images
- Azure Key Vault for secrets management
- Application Insights + Log Analytics for monitoring

**CI/CD Pipeline (GitHub Actions):**
- Automated build and test on every PR
- Staging deployment on merge to main
- Production deployment with manual approval on release tags
- Blue-green deployment strategy
- Post-deployment smoke tests

**Monitoring & Observability:**
- Application Insights telemetry integration
- Custom metrics for business events (VideoUpload, VideoProcessing, SearchQuery)
- Pre-configured alerts for error rate and response time
- Serilog integration with Application Insights sink

**Production Configuration:**
- Environment-specific appsettings (Production)
- Feature flags for service toggles
- Scalable Container Apps with auto-scaling rules
- RBAC-enabled Key Vault for secure secrets

## 📄 License

MIT License

# Tech4Logic Video Search - Architecture

## Overview

Tech4Logic Video Search is an enterprise-grade video content management and search platform that enables users to upload, process, and search through video content using AI-powered transcription and multilingual search capabilities.

## System Architecture

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                              Client Layer                                    │
├─────────────────────────────────────────────────────────────────────────────┤
│  ┌─────────────┐   ┌─────────────┐   ┌─────────────┐   ┌─────────────┐     │
│  │   Search    │   │   Upload    │   │   Player    │   │   Admin     │     │
│  │    Page     │   │    Page     │   │    Page     │   │  Dashboard  │     │
│  └──────┬──────┘   └──────┬──────┘   └──────┬──────┘   └──────┬──────┘     │
│         │                 │                 │                 │            │
│         └─────────────────┴────────┬────────┴─────────────────┘            │
│                                    │                                        │
│                            Next.js Frontend                                 │
│                            (React, TailwindCSS)                            │
└────────────────────────────────────┬────────────────────────────────────────┘
                                     │ HTTPS
                                     ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                              API Gateway                                     │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│                         ASP.NET Core Web API                                │
│                                                                             │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐   │
│  │    Auth      │  │   Videos     │  │   Search     │  │    Admin     │   │
│  │  Controller  │  │  Controller  │  │  Controller  │  │  Controller  │   │
│  └──────┬───────┘  └──────┬───────┘  └──────┬───────┘  └──────┬───────┘   │
│         │                 │                 │                 │            │
│         └─────────────────┴────────┬────────┴─────────────────┘            │
│                                    │                                        │
│  ┌─────────────────────────────────▼────────────────────────────────────┐  │
│  │                        Application Services                           │  │
│  │  ┌────────────┐  ┌────────────┐  ┌────────────┐  ┌────────────┐     │  │
│  │  │   Video    │  │   Search   │  │   User     │  │   Audit    │     │  │
│  │  │  Service   │  │  Service   │  │  Service   │  │  Service   │     │  │
│  │  └─────┬──────┘  └─────┬──────┘  └─────┬──────┘  └─────┬──────┘     │  │
│  └────────┼───────────────┼───────────────┼───────────────┼─────────────┘  │
│           │               │               │               │                │
│  ┌────────▼───────────────▼───────────────▼───────────────▼─────────────┐  │
│  │                        Infrastructure Adapters                        │  │
│  │  ┌────────────┐  ┌────────────┐  ┌────────────┐  ┌────────────┐     │  │
│  │  │   Blob     │  │  Video     │  │  Content   │  │   Search   │     │  │
│  │  │   Store    │  │  Indexer   │  │   Safety   │  │   Index    │     │  │
│  │  └─────┬──────┘  └─────┬──────┘  └─────┬──────┘  └─────┬──────┘     │  │
│  └────────┼───────────────┼───────────────┼───────────────┼─────────────┘  │
└───────────┼───────────────┼───────────────┼───────────────┼─────────────────┘
            │               │               │               │
            ▼               ▼               ▼               ▼
┌───────────────────────────────────────────────────────────────────────────────┐
│                              Azure Services                                    │
├───────────────────────────────────────────────────────────────────────────────┤
│  ┌────────────┐  ┌────────────┐  ┌────────────┐  ┌────────────┐             │
│  │   Blob     │  │   Video    │  │  Content   │  │    AI      │             │
│  │  Storage   │  │  Indexer   │  │   Safety   │  │   Search   │             │
│  └────────────┘  └────────────┘  └────────────┘  └────────────┘             │
│                                                                               │
│  ┌────────────┐  ┌────────────┐  ┌────────────┐                             │
│  │ PostgreSQL │  │  Key Vault │  │  Entra ID  │                             │
│  │  Database  │  │  (Secrets) │  │   (Auth)   │                             │
│  └────────────┘  └────────────┘  └────────────┘                             │
└───────────────────────────────────────────────────────────────────────────────┘

┌───────────────────────────────────────────────────────────────────────────────┐
│                           Background Processing                                │
├───────────────────────────────────────────────────────────────────────────────┤
│                                                                               │
│                    Azure Functions (Durable Functions)                        │
│                                                                               │
│  ┌─────────────────────────────────────────────────────────────────────────┐ │
│  │                    Video Processing Orchestrator                         │ │
│  │                                                                          │ │
│  │   ┌─────────┐   ┌─────────┐   ┌─────────┐   ┌─────────┐   ┌─────────┐ │ │
│  │   │ Malware │──▶│ Content │──▶│ Trans-  │──▶│ Search  │──▶│Complete │ │ │
│  │   │  Scan   │   │ Moderate│   │ cribe   │   │  Index  │   │         │ │ │
│  │   └─────────┘   └─────────┘   └─────────┘   └─────────┘   └─────────┘ │ │
│  └─────────────────────────────────────────────────────────────────────────┘ │
│                                                                               │
└───────────────────────────────────────────────────────────────────────────────┘
```

## Key Components

### Frontend (apps/web)
- **Technology**: Next.js 14, TypeScript, TailwindCSS, shadcn/ui
- **Features**:
  - Server-side rendering for SEO
  - Responsive design (mobile, tablet, desktop)
  - React Query for data fetching
  - Tech4Logic brand theming

### API (apps/api)
- **Technology**: ASP.NET Core 8, Entity Framework Core
- **Features**:
  - RESTful API with OpenAPI/Swagger
  - JWT authentication with Entra ID
  - Role-based access control (RBAC)
  - Security trimming on search
  - Structured logging with Serilog

### Worker (apps/worker)
- **Technology**: Azure Functions Durable (isolated .NET)
- **Features**:
  - Orchestrated video processing pipeline
  - Retry logic with exponential backoff
  - Idempotent operations
  - Status tracking and reporting

## Data Flow

### Video Upload Flow
```
1. User uploads video via Web UI
2. API creates VideoAsset record (status: Uploading)
3. API generates SAS URL for direct blob upload
4. Client uploads to Azure Blob Storage (quarantine container)
5. API updates status to Queued
6. Worker picks up video for processing
```

### Processing Pipeline
```
1. Malware Scan → Check for threats
2. Content Moderation → Check frames + transcript for policy violations
3. Transcription → Extract text with timestamps
4. Search Indexing → Push segments to search index
5. Status Update → Mark as Published or Quarantined
```

### Search Flow
```
1. User enters query (any language)
2. API applies security trimming (user's groups/permissions)
3. Query sent to Azure AI Search (hybrid: lexical + semantic)
4. Results grouped by video with segment timestamps
5. User clicks segment → Player jumps to timestamp
```

## Security Architecture

See [SECURITY.md](./SECURITY.md) for detailed security documentation.

### Key Principles
- Zero-trust: verify every request
- Least privilege: users only see authorized content
- Defense in depth: multiple security layers
- Audit everything: comprehensive logging

## Data Model

### Core Entities
- **VideoAsset**: Video metadata and ACL
- **TranscriptSegment**: Timestamped transcript chunks
- **ModerationResult**: Content safety outcomes
- **VideoProcessingJob**: Pipeline stage tracking
- **UserProfile**: Cached user data from Entra ID
- **AuditLog**: All critical actions logged

## Technology Decisions

See [ADRs](./adrs/) for detailed architecture decision records.

## Local Development

See [DEPLOYMENT.md](./DEPLOYMENT.md) for local development setup.

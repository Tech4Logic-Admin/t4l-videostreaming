# Tech4Logic Video Search - API Documentation

## Overview

The Tech4Logic Video Search API provides RESTful endpoints for video management, search, and administration.

**Base URL**: `http://localhost:5000/api` (development)

## Authentication

All endpoints (except health checks) require authentication via JWT bearer token from Microsoft Entra ID.

```http
Authorization: Bearer <token>
```

### Development Mode

When `FeatureFlags:UseDevAuth=true`, use the dev auth header:

```http
X-Dev-User-Id: dev-admin-oid
X-Dev-User-Role: Admin
```

## Endpoints

### System

#### GET /healthz
Health check endpoint.

**Response**: `200 OK`
```json
{
  "status": "Healthy"
}
```

#### GET /readyz
Readiness check endpoint.

**Response**: `200 OK`
```json
{
  "status": "Healthy"
}
```

#### GET /api/info
Get API version information.

**Response**: `200 OK`
```json
{
  "name": "Tech4Logic Video Search API",
  "version": "1.0.0",
  "environment": "Development",
  "timestamp": "2024-01-15T10:30:00Z"
}
```

---

### Videos

#### GET /api/videos
List videos accessible to the current user.

**Query Parameters**:
| Parameter | Type | Description |
|-----------|------|-------------|
| `page` | int | Page number (default: 1) |
| `pageSize` | int | Items per page (default: 20, max: 100) |
| `status` | string | Filter by status: Uploading, Queued, Scanning, Moderating, Indexing, Published, Quarantined, Rejected |
| `sortBy` | string | Sort field: createdAt, title |
| `sortDir` | string | Sort direction: asc, desc |

**Response**: `200 OK`
```json
{
  "items": [
    {
      "id": "550e8400-e29b-41d4-a716-446655440001",
      "title": "Welcome to Tech4Logic",
      "description": "Introduction video",
      "tags": ["intro", "welcome"],
      "status": "Published",
      "durationMs": 120000,
      "createdAt": "2024-01-15T10:00:00Z",
      "thumbnailUrl": "..."
    }
  ],
  "page": 1,
  "pageSize": 20,
  "totalCount": 45,
  "totalPages": 3
}
```

#### GET /api/videos/{id}
Get video details.

**Response**: `200 OK`
```json
{
  "id": "550e8400-e29b-41d4-a716-446655440001",
  "title": "Welcome to Tech4Logic",
  "description": "Introduction video about Tech4Logic platform",
  "tags": ["intro", "welcome"],
  "languageHint": "en",
  "durationMs": 120000,
  "status": "Published",
  "createdAt": "2024-01-15T10:00:00Z",
  "createdBy": {
    "oid": "user-oid",
    "displayName": "John Doe"
  },
  "moderationStatus": {
    "malwareScan": "Clean",
    "contentSafety": "Safe"
  },
  "transcriptSegments": [
    {
      "id": "seg-1",
      "startMs": 0,
      "endMs": 8000,
      "text": "Welcome to Tech4Logic Video Search demonstration.",
      "speaker": "Speaker 1"
    }
  ]
}
```

#### POST /api/videos
Create a new video upload.

**Request Body**:
```json
{
  "title": "My Video",
  "description": "Description of the video",
  "tags": ["demo", "tutorial"],
  "languageHint": "en",
  "allowedGroupIds": ["group-1", "group-2"],
  "allowedUserOids": ["user-oid-1"]
}
```

**Response**: `201 Created`
```json
{
  "id": "550e8400-e29b-41d4-a716-446655440099",
  "uploadUrl": "https://storage.blob.core.windows.net/quarantine/...",
  "expiresAt": "2024-01-15T11:00:00Z"
}
```

#### GET /api/videos/{id}/playback
Get a signed URL for video playback.

**Response**: `200 OK`
```json
{
  "playbackUrl": "https://storage.blob.core.windows.net/approved/...?sig=...",
  "expiresAt": "2024-01-15T11:00:00Z",
  "contentType": "video/mp4"
}
```

#### DELETE /api/videos/{id}
Delete a video (Admin or owner only).

**Response**: `204 No Content`

---

### Search

#### GET /api/search
Search for videos and transcript segments.

**Query Parameters**:
| Parameter | Type | Description |
|-----------|------|-------------|
| `q` | string | Search query (required) |
| `page` | int | Page number (default: 1) |
| `pageSize` | int | Items per page (default: 20) |
| `language` | string | Filter by language code |
| `semantic` | bool | Use semantic search (default: true) |

**Response**: `200 OK`
```json
{
  "query": "search demonstration",
  "totalCount": 5,
  "latencyMs": 45,
  "results": [
    {
      "videoId": "550e8400-e29b-41d4-a716-446655440001",
      "videoTitle": "Welcome to Tech4Logic",
      "thumbnailUrl": "...",
      "segments": [
        {
          "segmentId": "seg-id",
          "startMs": 8500,
          "endMs": 18000,
          "text": "This platform allows you to search through video content with ease.",
          "highlightedText": "This platform allows you to <em>search</em> through video content with ease.",
          "score": 0.95
        }
      ]
    }
  ]
}
```

---

### Moderation (Reviewer/Admin only)

#### GET /api/moderation/queue
Get videos pending review.

**Response**: `200 OK`
```json
{
  "items": [
    {
      "videoId": "...",
      "title": "...",
      "status": "Quarantined",
      "reasons": ["High violence score detected"],
      "severity": "High",
      "uploadedBy": "...",
      "uploadedAt": "..."
    }
  ],
  "totalCount": 3
}
```

#### POST /api/moderation/{videoId}/approve
Approve a quarantined video.

**Request Body**:
```json
{
  "notes": "Reviewed and approved - false positive"
}
```

**Response**: `200 OK`

#### POST /api/moderation/{videoId}/reject
Reject a quarantined video.

**Request Body**:
```json
{
  "notes": "Content violates policy"
}
```

**Response**: `200 OK`

---

### Admin (Admin only)

#### GET /api/admin/dashboard
Get dashboard metrics.

**Response**: `200 OK`
```json
{
  "overview": {
    "totalVideos": 150,
    "totalDurationHours": 45.5,
    "publishedVideos": 120,
    "pendingReview": 5,
    "processingJobs": 3
  },
  "recentActivity": {
    "uploadsToday": 10,
    "searchesToday": 250,
    "errorsToday": 2
  },
  "jobsByStage": {
    "MalwareScan": { "pending": 1, "inProgress": 0, "completed": 145, "failed": 2 },
    "ContentModeration": { "pending": 1, "inProgress": 1, "completed": 143, "failed": 3 },
    "Transcription": { "pending": 2, "inProgress": 1, "completed": 142, "failed": 3 },
    "SearchIndexing": { "pending": 3, "inProgress": 0, "completed": 140, "failed": 4 }
  }
}
```

#### GET /api/admin/audit
Get audit logs.

**Query Parameters**:
| Parameter | Type | Description |
|-----------|------|-------------|
| `page` | int | Page number |
| `pageSize` | int | Items per page |
| `action` | string | Filter by action type |
| `actorOid` | string | Filter by actor |
| `from` | datetime | Start date |
| `to` | datetime | End date |

**Response**: `200 OK`
```json
{
  "items": [
    {
      "id": "...",
      "actorOid": "user-oid",
      "actorDisplayName": "John Doe",
      "action": "video.uploaded",
      "targetType": "VideoAsset",
      "targetId": "...",
      "ipAddress": "192.168.1.1",
      "createdAt": "2024-01-15T10:30:00Z",
      "metadata": { "title": "New Video" }
    }
  ],
  "totalCount": 1000
}
```

#### GET /api/admin/metrics
Get daily metrics.

**Query Parameters**:
| Parameter | Type | Description |
|-----------|------|-------------|
| `from` | date | Start date (required) |
| `to` | date | End date (required) |

**Response**: `200 OK`
```json
{
  "metrics": [
    {
      "date": "2024-01-15",
      "uploads": 10,
      "approved": 8,
      "rejected": 1,
      "quarantined": 1,
      "avgIndexTimeMs": 15000,
      "searches": 250,
      "errors": 2,
      "uniqueUsers": 45
    }
  ]
}
```

---

## Error Responses

All errors follow this format:

```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.1",
  "title": "Bad Request",
  "status": 400,
  "detail": "Validation failed",
  "errors": {
    "title": ["Title is required"]
  },
  "traceId": "00-abc123..."
}
```

### Status Codes
| Code | Description |
|------|-------------|
| 200 | Success |
| 201 | Created |
| 204 | No Content |
| 400 | Bad Request - validation error |
| 401 | Unauthorized - missing/invalid token |
| 403 | Forbidden - insufficient permissions |
| 404 | Not Found |
| 429 | Too Many Requests - rate limited |
| 500 | Internal Server Error |

## Rate Limits

| Endpoint | Limit |
|----------|-------|
| Search | 60 requests/minute |
| Upload | 10 requests/minute |
| Playback | 100 requests/minute |
| Other | 120 requests/minute |

## Pagination

List endpoints support pagination:

```json
{
  "items": [...],
  "page": 1,
  "pageSize": 20,
  "totalCount": 100,
  "totalPages": 5
}
```

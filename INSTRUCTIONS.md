# Tech4Logic Video Search - Build Instructions & Progress Tracker

## Project Overview
**Name:** Tech4Logic Video Search (RBAC + Multilingual Timeline Search)

Enterprise-grade, containerized web application for:
- Speech video uploads in any language
- Malware/policy scanning
- Transcription/indexing with timestamps
- RBAC-based video discovery
- Multilingual timeline search with jump-to-time playback
- Admin reporting & dashboard

## Tech Stack
| Component | Technology |
|-----------|------------|
| Frontend | Next.js, TypeScript, TailwindCSS, shadcn/ui, React Query |
| API | ASP.NET Core .NET 8, REST, OpenAPI/Swagger |
| Worker | Azure Functions Durable (isolated .NET) |
| Database | PostgreSQL |
| Blob Storage | Azure Blob (Azurite locally) |
| Search | Azure AI Search (mock locally) |
| Transcription | Azure Video Indexer (mock locally) |
| Moderation | Azure AI Content Safety (mock locally) |
| Auth | Microsoft Entra ID (dev auth locally) |
| IaC | Bicep |

## Branding
- Primary: #455E9E
- Dark/Navy: #0F1B3D
- Accent: #6C7FD1
- Background: #F5F7FB

---

## Phase Progress Tracker

### Phase 0 — Repo & DevEx ✅ COMPLETED
**Goal:** Scaffold monorepo, tooling, docker compose, lint/format, CI skeleton, docs skeleton.

**Acceptance Criteria:**
- [x] `docker compose up` starts web+api+db+azurite ✅
- [x] `/healthz` returns ok ✅
- [x] CI runs (GitHub Actions configured) ✅

**Tasks:**
- [x] Create instruction tracking file
- [x] Scaffold monorepo with pnpm workspaces
- [x] Create apps/api (.NET 8 Web API)
- [x] Create apps/web (Next.js + TailwindCSS + shadcn/ui)
- [x] Create apps/worker (Azure Functions Durable)
- [x] Create docker-compose.yml
- [x] Create Makefile with up/down/test/lint/format/seed
- [x] Create docs skeleton (ARCHITECTURE, API, DEPLOYMENT, SECURITY, RUNBOOK, STATUS)
- [x] Setup GitHub Actions CI
- [x] Verify docker compose works end-to-end

---

### Phase 1 — Auth & RBAC 🔲 NOT STARTED
**Goal:** Implement Entra JWT validation (prod), dev auth (local), role checks, group claims mapping.

**Acceptance Criteria:**
- [ ] Unauthorized requests blocked
- [ ] Role restrictions tested
- [ ] UI shows user profile and role-based nav

---

### Phase 2 — Upload & Metadata 🔲 NOT STARTED
**Goal:** Upload UI and API with video record creation, SAS URL generation, quarantine storage.

**Acceptance Criteria:**
- [ ] Upload flow works locally
- [ ] Video shows "queued" status

---

### Phase 3 — Orchestration + Mock Pipeline 🔲 NOT STARTED
**Goal:** Durable Functions worker orchestrates: malware scan -> moderation -> indexing -> search push.

**Acceptance Criteria:**
- [ ] End-to-end demo: upload -> searchable -> search returns results

---

### Phase 4 — Real Azure Integrations 🔲 NOT STARTED
**Goal:** Implement Azure clients behind feature flags.

**Acceptance Criteria:**
- [ ] Integration tests run when AZURE_* env vars present
- [ ] Otherwise gracefully skipped

---

### Phase 5 — Search UX + Player Timeline 🔲 NOT STARTED
**Goal:** Production-grade search page with timeline segments and jump-to-time playback.

**Acceptance Criteria:**
- [ ] Playwright E2E covers search and jump

---

### Phase 6 — Moderation Review & Audit 🔲 NOT STARTED
**Goal:** Reviewer queue UI, approve/reject actions, audit logging.

**Acceptance Criteria:**
- [ ] Reviewer flow tested
- [ ] Audit entries visible

---

### Phase 7 — Admin Dashboard & Reports 🔲 NOT STARTED
**Goal:** KPIs, charts, job table, error trends, usage metrics.

**Acceptance Criteria:**
- [ ] Dashboard populated from real DB queries
- [ ] Includes date range filters

---

### Phase 8 — Hardening & Production Readiness 🔲 NOT STARTED
**Goal:** Rate limiting, validation, security headers, threat model, performance optimization.

**Acceptance Criteria:**
- [ ] Load sanity test passes
- [ ] Docs complete
- [ ] CI green
- [ ] Docker compose green

---

## Commands Reference

```bash
# Local Development
make up          # Start all services
make down        # Stop all services
make test        # Run all tests
make lint        # Run linters
make format      # Format code
make seed        # Seed database with sample data

# Individual Services
make api-test    # Run API tests
make web-test    # Run web tests
make e2e-test    # Run Playwright E2E tests
```

---

## Architecture Decision Records (ADRs)
See `/docs/adrs/` for all architecture decisions.

---

## Known Issues & Notes
*(Updated each phase)*

---

## Next Steps
Complete Phase 0 tasks, then proceed to Phase 1.

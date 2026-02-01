# Azure POC/Demo Hosting – Minimal Cost Guide

This document recommends the **lowest-cost way to host Tech4Logic Video Search on Azure** for a POC or demo, based on the current codebase and Bicep infrastructure.

---

## Single VM Demo (Cheapest POC) — Recommended for Demo

The **cheapest** way to run the full stack for a demo is a **single Ubuntu VM (B2s)** with Docker Compose. All resources are tagged for cost tracking.

### 1. Create the VM (from your Windows machine with Azure CLI)

```powershell
cd scripts\azure-vm-demo
.\deploy-vm.ps1
```

Or run manually with tags:

```bash
# Resource group (with meaningful tags)
az group create -n t4l-demo-rg -l eastus --tags project=t4l-videosearch environment=demo purpose=video-search-poc-demo application=t4l-videostreaming managedBy=script workload=docker-compose role=demo-server

# VM (B2s, Ubuntu 22.04; use Standard public IP if Basic quota is 0)
az vm create -g t4l-demo-rg -n t4l-demo-vm --image Ubuntu2204 --size Standard_B2s --public-ip-sku Standard --admin-username t4ldemo --generate-ssh-keys --tags project=t4l-videosearch environment=demo purpose=video-search-poc-demo application=t4l-videostreaming managedBy=script workload=docker-compose role=demo-server

# Open ports (80=web via Caddy, 443=HTTPS, 3000/5000 optional)
az vm open-port -g t4l-demo-rg -n t4l-demo-vm --port 80   --priority 1001
az vm open-port -g t4l-demo-rg -n t4l-demo-vm --port 443  --priority 1002
az vm open-port -g t4l-demo-rg -n t4l-demo-vm --port 3000 --priority 1003
az vm open-port -g t4l-demo-rg -n t4l-demo-vm --port 5000 --priority 1004
```

Note the **public IP** from the VM create output.

### 2. SSH into the VM

```bash
ssh t4ldemo@<public-ip>
```

### 3. Install Docker and Docker Compose plugin

```bash
sudo apt update
sudo apt install -y docker.io docker-compose-plugin
sudo usermod -aG docker $USER
newgrp docker   # refresh group (or log out and back in)
```

### 4. Get the project onto the VM

- **Option A (clone — recommended):** Fast and simple; no large folders to copy.
  ```bash
  git clone <your-repo-url> ~/t4l-videostreaming
  cd ~/t4l-videostreaming
  ```
- **Option B (SCP from your machine):** Use if you have local changes and no push yet.
  ```bash
  # On your Windows machine (PowerShell or Git Bash):
  scp -r C:\Users\SiddharthaSingh\Documents\workspace\t4l-videostreaming t4ldemo@<public-ip>:~/t4l-videostreaming
  # Then on VM:
  cd ~/t4l-videostreaming
  ```

### 5. Start the stack (with Caddy on port 80)

From the repo root on the VM, set the VM’s public IP for CORS (so the browser can call the API), then start:

```bash
export CORS_ORIGIN=http://<public-ip>   # use the VM’s public IP
docker compose -f docker-compose.yml -f docker-compose.demo.yml up -d --build
```

This starts Postgres, Azurite, API, Web, and **Caddy** so the app is served on **port 80** (web at `/`, API at `/api`).

### 6. Verify

- **API health:** `curl http://localhost/api/healthz`
- **Web:** Open `http://<public-ip>/` in a browser.

### 7. Resource hygiene (save cost)

- **Stop VM when not in use:** `az vm stop -g t4l-demo-rg -n t4l-demo-vm`
- **Start when needed:** `az vm start -g t4l-demo-rg -n t4l-demo-vm`
- **Delete when done:** `az group delete -n t4l-demo-rg --yes --no-wait`

### 8. Troubleshooting

- **Logs:** `docker compose -f docker-compose.yml -f docker-compose.demo.yml logs -f web` (or `api`)
- **Clean rebuild:** `docker compose -f docker-compose.yml -f docker-compose.demo.yml down -v && docker compose -f docker-compose.yml -f docker-compose.demo.yml up -d --build`
- **Firewall on VM:** `sudo ufw status` — allow 80/443/3000/5000 if enabled.

---

## Application Summary

| Component | Tech | Current Azure Target |
|-----------|------|----------------------|
| **Web** | Next.js 14, React, Tailwind | Container Apps |
| **API** | ASP.NET Core 8 | Container Apps |
| **Worker** | Azure Functions (Durable) | Not in Bicep (optional) |
| **Database** | PostgreSQL 16 | Azure Database for PostgreSQL Flexible (B1ms) |
| **Storage** | Blob (videos, quarantine, etc.) | Azure Storage Standard_LRS |
| **AI services** | Video Indexer, Content Safety, AI Search | Optional – **mocks available** |

The app already supports **feature flags** to use **mock** implementations for Video Indexer, Content Safety, and Search (`UseMockVideoIndexer`, `UseMockContentSafety`, `UseMockSearch`). For a POC, using these avoids any cost for those Azure AI services.

---

## Recommended Solution: Use Existing Bicep with POC Tweaks

**Best approach:** Keep the current Bicep stack and add a **POC/demo** configuration that minimizes cost while keeping the same architecture.

### 1. Scale Container Apps to Zero When Idle

- **Container Apps (Consumption)** supports **scale to zero** (`minReplicas: 0`).
- When no one is using the app, you pay **no compute** for API and Web.
- First request after idle has a short cold start (typically a few seconds).

**Change:** Add a Bicep parameter (e.g. `minReplicas`) and use `minReplicas: 0` for POC. See [Infra changes](#infra-changes-for-poc) below.

**Savings:** API + Web go from always-on to **pay-per-use**. For a demo used a few hours per week, compute cost can be near zero.

### 2. Keep PostgreSQL Flexible Server B1ms

- **Azure Database for PostgreSQL – Flexible Server** with **B1ms** (Burstable, 1 vCore) is already the **cheapest** managed Postgres option in your templates.
- Approximate cost: **~\$12–15/month** (region-dependent).
- There is no cheaper managed PostgreSQL on Azure that fits your app without changing code.

**No change needed** for POC.

### 3. Use Mocks for AI Services (No Video Indexer / Content Safety / AI Search)

- The app already has **mock** implementations and feature flags.
- For POC, set in Container App env (or Key Vault / config):
  - `FeatureFlags__UseMockVideoIndexer=true`
  - `FeatureFlags__UseMockContentSafety=true`
  - `FeatureFlags__UseMockSearch=true`

**Savings:** **\$0** for Video Indexer, Content Safety, and AI Search. No need to create those resources for the demo.

### 4. Deploy Worker Only If Needed

- The **Azure Functions** worker (video processing pipeline) is **not** in the current Bicep.
- For a **UI/data demo** (search, upload UI, metadata), you can **skip** deploying the worker.
- If you do deploy it, use **Azure Functions Consumption** so you only pay per execution (very low if you process few or no videos).

**Recommendation:** Omit worker for minimal POC; add later if you need to demo full processing.

### 5. Keep Other Services As-Is (Already Low Cost)

- **Storage (Standard_LRS)** – Pay for data stored and transactions; small for a POC.
- **ACR Basic** – Low fixed cost; needed for Container Apps images.
- **Key Vault** – Low cost for secret storage.
- **Log Analytics + Application Insights** – First **5 GB/month** ingestion is free; keep short retention (e.g. 7 days) for POC to limit cost.

---

## Estimated Monthly Cost (POC/Demo)

Rough order of magnitude (eastus; use Azure Pricing Calculator for exact numbers):

| Resource | Est. monthly cost (POC) |
|----------|--------------------------|
| Container Apps (API + Web, scale to zero) | **\$0–5** (light use) |
| PostgreSQL Flexible B1ms | **\$12–15** |
| Storage (Standard_LRS, small data) | **\$1–3** |
| ACR Basic | **\$5** |
| Key Vault | **\< \$1** |
| Log Analytics + App Insights (within free tier) | **\$0** (or a few \$ if over 5 GB) |
| **Total** | **~\$20–30/month** |

If you do **not** scale to zero and keep `minReplicas: 1`, add roughly **\$15–30/month** for Container Apps.

---

## Infra Changes for POC

### Option A: Use the POC parameter file (implemented)

A POC parameter file is provided: **`infra/main-poc.bicepparam`**.

It sets:

- `environmentName = 'poc'`
- `containerAppMinReplicas = 0` (scale to zero when idle)
- `useMockAiServices = true` (no Video Indexer, Content Safety, or AI Search cost)

Deploy with:

```bash
cd infra
az deployment sub create \
  --location eastus \
  --template-file main.bicep \
  --parameters main-poc.bicepparam
```

The Bicep templates already support:

- **`main.bicep`**: `containerAppMinReplicas` (default 1) and `useMockAiServices` (default false).
- **`container-apps.bicep`**: `minReplicas` and `useMockAiServices`; when true, API gets `FeatureFlags__UseMockVideoIndexer`, `UseMockContentSafety`, and `UseMockSearch` set to `true`.

### Option B: Cheapest alternative (different services)

If you need **absolute minimum** and can accept different services:

- **Web:** **Azure Static Web Apps** (free tier) – good for Next.js and static/SSR with minimal config. You’d point `NEXT_PUBLIC_API_URL` to your API.
- **API:** **Azure App Service** – **Free (F1)** or **Basic B1** (~\$13/mo). Free has limits (e.g. 60 min/day, subdomain); Basic is more stable for demos.
- **Database:** Keep **PostgreSQL Flexible B1ms** (no cheaper managed Postgres that matches your stack).
- **Worker:** Omit or run as **Azure Functions Consumption**.

This reduces Container Apps and ACR but adds Static Web Apps + App Service and may require CI/deploy and networking adjustments. For “minimal change,” **Option A (existing Bicep + scale to zero + mocks)** is the best balance.

---

## Deployment Steps (POC)

1. **Use POC parameters**  
   Deploy with `environmentName=poc` and `minReplicas=0` (after adding the parameter), e.g.:
   ```bash
   az deployment sub create \
     --location eastus \
     --template-file main.bicep \
     --parameters main.bicepparam.poc.json
   ```
2. **Set mock flags**  
   Ensure API Container App has:
   - `FeatureFlags__UseMockVideoIndexer=true`
   - `FeatureFlags__UseMockContentSafety=true`
   - `FeatureFlags__UseMockSearch=true`
   (via Bicep env for `poc` or manually in portal.)
3. **Configure Key Vault**  
   Set `DbConnectionString` and `StorageConnectionString` (and any other required secrets) as in existing docs.
4. **Build and push images**  
   Same as staging: build API and Web, push to ACR, update Container Apps to use new images.
5. **Optional: Entra ID**  
   For full auth demo, set `aadTenantId` and `aadClientId`; for internal POC you can keep `UseDevAuth` if that path exists and is acceptable for your security.

---

## Summary

| Goal | Recommendation |
|------|----------------|
| **Minimal cost** | Use existing Bicep + **scale to zero** (minReplicas 0) + **mocks** for AI services; **omit worker** for pure UI/data demo. |
| **Est. POC cost** | **~\$20–30/month**, mostly PostgreSQL + storage + ACR. |
| **Best single change** | Add `minReplicas` parameter and set it to `0` for POC to avoid paying for idle API/Web. |

This keeps your current architecture, avoids Video Indexer / Content Safety / AI Search cost via mocks, and minimizes compute cost with scale-to-zero Container Apps while staying suitable for a POC/demo.

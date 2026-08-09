# ERP AI Copilot Progress

Last updated: 2026-08-09

## Current Status

The project is at Phase 2.3: grounded Knowledge RAG with source citations.

Implemented:

- .NET 8 solution with API, Web UI, Core, Infrastructure, Tools, Copilot, and Knowledge projects.
- Safe read-only ERP tool calling for common ERP questions.
- SQLite demo data and repository layer.
- Docker Compose stack for API, Web UI, Ollama, Qdrant, document parser, and embedding service.
- Document ingestion pipeline for PDF, DOCX, TXT, and Markdown.
- Semantic search using local embeddings and Qdrant.
- Grounded RAG service with source blocks, citation validation, no-evidence refusal, and offline tests.
- CI workflow for .NET build/test and Docker image build verification.

Updated in this review:

- Fixed API/Web Dockerfile restore inputs so the solution restore includes `ERP.AI.Knowledge.Tests`.
- Added release-compose RAG environment overrides to match development compose.
- Synced the compose RAG environment variables with `.env.example`.
- Tightened API readiness so missing Ollama model, embedding, or Qdrant failures mark readiness unhealthy.
- Prevented production exception responses from exposing raw exception messages.
- Fixed background document indexing to create its own DI scope and log failures.
- Expanded `.gitignore` for Python sidecar artifacts and SQLite variants.
- Reworked README into a clearer onboarding and project-status document.
- Added API integration tests for health, readiness, copilot write refusal, and no-evidence RAG.
- Added Web proxy integration tests to verify JSON content type and forwarded path/query.
- Switched Web proxy controllers to `IHttpClientFactory` and preserved upstream response content type.
- Added `scripts/docker-smoke.ps1` for local container health verification.
- Split heavy sidecar Docker builds into a separate path-filtered/manual GitHub Actions workflow.
- Added opt-in API key protection for `/api/*`, config-driven current user/permissions, and Web proxy API-key forwarding.

## Known Gaps

- API key protection is a deployment guard, not full production identity.
- Per-user authentication, tenant-aware authorization, and user management are not implemented.
- Write actions are intentionally blocked until a human approval workflow exists.
- Full RAG behavior requires local sidecars and model downloads; unit tests remain offline.
- Browser UI has no automated Playwright coverage yet.

## Verification Checklist

Run before release:

```bash
dotnet restore ERP.AI.sln
dotnet build ERP.AI.sln --configuration Release
dotnet test ERP.AI.sln --configuration Release --no-build --verbosity normal
docker build -f docker/Dockerfile.api -t erp-ai-api:local .
docker build -f docker/Dockerfile.web -t erp-ai-web:local .
./scripts/docker-smoke.ps1
```

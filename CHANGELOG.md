# Changelog

All notable changes to **ERP AI Copilot** will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [0.2.3] - 2026-08-09

### Added

- **Grounded RAG Chat Engine (`ERP.AI.Knowledge`)**: `IKnowledgeRagService` / `KnowledgeRagService` — full Retrieval-Augmented Generation pipeline: question → Qdrant semantic search → retrieval quality gate → context builder → Ollama LLM → citation validation → grounded answer.
- **Retrieval Quality Gate**: No LLM call when `results.Count == 0` OR `topScore < MinimumScore`. Returns deterministic refusal string in user's language. Unit-tested with `llmCallCount == 0` assertion.
- **Grounding Context Builder (`IGroundingContextBuilder` / `GroundingContextBuilder`)**: Deduplicates chunks by `ChunkId`, respects `MaxContextCharacters` / `MaxChunkCharacters` budget, builds explicit `BEGIN SOURCE [N] ... END SOURCE [N]` blocks with full metadata headers for prompt injection resistance, supports adjacent-chunk expansion within budget.
- **Citation Validator (`ICitationValidator` / `CitationValidator`)**: Post-generation validation of `[N]` citation IDs. Removes unknown citation references, triggers max 1 correction retry if factual answer has no citations.
- **RAG Conversation Store (`KnowledgeRagConversationStore`)**: Thread-safe in-memory conversation history keyed by `ConversationId` GUID. Keeps last `MaxConversationTurns` turns for follow-up disambiguation. Non-authoritative — every turn still retrieves fresh evidence from Qdrant. Auto-evicts idle conversations after 30 minutes.
- **RAG Prompt Manager (`RagPromptManager`)**: Loads `knowledge-rag-system.txt` from multi-path fallback, with embedded 15-rule default. Separate from ERP Tool Calling prompt.
- **Grounded RAG System Prompt (`samples/prompts/knowledge-rag-system.txt`)**: 15 strict rules — evidence-only answering, `[N]` citation format, prompt injection resistance, conflict surfacing, anti-hallucination rules, `BEGIN/END SOURCE [N]` trust boundary.
- **API Endpoint `POST /api/knowledge/ask`**: Grounded RAG chat endpoint with 400 / 503 error mapping. Injects `IKnowledgeRagService`. No stack traces exposed.
- **Phase 2.3 DTOs** (`RagChatRequest`, `RagChatResponse`, `SourceCitationDto`, `GroundingContext`, `GroundingContextOptions`, `RagOptions`, `CitationValidationResult`).
- **Ask Knowledge UI Tab** (`ERP.AI.Web`): Third subtab in Knowledge Center with question textarea, sample question pills, scrollable answer history, chat bubbles (user right / AI left), clickable `[N]` citation badges scrolling to source cards, source cards with similarity %, snippet, and Open Document button, no-evidence state panel with action suggestions, collapsible debug panel (TraceId, retrieval/generation/total ms), loading animation.
- **RAG Configuration** (`appsettings.json` `Rag` section + Docker env vars): `TopK`, `MinimumScore`, `MaximumSources`, `MaxContextCharacters`, `MaxChunkCharacters`, `MaxCitationSnippetCharacters`, `IncludeAdjacentChunks`, `MaxAdjacentChunks`, `MaxConversationTurns`, `Temperature`, `MaxOutputTokens`.
- **Test Sample Documents**: `prompt-injection-test.txt` (with embedded attack instructions for anti-injection testing), `policy-conflict-v1.txt` and `policy-conflict-v2.txt` (conflicting approval thresholds for conflict-detection testing), `rag-evaluation.json` (15-case acceptance test dataset).
- **17 Unit Tests** (`KnowledgeRagTests.cs`): All offline — no Ollama / Qdrant required. Covers no-evidence LLM-gate, citation validation, deduplication, context budget, retry logic, conversation history, failure propagation, prompt injection scenarios.

### Changed

- `KnowledgeController` extended with `POST /api/knowledge/ask` action and `IKnowledgeRagService` dependency injection.
- `ERP.AI.Api/Program.cs`: Registered `IGroundingContextBuilder`, `ICitationValidator`, `KnowledgeRagConversationStore`, `IKnowledgeRagService`.
- `docker-compose.yml`: Added `Rag__*` environment variables to `api` service with `.env` defaults.
- `.env.example`: Added `RAG_TOP_K`, `RAG_MIN_SCORE`, `RAG_MAX_SOURCES`, `RAG_MAX_CONTEXT_CHARACTERS`, `RAG_TEMPERATURE`.

---

## [0.2.2] - 2026-08-09


### Added
- **Local Multilingual Vector Embedding Sidecar Service (`services/embedding-service`)**: Python FastAPI sidecar running `sentence-transformers` with model `BAAI/bge-m3` on port `8010` supporting batch vector generation (`POST /embed`).
- **Qdrant Vector Database Integration (`qdrant`)**: Integrated official Qdrant container service on port `6333` storing document chunk embeddings in `erp_knowledge_chunks` collection with cosine similarity distance metric and rich payload metadata (`chunkId`, `documentId`, `documentTitle`, `fileName`, `content`, `headingPath`, `startPage`, `endPage`).
- **Vector Store & Search Engine Abstractions (`ERP.AI.Knowledge`)**: `IEmbeddingService`, `LocalEmbeddingServiceClient`, `IKnowledgeVectorStore`, `QdrantKnowledgeVectorStore`, `IKnowledgeIndexingService`, `KnowledgeIndexingService`, `IKnowledgeSearchService`, `KnowledgeSearchService`.
- **Document Vector Status Tracking (`EmbeddingStatus`)**: Added `EmbeddingStatus` (`NotIndexed`, `Queued`, `Indexing`, `Indexed`, `Failed`, `Outdated`), `EmbeddingModel`, `EmbeddingError`, `EmbeddedChunkCount`, `IndexedAt` metadata to `KnowledgeDocument`.
- **Semantic Search API Endpoints (`KnowledgeController`)**:
  - `POST /api/knowledge/search`: Vector similarity search with Top-K (default 5, max 20), `MinimumScore` thresholding (default 0.35), category/language/document filters.
  - `POST /api/knowledge/documents/{documentId}/reindex`: Re-index document vectors into Qdrant.
  - `POST /api/knowledge/documents/index-unindexed`: Batch index all unindexed documents.
  - `POST /api/knowledge/index/rebuild`: Rebuild entire Qdrant index.
- **Semantic Search Web UI**: Dedicated `Semantic Search` tab in Knowledge Base UI with natural language query input, quick sample pills, category filter, Top-K selector, similarity score badges (`87.2%`), document title, heading path, and content snippet.
- **Synchronized Vector Lifecycle**:
  - Document deletion automatically purges point vectors from Qdrant.
  - Document reprocessing deletes old vectors and re-embeds current chunks.
- **Evaluation Dataset & Quality Tests (`samples/knowledge/search-evaluation.json` & `ERP.AI.Knowledge.Tests`)**: Added 5 new unit tests (23 passing total) verifying cross-language Vietnamese query -> English document retrieval, query validation, score thresholding, batching, and vector deletion.

---

## [0.2.1] - 2026-08-09

### Added
- **Document Ingestion & Knowledge Base Engine (`ERP.AI.Knowledge`)**: Production-ready pipeline for enterprise documents (`.pdf`, `.docx`, `.txt`, `.md`).
- **Python FastAPI Docling Sidecar Service (`services/document-parser`)**: Internal container service for extracting text, pages, and sections from PDF and DOCX documents via `Docling` / `pypdf` / `python-docx`.
- **SHA-256 Duplicate Document Detection**: Prevents duplicate file ingestion with conflict handling (`409 Conflict`).
- **Structure-Aware Chunker**: Splits normalized document text into index-ordered chunks (`KnowledgeChunk`).
- **Knowledge API Endpoints & UI**: Upload, document list, detail, chunk list, extracted text viewer, reprocess, and delete actions.

---

## [0.1.2] - 2026-08-09

### Added
- **GitHub Container Registry (GHCR)** image publishing pipeline (`ghcr.io/duhieu/erp-ai-api`, `ghcr.io/duhieu/erp-ai-web`, `ghcr.io/duhieu/erp-ai-document-parser`).
- **Release Docker Compose (`docker-compose.release.yml`)**.

---

## [0.1.1] - 2026-08-09

### Added
- Complete Docker Compose stack (`erp-ai-web`, `erp-ai-api`, `erp-ai-ollama`, `erp-ai-ollama-init`).

---

## [0.1.0] - 2026-08-09

### Added
- Initial ERP AI Copilot release with .NET 8 Clean Architecture and Safe ERP Tool Calling.

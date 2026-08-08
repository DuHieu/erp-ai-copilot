// --- Phase 2.1 & 2.2 Knowledge Base & Semantic Search Module ---

let currentKnowledgeDocuments = [];
let currentDetailDocumentId = null;

// Knowledge Subtab Switching
function switchKnowledgeSubtab(subtab) {
    const docsBtn = document.getElementById('subtabDocsBtn');
    const searchBtn = document.getElementById('subtabSearchBtn');
    const askBtn = document.getElementById('subtabAskBtn');
    const docsPane = document.getElementById('knowledgeDocsPane');
    const searchPane = document.getElementById('knowledgeSearchPane');
    const askPane = document.getElementById('knowledgeAskPane');

    [docsBtn, searchBtn, askBtn].forEach(b => b && b.classList.remove('active'));
    [docsPane, searchPane, askPane].forEach(p => p && p.classList.add('hidden'));

    if (subtab === 'docs') {
        docsBtn && docsBtn.classList.add('active');
        docsPane && docsPane.classList.remove('hidden');
        loadKnowledgeDocuments();
    } else if (subtab === 'search') {
        searchBtn && searchBtn.classList.add('active');
        searchPane && searchPane.classList.remove('hidden');
    } else if (subtab === 'ask') {
        askBtn && askBtn.classList.add('active');
        askPane && askPane.classList.remove('hidden');
    }
}


// Load Documents Table
async function loadKnowledgeDocuments() {
    const tableBody = document.getElementById('knowledgeTableBody');
    const categoryFilter = document.getElementById('knowledgeCategoryFilter').value;
    const searchInput = document.getElementById('knowledgeSearchInput').value;

    let url = '/api/knowledge/documents?page=1&pageSize=100';
    if (categoryFilter) url += `&category=${encodeURIComponent(categoryFilter)}`;
    if (searchInput) url += `&search=${encodeURIComponent(searchInput)}`;

    try {
        const response = await fetch(url);
        if (!response.ok) throw new Error(`HTTP ${response.status}`);

        const data = await response.json();
        currentKnowledgeDocuments = data.items || [];
        renderKnowledgeTable(currentKnowledgeDocuments);
    } catch (err) {
        console.error('Failed loading knowledge documents:', err);
        tableBody.innerHTML = `<tr><td colspan="10" class="error-cell">Failed to load documents: ${err.message}</td></tr>`;
    }
}

function renderKnowledgeTable(documents) {
    const tableBody = document.getElementById('knowledgeTableBody');
    if (!documents || documents.length === 0) {
        tableBody.innerHTML = `<tr><td colspan="10" class="empty-cell">No knowledge documents found. Click "+ Upload Document" to ingest file.</td></tr>`;
        return;
    }

    tableBody.innerHTML = documents.map(doc => {
        const statusBadge = getStatusBadge(doc.status);
        const vectorBadge = getVectorIndexBadge(doc.embeddingStatus);
        const formatBadge = getFormatBadge(doc.fileExtension);
        const sizeMb = (doc.fileSize / (1024 * 1024)).toFixed(2);
        const dateStr = new Date(doc.uploadedAt).toLocaleDateString('vi-VN');

        return `
            <tr>
                <td class="doc-title-cell">
                    <span class="doc-name" onclick="openDetailModal('${doc.documentId}')">${escapeHtml(doc.title)}</span>
                    <span class="doc-subtext">${escapeHtml(doc.fileName)}</span>
                </td>
                <td>${formatBadge}</td>
                <td>${sizeMb} MB</td>
                <td>${statusBadge}</td>
                <td>${vectorBadge}</td>
                <td>${doc.pageCount}</td>
                <td>${doc.chunkCount}</td>
                <td><span class="category-pill">${escapeHtml(doc.category || 'General')}</span></td>
                <td>${dateStr}</td>
                <td class="actions-cell">
                    <button class="action-btn" onclick="openDetailModal('${doc.documentId}')" title="View Detail & Chunks">👁️ View</button>
                </td>
            </tr>
        `;
    }).join('');
}

function getStatusBadge(status) {
    switch (status) {
        case 'Processed': return '<span class="badge badge-processed">Processed</span>';
        case 'Processing': return '<span class="badge badge-processing">Processing</span>';
        case 'Failed': return '<span class="badge badge-failed">Failed</span>';
        default: return `<span class="badge">${status}</span>`;
    }
}

function getVectorIndexBadge(status) {
    switch (status) {
        case 'Indexed': return '<span class="badge badge-indexed">Indexed</span>';
        case 'Indexing': return '<span class="badge badge-indexing">Indexing</span>';
        case 'Failed': return '<span class="badge badge-failed">Failed</span>';
        case 'Outdated': return '<span class="badge badge-outdated">Outdated</span>';
        default: return '<span class="badge">Not Indexed</span>';
    }
}

function getFormatBadge(ext) {
    const e = (ext || '').toLowerCase();
    if (e === '.pdf') return '<span class="format-badge format-pdf">PDF</span>';
    if (e === '.docx') return '<span class="format-badge format-docx">DOCX</span>';
    if (e === '.txt') return '<span class="format-badge format-txt">TXT</span>';
    if (e === '.md' || e === '.markdown') return '<span class="format-badge format-md">MD</span>';
    return `<span class="format-badge">${ext}</span>`;
}

function handleKnowledgeSearch() {
    loadKnowledgeDocuments();
}

// Upload Modal Handlers
function openUploadModal() {
    document.getElementById('uploadModal').classList.remove('hidden');
}

function closeUploadModal() {
    document.getElementById('uploadModal').classList.add('hidden');
    document.getElementById('uploadForm').reset();
}

async function handleDocumentUpload(event) {
    event.preventDefault();
    const fileInput = document.getElementById('uploadFileInput');
    if (!fileInput.files || fileInput.files.length === 0) return;

    const file = fileInput.files[0];
    const submitBtn = document.getElementById('uploadSubmitBtn');
    submitBtn.disabled = true;
    submitBtn.innerText = 'Ingesting Document...';

    const formData = new FormData();
    formData.append('file', file);
    formData.append('title', document.getElementById('uploadTitleInput').value || file.name);
    formData.append('category', document.getElementById('uploadCategoryInput').value);
    formData.append('version', document.getElementById('uploadVersionInput').value);
    formData.append('source', document.getElementById('uploadSourceInput').value);
    formData.append('description', document.getElementById('uploadDescriptionInput').value);

    try {
        const response = await fetch('/api/knowledge/documents', {
            method: 'POST',
            body: formData
        });

        if (response.status === 409) {
            const err = await response.json();
            alert(`Duplicate Document Warning:\n${err.error}`);
            return;
        }

        if (!response.ok) {
            const err = await response.json();
            throw new Error(err.error || `HTTP ${response.status}`);
        }

        closeUploadModal();
        loadKnowledgeDocuments();
        alert('Document uploaded and ingested successfully!');
    } catch (err) {
        alert(`Ingestion failed: ${err.message}`);
    } finally {
        submitBtn.disabled = false;
        submitBtn.innerText = 'Start Ingestion';
    }
}

// Document Detail Modal Handlers
async function openDetailModal(documentId) {
    currentDetailDocumentId = documentId;
    document.getElementById('detailModal').classList.remove('hidden');
    switchDetailTab('overview');

    try {
        const response = await fetch(`/api/knowledge/documents/${documentId}`);
        if (!response.ok) throw new Error(`HTTP ${response.status}`);
        const doc = await response.json();

        document.getElementById('detailModalTitle').innerText = `${doc.title} (${doc.fileName})`;
        renderDetailOverview(doc);
    } catch (err) {
        console.error('Error fetching detail:', err);
    }
}

function closeDetailModal() {
    document.getElementById('detailModal').classList.add('hidden');
    currentDetailDocumentId = null;
}

function switchDetailTab(tab) {
    const overviewBtn = document.getElementById('detailOverviewTabBtn');
    const textBtn = document.getElementById('detailTextTabBtn');
    const chunksBtn = document.getElementById('detailChunksTabBtn');

    const overviewPane = document.getElementById('detailOverviewPane');
    const textPane = document.getElementById('detailTextPane');
    const chunksPane = document.getElementById('detailChunksPane');

    overviewBtn.classList.remove('active');
    textBtn.classList.remove('active');
    chunksBtn.classList.remove('active');

    overviewPane.classList.add('hidden');
    textPane.classList.add('hidden');
    chunksPane.classList.add('hidden');

    if (tab === 'overview') {
        overviewBtn.classList.add('active');
        overviewPane.classList.remove('hidden');
    } else if (tab === 'text') {
        textBtn.classList.add('active');
        textPane.classList.remove('hidden');
        loadExtractedText(currentDetailDocumentId);
    } else if (tab === 'chunks') {
        chunksBtn.classList.add('active');
        chunksPane.classList.remove('hidden');
        loadDocumentChunks(currentDetailDocumentId);
    }
}

function renderDetailOverview(doc) {
    const grid = document.getElementById('detailGrid');
    const sizeMb = (doc.fileSize / (1024 * 1024)).toFixed(2);

    grid.innerHTML = `
        <div class="detail-item"><span class="label">Document ID:</span><span class="value">${doc.documentId}</span></div>
        <div class="detail-item"><span class="label">Original File:</span><span class="value">${escapeHtml(doc.originalFileName)}</span></div>
        <div class="detail-item"><span class="label">Format & Size:</span><span class="value">${doc.fileExtension.toUpperCase()} (${sizeMb} MB)</span></div>
        <div class="detail-item"><span class="label">SHA-256 Hash:</span><span class="value" style="font-size:11px;">${doc.fileHash}</span></div>
        <div class="detail-item"><span class="label">Processing Status:</span><span class="value">${doc.status} (${doc.processingStage})</span></div>
        <div class="detail-item"><span class="label">Vector Index Status:</span><span class="value">${doc.embeddingStatus} (${doc.embeddedChunkCount} chunks)</span></div>
        <div class="detail-item"><span class="label">Embedding Model:</span><span class="value">${doc.embeddingModel || 'BAAI/bge-m3'}</span></div>
        <div class="detail-item"><span class="label">Structure Stats:</span><span class="value">${doc.pageCount} Pages • ${doc.chunkCount} Chunks • ${doc.characterCount} Chars</span></div>
        <div class="detail-item"><span class="label">Category:</span><span class="value">${escapeHtml(doc.category || 'General')}</span></div>
        <div class="detail-item"><span class="label">Source / Dept:</span><span class="value">${escapeHtml(doc.source || 'N/A')}</span></div>
        <div class="detail-item"><span class="label">Version:</span><span class="value">${escapeHtml(doc.version || 'v1.0')}</span></div>
        <div class="detail-item"><span class="label">Uploaded At:</span><span class="value">${new Date(doc.uploadedAt).toLocaleString('vi-VN')}</span></div>
    `;
}

async function loadExtractedText(documentId) {
    const viewer = document.getElementById('detailTextViewer');
    viewer.innerText = 'Loading extracted text...';
    try {
        const resp = await fetch(`/api/knowledge/documents/${documentId}/content`);
        if (!resp.ok) throw new Error(`HTTP ${resp.status}`);
        const data = await resp.json();
        viewer.innerText = data.content || '(No text content extracted)';
    } catch (err) {
        viewer.innerText = `Failed to load text: ${err.message}`;
    }
}

async function loadDocumentChunks(documentId) {
    const list = document.getElementById('detailChunksList');
    list.innerHTML = '<div class="loading-cell">Loading chunks...</div>';
    try {
        const resp = await fetch(`/api/knowledge/documents/${documentId}/chunks?page=1&pageSize=100`);
        if (!resp.ok) throw new Error(`HTTP ${resp.status}`);
        const data = await resp.json();

        if (!data.items || data.items.length === 0) {
            list.innerHTML = '<div class="empty-cell">No chunks generated.</div>';
            return;
        }

        list.innerHTML = data.items.map(c => `
            <div class="chunk-card">
                <div class="chunk-header">
                    <span>Chunk #${c.chunkIndex + 1} • ID: ${c.chunkId}</span>
                    <span>${c.characterCount} chars • ~${c.tokenEstimate} tokens</span>
                </div>
                ${c.headingPath ? `<div class="heading-path">📍 ${escapeHtml(c.headingPath)}</div>` : ''}
                <div class="chunk-content">${escapeHtml(c.content)}</div>
            </div>
        `).join('');
    } catch (err) {
        list.innerHTML = `<div class="error-cell">Failed to load chunks: ${err.message}</div>`;
    }
}

async function triggerReindexDoc() {
    if (!currentDetailDocumentId) return;
    const btn = document.getElementById('reindexBtn');
    btn.disabled = true;
    btn.innerText = 'Indexing...';

    try {
        const resp = await fetch(`/api/knowledge/documents/${currentDetailDocumentId}/reindex`, { method: 'POST' });
        if (!resp.ok) throw new Error(`HTTP ${resp.status}`);
        alert('Vector re-indexing completed!');
        openDetailModal(currentDetailDocumentId);
        loadKnowledgeDocuments();
    } catch (err) {
        alert(`Re-indexing failed: ${err.message}`);
    } finally {
        btn.disabled = false;
        btn.innerText = 'Re-index Vectors';
    }
}

async function triggerReprocess() {
    if (!currentDetailDocumentId) return;
    if (!confirm('Reprocess document? This will recreate chunks and re-embed vector points in Qdrant.')) return;

    try {
        const resp = await fetch(`/api/knowledge/documents/${currentDetailDocumentId}/reprocess`, { method: 'POST' });
        if (!resp.ok) throw new Error(`HTTP ${resp.status}`);
        alert('Reprocessing & vector re-indexing completed!');
        openDetailModal(currentDetailDocumentId);
        loadKnowledgeDocuments();
    } catch (err) {
        alert(`Reprocessing failed: ${err.message}`);
    }
}

async function triggerDeleteDoc() {
    if (!currentDetailDocumentId) return;
    if (!confirm('Are you sure you want to delete this document, chunks, and Qdrant vector points?')) return;

    try {
        const resp = await fetch(`/api/knowledge/documents/${currentDetailDocumentId}`, { method: 'DELETE' });
        if (!resp.ok) throw new Error(`HTTP ${resp.status}`);
        closeDetailModal();
        loadKnowledgeDocuments();
        alert('Document deleted successfully!');
    } catch (err) {
        alert(`Delete failed: ${err.message}`);
    }
}

async function triggerBatchIndexUnindexed() {
    try {
        const resp = await fetch('/api/knowledge/documents/index-unindexed', { method: 'POST' });
        if (!resp.ok) throw new Error(`HTTP ${resp.status}`);
        const data = await resp.json();
        alert(data.message || 'Batch indexing triggered!');
        loadKnowledgeDocuments();
    } catch (err) {
        alert(`Batch indexing failed: ${err.message}`);
    }
}

// --- Phase 2.2 Semantic Search Handlers ---
function setSemanticQuery(queryText) {
    document.getElementById('semanticQueryInput').value = queryText;
    document.getElementById('semanticSearchForm').requestSubmit();
}

async function handleSemanticSearchSubmit(event) {
    event.preventDefault();
    const query = document.getElementById('semanticQueryInput').value.trim();
    if (!query) return;

    const category = document.getElementById('semanticCategorySelect').value;
    const topK = parseInt(document.getElementById('semanticTopKSelect').value);
    const minScore = parseFloat(document.getElementById('semanticMinScoreSelect').value);

    const btn = document.getElementById('semanticSearchBtn');
    const wrapper = document.getElementById('searchResultsWrapper');

    btn.disabled = true;
    btn.innerText = 'Searching...';
    wrapper.innerHTML = '<div class="loading-cell">Encoding query & searching vector embeddings in Qdrant...</div>';

    try {
        const response = await fetch('/api/knowledge/search', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                query: query,
                topK: topK,
                minimumScore: minScore,
                category: category || null
            })
        });

        if (!response.ok) {
            const err = await response.json();
            throw new Error(err.error || err.details || `HTTP ${response.status}`);
        }

        const data = await response.json();
        renderSemanticSearchResults(data);
    } catch (err) {
        wrapper.innerHTML = `
            <div class="empty-search-state" style="border-color: var(--danger);">
                <div class="empty-icon">⚠️</div>
                <p style="color: var(--danger);">Semantic Search Error: ${escapeHtml(err.message)}</p>
            </div>
        `;
    } finally {
        btn.disabled = false;
        btn.innerText = 'Search';
    }
}

function renderSemanticSearchResults(data) {
    const wrapper = document.getElementById('searchResultsWrapper');
    const results = data.results || [];

    if (results.length === 0) {
        wrapper.innerHTML = `
            <div class="empty-search-state">
                <div class="empty-icon">🔍</div>
                <p>No relevant knowledge chunks found above similarity threshold for query "<strong>${escapeHtml(data.query)}</strong>".</p>
            </div>
        `;
        return;
    }

    wrapper.innerHTML = `
        <div style="font-size:12px; color:var(--text-muted); margin-bottom:4px;">
            Found <strong>${results.length}</strong> matching chunks in <strong>${data.durationMs} ms</strong>
        </div>
        ` + results.map(r => {
        const scorePercent = (r.score * 100).toFixed(1);
        let scoreClass = 'score-low';
        if (r.score >= 0.70) scoreClass = 'score-high';
        else if (r.score >= 0.50) scoreClass = 'score-medium';

        return `
            <div class="search-result-card">
                <div class="result-card-header">
                    <div class="result-title-group">
                        <span class="rank-badge">#${r.rank}</span>
                        <span class="doc-title">${escapeHtml(r.documentTitle || r.fileName)}</span>
                    </div>
                    <span class="score-badge ${scoreClass}">Similarity ${r.score} (${scorePercent}%)</span>
                </div>
                
                <div class="result-meta-row">
                    <span class="meta-item">📁 ${escapeHtml(r.fileName)}</span>
                    <span class="meta-item">🏷️ ${escapeHtml(r.category || 'General')}</span>
                    ${r.startPage ? `<span class="meta-item">📄 Page ${r.startPage}</span>` : ''}
                    ${r.headingPath ? `<span class="meta-item heading-path">📍 ${escapeHtml(r.headingPath)}</span>` : ''}
                </div>

                <div class="result-snippet">${escapeHtml(r.content)}</div>
                
                <div style="display:flex; gap:8px; justify-content:flex-end;">
                    <button class="secondary-btn" onclick="openDetailModal('${r.documentId}')">Open Document</button>
                </div>
            </div>
        `;
    }).join('');
}

function escapeHtml(str) {
    if (!str) return '';
    return str
        .replace(/&/g, "&amp;")
        .replace(/</g, "&lt;")
        .replace(/>/g, "&gt;")
        .replace(/"/g, "&quot;")
        .replace(/'/g, "&#039;");
}

// ============================================================
// Phase 2.3 — Ask Knowledge (Grounded RAG Chat)
// ============================================================

let ragConversationId = null;

function resetRagConversation() {
    ragConversationId = null;
    const historyEl = document.getElementById('ragAnswerHistory');
    if (historyEl) historyEl.innerHTML = '';
    const inputEl = document.getElementById('ragQuestionInput');
    if (inputEl) inputEl.value = '';
    hideRagStates();
}

function setRagSampleQuestion(q) {
    const input = document.getElementById('ragQuestionInput');
    if (input) { input.value = q; input.focus(); }
}

async function submitRagQuestion() {
    const input = document.getElementById('ragQuestionInput');
    const question = (input ? input.value : '').trim();
    if (!question) return;

    showRagLoading(true);
    hideRagStates();

    const requestBody = { question };
    if (ragConversationId) requestBody.conversationId = ragConversationId;

    try {
        const response = await fetch('/api/knowledge/ask', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(requestBody)
        });

        const data = await response.json();

        if (!response.ok) {
            showRagError(data.error || `Server error ${response.status}`);
            return;
        }

        ragConversationId = data.conversationId;
        renderRagResponse(question, data);

        if (input) input.value = '';

    } catch (err) {
        showRagError(`Network error: ${err.message}`);
    } finally {
        showRagLoading(false);
    }
}

function renderRagResponse(question, data) {
    const container = document.getElementById('ragAnswerHistory');
    if (!container) return;

    if (data.noEvidence) {
        const turn = document.createElement('div');
        turn.className = 'rag-turn';
        turn.innerHTML = `
            <div class="rag-question-bubble">
                <span class="rag-question-label">You</span>
                <div class="rag-question-text">${escapeHtml(question)}</div>
            </div>
            <div class="rag-no-evidence">
                <div class="rag-no-evidence-icon">🔍</div>
                <div class="rag-no-evidence-title">No Supporting Evidence Found</div>
                <div class="rag-no-evidence-msg">${escapeHtml(data.answer)}</div>
                <div class="rag-no-evidence-actions">
                    <button class="secondary-btn" onclick="switchKnowledgeSubtab('docs')">📄 Upload Documents</button>
                    <button class="secondary-btn" onclick="switchKnowledgeSubtab('search')">🔍 Try Semantic Search</button>
                </div>
            </div>
        `;
        container.appendChild(turn);
        turn.scrollIntoView({ behavior: 'smooth', block: 'start' });
        return;
    }

    // Render grounded answer
    const citationIdPrefix = `rag-${Date.now()}`;
    const answerHtml = renderAnswerWithCitations(data.answer, citationIdPrefix);
    const sourcesHtml = renderSourceCards(data.sources, citationIdPrefix);
    const debugHtml = renderDebugPanel(data);

    const turn = document.createElement('div');
    turn.className = 'rag-turn';
    turn.innerHTML = `
        <div class="rag-question-bubble">
            <span class="rag-question-label">You</span>
            <div class="rag-question-text">${escapeHtml(question)}</div>
        </div>
        <div class="rag-answer-bubble">
            <div class="rag-answer-header">
                <span class="rag-answer-label">📚 Knowledge Assistant</span>
                <span class="rag-grounded-badge">Grounded Answer ✓</span>
            </div>
            <div class="rag-answer-text">${answerHtml}</div>
            ${sourcesHtml}
            ${debugHtml}
        </div>
    `;
    container.appendChild(turn);
    turn.scrollIntoView({ behavior: 'smooth', block: 'start' });
}

function renderAnswerWithCitations(answer, prefix) {
    if (!answer) return '';
    // Replace [N] with clickable citation badges
    const escaped = escapeHtml(answer).replace(/\[(\d+)\]/g, (_, n) => {
        return `<a class="rag-citation-link" href="#${prefix}-citation-${n}" onclick="highlightCitation('${prefix}-citation-${n}')">[${n}]</a>`;
    });
    // Convert newlines to <br>
    return escaped.replace(/\n/g, '<br>');
}

function renderSourceCards(sources, prefix) {
    if (!sources || sources.length === 0) return '';

    const cards = sources.map(s => {
        const score = (s.score * 100).toFixed(1);
        const page = s.startPage ? `· Page ${s.startPage}${s.endPage && s.endPage !== s.startPage ? '-' + s.endPage : ''}` : '';
        const section = s.sectionTitle ? `<div class="rag-src-section">${escapeHtml(s.sectionTitle)}</div>` : '';
        const snippet = s.snippet ? `<div class="rag-src-snippet">"${escapeHtml(s.snippet)}"</div>` : '';
        return `
            <div class="rag-source-card" id="${prefix}-citation-${s.citationId}">
                <div class="rag-src-header">
                    <span class="rag-src-id">[${s.citationId}]</span>
                    <span class="rag-src-title">${escapeHtml(s.documentTitle)}</span>
                    <span class="rag-src-score">${score}%</span>
                </div>
                ${section}
                ${s.category ? `<div class="rag-src-meta"><span class="category-pill">${escapeHtml(s.category)}</span> ${page}</div>` : ''}
                ${snippet}
                <div class="rag-src-actions">
                    <button class="action-btn" onclick="openDetailModal('${s.documentId}')">📄 Open Document</button>
                </div>
            </div>
        `;
    }).join('');

    return `
        <div class="rag-sources">
            <div class="rag-sources-label">Sources</div>
            <div class="rag-sources-grid">${cards}</div>
        </div>
    `;
}

function renderDebugPanel(data) {
    const debugId = `debug-${Date.now()}`;
    return `
        <div class="rag-debug-wrapper">
            <button class="rag-debug-toggle" onclick="toggleRagDebug('${debugId}')">🔧 Debug Info</button>
            <div class="rag-debug-panel hidden" id="${debugId}">
                <div class="rag-debug-item"><strong>Trace ID:</strong> ${data.traceId}</div>
                <div class="rag-debug-item"><strong>Retrieved chunks:</strong> ${data.retrievedChunkCount}</div>
                <div class="rag-debug-item"><strong>Used chunks:</strong> ${data.usedChunkCount}</div>
                <div class="rag-debug-item"><strong>Retrieval:</strong> ${data.retrievalDurationMs} ms</div>
                <div class="rag-debug-item"><strong>Generation:</strong> ${data.generationDurationMs} ms</div>
                <div class="rag-debug-item"><strong>Total:</strong> ${data.durationMs} ms</div>
            </div>
        </div>
    `;
}

function toggleRagDebug(id) {
    const panel = document.getElementById(id);
    if (panel) panel.classList.toggle('hidden');
}

function highlightCitation(id) {
    const el = document.getElementById(id);
    if (!el) return;
    el.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
    el.classList.add('rag-source-highlight');
    setTimeout(() => el.classList.remove('rag-source-highlight'), 2000);
}

function showRagLoading(show) {
    const spinner = document.getElementById('ragLoadingSpinner');
    if (spinner) spinner.classList.toggle('hidden', !show);
    const btn = document.getElementById('ragAskBtn');
    if (btn) btn.disabled = show;
}

function hideRagStates() {
    const err = document.getElementById('ragErrorMsg');
    if (err) err.classList.add('hidden');
}

function showRagError(msg) {
    const err = document.getElementById('ragErrorMsg');
    if (err) {
        err.textContent = `Error: ${msg}`;
        err.classList.remove('hidden');
    }
}


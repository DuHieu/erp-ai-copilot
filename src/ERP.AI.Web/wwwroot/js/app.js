const API_BASE_URL = '/api/copilot/chat';

function sendQuickPrompt(promptText) {
    const input = document.getElementById('userInput');
    input.value = promptText;
    document.getElementById('chatForm').dispatchEvent(new Event('submit', { cancelable: true, bubbles: true }));
}

function clearChat() {
    const container = document.getElementById('messagesContainer');
    container.innerHTML = `
        <div class="welcome-banner">
            <div class="welcome-icon">🤖</div>
            <h2>Welcome to ERP AI Copilot</h2>
            <p>Ask anything about receivables, revenue, inventory alerts, or project budgets in natural Vietnamese or English. All queries are backed by safe C# ERP tools.</p>
        </div>
    `;
}

async function handleChatSubmit(event) {
    event.preventDefault();
    const input = document.getElementById('userInput');
    const message = input.value.trim();
    if (!message) return;

    input.value = '';
    const container = document.getElementById('messagesContainer');

    // Remove welcome banner if present
    const banner = container.querySelector('.welcome-banner');
    if (banner) banner.remove();

    // Render User Message
    appendUserMessage(container, message);

    // Render AI Loading Message
    const loadingId = 'loading-' + Date.now();
    appendLoadingMessage(container, loadingId);
    scrollToBottom(container);

    try {
        const response = await fetch(API_BASE_URL, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ message: message })
        });

        const loadingElem = document.getElementById(loadingId);
        if (loadingElem) loadingElem.remove();

        if (!response.ok) {
            const errData = await response.json().catch(() => ({}));
            appendAiMessage(container, `Error (${response.status}): ${errData.details || errData.error || 'Failed to reach API service.'}`, [], null, 0);
        } else {
            const data = await response.json();
            appendAiMessage(container, data.answer, data.toolsUsed || [], data.traceDetails || [], data.totalDurationMs || 0);
        }
    } catch (err) {
        const loadingElem = document.getElementById(loadingId);
        if (loadingElem) loadingElem.remove();
        appendAiMessage(container, `Network Error: Unable to connect to ERP AI API endpoint at ${API_BASE_URL}. Ensure API service is running.`, [], null, 0);
    }

    scrollToBottom(container);
}

function appendUserMessage(container, text) {
    const msgDiv = document.createElement('div');
    msgDiv.className = 'message-bubble message-user';
    msgDiv.innerHTML = `<div class="content">${escapeHtml(text)}</div>`;
    container.appendChild(msgDiv);
}

function appendLoadingMessage(container, id) {
    const msgDiv = document.createElement('div');
    msgDiv.className = 'message-bubble message-ai';
    msgDiv.id = id;
    msgDiv.innerHTML = `
        <div class="content" style="color: #94a3b8; font-style: italic;">
            ⚡ ERP AI Copilot is inspecting ERP tools and querying backend data...
        </div>
    `;
    container.appendChild(msgDiv);
}

function appendAiMessage(container, answerText, toolsUsed, traceDetails, durationMs) {
    const msgDiv = document.createElement('div');
    msgDiv.className = 'message-bubble message-ai';

    let toolsHtml = '';
    if (toolsUsed && toolsUsed.length > 0) {
        toolsHtml = toolsUsed.map(t => `<span class="tool-badge">⚡ Tool Executed: ${escapeHtml(t)}</span>`).join(' ');
    }

    let metaHtml = '';
    if (durationMs > 0) {
        metaHtml = `<div class="trace-meta"><span class="trace-pill">Execution Duration: ${durationMs}ms</span></div>`;
    }

    msgDiv.innerHTML = `
        ${toolsHtml}
        <div class="content">${escapeHtml(answerText)}</div>
        ${metaHtml}
    `;
    container.appendChild(msgDiv);
}

function scrollToBottom(container) {
    container.scrollTop = container.scrollHeight;
}

function escapeHtml(text) {
    if (!text) return '';
    return text.replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;");
}

// Navigation Tab Switching (Copilot vs Knowledge Base)
function switchTab(tab) {
    const copilotBtn = document.getElementById('tabCopilotBtn');
    const knowledgeBtn = document.getElementById('tabKnowledgeBtn');
    const copilotView = document.getElementById('copilotView');
    const knowledgeView = document.getElementById('knowledgeView');
    const copilotPrompts = document.getElementById('copilotPromptsSection');
    const knowledgeInfo = document.getElementById('knowledgeInfoSection');

    if (tab === 'copilot') {
        copilotBtn.classList.add('active');
        knowledgeBtn.classList.remove('active');
        copilotView.classList.remove('hidden');
        knowledgeView.classList.add('hidden');
        copilotPrompts.classList.remove('hidden');
        knowledgeInfo.classList.add('hidden');
    } else if (tab === 'knowledge') {
        knowledgeBtn.classList.add('active');
        copilotBtn.classList.remove('active');
        knowledgeView.classList.remove('hidden');
        copilotView.classList.add('hidden');
        knowledgeInfo.classList.remove('hidden');
        copilotPrompts.classList.add('hidden');

        if (typeof loadKnowledgeDocuments === 'function') {
            loadKnowledgeDocuments();
        }
    }
}

import { spawn } from 'node:child_process';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import process from 'node:process';
import { chromium } from 'playwright';

const currentDir = dirname(fileURLToPath(import.meta.url));
const repoRoot = resolve(currentDir, '../../..');
const port = process.env.WEB_SMOKE_PORT || '5011';
const baseUrl = `http://127.0.0.1:${port}`;
const webProject = resolve(repoRoot, 'src/ERP.AI.Web/ERP.AI.Web.csproj');

const webProcess = spawn(
  'dotnet',
  ['run', '--project', webProject, '--urls', baseUrl],
  {
    cwd: repoRoot,
    env: {
      ...process.env,
      ASPNETCORE_ENVIRONMENT: 'Development'
    },
    stdio: ['ignore', 'pipe', 'pipe']
  }
);

let webOutput = '';
webProcess.stdout.on('data', chunk => {
  webOutput += chunk.toString();
});
webProcess.stderr.on('data', chunk => {
  webOutput += chunk.toString();
});

try {
  await waitForServer(baseUrl, 120_000);
  await runSmoke();
  console.log(`Web smoke passed at ${baseUrl}`);
} finally {
  stopProcess(webProcess);
}

async function runSmoke() {
  const browser = await chromium.launch({ headless: process.env.HEADLESS !== 'false' });
  const page = await browser.newPage({ viewport: { width: 1440, height: 900 } });
  const pageErrors = [];

  page.on('pageerror', error => pageErrors.push(error.message));

  await page.route('**/api/copilot/chat', async route => {
    if (route.request().method() !== 'POST') {
      await route.fallback();
      return;
    }

    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        answer: 'Smoke answer from mocked ERP API.',
        toolsUsed: ['SmokeTool'],
        traceDetails: [],
        totalDurationMs: 12
      })
    });
  });

  await page.goto(baseUrl, { waitUntil: 'networkidle' });
  await page.locator('.brand-title').waitFor({ timeout: 10_000 });
  await expectText(page, '.brand-title', 'ERP AI Copilot');

  await page.fill('#userInput', 'Top 5 customers by receivables');
  await page.click('#sendBtn');

  await page.locator('text=Smoke answer from mocked ERP API.').waitFor({ timeout: 10_000 });
  await page.locator('text=Tool Executed: SmokeTool').waitFor({ timeout: 10_000 });

  if (pageErrors.length > 0) {
    throw new Error(`Browser page errors:\n${pageErrors.join('\n')}`);
  }

  await browser.close();
}

async function expectText(page, selector, expectedText) {
  const actual = (await page.locator(selector).first().textContent())?.trim() ?? '';
  if (!actual.includes(expectedText)) {
    throw new Error(`Expected ${selector} to include "${expectedText}", got "${actual}".`);
  }
}

async function waitForServer(url, timeoutMs) {
  const deadline = Date.now() + timeoutMs;
  let lastError = '';

  while (Date.now() < deadline) {
    if (webProcess.exitCode !== null) {
      throw new Error(`Web process exited early with code ${webProcess.exitCode}.\n${webOutput}`);
    }

    try {
      const response = await fetch(url);
      if (response.ok) {
        return;
      }
      lastError = `HTTP ${response.status}`;
    } catch (error) {
      lastError = error.message;
    }

    await new Promise(resolveTimer => setTimeout(resolveTimer, 1_000));
  }

  throw new Error(`Timed out waiting for ${url}. Last error: ${lastError}\n${webOutput}`);
}

function stopProcess(childProcess) {
  if (childProcess.exitCode !== null) {
    return;
  }

  childProcess.kill('SIGTERM');
  setTimeout(() => {
    if (childProcess.exitCode === null) {
      childProcess.kill('SIGKILL');
    }
  }, 5_000).unref();
}

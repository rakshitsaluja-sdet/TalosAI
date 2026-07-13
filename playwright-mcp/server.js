#!/usr/bin/env node

/**
 * Playwright MCP Server
 *
 * A genuine MCP (Model Context Protocol) server: browser-automation
 * capabilities are registered as MCP tools via the official
 * @modelcontextprotocol/sdk and served over Streamable HTTP at POST/GET /mcp,
 * so any MCP client can discover them via tools/list and invoke them via
 * tools/call.
 *
 * Previously this file was a plain Node http server with ad hoc REST routes
 * (/api/navigate, /api/click, ...) and no MCP protocol layer at all — no MCP
 * client could enumerate or call tools against it despite the name. That
 * REST surface has been replaced by the MCP tools below.
 */

const { chromium } = require('playwright');
const { McpServer } = require('@modelcontextprotocol/sdk/server/mcp.js');
const { StreamableHTTPServerTransport } = require('@modelcontextprotocol/sdk/server/streamableHttp.js');
const { z } = require('zod');
const http = require('http');
const url = require('url');

const PORT = process.env.PORT || 3000;
const HOST = process.env.HOST || 'localhost';

// Single shared browser session — this tool is driven by one agent/session
// at a time in practice (pragmatic scope, matching the same tradeoff made in
// MCPBridge's C# tool handlers rather than a full multi-tenant redesign).
// A mutex serializes tool calls so two concurrent MCP requests can't race on
// the same page/context.
let browser = null;
let context = null;
let page = null;
let toolMutex = Promise.resolve();

function withLock(fn) {
    const run = toolMutex.then(fn, fn);
    toolMutex = run.then(() => undefined, () => undefined);
    return run;
}

function log(message) {
    console.log(`[playwright-mcp] ${message}`);
}

async function initBrowser() {
    if (!browser) {
        log('Launching Chromium browser...');
        browser = await chromium.launch({
            headless: false,
            args: ['--start-maximized'],
            slowMo: 50
        });

        // No fixed viewport = use the full maximized window.
        context = await browser.newContext({
            viewport: null,
            ignoreHTTPSErrors: true
        });

        page = await context.newPage();
        log('Browser initialized (maximized, no fixed viewport).');
    }
    return { browser, context, page };
}

function textResult(payload) {
    return { content: [{ type: 'text', text: JSON.stringify(payload) }] };
}

function errorResult(error) {
    return {
        content: [{ type: 'text', text: JSON.stringify({ success: false, error: error.message }) }],
        isError: true
    };
}

// ── MCP server + tool registration ───────────────────────────────────
function createMcpServer() {
    const server = new McpServer({ name: 'playwright-mcp', version: '2.0.0' });

    server.tool(
        'init_browser',
        'Launch/initialize the shared Chromium browser session.',
        {},
        async () => withLock(async () => {
            try {
                await initBrowser();
                return textResult({ success: true, message: 'Browser initialized', browserVersion: browser.version() });
            } catch (error) {
                return errorResult(error);
            }
        })
    );

    server.tool(
        'navigate',
        'Navigate the current page to a URL.',
        {
            url: z.string().describe('URL to navigate to'),
            waitUntil: z.string().optional().describe("load | domcontentloaded | networkidle (default networkidle)")
        },
        async ({ url: targetUrl, waitUntil }) => withLock(async () => {
            try {
                await initBrowser();
                log(`Navigating to: ${targetUrl}`);
                await page.goto(targetUrl, { waitUntil: waitUntil || 'networkidle', timeout: 30000 });
                return textResult({ success: true, url: page.url(), title: await page.title() });
            } catch (error) {
                return errorResult(error);
            }
        })
    );

    server.tool(
        'inspect_elements',
        'Inspect interactive elements (inputs, buttons, links, etc.) currently on the page.',
        {
            selector: z.string().optional().describe('CSS selector to scope the search (default: input, button, select, textarea, a)'),
            limit: z.number().int().positive().optional().describe('Maximum number of elements to return (default 50)')
        },
        async ({ selector, limit }) => withLock(async () => {
            try {
                await initBrowser();
                const targetSelector = selector || 'input, button, select, textarea, a';
                const maxElements = Math.min(limit || 50, 500);

                const elements = await page.$$(targetSelector);
                const elementData = [];

                for (let i = 0; i < Math.min(elements.length, maxElements); i++) {
                    const element = elements[i];
                    const info = await element.evaluate(el => ({
                        tagName: el.tagName.toLowerCase(),
                        id: el.id || null,
                        name: el.name || null,
                        className: el.className || null,
                        type: el.type || null,
                        placeholder: el.placeholder || null,
                        text: el.textContent?.trim().substring(0, 100) || null,
                        href: el.href || null,
                        visible: el.offsetParent !== null
                    }));
                    const generatedSelector = await element.evaluate(el => {
                        if (el.id) return `#${el.id}`;
                        if (el.name) return `[name="${el.name}"]`;
                        return null;
                    });

                    elementData.push({ index: i, ...info, selector: generatedSelector });
                }

                return textResult({ success: true, count: elementData.length, elements: elementData });
            } catch (error) {
                return errorResult(error);
            }
        })
    );

    server.tool(
        'take_screenshot',
        'Take a screenshot of the current page and return it as image content.',
        {
            fullPage: z.boolean().optional().describe('Capture the full scrollable page (default false)')
        },
        async ({ fullPage }) => withLock(async () => {
            try {
                await initBrowser();
                const screenshot = await page.screenshot({ fullPage: !!fullPage, type: 'png' });
                return {
                    content: [{ type: 'image', data: screenshot.toString('base64'), mimeType: 'image/png' }]
                };
            } catch (error) {
                return errorResult(error);
            }
        })
    );

    server.tool(
        'get_page_info',
        'Get the current page title, URL, and viewport size.',
        {},
        async () => withLock(async () => {
            try {
                await initBrowser();
                return textResult({
                    success: true,
                    title: await page.title(),
                    url: page.url(),
                    viewport: page.viewportSize()
                });
            } catch (error) {
                return errorResult(error);
            }
        })
    );

    server.tool(
        'click_element',
        'Click an element by CSS selector.',
        {
            selector: z.string().describe('CSS selector of the element to click'),
            timeout: z.number().int().positive().optional().describe('Timeout in ms (default 30000)')
        },
        async ({ selector, timeout }) => withLock(async () => {
            try {
                await initBrowser();
                await page.click(selector, { timeout: timeout || 30000 });
                return textResult({ success: true, selector });
            } catch (error) {
                return errorResult(error);
            }
        })
    );

    server.tool(
        'fill_input',
        'Fill a text input identified by CSS selector.',
        {
            selector: z.string().describe('CSS selector of the input to fill'),
            text: z.string().describe('Text to enter'),
            timeout: z.number().int().positive().optional().describe('Timeout in ms (default 30000)')
        },
        async ({ selector, text, timeout }) => withLock(async () => {
            try {
                await initBrowser();
                await page.fill(selector, text, { timeout: timeout || 30000 });
                return textResult({ success: true, selector });
            } catch (error) {
                return errorResult(error);
            }
        })
    );

    server.tool(
        'maximize_page',
        'Ensure the browser window/viewport is maximized (no fixed viewport).',
        {},
        async () => withLock(async () => {
            try {
                await initBrowser();
                // This context is created with viewport: null (full window size),
                // so there is no separate resize step — this tool confirms that
                // state rather than performing a no-op call to an undefined helper.
                return textResult({ success: true, viewport: page.viewportSize() });
            } catch (error) {
                return errorResult(error);
            }
        })
    );

    server.tool(
        'generate_page_object',
        'Inspect the current (or given) page and generate a C# Playwright Page Object class.',
        {
            url: z.string().optional().describe('Navigate here first, if different from the current page'),
            className: z.string().optional().describe('Class name to generate (default derived from page title)')
        },
        async ({ url: targetUrl, className }) => withLock(async () => {
            try {
                await initBrowser();
                if (targetUrl && page.url() !== targetUrl) {
                    await page.goto(targetUrl, { waitUntil: 'networkidle' });
                }

                const elements = await page.$$('input, button, select, textarea, a[href]');
                const locators = [];

                for (const element of elements) {
                    const info = await element.evaluate(el => ({
                        tagName: el.tagName.toLowerCase(),
                        id: el.id,
                        name: el.name,
                        type: el.type
                    }));

                    if (info.id || info.name) {
                        const locatorName = (info.id || info.name)
                            .replace(/[-_]/g, ' ')
                            .split(' ')
                            .map(word => word.charAt(0).toUpperCase() + word.slice(1).toLowerCase())
                            .join('');
                        const selector = info.id ? `#${info.id}` : `[name="${info.name}"]`;
                        locators.push({ name: locatorName, selector, type: info.type || info.tagName });
                    }
                }

                const pageTitle = await page.title();
                const pageClassName = className || pageTitle.replace(/\s+/g, '') + 'Page';
                const code = generateCSharpPageObject(pageClassName, locators);

                return textResult({ success: true, className: pageClassName, locatorCount: locators.length, code });
            } catch (error) {
                return errorResult(error);
            }
        })
    );

    server.tool(
        'close_browser',
        'Close the shared browser session.',
        {},
        async () => withLock(async () => {
            try {
                if (browser) {
                    await browser.close();
                    browser = null;
                    context = null;
                    page = null;
                }
                return textResult({ success: true, message: 'Browser closed' });
            } catch (error) {
                return errorResult(error);
            }
        })
    );

    return server;
}

// Generate C# Page Object code
function generateCSharpPageObject(className, locators) {
    const uniqueLocators = Array.from(new Map(locators.map(l => [l.name, l])).values());

    let code = `using Microsoft.Playwright;

namespace PageObjects
{
    public class ${className}
    {
        private readonly IPage _page;

        public ${className}(IPage page)
        {
            _page = page;
        }

        // Locators\n`;

    uniqueLocators.forEach(locator => {
        code += `        private ILocator ${locator.name} => _page.Locator("${locator.selector}");\n`;
    });

    code += `
        // Actions
        public async Task NavigateAsync(string url)
        {
            await _page.GotoAsync(url, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        }
\n`;

    uniqueLocators.forEach(locator => {
        if (locator.type === 'button' || locator.type === 'submit') {
            code += `        public async Task Click${locator.name}Async()
        {
            await ${locator.name}.ClickAsync();
        }
\n`;
        } else if (locator.type === 'text' || locator.type === 'password' || locator.type === 'email') {
            code += `        public async Task Fill${locator.name}Async(string value)
        {
            await ${locator.name}.FillAsync(value);
        }
\n`;
        } else if (locator.type === 'select') {
            code += `        public async Task Select${locator.name}Async(string value)
        {
            await ${locator.name}.SelectOptionAsync(value);
        }
\n`;
        }
    });

    code += `    }\n}\n`;
    return code;
}

// ── HTTP transport: MCP endpoint + a plain /health liveness check ───────
async function readJsonBody(req) {
    return new Promise((resolve, reject) => {
        let body = '';
        req.on('data', chunk => { body += chunk.toString(); });
        req.on('end', () => {
            if (!body) return resolve(undefined);
            try { resolve(JSON.parse(body)); } catch (err) { reject(err); }
        });
        req.on('error', reject);
    });
}

const server = http.createServer(async (req, res) => {
    res.setHeader('Access-Control-Allow-Origin', '*');
    res.setHeader('Access-Control-Allow-Methods', 'GET, POST, DELETE, OPTIONS');
    res.setHeader('Access-Control-Allow-Headers', 'Content-Type, Authorization, Mcp-Session-Id');

    if (req.method === 'OPTIONS') {
        res.writeHead(200);
        res.end();
        return;
    }

    const pathname = url.parse(req.url, true).pathname;

    try {
        if (pathname === '/health' && req.method === 'GET') {
            res.setHeader('Content-Type', 'application/json');
            res.writeHead(200);
            res.end(JSON.stringify({
                status: 'ok',
                server: 'playwright-mcp',
                version: '2.0.0',
                browserConnected: !!browser,
                uptime: process.uptime()
            }));
            return;
        }

        if (pathname === '/mcp') {
            // Stateless mode: a fresh server + transport per request, matching
            // the SDK's documented stateless Streamable HTTP pattern.
            const mcpServer = createMcpServer();
            const transport = new StreamableHTTPServerTransport({ sessionIdGenerator: undefined });

            res.on('close', () => {
                transport.close();
                mcpServer.close();
            });

            await mcpServer.connect(transport);

            const body = req.method === 'POST' ? await readJsonBody(req) : undefined;
            await transport.handleRequest(req, res, body);
            return;
        }

        if (pathname === '/' && req.method === 'GET') {
            res.setHeader('Content-Type', 'application/json');
            res.writeHead(200);
            res.end(JSON.stringify({
                server: 'playwright-mcp',
                version: '2.0.0',
                protocol: 'Model Context Protocol (Streamable HTTP)',
                mcpEndpoint: '/mcp',
                healthEndpoint: '/health',
                note: 'Use an MCP client (tools/list, tools/call) against /mcp — this server has no other REST API surface.'
            }, null, 2));
            return;
        }

        res.setHeader('Content-Type', 'application/json');
        res.writeHead(404);
        res.end(JSON.stringify({ error: 'Endpoint not found', path: pathname }));
    } catch (error) {
        log(`Server error: ${error.message}`);
        console.error(error);
        if (!res.headersSent) {
            res.setHeader('Content-Type', 'application/json');
            res.writeHead(500);
        }
        res.end(JSON.stringify({ error: 'Internal server error', message: error.message }));
    }
});

server.listen(PORT, HOST, () => {
    log(`Playwright MCP server listening on http://${HOST}:${PORT}`);
    log(`MCP endpoint:    http://${HOST}:${PORT}/mcp`);
    log(`Health endpoint: http://${HOST}:${PORT}/health`);
});

process.on('SIGINT', async () => {
    log('Shutting down...');
    if (browser) {
        await browser.close();
    }
    server.close(() => process.exit(0));
});

// Surface fatal errors as a clean exit instead of continuing in an unknown
// state — logging-and-continuing after an uncaught exception risks operating
// on a corrupted process.
process.on('uncaughtException', (error) => {
    console.error('[playwright-mcp] Uncaught exception, exiting:', error);
    process.exit(1);
});

process.on('unhandledRejection', (reason) => {
    console.error('[playwright-mcp] Unhandled promise rejection, exiting:', reason);
    process.exit(1);
});

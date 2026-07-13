# Playwright MCP Server

A genuine MCP (Model Context Protocol) server that exposes Playwright
browser automation as tools any MCP client can discover (`tools/list`) and
invoke (`tools/call`), served over Streamable HTTP.

## Purpose

This server lets an MCP-aware agent (GitHub Copilot, Claude, etc.):
- Navigate and interact with a web page (`navigate`, `click_element`, `fill_input`)
- Inspect interactive elements on the page (`inspect_elements`)
- Take screenshots, returned as MCP image content (`take_screenshot`)
- Generate a starter C# Playwright Page Object from the current page (`generate_page_object`)

## Installation

### Prerequisites
- Node.js (v18 or higher)
- npm (v9 or higher)

### Setup

```powershell
cd playwright-mcp
npm install
npx playwright install chromium
```

## Usage

### Start the server

```powershell
node server.js
```

By default it listens on `http://localhost:3000`:
- `GET /health` — liveness check
- `POST /mcp` / `GET /mcp` — the MCP endpoint (Streamable HTTP transport)

Point your MCP client at `http://localhost:3000/mcp`.

### Available tools

| Tool | Description |
|------|-------------|
| `init_browser` | Launch/initialize the shared Chromium session |
| `navigate` | Navigate to a URL |
| `inspect_elements` | List interactive elements matching a selector |
| `take_screenshot` | Capture a screenshot (returned as image content) |
| `get_page_info` | Title, URL, viewport size |
| `click_element` | Click by CSS selector |
| `fill_input` | Fill a text input by CSS selector |
| `maximize_page` | Confirm the browser window is using the full viewport |
| `generate_page_object` | Inspect the page and generate a C# Playwright Page Object |
| `close_browser` | Close the shared browser session |

## Notes

- Tool calls are serialized behind a mutex — this server is designed for one
  agent/session at a time, not concurrent multi-tenant use.
- `PORT` and `HOST` environment variables override the default bind address.

# mcp_server/server.py
"""
Real MCP-protocol server (JSON-RPC over SSE, via the official `mcp` SDK) that
proxies tool calls through to MCPBridge (the C# tool router) at BRIDGE_URL.

This supersedes an earlier REST-only implementation that exposed plain HTTP
routes with no MCP tools/list or tools/call handling — that meant no MCP
client could actually discover or invoke tools against it. This file is now
the single source of truth for the Python MCP server.
"""
import json
import time

import httpx
from mcp.server import Server
from mcp.server.sse import SseServerTransport
from mcp import types
from starlette.applications import Starlette
from starlette.responses import JSONResponse
from starlette.routing import Mount, Route

BRIDGE_URL = "http://localhost:5555"
app = Server("mcpbridge-mcp")

# ── Curated tool definitions ─────────────────────────────────────────
# These carry precise parameter schemas (types, enums, required fields) for
# the most commonly used tools. Any MCPBridge tool NOT listed here is still
# discoverable — see list_tools() below — via a generic auto-discovered
# schema, so tool coverage never silently drifts out of sync with MCPBridge.
TOOL_SCHEMAS = [
    types.Tool(name="launch_browser",
               description="Launch Chrome/Edge browser via C# Selenium",
               inputSchema={"type": "object", "properties": {
                   "browser": {"type": "string", "enum": ["chrome", "edge"]},
                   "headless": {"type": "boolean", "default": False}
               }, "required": ["browser"]}),

    types.Tool(name="navigate",
               description="Navigate to a URL",
               inputSchema={"type": "object", "properties": {
                   "url": {"type": "string"}
               }, "required": ["url"]}),

    types.Tool(name="navigate_to",
               description="Navigate to a URL (Playwright primary tool)",
               inputSchema={"type": "object", "properties": {
                   "url": {"type": "string"}
               }, "required": ["url"]}),

    types.Tool(name="find_element",
               description="Find element — strategy: css|xpath|id|name",
               inputSchema={"type": "object", "properties": {
                   "strategy": {"type": "string"},
                   "value": {"type": "string"}
               }, "required": ["strategy", "value"]}),

    types.Tool(name="click",
               description="Click an element",
               inputSchema={"type": "object", "properties": {
                   "strategy": {"type": "string"},
                   "value": {"type": "string"}
               }, "required": ["strategy", "value"]}),

    types.Tool(name="click_element",
               description="Click an element by CSS selector (Playwright primary tool)",
               inputSchema={"type": "object", "properties": {
                   "selector": {"type": "string"}
               }, "required": ["selector"]}),

    types.Tool(name="type_text",
               description="Type text into an input",
               inputSchema={"type": "object", "properties": {
                   "strategy": {"type": "string"},
                   "value": {"type": "string"},
                   "text": {"type": "string"}
               }, "required": ["strategy", "value", "text"]}),

    types.Tool(name="fill_input",
               description="Fill an input by CSS selector (Playwright primary tool)",
               inputSchema={"type": "object", "properties": {
                   "selector": {"type": "string"},
                   "value": {"type": "string"}
               }, "required": ["selector", "value"]}),

    types.Tool(name="get_text",
               description="Get visible text of element",
               inputSchema={"type": "object", "properties": {
                   "strategy": {"type": "string"},
                   "value": {"type": "string"}
               }, "required": ["strategy", "value"]}),

    types.Tool(name="assert_visible",
               description="Assert element is visible",
               inputSchema={"type": "object", "properties": {
                   "strategy": {"type": "string"},
                   "value": {"type": "string"}
               }, "required": ["strategy", "value"]}),

    types.Tool(name="assert_text_contains",
               description="Assert element text contains substring",
               inputSchema={"type": "object", "properties": {
                   "strategy": {"type": "string"},
                   "value": {"type": "string"},
                   "expected_text": {"type": "string"}
               }, "required": ["strategy", "value", "expected_text"]}),

    types.Tool(name="assert_text_visible",
               description="Assert page contains text (Playwright primary tool)",
               inputSchema={"type": "object", "properties": {
                   "text": {"type": "string"},
                   "scope": {"type": "string"}
               }, "required": ["text"]}),

    types.Tool(name="assert_url_contains",
               description="Assert current URL contains expected string",
               inputSchema={"type": "object", "properties": {
                   "expected": {"type": "string"}
               }, "required": ["expected"]}),

    types.Tool(name="assert_element_visible",
               description="Assert element is visible (Playwright primary tool)",
               inputSchema={"type": "object", "properties": {
                   "selector": {"type": "string"}
               }, "required": ["selector"]}),

    types.Tool(name="assert_element_hidden",
               description="Assert element is hidden (Playwright primary tool)",
               inputSchema={"type": "object", "properties": {
                   "selector": {"type": "string"}
               }, "required": ["selector"]}),

    types.Tool(name="assert_page_title",
               description="Assert page title equals expected (Playwright primary tool)",
               inputSchema={"type": "object", "properties": {
                   "expected": {"type": "string"}
               }, "required": ["expected"]}),

    types.Tool(name="assert_input_value",
               description="Assert an input's value equals expected (Playwright primary tool)",
               inputSchema={"type": "object", "properties": {
                   "selector": {"type": "string"},
                   "expected": {"type": "string"}
               }, "required": ["selector", "expected"]}),

    types.Tool(name="take_screenshot",
               description="Take screenshot",
               inputSchema={"type": "object", "properties": {
                   "filename": {"type": "string", "default": "screenshot.png"}
               }}),

    types.Tool(name="execute_script",
               description="Execute JavaScript on page",
               inputSchema={"type": "object", "properties": {
                   "script": {"type": "string"}
               }, "required": ["script"]}),

    types.Tool(name="get_page_info",
               description="Get page title, URL and partial source",
               inputSchema={"type": "object", "properties": {}}),

    types.Tool(name="close_browser",
               description="Close browser session",
               inputSchema={"type": "object", "properties": {}}),

    # API tools
    types.Tool(name="configure_api",
               description="Set the base URL for RestSharp API calls",
               inputSchema={"type": "object", "properties": {
                   "base_url": {"type": "string"}
               }, "required": ["base_url"]}),

    types.Tool(name="api_get",
               description="HTTP GET via RestSharp",
               inputSchema={"type": "object", "properties": {
                   "endpoint": {"type": "string"},
                   "headers": {"type": "object"}
               }, "required": ["endpoint"]}),

    types.Tool(name="api_post",
               description="HTTP POST via RestSharp",
               inputSchema={"type": "object", "properties": {
                   "endpoint": {"type": "string"},
                   "body": {"type": "object"},
                   "headers": {"type": "object"}
               }, "required": ["endpoint"]}),

    types.Tool(name="api_request",
               description="Make an HTTP request of any verb (Playwright primary tool)",
               inputSchema={"type": "object", "properties": {
                   "method": {"type": "string", "enum": ["GET", "POST", "PUT", "PATCH", "DELETE"]},
                   "url": {"type": "string"},
                   "body": {"type": "string"},
                   "headers": {"type": "string"},
                   "baseUrl": {"type": "string"}
               }, "required": ["method", "url"]}),

    types.Tool(name="assert_status_code",
               description="Assert last API response status code",
               inputSchema={"type": "object", "properties": {
                   "expected_status": {"type": "integer"}
               }, "required": ["expected_status"]}),

    types.Tool(name="assert_json_path",
               description="Assert JSON path value in last API response",
               inputSchema={"type": "object", "properties": {
                   "json_path": {"type": "string"},
                   "expected_value": {"type": "string"}
               }, "required": ["json_path", "expected_value"]}),

    types.Tool(name="assert_response_body_contains",
               description="Assert last API response body contains a string",
               inputSchema={"type": "object", "properties": {
                   "text": {"type": "string"}
               }, "required": ["text"]}),

    types.Tool(name="assert_response_header",
               description="Assert a header on the last API response equals expected",
               inputSchema={"type": "object", "properties": {
                   "header": {"type": "string"},
                   "expected": {"type": "string"}
               }, "required": ["header", "expected"]}),

    # SpecFlow tools
    types.Tool(name="run_feature",
               description="Run a SpecFlow/ReqNroll .feature file",
               inputSchema={"type": "object", "properties": {
                   "feature_file": {"type": "string"},
                   "tags": {"type": "string"},
                   "project_path": {"type": "string"}
               }}),

    types.Tool(name="run_scenario",
               description="Run a specific scenario by name",
               inputSchema={"type": "object", "properties": {
                   "scenario_name": {"type": "string"},
                   "project_path": {"type": "string"}
               }, "required": ["scenario_name"]}),

    types.Tool(name="list_scenarios",
               description="List all available test scenarios",
               inputSchema={"type": "object", "properties": {
                   "project_path": {"type": "string"}
               }}),
]

_CURATED_TOOL_NAMES = {tool.name for tool in TOOL_SCHEMAS}

# ── Dynamic tool discovery from MCPBridge (with short-lived cache) ──────
_bridge_tools_cache: list[dict] | None = None
_bridge_tools_cache_at: float = 0.0
_CACHE_TTL_SECONDS = 30


async def _fetch_bridge_tools() -> list[dict]:
    """Fetches MCPBridge's live tool list, cached briefly to avoid hammering
    it on every tools/list call while still picking up new tools without a
    server restart (the previous populate-once cache never refreshed)."""
    global _bridge_tools_cache, _bridge_tools_cache_at
    now = time.monotonic()
    if _bridge_tools_cache is not None and (now - _bridge_tools_cache_at) < _CACHE_TTL_SECONDS:
        return _bridge_tools_cache

    async with httpx.AsyncClient(timeout=10.0) as client:
        response = await client.get(f"{BRIDGE_URL}/tools")
        response.raise_for_status()
        tools = response.json()

    _bridge_tools_cache = tools
    _bridge_tools_cache_at = now
    return tools


@app.list_tools()
async def list_tools() -> list[types.Tool]:
    all_tools = list(TOOL_SCHEMAS)

    try:
        bridge_tools = await _fetch_bridge_tools()
    except Exception:
        # MCPBridge unreachable — degrade to the curated list rather than
        # failing tool discovery entirely.
        return all_tools

    for tool in bridge_tools:
        name = tool.get("name")
        if not name or name in _CURATED_TOOL_NAMES:
            continue
        # No detailed schema available for this tool from MCPBridge's /tools
        # endpoint (it only returns name/category/fallback, not parameters) —
        # expose it with a permissive schema rather than silently omitting it,
        # so every MCPBridge tool is at least discoverable and callable.
        all_tools.append(types.Tool(
            name=name,
            description=(
                f"{tool.get('category', 'general')} automation tool: {name} "
                "(auto-discovered from MCPBridge — no detailed parameter schema yet)"
            ),
            inputSchema={"type": "object", "additionalProperties": True}
        ))

    return all_tools


@app.call_tool()
async def call_tool(name: str, arguments: dict) -> list[types.TextContent]:
    async with httpx.AsyncClient(timeout=60.0) as client:
        response = await client.post(
            f"{BRIDGE_URL}/execute",
            json={"toolName": name, "arguments": arguments}
        )
        result = response.json()
        return [types.TextContent(type="text", text=json.dumps(result))]


# ── Health check (plain REST, for quick liveness checks outside MCP) ────
async def health_check(request):
    try:
        async with httpx.AsyncClient(timeout=5.0) as client:
            response = await client.get(f"{BRIDGE_URL}/health")
            mcpbridge_status = response.json()
        return JSONResponse({
            "status": "ok",
            "service": "mcp_server",
            "mcpbridge": mcpbridge_status
        })
    except Exception as e:
        return JSONResponse({
            "status": "error",
            "service": "mcp_server",
            "error": str(e)
        }, status_code=503)


# Serve MCP over SSE so VS Code and other MCP clients can connect
def create_starlette_app():
    sse = SseServerTransport("/messages/")

    async def handle_sse(request):
        async with sse.connect_sse(
            request.scope, request.receive, request._send
        ) as streams:
            await app.run(streams[0], streams[1],
                           app.create_initialization_options())

    return Starlette(routes=[
        Route("/health", health_check),
        Route("/sse", endpoint=handle_sse),
        Mount("/messages/", app=sse.handle_post_message),
    ])


if __name__ == "__main__":
    import uvicorn
    starlette_app = create_starlette_app()
    print("=" * 60)
    print("MCP Server for MCPBridge")
    print("=" * 60)
    print(f"MCPBridge endpoint: {BRIDGE_URL}")
    print("Starting server on http://0.0.0.0:8765 (SSE: /sse, health: /health)")
    print("=" * 60)
    uvicorn.run(starlette_app, host="0.0.0.0", port=8765)

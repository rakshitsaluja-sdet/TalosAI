import sys
import json
import asyncio
import webbrowser
import threading
from aiohttp import web
from pathlib import Path

# Import the agent runner directly
sys.path.insert(0, str(Path(__file__).parent))
from agent_runner import run_agent_github, check_mcpbridge_health as check_bridge

# Base directory (agent folder)
BASE_DIR = Path(__file__).parent


async def run_agent(request: web.Request):
    """
    POST /run
    Body: { "goal": "<user prompt>" }
    Executes the agent and returns result as JSON.
    """
    try:
        payload = await request.json()
    except Exception:
        return web.json_response({"error": "Invalid JSON payload"}, status=400)

    goal = payload.get("goal", "").strip()
    if not goal:
        return web.json_response({"error": "Goal is required"}, status=400)

    # Check MCPBridge health first
    bridge_ok = await check_bridge()
    if not bridge_ok:
        return web.json_response({
            "error": "MCPBridge not available at http://localhost:5555. Please start it first.",
            "success": False
        }, status=503)

    try:
        # Call the agent function directly (async)
        print(f"\n?? UI Request: {goal}")
        result = await run_agent_github(goal)
        
        if result:
            return web.json_response({
                "success": True,
                "result": result,
                "type": "final"
            })
        else:
            return web.json_response({
                "success": False,
                "error": "Agent did not return a result (may have exceeded max iterations)",
                "type": "final"
            })
    
    except Exception as e:
        error_message = str(e)
        
        # Filter out technical RAG/ChromaDB errors that don't affect functionality
        if "MetadataValue" in error_message or "chromadb" in error_message.lower():
            print(f"? Warning: RAG storage issue (non-critical): {e}")
            # Return a user-friendly message
            return web.json_response({
                "success": False,
                "error": "Agent encountered a minor storage issue but the test execution may have completed. Please check the browser/screenshots.",
                "type": "warning"
            })
        
        # For real errors, show them
        print(f"? Error in agent execution: {e}")
        import traceback
        traceback.print_exc()
        
        return web.json_response({
            "success": False,
            "error": f"Agent execution failed: {error_message}",
            "type": "final"
        }, status=500)


async def serve_ui(_):
    """
    Serve agent_ui.html
    """
    html = (BASE_DIR / "agent_ui.html").read_text(encoding="utf-8")
    return web.Response(text=html, content_type="text/html")


async def health(_):
    """
    Server-side MCPBridge health check.
    This avoids browser CORS issues.
    """
    bridge_ok = await check_bridge()
    
    return web.json_response({
        "status": "ok",
        "bridge": "online" if bridge_ok else "offline"
    })


def main():
    app = web.Application()
    app.router.add_get("/", serve_ui)
    app.router.add_get("/health", health)
    app.router.add_post("/run", run_agent)

    print("\n?? QA Agent UI Server")
    print("?? http://localhost:8080")
    print("?? Opening browser...\n")

    # Auto-open browser after server starts
    def open_browser():
        import time
        time.sleep(1.5)  # Wait for server to fully start
        webbrowser.open('http://localhost:8080')
    
    threading.Thread(target=open_browser, daemon=True).start()

    web.run_app(app, port=8080)


if __name__ == "__main__":
    main()
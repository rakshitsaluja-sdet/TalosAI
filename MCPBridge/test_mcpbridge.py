"""
MCPBridge Baseline Health Check
================================
Calls every tool category via POST /execute and records pass/fail.
Run this with MCPBridge already running on http://localhost:5555.

Usage:
    python test_mcpbridge.py
    python test_mcpbridge.py --url http://localhost:5555
    python test_mcpbridge.py --category browser
    python test_mcpbridge.py --category api
"""

import sys
import json
import time
import argparse
import httpx
from pathlib import Path

# ─── Config ───────────────────────────────────────────────────────────────────
BASE_URL = "http://localhost:5555"
TIMEOUT  = 30  # seconds per call

# ANSI colours
GREEN  = "\033[92m"
RED    = "\033[91m"
YELLOW = "\033[93m"
CYAN   = "\033[96m"
RESET  = "\033[0m"
BOLD   = "\033[1m"

# ─── Result tracking ──────────────────────────────────────────────────────────
results: list[dict] = []

def call(tool_name: str, arguments: dict) -> dict:
    """POST to /execute and return the parsed response."""
    try:
        r = httpx.post(
            f"{BASE_URL}/execute",
            json={"toolName": tool_name, "arguments": arguments},
            timeout=TIMEOUT,
        )
        return r.json()
    except httpx.TimeoutException:
        return {"success": False, "error": f"Timeout after {TIMEOUT}s"}
    except Exception as e:
        return {"success": False, "error": str(e)}


def test(category: str, tool_name: str, arguments: dict,
         note: str = "", expect_success: bool = True) -> bool:
    """Run one tool call, record result, print status line."""
    start = time.time()
    resp  = call(tool_name, arguments)
    elapsed = round(time.time() - start, 2)

    success  = resp.get("success", False)
    passed   = success == expect_success
    status   = f"{GREEN}PASS{RESET}" if passed else f"{RED}FAIL{RESET}"
    expected = "" if expect_success else f"{YELLOW}[expected failure]{RESET}"

    result_text = ""
    if not passed:
        result_text = f"  → {resp.get('error') or json.dumps(resp.get('result', ''))[:120]}"

    print(f"  {status}  {tool_name:<40} {elapsed:>5}s  {note} {expected}{result_text}")

    results.append({
        "category":    category,
        "tool":        tool_name,
        "passed":      passed,
        "success":     success,
        "elapsed":     elapsed,
        "note":        note,
        "response":    resp,
    })
    return passed


def separator(label: str):
    print(f"\n{BOLD}{CYAN}{'─'*60}{RESET}")
    print(f"{BOLD}{CYAN}  {label}{RESET}")
    print(f"{BOLD}{CYAN}{'─'*60}{RESET}")


# ─── Test suites ──────────────────────────────────────────────────────────────

def test_health():
    separator("HEALTH CHECK")
    try:
        r = httpx.get(f"{BASE_URL}/health", timeout=5)
        data = r.json()
        ok = data.get("status") == "ok"
        print(f"  {'PASS' if ok else 'FAIL'}  /health  →  {data}")
        results.append({"category": "health", "tool": "/health", "passed": ok, "elapsed": 0})
        return ok
    except Exception as e:
        print(f"  {RED}FAIL{RESET}  /health  →  {e}")
        results.append({"category": "health", "tool": "/health", "passed": False, "elapsed": 0})
        return False


def test_tools_endpoint():
    separator("TOOLS ENDPOINT")
    try:
        r    = httpx.get(f"{BASE_URL}/tools", timeout=5)
        data = r.json()
        ok   = isinstance(data, list) and len(data) > 0
        print(f"  {'PASS' if ok else 'FAIL'}  /tools  →  {len(data)} tools registered")
        results.append({"category": "discovery", "tool": "/tools", "passed": ok, "elapsed": 0})
        return ok
    except Exception as e:
        print(f"  {RED}FAIL{RESET}  /tools  →  {e}")
        results.append({"category": "discovery", "tool": "/tools", "passed": False, "elapsed": 0})
        return False


def test_browser():
    """
    Browser tests must run in sequence: launch → interact → close.
    Uses a public stable page (example.com) so no credentials needed.
    """
    separator("BROWSER TOOLS  (Playwright → Selenium fallback)")

    # 1. Launch
    if not test("browser", "launch_browser",
                {"browser": "chromium", "headless": True},
                note="launch headless chromium"):
        print(f"  {YELLOW}⚠  Browser failed to launch — skipping remaining browser tests{RESET}")
        return

    # 2. Navigate
    test("browser", "navigate",
         {"url": "https://example.com"},
         note="navigate to example.com")

    # 3. Assert page title / URL
    test("browser", "assert_page_title",
         {"title": "Example Domain"},
         note="assert page title")

    test("browser", "assert_url_contains",
         {"url": "example.com"},
         note="assert URL contains")

    # 4. Get element text
    test("browser", "get_element_text",
         {"selector": "h1"},
         note="get h1 text")

    # 5. Assert element visible
    test("browser", "assert_element_visible",
         {"selector": "h1"},
         note="assert h1 visible")

    # 6. Screenshot
    test("browser", "take_screenshot",
         {"filename": "test_baseline.png"},
         note="take screenshot")

    # 7. Negative — element that doesn't exist
    test("browser", "assert_element_visible",
         {"selector": "#does-not-exist"},
         note="non-existent element",
         expect_success=False)

    # 8. fill_input — navigate to a page with a form
    test("browser", "navigate",
         {"url": "https://the-internet.herokuapp.com/login"},
         note="navigate to login form")

    test("browser", "fill_input",
         {"selector": "#username", "value": "tomsmith"},
         note="fill username field")

    test("browser", "fill_input",
         {"selector": "#password", "value": "SuperSecretPassword!"},
         note="fill password field")

    test("browser", "click_element",
         {"selector": "button[type='submit']"},
         note="click submit button")

    test("browser", "assert_element_visible",
         {"selector": ".flash.success"},
         note="assert success message visible")

    # 9. press_key — go back, press Tab
    test("browser", "go_back",   {}, note="go back")
    test("browser", "press_key", {"key": "Tab"}, note="press Tab key")

    # 10. Close
    test("browser", "close_browser", {}, note="close browser")


def test_api():
    """
    API tests use httpbin.org — a public echo/testing service.
    No credentials needed.
    """
    separator("API TOOLS  (RestSharp)")

    test("api", "configure_api",
         {"base_url": "https://httpbin.org"},
         note="configure base URL")

    test("api", "api_get",
         {"endpoint": "/get"},
         note="GET /get")

    test("api", "api_post",
         {"endpoint": "/post", "body": {"qa": "test", "tool": "mcpbridge"}},
         note="POST /post with JSON body")

    test("api", "api_put",
         {"endpoint": "/put", "body": {"update": true}},
         note="PUT /put")

    test("api", "api_delete",
         {"endpoint": "/delete"},
         note="DELETE /delete")

    test("api", "assert_status_code",
         {"expected": 200},
         note="assert last response was 200")

    # Negative — assert wrong status code
    test("api", "assert_status_code",
         {"expected": 404},
         note="assert 404 (should fail — last was 200)",
         expect_success=False)


def test_test_data():
    """Bogus data generation — fully stateless, no dependencies."""
    separator("TEST DATA TOOLS  (Bogus)")

    test("testdata", "generate_person_data",
         {"count": 2},
         note="generate 2 persons")

    test("testdata", "generate_user_data",
         {"count": 1},
         note="generate 1 user")

    test("testdata", "generate_product_data",
         {"count": 3},
         note="generate 3 products")

    test("testdata", "generate_order_data",
         {"count": 2},
         note="generate 2 orders")

    test("testdata", "generate_financial_data",
         {"count": 1},
         note="generate 1 financial record")

    test("testdata", "generate_custom_data",
         {"data_type": "internet", "count": 2},
         note="generate internet data")

    test("testdata", "generate_batch_test_data",
         {"count": 5},
         note="generate batch (5 of each type)")

    # Negative — invalid data type
    test("testdata", "generate_custom_data",
         {"data_type": "invalid_type_xyz"},
         note="invalid data_type",
         expect_success=False)


def test_reporting():
    """Reporting lifecycle — configure → start → log → end → generate."""
    separator("REPORTING TOOLS  (Allure)")

    test("reporting", "configure_reporting",
         {"report_dir": "test-results/baseline"},
         note="configure report dir")

    resp = call("start_test", {"test_name": "MCPBridge Baseline", "feature": "Health Check"})
    test_id = resp.get("result", {}).get("id", 1) if isinstance(resp.get("result"), dict) else 1
    results.append({"category": "reporting", "tool": "start_test",
                    "passed": resp.get("success", False), "elapsed": 0})
    status = f"{GREEN}PASS{RESET}" if resp.get("success") else f"{RED}FAIL{RESET}"
    print(f"  {status}  {'start_test':<40}       test_id={test_id}")

    test("reporting", "log_test_step",
         {"test_id": test_id, "step": "MCPBridge health check step 1"},
         note="log step")

    test("reporting", "end_test",
         {"test_id": test_id, "status": "passed"},
         note="end test as passed")

    test("reporting", "generate_report",
         {"type": "summary"},
         note="generate summary report")

    test("reporting", "get_test_statistics",
         {},
         note="get test statistics")

    test("reporting", "export_to_html",
         {"output": "test-results/baseline_report"},
         note="export HTML report")


def test_solution_reader():
    """
    Solution reader — reads the actual TalosAI project structure.
    Resolved relative to this script's location, so it works on any checkout.
    """
    separator("SOLUTION READER TOOLS")

    project_path = str((Path(__file__).resolve().parent.parent / "TalosAI" / "automation"))

    test("solution", "scan_solution",
         {},
         note="scan solution structure")

    test("solution", "read_feature_files",
         {"project_path": project_path},
         note="read all .feature files")

    test("solution", "read_step_definitions",
         {"project_path": project_path},
         note="read step definitions")

    test("solution", "read_page_objects",
         {"project_path": project_path},
         note="read page objects")

    test("solution", "read_config",
         {"project_path": project_path},
         note="read appsettings.json")


def test_unknown_tool():
    """Confirm router returns a clean error for unknown tools."""
    separator("ROUTER ERROR HANDLING")

    test("router", "this_tool_does_not_exist",
         {},
         note="unknown tool name → expect clean error",
         expect_success=False)

    test("router", "execute",
         {},
         note="empty tool name → expect clean error",
         expect_success=False)


# ─── Summary report ───────────────────────────────────────────────────────────

def print_summary():
    print(f"\n{BOLD}{'='*60}{RESET}")
    print(f"{BOLD}  MCPBridge Baseline Results{RESET}")
    print(f"{BOLD}{'='*60}{RESET}\n")

    # Group by category
    categories: dict[str, list] = {}
    for r in results:
        categories.setdefault(r["category"], []).append(r)

    total_pass = total_fail = 0

    for cat, items in categories.items():
        passed = sum(1 for i in items if i["passed"])
        failed = len(items) - passed
        total_pass += passed
        total_fail += failed

        bar   = f"{GREEN}{'■' * passed}{RESET}{RED}{'■' * failed}{RESET}"
        label = f"{passed}/{len(items)}"
        print(f"  {cat:<20} {label:<8} {bar}")

    print(f"\n{BOLD}  Total:  "
          f"{GREEN}{total_pass} passed{RESET}  "
          f"{RED}{total_fail} failed{RESET}  "
          f"({len(results)} tests){RESET}")

    # List failures with their error messages
    failures = [r for r in results if not r["passed"]]
    if failures:
        print(f"\n{BOLD}{RED}  Failures:{RESET}")
        for f in failures:
            err = f["response"].get("error", "") if "response" in f else ""
            print(f"  {RED}✗{RESET}  [{f['category']}] {f['tool']}")
            if err:
                print(f"       {err[:100]}")

    print(f"\n{BOLD}{'='*60}{RESET}\n")

    return total_fail == 0


# ─── Entry point ──────────────────────────────────────────────────────────────

SUITES = {
    "health":   test_health,
    "tools":    test_tools_endpoint,
    "browser":  test_browser,
    "api":      test_api,
    "testdata": test_test_data,
    "reporting":test_reporting,
    "solution": test_solution_reader,
    "router":   test_unknown_tool,
}

if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="MCPBridge Baseline Health Check")
    parser.add_argument("--url",      default=BASE_URL,
                        help="MCPBridge base URL (default: http://localhost:5555)")
    parser.add_argument("--category", default="all",
                        choices=["all"] + list(SUITES.keys()),
                        help="Run a specific test category only")
    args = parser.parse_args()

    BASE_URL = args.url

    print(f"\n{BOLD}MCPBridge Baseline Health Check{RESET}")
    print(f"Target: {BASE_URL}")
    print(f"{'='*60}")

    # Health check must pass before anything else
    if not test_health():
        print(f"\n{RED}MCPBridge is not reachable at {BASE_URL}.{RESET}")
        print("Start it first:  cd MCPBridge && dotnet run")
        sys.exit(1)

    # Run selected suites
    if args.category == "all":
        for name, suite in SUITES.items():
            if name != "health":   # already ran above
                suite()
    else:
        if args.category != "health":
            SUITES[args.category]()

    all_passed = print_summary()
    sys.exit(0 if all_passed else 1)

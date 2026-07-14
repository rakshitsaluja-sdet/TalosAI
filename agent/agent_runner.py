"""
Agent Runner - Claude Agent SDK Integration
Uses the Claude Agent SDK (existing Claude Code OAuth session) to interact with MCPBridge automation tools
"""
import os
import sys
import asyncio
import json
from pathlib import Path
from typing import List, Dict, Any, Optional
import httpx
from claude_agent_sdk import (
    tool,
    create_sdk_mcp_server,
    ClaudeAgentOptions,
    query,
    AssistantMessage,
    TextBlock,
    ToolUseBlock,
    ResultMessage,
)
from rag_store import RagStore
from execution_state import ExecutionState


# Configuration
MCPBRIDGE_URL = "http://localhost:5555"
MCP_SERVER_URL = "http://localhost:8000"
# Optional override, e.g. "claude-opus-4-8" - leave unset to use the CLI's default model.
CLAUDE_MODEL = os.getenv("CLAUDE_MODEL")

# ================================
# Tool Capability Registry 
# ================================
TOOL_CAPABILITIES = {
    # Selenium / Browser
    "launch_browser": ["resource"],
    "close_browser": ["resource"],
    "navigate": ["location"],
    "navigate_to": ["location"],
    "click_by_text": ["action"],
    "click_element": ["action"],
    "click": ["action"],
    "fill_input": ["action"],
    "type_text": ["action"],
    "press_key": ["action"],

    # Solution Reader
    "scan_solution": ["read_once"],
    "read_feature_files": ["read_once"],
    "read_step_definitions": ["read_once"],
    "read_page_objects": ["read_once"],
    "read_config": ["read_once"],

    # Solution Writer
    "write_feature_file": ["artifact"],
    "write_step_definition": ["artifact"],
    "write_page_object": ["artifact"],
    "scaffold_feature": ["artifact"],
    "append_to_step_definition": ["artifact"],

    # Reporting
    "configure_reporting": ["resource"],
    "start_test": ["resource"],
    "generate_report": ["artifact"]
}

# === Agent Runtime Memory ===
RAG_STORE = RagStore("../.agent_runtime/vector_store")
EXEC_STATE = ExecutionState("../.agent_runtime/execution_state.json")

# Reset execution state on startup (new session should start fresh)
EXEC_STATE.reset()
print("[INFO] Execution state reset - starting fresh session")

# Tool schemas for AI function calling
TOOL_SCHEMAS = [
    {
        "type": "function",
        "function": {
            "name": "launch_browser",
            "description": "Launch a web browser (Chrome by default). Must be called before any browser interaction.",
            "parameters": {
                "type": "object",
                "properties": {
                    "browser": {
                        "type": "string",
                        "description": "Browser type: chrome, firefox, or edge",
                        "enum": ["chrome", "firefox", "edge"],
                        "default": "chrome"
                    },
                    "headless": {
                        "type": "boolean",
                        "description": "Run browser in headless mode (no GUI)",
                        "default": False
                    }
                },
                "required": []
            }
        }
    },
    {
        "type": "function",
        "function": {
            "name": "navigate",
            "description": "Navigate the browser to a specific URL. Browser must be launched first.",
            "parameters": {
                "type": "object",
                "properties": {
                    "url": {
                        "type": "string",
                        "description": "The URL to navigate to (must include http:// or https://)"
                    }
                },
                "required": ["url"]
            }
        }
    },
    {
        "type": "function",
        "function": {
            "name": "find_element",
            "description": "Find an element on the page and get its properties",
            "parameters": {
                "type": "object",
                "properties": {
                    "strategy": {
                        "type": "string",
                        "description": "Locator strategy",
                        "enum": ["css", "xpath", "id", "name", "text", "tag"],
                        "default": "css"
                    },
                    "value": {
                        "type": "string",
                        "description": "Locator value (CSS selector, XPath, ID, etc.)"
                    }
                },
                "required": ["value"]
            }
        }
    },
    {
        "type": "function",
        "function": {
            "name": "click",
            "description": "Click on an element using CSS selector, XPath, ID, or name locator. For clicking by visible text, use click_by_text instead.",
            "parameters": {
                "type": "object",
                "properties": {
                    "strategy": {
                        "type": "string",
                        "description": "Locator strategy (CSS, XPath, ID, name only - NOT text)",
                        "enum": ["css", "xpath", "id", "name"],
                        "default": "css"
                    },
                    "value": {
                        "type": "string",
                        "description": "Locator value to identify the element"
                    }
                },
                "required": ["value"]
            }
        }
    },
    {
        "type": "function",
        "function": {
            "name": "click_by_text",
            "description": "[PREFERRED] Click on an element by its visible text content (e.g., button text, link text). This is the RECOMMENDED tool for clicking buttons, links, or any element with visible text. Uses intelligent ElementToBeClickable waits and tries multiple strategies automatically (button, link, span, div). Much more reliable than XPath text-based locators.",
            "parameters": {
                "type": "object",
                "properties": {
                    "text": {
                        "type": "string",
                        "description": "The visible text content of the element to click (e.g., 'Submit', 'Login', 'Next')"
                    },
                    "exact_match": {
                        "type": "boolean",
                        "description": "Whether to match exact text (true) or partial text (false). Default is false for flexibility.",
                        "default": False
                    },
                    "timeout": {
                        "type": "integer",
                        "description": "Maximum wait time in seconds for the element to become clickable. Default is 30 seconds.",
                        "default": 30
                    }
                },
                "required": ["text"]
            }
        }
    },
    {
        "type": "function",
        "function": {
            "name": "type_text",
            "description": "Type text into an input field or text area",
            "parameters": {
                "type": "object",
                "properties": {
                    "strategy": {
                        "type": "string",
                        "description": "Locator strategy",
                        "enum": ["css", "xpath", "id", "name"],
                        "default": "css"
                    },
                    "value": {
                        "type": "string",
                        "description": "Locator value to identify the input element"
                    },
                    "text": {
                        "type": "string",
                        "description": "Text to type into the element"
                    }
                },
                "required": ["value", "text"]
            }
        }
    },
    {
        "type": "function",
        "function": {
            "name": "fill_input",
            "description": "Fill an input field with text (Playwright). Use CSS selector, or prefix with # for ID. Example: #username or input[name='username']",
            "parameters": {
                "type": "object",
                "properties": {
                    "selector": {
                        "type": "string",
                        "description": "CSS selector to identify the input (e.g., '#user-name', 'input[name=username]', '#password')"
                    },
                    "value": {
                        "type": "string",
                        "description": "Text to fill into the input"
                    },
                    "clearFirst": {
                        "type": "boolean",
                        "description": "Clear field before typing",
                        "default": True
                    }
                },
                "required": ["selector", "value"]
            }
        }
    },
    {
        "type": "function",
        "function": {
            "name": "click_element",
            "description": "Click an element using CSS selector (Playwright). Use CSS selector like #button-id or button.class-name",
            "parameters": {
                "type": "object",
                "properties": {
                    "selector": {
                        "type": "string",
                        "description": "CSS selector to identify the element (e.g., '#login-button', 'button[type=submit]')"
                    },
                    "timeoutMs": {
                        "type": "integer",
                        "description": "Timeout in milliseconds",
                        "default": 30000
                    }
                },
                "required": ["selector"]
            }
        }
    },
    {
        "type": "function",
        "function": {
            "name": "get_text",
            "description": "Get the text content of an element",
            "parameters": {
                "type": "object",
                "properties": {
                    "strategy": {
                        "type": "string",
                        "description": "Locator strategy",
                        "enum": ["css", "xpath", "id", "name"],
                        "default": "css"
                    },
                    "value": {
                        "type": "string",
                        "description": "Locator value"
                    }
                },
                "required": ["value"]
            }
        }
    },
    {
        "type": "function",
        "function": {
            "name": "take_screenshot",
            "description": "Take a screenshot of the current page. Only use when: 1) User explicitly requests a screenshot, 2) Debugging a failure, 3) After completing a major test section (not after every single action). Do NOT use after every fill/click.",
            "parameters": {
                "type": "object",
                "properties": {
                    "filename": {
                        "type": "string",
                        "description": "Filename for the screenshot",
                        "default": "screenshot.png"
                    }
                },
                "required": []
            }
        }
    },
    {
        "type": "function",
        "function": {
            "name": "assert_visible",
            "description": "Assert that an element is visible on the page",
            "parameters": {
                "type": "object",
                "properties": {
                    "strategy": {
                        "type": "string",
                        "description": "Locator strategy",
                        "default": "css"
                    },
                    "value": {
                        "type": "string",
                        "description": "Locator value"
                    }
                },
                "required": ["value"]
            }
        }
    },
    {
        "type": "function",
        "function": {
            "name": "close_browser",
            "description": "Close the browser and end the session. Should be called when automation is complete.",
            "parameters": {
                "type": "object",
                "properties": {},
                "required": []
            }
        }
    },
    {
        "type": "function",
        "function": {
            "name": "configure_api",
            "description": "Configure API testing with a base URL",
            "parameters": {
                "type": "object",
                "properties": {
                    "base_url": {
                        "type": "string",
                        "description": "Base URL for API requests"
                    }
                },
                "required": ["base_url"]
            }
        }
    },
    {
        "type": "function",
        "function": {
            "name": "api_get",
            "description": "Make a GET request to an API endpoint",
            "parameters": {
                "type": "object",
                "properties": {
                    "endpoint": {
                        "type": "string",
                        "description": "API endpoint path"
                    }
                },
                "required": ["endpoint"]
            }
        }
    },
    # Azure DevOps Integration Tools
    {
        "type": "function",
        "function": {
            "name": "configure_azure_devops",
            "description": "Configure connection to Azure DevOps to access work items and user stories. Must be called before any other Azure DevOps operations.",
            "parameters": {
                "type": "object",
                "properties": {
                    "organization": {
                        "type": "string",
                        "description": "Azure DevOps organization name (e.g., 'TalosAI')"
                    },
                    "project": {
                        "type": "string",
                        "description": "Project name (e.g., 'Track 2 - Billing and Rating Modernization')"
                    },
                    "pat": {
                        "type": "string",
                        "description": "Personal Access Token (optional if AZURE_DEVOPS_PAT environment variable is set)"
                    }
                },
                "required": ["organization", "project"]
            }
        }
    },
    {
        "type": "function",
        "function": {
            "name": "get_active_user_stories",
            "description": "Get all active user stories from Azure DevOps (states: New, Active, Committed, In Progress). Returns IDs, titles, states, descriptions, and acceptance criteria.",
            "parameters": {
                "type": "object",
                "properties": {},
                "required": []
            }
        }
    },
    {
        "type": "function",
        "function": {
            "name": "get_user_story",
            "description": "Get detailed information about a specific user story by ID from Azure DevOps. Returns full details including description, acceptance criteria, assignee, etc.",
            "parameters": {
                "type": "object",
                "properties": {
                    "id": {
                        "type": "integer",
                        "description": "Work item ID number"
                    }
                },
                "required": ["id"]
            }
        }
    },
    {
        "type": "function",
        "function": {
            "name": "get_work_items_by_query",
            "description": "Query Azure DevOps work items using WIQL (Work Item Query Language)",
            "parameters": {
                "type": "object",
                "properties": {
                    "wiql": {
                        "type": "string",
                        "description": "WIQL query string (optional, defaults to active user stories)"
                    }
                },
                "required": []
            }
        }
    },
    {
        "type": "function",
        "function": {
            "name": "get_user_stories_by_iteration",
            "description": "Get user stories from a specific iteration/sprint",
            "parameters": {
                "type": "object",
                "properties": {
                    "iteration_path": {
                        "type": "string",
                        "description": "Iteration path (e.g., 'Sprint 1')"
                    }
                },
                "required": ["iteration_path"]
            }
        }
    },
    {
        "type": "function",
        "function": {
            "name": "get_user_stories_by_tag",
            "description": "Get user stories filtered by tag",
            "parameters": {
                "type": "object",
                "properties": {
                    "tag": {
                        "type": "string",
                        "description": "Tag to filter by (e.g., 'API', 'UI')"
                    }
                },
                "required": ["tag"]
            }
        }
    },
    {
        "type": "function",
        "function": {
            "name": "generate_test_scenarios",
            "description": "Auto-generate Gherkin test scenarios from an Azure DevOps user story. Returns feature file template, step definitions, and test cases.",
            "parameters": {
                "type": "object",
                "properties": {
                    "id": {
                        "type": "integer",
                        "description": "User story ID to generate tests from"
                    }
                },
                "required": ["id"]
            }
        }
    },
    # Performance Testing Tools (NBomber)
    {
        "type": "function",
        "function": {
            "name": "configure_performance_test",
            "description": "Configure performance test parameters. Call this ONCE at the start of a performance session ONLY. Do NOT call again for subsequent tests — the base_url and session_name persist for the entire session. If base_url is already set, go directly to run_load_test, run_stress_test, or run_spike_test.",
            "parameters": {
                "type": "object",
                "properties": {
                    "base_url":      {"type": "string",  "description": "Base URL for performance testing"},
                    "duration":      {"type": "integer", "description": "Test duration in seconds (default: 10)"},
                    "virtual_users": {"type": "integer", "description": "Number of concurrent virtual users (default: 10)"},
                    "headers":       {"type": "object",  "description": "HTTP headers as key-value pairs (e.g. Authorization)"},
                    "session_name":  {"type": "string",  "description": "Label for this test session — used as the NBomber report folder name. ALWAYS extract from user prompt if they mention 'session name X' or 'named X'. Defaults to timestamp if omitted."},
                    "report_folder": {"type": "string",  "description": "Root folder for NBomber reports (default: nbomber-reports)"}
                },
                "required": ["base_url"]
            }
        }
    },
    {
        "type": "function",
        "function": {
            "name": "run_load_test",
            "description": "Run load test to measure performance under expected load",
            "parameters": {
                "type": "object",
                "properties": {
                    "endpoint": {"type": "string", "description": "API endpoint path (e.g., '/api/users')"},
                    "method": {"type": "string", "description": "HTTP method (GET, POST, PUT, DELETE)", "default": "GET"},
                    "scenario_name": {"type": "string", "description": "Name for this test scenario"}
                },
                "required": ["endpoint"]
            }
        }
    },
    {
        "type": "function",
        "function": {
            "name": "run_stress_test",
            "description": "Run stress test by ramping up users to find breaking point",
            "parameters": {
                "type": "object",
                "properties": {
                    "endpoint": {"type": "string", "description": "API endpoint path"},
                    "max_users": {"type": "integer", "description": "Maximum number of users to ramp to (default: 100)"}
                },
                "required": ["endpoint"]
            }
        }
    },
    {
        "type": "function",
        "function": {
            "name": "run_spike_test",
            "description": "Run spike test to test recovery from sudden traffic spikes. Runs normal load, then sudden spike, then recovers back to normal load.",
            "parameters": {
                "type": "object",
                "properties": {
                    "endpoint": {"type": "string", "description": "API endpoint path"},
                    "normal_load": {"type": "integer", "description": "Normal load users (default: 10)"},
                    "spike_load": {"type": "integer", "description": "Spike load users (default: 100)"}
                },
                "required": ["endpoint"]
            }
        }
    },
    {
        "type": "function",
        "function": {
            "name": "get_performance_summary",
            "description": (
                "Get a quick INLINE text summary of all performance tests in this session. "
                "Use ONLY when the user wants to VIEW results in the console without saving a file — "
                "e.g. 'show me the results', 'what were the numbers', 'summarise the tests'. "
                "Do NOT use this when the user says 'generate report', 'create report', "
                "'save report', or 'export report' — use export_performance_report for those."
            ),
            "parameters": {
                "type": "object",
                "properties": {},
                "required": []
            }
        }
    },
    {
        "type": "function",
        "function": {
            "name": "export_performance_report",
            "description": (
                "Generate and SAVE a consolidated HTML performance report covering ALL tests "
                "(load, stress, spike) run in this session. "
                "ALWAYS use this when the user says 'generate report', 'generate performance report', "
                "'create report', 'save report', 'export report', or 'give me the report'. "
                "Works even if the user never provided a session_name — a timestamped name is used automatically. "
                "report_name sets the title in the HTML header. "
                "Returns the file path so the user knows where to find it."
            ),
            "parameters": {
                "type": "object",
                "properties": {
                    "report_name":   {"type": "string", "description": "Title for the report (e.g. 'Warmup Scenario')"},
                    "output_folder": {"type": "string", "description": "Folder to write the HTML file (default: nbomber-reports)"}
                },
                "required": []
            }
        }
    },
    # Test Data Generation Tools (Bogus)
    {
        "type": "function",
        "function": {
            "name": "generate_person_data",
            "description": "Generate fake person data (names, emails, addresses, phones, etc.)",
            "parameters": {
                "type": "object",
                "properties": {
                    "count": {"type": "integer", "description": "Number of people to generate (default: 1)"},
                    "locale": {"type": "string", "description": "Locale for data (e.g., 'en', 'fr', 'de')", "default": "en"}
                },
                "required": []
            }
        }
    },
    {
        "type": "function",
        "function": {
            "name": "generate_user_data",
            "description": "Generate fake user account data (usernames, passwords, emails, avatars)",
            "parameters": {
                "type": "object",
                "properties": {
                    "count": {"type": "integer", "description": "Number of users to generate (default: 1)"}
                },
                "required": []
            }
        }
    },
    {
        "type": "function",
        "function": {
            "name": "generate_product_data",
            "description": "Generate fake product data for e-commerce testing",
            "parameters": {
                "type": "object",
                "properties": {
                    "count": {"type": "integer", "description": "Number of products to generate (default: 1)"}
                },
                "required": []
            }
        }
    },
    {
        "type": "function",
        "function": {
            "name": "generate_order_data",
            "description": "Generate fake order data with items and totals",
            "parameters": {
                "type": "object",
                "properties": {
                    "count": {"type": "integer", "description": "Number of orders to generate (default: 1)"}
                },
                "required": []
            }
        }
    },
    {
        "type": "function",
        "function": {
            "name": "generate_financial_data",
            "description": "Generate fake financial transaction data",
            "parameters": {
                "type": "object",
                "properties": {
                    "count": {"type": "integer", "description": "Number of transactions to generate (default: 1)"}
                },
                "required": []
            }
        }
    },
    {
        "type": "function",
        "function": {
            "name": "generate_custom_data",
            "description": "Generate custom fake data (lorem, internet, dates, vehicles, system)",
            "parameters": {
                "type": "object",
                "properties": {
                    "data_type": {"type": "string", "description": "Type of data: lorem, internet, date, vehicle, system"},
                    "count": {"type": "integer", "description": "Number of items to generate (default: 1)"}
                },
                "required": ["data_type"]
            }
        }
    },
    {
        "type": "function",
        "function": {
            "name": "generate_batch_test_data",
            "description": "Generate complete test data set (users, products, orders)",
            "parameters": {
                "type": "object",
                "properties": {
                    "count": {"type": "integer", "description": "Number of each type to generate (default: 10)"}
                },
                "required": []
            }
        }
    },
    # Image/Visual Testing Tools (Magick.NET)
    {
        "type": "function",
        "function": {
            "name": "compare_images",
            "description": "Compare two images for visual regression testing",
            "parameters": {
                "type": "object",
                "properties": {
                    "baseline_image": {"type": "string", "description": "Path to baseline image"},
                    "current_image": {"type": "string", "description": "Path to current/test image"},
                    "diff_image": {"type": "string", "description": "Output path for diff image", "default": "diff.png"},
                    "threshold": {"type": "number", "description": "Difference threshold (0.01 = 1%)", "default": 0.01}
                },
                "required": ["baseline_image", "current_image"]
            }
        }
    },
    {
        "type": "function",
        "function": {
            "name": "resize_image",
            "description": "Resize image to specific dimensions",
            "parameters": {
                "type": "object",
                "properties": {
                    "input": {"type": "string", "description": "Input image path"},
                    "output": {"type": "string", "description": "Output image path"},
                    "width": {"type": "integer", "description": "Target width in pixels"},
                    "height": {"type": "integer", "description": "Target height in pixels"}
                },
                "required": ["input", "output", "width", "height"]
            }
        }
    },
    {
        "type": "function",
        "function": {
            "name": "create_screenshot_comparison",
            "description": "Create side-by-side comparison of baseline, current, and diff images",
            "parameters": {
                "type": "object",
                "properties": {
                    "baseline": {"type": "string", "description": "Baseline image path"},
                    "current": {"type": "string", "description": "Current image path"},
                    "output": {"type": "string", "description": "Output comparison image path", "default": "comparison.png"}
                },
                "required": ["baseline", "current"]
            }
        }
    },
    # Test Reporting Tools (Allure)
    {
        "type": "function",
        "function": {
            "name": "configure_reporting",
            "description": "Configure test reporting directory",
            "parameters": {
                "type": "object",
                "properties": {
                    "report_dir": {"type": "string", "description": "Report output directory", "default": "allure-results"}
                },
                "required": []
            }
        }
    },
    {
        "type": "function",
        "function": {
            "name": "start_test",
            "description": "Start tracking a test execution",
            "parameters": {
                "type": "object",
                "properties": {
                    "test_name": {"type": "string", "description": "Test name"},
                    "feature": {"type": "string", "description": "Feature name (optional)"},
                    "scenario": {"type": "string", "description": "Scenario name (optional)"}
                },
                "required": ["test_name"]
            }
        }
    },
    {
        "type": "function",
        "function": {
            "name": "log_test_step",
            "description": "Log a test step during execution",
            "parameters": {
                "type": "object",
                "properties": {
                    "test_id": {"type": "integer", "description": "Test ID (from start_test)"},
                    "step": {"type": "string", "description": "Step description"}
                },
                "required": ["step"]
            }
        }
    },
    {
        "type": "function",
        "function": {
            "name": "attach_screenshot",
            "description": "Attach screenshot to test report",
            "parameters": {
                "type": "object",
                "properties": {
                    "test_id": {"type": "integer", "description": "Test ID"},
                    "screenshot": {"type": "string", "description": "Screenshot file path"},
                    "title": {"type": "string", "description": "Screenshot title", "default": "Screenshot"}
                },
                "required": ["screenshot"]
            }
        }
    },
    {
        "type": "function",
        "function": {
            "name": "end_test",
            "description": "End test execution and record results",
            "parameters": {
                "type": "object",
                "properties": {
                    "test_id": {"type": "integer", "description": "Test ID"},
                    "status": {"type": "string", "description": "Test status (passed, failed, skipped)"},
                    "error": {"type": "string", "description": "Error message if failed"}
                },
                "required": ["status"]
            }
        }
    },
    {
        "type": "function",
        "function": {
            "name": "generate_report",
            "description": "Generate test execution summary report. Only use AFTER all test steps are complete, not during test execution. Wait until the user has finished all interactions before generating report.",
            "parameters": {
                "type": "object",
                "properties": {
                    "type": {"type": "string", "description": "Report type", "default": "summary"}
                },
                "required": []
            }
        }
    },
    {
        "type": "function",
        "function": {
            "name": "export_to_html",
            "description": "Export test report to HTML format with automatic timestamp in filename",
            "parameters": {
                "type": "object",
                "properties": {
                    "output": {"type": "string", "description": "HTML output filename (timestamp will be added automatically)"}
                },
                "required": []
            }
        }
    },
    {
        "type": "function",
        "function": {
            "name": "generate_allure_report",
            "description": "Generate proper Allure HTML report using Allure CLI from JSON results",
            "parameters": {
                "type": "object",
                "properties": {
                    "output_dir": {"type": "string", "description": "Output directory for Allure HTML report"}
                },
                "required": []
            }
        }
    },
    {
        "type": "function",
        "function": {
            "name": "open_allure_report",
            "description": "Open generated Allure report in browser",
            "parameters": {
                "type": "object",
                "properties": {
                    "report_dir": {"type": "string", "description": "Directory containing Allure report"}
                },
                "required": []
            }
        }
    },
 {
     "type": "function",
     "function": {
         "name": "scan_solution",
         "description": "Scan entire solution to count projects and analyze structure",
         "parameters": {
             "type": "object",
             "properties": {},
             "required": []
         }
     }
 },
 {
     "type": "function",
     "function": {
         "name": "read_feature_files",
         "description": "Read all feature files from TalosAI project and count scenarios",
         "parameters": {
             "type": "object",
             "properties": {
                 "project_path": {"type": "string", "description": "Path to TalosAI project"}
             },
             "required": ["project_path"]
         }
     }
 },
 {
     "type": "function",
     "function": {
         "name": "write_feature_file",
         "description": "Write a new feature file to TalosAI project",
         "parameters": {
             "type": "object",
             "properties": {
                 "project_path": {"type": "string", "description": "Path to project"},
                 "file_name": {"type": "string", "description": "Feature file name"},
                 "content": {"type": "string", "description": "Feature content"},
                 "sub_folder": {"type": "string", "description": "Subfolder (default: Features)"}
             },
             "required": ["project_path", "file_name", "content"]
         }
     }
 },
# ─────────────────────────────────────────────
# Solution Writer tools – Playwright
# ─────────────────────────────────────────────

{
    "type": "function",
    "function": {
        "name": "write_playwright_feature",
        "description": (
            "Write a Playwright-tagged .feature file. "
            "Automatically adds @playwright tag to every scenario "
            "to route execution through PlaywrightHooks."
        ),
        "parameters": {
            "type": "object",
            "properties": {
                "project_path": {"type": "string"},
                "file_name":    {"type": "string"},
                "content":      {"type": "string"},
                "sub_folder":   {"type": "string", "default": "Features"},
                "overwrite":    {"type": "boolean", "default": False}
            },
            "required": ["project_path", "file_name", "content"]
        }
    }
},

{
    "type": "function",
    "function": {
        "name": "write_playwright_steps",
        "description": (
            "Write Playwright async step definition (.cs). "
            "Validates inheritance from PlaywrightBaseSteps and "
            "enforces async Task usage."
        ),
        "parameters": {
            "type": "object",
            "properties": {
                "project_path": {"type": "string"},
                "file_name":    {"type": "string"},
                "content":      {"type": "string"},
                "sub_folder":   {"type": "string", "default": "Steps"},
                "overwrite":    {"type": "boolean", "default": False}
            },
            "required": ["project_path", "file_name", "content"]
        }
    }
},

{
    "type": "function",
    "function": {
        "name": "write_playwright_page_object",
        "description": (
            "Write Playwright page object (.cs). "
            "Validates inheritance from PlaywrightBasePage and "
            "rejects Thread.Sleep usage."
        ),
        "parameters": {
            "type": "object",
            "properties": {
                "project_path": {"type": "string"},
                "file_name":    {"type": "string"},
                "content":      {"type": "string"},
                "sub_folder":   {"type": "string", "default": "Pages"},
                "overwrite":    {"type": "boolean", "default": False}
            },
            "required": ["project_path", "file_name", "content"]
        }
    }
},

{
    "type": "function",
    "function": {
        "name": "scaffold_playwright_feature",
        "description": (
            "Create a complete Playwright SpecFlow feature scaffold: "
            "feature file, step definition, and optional page object. "
            "All files are validated before writing."
        ),
        "parameters": {
            "type": "object",
            "properties": {
                "project_path":        {"type": "string"},
                "feature_name":        {"type": "string"},
                "feature_content":     {"type": "string"},
                "step_def_content":    {"type": "string"},
                "page_object_content": {"type": "string"}
            },
            "required": [
                "project_path",
                "feature_name",
                "feature_content",
                "step_def_content"
            ]
        }
    }
}
]

# ================================
# Claude Agent SDK MCP tool bridge
# ================================
# Every entry in TOOL_SCHEMAS already fully describes a tool (name, description,
# JSON-schema parameters) and execute_tool() already knows how to run any of them
# against MCPBridge generically - so wrap each schema once instead of hand-writing
# ~40 @tool-decorated functions.
def _make_bridge_tool(schema: dict):
    fn = schema["function"]
    tool_name = fn["name"]
    input_schema = fn.get("parameters") or {"type": "object", "properties": {}}

    @tool(tool_name, fn.get("description", ""), input_schema)
    async def handler(args: dict, _tool_name: str = tool_name) -> Dict[str, Any]:
        result = await dispatch_tool_call(_tool_name, args)
        return {"content": [{"type": "text", "text": json.dumps(result)}]}

    return handler


TALOSAI_MCP_SERVER = create_sdk_mcp_server(
    name="talosai",
    tools=[_make_bridge_tool(schema) for schema in TOOL_SCHEMAS],
)

def get_relevant_tools(user_request: str) -> list:
    """
    Return ONLY tool schemas relevant to the user request.
    Fully aligned with current agent_runner TOOL_SCHEMAS and system rules.
    """

    request_lower = user_request.lower()

    # ─── RUNTIME STATE INJECTION ──────────────────────────────────────────────
    # Driven by EXEC_STATE, not keyword guessing.
    # If a resource is already active, its interaction tools are always included
    # regardless of how the user phrases the next instruction.
    # This is what fixes "Enter username / password" after "Launch browser" —
    # no keyword match needed; the state says the browser is open.
    always_include: set = set()

    if EXEC_STATE.get("browser_open"):
        always_include.update([
            "navigate",
            "fill_input", "type_text",           # form inputs
            "click_by_text", "click_element", "click",
            "press_key",
            "get_text", "get_attribute",
            "assert_visible", "assert_text_contains", "assert_url_contains",
            "get_page_info", "select_dropdown",
            "execute_script",
            "close_browser",
        ])

    if EXEC_STATE.get("api_configured"):
        always_include.update([
            "api_get", "api_post", "api_put", "api_delete",
            "assert_status_code", "assert_json_path",
        ])

    if EXEC_STATE.get("ado_configured"):
        always_include.update([
            "get_active_user_stories", "get_user_story",
            "get_work_items_by_query", "get_user_stories_by_iteration",
            "get_user_stories_by_tag", "generate_test_scenarios",
        ])

    if EXEC_STATE.get("performance_configured"):
        always_include.update([
            # configure_performance_test intentionally excluded — session already active
            "run_load_test",
            "run_stress_test",
            "run_spike_test",
            "get_performance_summary",
            "export_performance_report",
        ])
    # ─────────────────────────────────────────────────────────────────────────

    # --- Keyword → tool mapping (STRICTLY from TOOL_SCHEMAS) ---
    tool_groups = {

        # ======================
        # UI / Selenium
        # ======================
        ("browser", "navigate", "ui", "page", "url", "selenium", "click", "sign in", "login"):
            [
                "launch_browser",
                "navigate",
                "find_element",
                "click_by_text",     # ✅ preferred
                "click",             # fallback only
                "type_text",
                "get_text",
                "assert_visible",
                "take_screenshot",
                "close_browser"
            ],

        # ======================
        # API Testing
        # ======================
        ("api", "endpoint", "rest", "get", "post", "put", "delete", "http"):
            [
                "configure_api",
                "api_get"
            ],

        # ======================
        # Azure DevOps
        # ======================
        ("azure", "devops", "ado", "story", "user story", "sprint", "iteration"):
            [
                "configure_azure_devops",
                "get_active_user_stories",
                "get_user_story",
                "get_work_items_by_query",
                "get_user_stories_by_iteration",
                "get_user_stories_by_tag",
                "generate_test_scenarios"
            ],

        # ======================
        # Solution Analysis / Authoring
        # ======================
        ("feature", "gherkin", "specflow", "reqnroll", "step", "page object", "scaffold", "generate test"):
            [
                "scan_solution",
                "read_feature_files",
                "write_feature_file",
                "write_step_definition",
                "write_page_object",
                "append_to_step_definition",
                "scaffold_feature"
            ],

        # ======================
        # Performance
        # ======================
        ("performance", "load", "stress", "spike", "nbomber", "performance report", "export", "html report"):
            [
                "configure_performance_test",
                "run_load_test",
                "run_stress_test",
                "run_spike_test",
                "get_performance_summary",
                "export_performance_report",  # ← NBomber-styled HTML export
            ],

        # ======================
        # Test Data
        # ======================
        ("test data", "fake", "generate data"):
            [
                "generate_person_data",
                "generate_user_data",
                "generate_product_data",
                "generate_order_data",
                "generate_financial_data",
                "generate_batch_test_data"
            ],

        # ======================
        # Image / Visual
        # ======================
        ("image", "visual", "compare", "baseline", "screenshot diff"):
            [
                "compare_images",
                "resize_image",
                "create_screenshot_comparison"
            ],

        # ======================
        # Reporting / Notifications
        # ======================
        ("allure", "test report", "test result", "test step", "log step", "attach screenshot"):
            [
                "configure_reporting",
                "start_test",
                "log_test_step",
                "attach_screenshot",
                "end_test",
                "generate_report",
                "export_to_html",
                "take_screenshot"
            ],

        ("teams", "notify", "message", "send"):
            [
                "send_teams_message",
                "send_test_results"
            ]
    }

    # --- Build relevant tool name set ---
    # Start from state-injected tools, then layer in keyword-matched tools on top
    relevant_names = set(always_include)

    for keywords, tools in tool_groups.items():
        if any(kw in request_lower for kw in keywords):
            relevant_names.update(tools)

    # --- Fallback: only reached when browser is NOT open AND no keywords matched ---
    # (e.g. a brand-new session with an unrecognised prompt)
    # fill_input / type_text included here so a first-prompt like
    # "go to X and log in with Y / Z" still gets form tools.
    if not relevant_names:
        relevant_names.update([
            "launch_browser",
            "navigate",
            "fill_input", "type_text",
            "click_by_text", "click_element",
            "press_key",
            "close_browser",
        ])

    # --- Filter schemas ---
    filtered_schemas = [
        schema for schema in TOOL_SCHEMAS
        if schema["function"]["name"] in relevant_names
    ]

    print(f"[Tools] {len(filtered_schemas)}/{len(TOOL_SCHEMAS)} tools loaded for this prompt")
    return filtered_schemas

async def execute_tool(tool_name: str, arguments: Dict[str, Any]) -> Dict[str, Any]:
    """Execute a tool via MCPBridge"""
    print(f"\n?? Executing tool: {tool_name}")
    print(f"   Arguments: {json.dumps(arguments, indent=2)}")
    
    try:
        async with httpx.AsyncClient(timeout=60.0) as http_client:
            response = await http_client.post(
                f"{MCPBRIDGE_URL}/execute",
                json={"toolName": tool_name, "arguments": arguments}
            )
            result = response.json()
            
            if result.get("success"):
                print(f"   ? Success: {result.get('result', {})}")
            else:
                print(f"   ? Failed: {result.get('error')}")
            
            return result
    except Exception as e:
        print(f"   ? Error: {e}")
        return {"success": False, "error": str(e)}

async def check_mcpbridge_health() -> bool:
    """Check if MCPBridge is running and healthy"""
    try:
        async with httpx.AsyncClient(timeout=5.0) as client:
            response = await client.get(f"{MCPBRIDGE_URL}/health")
            if response.status_code == 200:
                data = response.json()
                print(f"? MCPBridge: {data.get('service')} - {data.get('status')}")
                return True
    except Exception as e:
        print(f"? MCPBridge not available: {e}")
        print(f"  Make sure MCPBridge is running on {MCPBRIDGE_URL}")
        return False
    return False

    # ================================ Smart Reuse Policy (Generic) ================================

def should_execute_tool(tool_name: str, arguments: dict, is_user_request: bool = False):
    """
    Determine if a tool should be executed or reused.
    
    Args:
        tool_name: Name of the tool
        arguments: Tool arguments
        is_user_request: True if this is the first iteration of a user's direct request
    
    Returns:
        True if tool should execute, False if it should be skipped (reused)
    """
    # ALWAYS execute tools on user's direct request (first iteration)
    if is_user_request:
        return True
    
    capabilities = TOOL_CAPABILITIES.get(tool_name, [])

    # Resource reuse (browser, reporting, api, etc.)
    if "resource" in capabilities:
        if tool_name == "launch_browser" and EXEC_STATE.get("browser_open"):
            return False
        if tool_name == "configure_reporting" and EXEC_STATE.get("reporting_configured"):
            return False

    # Location reuse (navigation) - DISABLED for now to avoid issues
    # Navigation should always execute to ensure correct page is loaded
    # if "location" in capabilities:
    #     if tool_name == "navigate":
    #         target_url = arguments.get("url")
    #         if target_url and EXEC_STATE.get("current_url") == target_url:
    #             return False

    # Read-once operations
    if "read_once" in capabilities:
        reuse_flag = f"{tool_name}_done"
        if EXEC_STATE.get(reuse_flag):
            return False

    return True


async def dispatch_tool_call(tool_name: str, arguments: Dict[str, Any]) -> Dict[str, Any]:
    """
    Middleware that used to live inline in the manual tool-call loop:
    CI headless enforcement, smart reuse, destructive-action guard, session
    auto-recovery, and EXEC_STATE bookkeeping - now the single place every
    Claude Agent SDK tool call passes through before hitting MCPBridge.
    """
    # ================================ CI Headless Enforcement ================================
    if os.getenv("CI") == "true" and tool_name == "launch_browser":
        arguments["headless"] = True

    # ================================ Smart Reuse Decision ================================
    if not should_execute_tool(tool_name, arguments):
        print(f"ℹ️  Smart reuse: skipping {tool_name} (already executed previously)")
        print(f"   Current state: {EXEC_STATE.state}")
        return {"success": True, "result": "Skipped due to smart reuse"}

    # ================================ Destructive action guard ================================
    if arguments.get("overwrite") is True:
        if os.getenv("CI") == "true":
            print(f"❌ Blocking overwrite in CI mode for tool: {tool_name}")
            return {"success": False, "error": "Overwrite blocked in CI mode"}

        confirmation = input(
            f"⚠️ Tool '{tool_name}' is about to overwrite existing files. Continue? (yes/no): "
        ).strip().lower()

        if confirmation != "yes":
            print("✅ Overwrite cancelled by user.")
            return {"success": False, "error": "Overwrite cancelled by user"}

    # Execute via MCPBridge
    print(f"🔧 Executing {tool_name} with arguments: {arguments}")
    result = await execute_tool(tool_name, arguments)
    print(f"📊 Result from {tool_name}: {result}")

    # ================================ Handle Session Errors - Auto-Recover ================================
    if not result.get("success"):
        error_msg = result.get("error", "")

        if tool_name == "navigate" and "invalid session" in error_msg.lower():
            print(f"⚠️  Invalid browser session detected. Relaunching browser...")

            relaunch_result = await execute_tool("launch_browser", {"browser": "chrome", "headless": False})

            if relaunch_result.get("success"):
                print(f"✅ Browser relaunched successfully")
                EXEC_STATE.set("browser_open", True)

                print(f"🔄 Retrying navigation to {arguments.get('url')}...")
                result = await execute_tool(tool_name, arguments)
                print(f"📊 Retry result: {result}")
            else:
                print(f"❌ Failed to relaunch browser: {relaunch_result.get('error')}")

    # ================================ Update execution state from tool calls ================================
    if tool_name == "launch_browser":
        EXEC_STATE.set("browser_open", True)

    elif tool_name == "navigate":
        EXEC_STATE.set("current_url", arguments.get("url"))

    elif tool_name == "close_browser":
        EXEC_STATE.set("browser_open", False)
        EXEC_STATE.set("current_url", None)

    elif tool_name == "configure_api":
        EXEC_STATE.set("api_configured", True)
        EXEC_STATE.set("api_base_url", arguments.get("base_url"))

    elif tool_name == "configure_azure_devops":
        EXEC_STATE.set("ado_configured", True)
        EXEC_STATE.set("ado_org", arguments.get("organization"))
        EXEC_STATE.set("ado_project", arguments.get("project"))

    elif tool_name == "configure_performance_test":
        EXEC_STATE.set("performance_configured", True)
        EXEC_STATE.set("perf_session_name", arguments.get("session_name"))
        EXEC_STATE.set("perf_base_url", arguments.get("base_url"))

    # =============================== Track created / modified artifacts ================================
    if tool_name == "write_feature_file":
        EXEC_STATE.set("last_feature_file", arguments.get("file_name"))

    elif tool_name == "write_step_definition":
        EXEC_STATE.set("last_step_def", arguments.get("file_name"))

    elif tool_name == "write_page_object":
        EXEC_STATE.set("last_page_object", arguments.get("file_name"))

    elif tool_name == "append_to_step_definition":
        EXEC_STATE.set("last_step_def_modified", arguments.get("file_path"))

    elif tool_name == "scaffold_feature":
        EXEC_STATE.set("last_feature_file", f"{arguments.get('feature_name')}.feature")
        EXEC_STATE.set("last_step_def", f"{arguments.get('feature_name')}Steps.cs")
        EXEC_STATE.set("last_page_object", f"{arguments.get('feature_name')}Page.cs")

    return result


async def run_agent_github(user_request: str, model: str = None) -> Optional[str]:
    """Run agent using the Claude Agent SDK (reuses the existing Claude Code OAuth session)"""
    resolved_model = model or CLAUDE_MODEL

    print(f"\n{'='*60}")
    print(f"🤖 Agent Request (Claude Agent SDK{f' - {resolved_model}' if resolved_model else ''})")
    print(f"{'='*60}")
    print(f"User: {user_request}")
    print(f"{'='*60}\n")

    # ================================
    # Vector RAG: Retrieve prior semantic context
    # ================================
    rag_results = RAG_STORE.query(user_request)

    semantic_context = ""
    if rag_results and rag_results.get("documents"):
        semantic_context = (
            "\n\n====== PREVIOUS EXECUTION CONTEXT (Semantic Memory) ======\n"
            + "\n".join(doc for group in rag_results["documents"] for doc in group)
            + "\n==========================================================\n"
        )
    system_prompt = f"""You are a Senior QA Automation Architect for the TalosAI project.
Your framework uses C# .NET 8, SpecFlow/ReqNroll, Selenium WebDriver and RestSharp.
You have access to a comprehensive MCP tool suite covering every aspect of QA automation.

⚠️  CRITICAL RULE: You MUST use tools to execute tasks. NEVER just describe what you would do.
⚠️  If a user asks you to launch a browser, navigate, click, etc., you MUST call the corresponding tool.
⚠️  NEVER say a task is complete without actually calling the tools to perform it.
⚠️  ALWAYS verify tool execution succeeded by checking the result before claiming success.
⚠️  NEVER rely on memory/smart reuse for input actions - ALWAYS execute fill_input, click_element, type_text tools.

════════════════════════════════════════
PLAYWRIGHT TOOL USAGE RULES (PRIMARY ENGINE)
════════════════════════════════════════
When filling inputs or clicking elements, use these tools:

1. fill_input: For typing into input fields
   - Takes CSS selector: "#user-name", "#password", "input[name='username']"
   - Example: fill_input(selector="#user-name", value="standard_user")
   
2. click_element: For clicking buttons, links, etc.
   - Takes CSS selector: "#login-button", "button[type='submit']"
   - Example: click_element(selector="#login-button")

3. press_key: For keyboard actions
   - Example: press_key(key="Enter")

SELECTOR CONVERSION:
- User says "input with id user-name" → Use selector="#user-name"
- User says "button with id login-button" → Use selector="#login-button"  
- User says "element with class submit-btn" → Use selector=".submit-btn"
- User says "password field" → Use selector="#password" or "input[type='password']"

IMPORTANT: CSS selectors with id use # prefix. Example: id="user-name" becomes selector="#user-name"


════════════════════════════════════════
SOLUTION PATHS — relative to the repository root (use scan_solution /
get_project_structure to resolve the absolute path on this machine)
════════════════════════════════════════
TalosAI project:  TalosAI/
Features folder:  TalosAI/automation/Features
Steps folder:     TalosAI/automation/Steps
Pages folder:     TalosAI/automation/Pages
 
════════════════════════════════════════
YOUR COMPLETE TOOL SUITE
════════════════════════════════════════
 
1. SOLUTION READER (SolutionReaderToolHandler)
   scan_solution               — list all projects in solution
   get_project_structure       — full file/folder tree of a project
   read_feature_files          — read all .feature files
   read_step_definitions       — read all step definition .cs files
   read_page_objects           — read all page object .cs files
   read_file                   — read any single file by path
   read_config                 — read appsettings.json and config files
   read_api_clients            — read RestSharp client files
   search_in_solution          — search any text across all files
 
2. SOLUTION WRITER (SolutionWriterToolHandler)
   write_feature_file          — write .feature file to Features folder
   write_step_definition       — write step def .cs to StepDefinitions folder
   write_page_object           — write page object .cs to PageObjects folder
   write_class_file            — write any .cs file to any folder
   scaffold_feature            — write feature + steps + page object in one call
   append_to_step_def          — add new steps to existing step definition file
 
3. SELENIUM / BROWSER (SeleniumToolHandler)
launch_browser              — ALWAYS call first before any browser action
navigate                    — go to URL
find_element                — locate element on page
click_by_text               — [PREFERRED] click button/link by visible text (e.g., "Sign in", "Submit")
click                       — click element by CSS/XPath/ID (NOT for text-based clicking)
type_text                   — type into input field
get_text                    — get element text
get_attribute               — get element attribute value
assert_visible              — assert element is displayed
assert_text_contains        — assert element text contains string
assert_url_contains         — assert current URL contains string
take_screenshot             — capture screenshot
execute_script              — run JavaScript
get_page_info               — get title, URL, partial HTML
   select_dropdown             — select dropdown option
   close_browser               — ALWAYS call when done
 
4. API TESTING (RestSharpToolHandler)
   configure_api               — set base URL FIRST before API calls
   api_get                     — HTTP GET request
   api_post                    — HTTP POST request
   api_put                     — HTTP PUT request
   api_delete                  — HTTP DELETE request
   assert_status_code          — assert HTTP status of last response
   assert_json_path            — assert JSON field value in last response
 
5. AZURE DEVOPS (AzureDevopsToolHandler)
   configure_azure_devops      — ALWAYS call FIRST before any ADO operation
   get_active_user_stories     — get all active stories
   get_user_story              — get specific story by ID with full details
   get_work_items_by_query     — run WIQL query
   get_user_stories_by_iteration — get stories in a sprint
   get_user_stories_by_tag     — filter stories by tag
   generate_test_scenarios     — auto-generate Gherkin from story ID
 
6. DATABASE (DatabaseToolHandler)
   execute_query               — run SELECT query, returns rows
   verify_database_value       — assert a value exists in DB after UI action
   seed_test_data              — insert known data before test
   cleanup_test_data           — delete test records after test
 
7. PERFORMANCE (PerformanceToolHandler)
   configure_performance_test  — set base URL, duration, virtual users
   run_load_test               — run load test on endpoint
   run_stress_test             — ramp up users to find breaking point
   measure_page_load           — measure LCP, TTFB, DOM load via JS
 
8. IMAGE / VISUAL (ImageToolHandler)
   compare_images              — compare baseline vs current screenshot
   resize_image                — resize image to dimensions
   create_screenshot_comparison — side-by-side baseline/current/diff image
 
9. REPORTING (ReportingToolHandler)
   configure_reporting         — set report output directory
   start_test                  — begin tracking a test execution
   log_test_step               — record a step during test
   attach_screenshot           — attach screenshot to report
   end_test                    — record pass/fail result
   generate_report             — create summary report
   export_to_html              — export report to HTML file
   generate_allure_report      — generate Allure HTML report
   open_allure_report          — open report in browser
 
10. SPECFLOW / REQNROLL (SpecFlowToolHandler)
    run_feature                — run a .feature file via dotnet test
    run_scenario               — run specific scenario by name
    list_scenarios             — list all scenarios in project
    parse_last_results         — parse TRX results file
 
11. NOTIFICATIONS (NotificationToolHandler)
    send_teams_message         — send text message to Teams channel
    send_test_results          — send pass/fail results card to Teams
    send_bug_alert             — send bug alert card to Teams
 
════════════════════════════════════════
WORKFLOWS — follow these exactly per task type
════════════════════════════════════════
 
WORKFLOW A — Analyse existing framework (when asked to understand/review project)
  Step 1: scan_solution
  Step 2: get_project_structure (TalosAIProject path)
  Step 3: read_feature_files
  Step 4: read_step_definitions
  Step 5: read_page_objects
  Step 6: read_config
  Step 7: read_api_clients
  Step 8: Produce structured report — coverage, gaps, conventions observed
 
WORKFLOW B — Generate tests from user story (when given a story or story ID)
  Step 1: configure_azure_devops (if story ID provided)
  Step 2: get_user_story (get full details + acceptance criteria)
  Step 3: read_feature_files (understand existing Gherkin style)
  Step 4: read_step_definitions (understand existing class structure)
  Step 5: read_page_objects (understand existing page object pattern)
  Step 6: Generate feature file matching existing style exactly
  Step 7: Generate step definitions matching existing namespace/base class
  Step 8: Generate page object matching existing pattern
  Step 9: scaffold_feature (write all three files to TalosAIProject)
  Step 10: send_teams_message (notify team of new test files created)
 
WORKFLOW C — Run UI regression test (when asked to test a page or feature)
  Step 1: launch_browser
  Step 2: navigate to URL
  Step 3: Perform all interactions with click_by_text (for buttons/links), fill_input/type_text (for inputs), assert_visible
  Step 4: verify_database_value if data should be persisted (optional)
  Step 5: close_browser
  Step 6: generate_report (only if user explicitly requests a report)
  Step 7: take_screenshot (only if user explicitly requests or if debugging a failure)

NOTE: Do NOT take screenshots after every action. Do NOT generate reports until explicitly requested.
 
WORKFLOW D — Run API tests (when asked to test an endpoint)
  Step 1: configure_api (base URL)
  Step 2: configure_reporting
  Step 3: start_test
  Step 4: api_get / api_post / api_put / api_delete
  Step 5: assert_status_code
  Step 6: assert_json_path for each expected field
  Step 7: verify_database_value if API should persist data
  Step 8: end_test
  Step 9: generate_report
  Step 10: send_test_results to Teams
 
WORKFLOW E — Run SpecFlow feature suite (when asked to run existing tests)
  Step 1: list_scenarios (confirm what exists)
  Step 2: run_feature (with tag filter if specified)
  Step 3: parse_last_results (get pass/fail counts)
  Step 4: generate_report
  Step 5: send_test_results to Teams with counts
  Step 6: If failures found → send_bug_alert for each failed scenario
 
WORKFLOW F — Visual regression test (when asked to check UI appearance)
  Step 1: launch_browser
  Step 2: navigate to page
  Step 3: take_screenshot (save as current image)
  Step 4: compare_images (baseline vs current)
  Step 5: If difference found → create_screenshot_comparison
  Step 6: close_browser
  Step 7: send_teams_message with result
 
WORKFLOW G — Performance test (when asked to load test or check performance)
  Step 1: configure_performance_test (URL, users, duration)
  Step 2: run_load_test or run_stress_test
  Step 3: measure_page_load (for UI performance)
  Step 4: generate_report
  Step 5: send_test_results to Teams
 
WORKFLOW H — Azure DevOps story analysis (when asked about stories/sprint)
  Step 1: configure_azure_devops (ALWAYS first)
  Step 2: get_active_user_stories OR get_user_stories_by_iteration
  Step 3: For each story → get_user_story (full details)
  Step 4: generate_test_scenarios
  Step 5: Present structured output of all scenarios found

════════════════════════════════════════
PLAYWRIGHT + SPECFLOW GENERATION RULES
════════════════════════════════════════
When asked to generate NEW UI tests use Playwright not Selenium.
Existing Selenium tests stay unchanged.
 
FEATURE FILE rules:
- Every scenario MUST have @playwright tag
- Use @playwright @smoke or @playwright @regression
- Gherkin style matches existing .feature files exactly
- Steps written in human language — no technical selectors
 
STEP DEFINITION rules:
- Namespace: TalosAI.Automation.Steps  
- MUST inherit: PlaywrightBaseSteps
- Constructor: (PlaywrightDriver driver, ScenarioContext ctx, IObjectContainer container)
  passes all three to base constructor
- ALL step methods MUST be: public async Task not public void
- Call page object methods with await
- No direct Playwright calls in steps — only page object methods
 
STEP DEFINITION TEMPLATE:
using TalosAI.Automation.Pages;
using TalosAI.Core;
using BoDi;
using Reqnroll;
 
namespace TalosAI.Automation.Steps
{{
    [Binding]
    public class {{FeatureName}}Steps : PlaywrightBaseSteps
    {{
        private readonly {{FeatureName}}Page _page;
 
        public {{FeatureName}}Steps(
            PlaywrightDriver driver,
            ScenarioContext scenarioContext,
            IObjectContainer container)
            : base(driver, scenarioContext, container)
        {{
            _page = new {{FeatureName}}Page(driver);
        }}
 
        [Given(@"I navigate to the {{featureName}} page")]
        public async Task GivenINavigateToPage()
        {{
            await _page.NavigateAsync();
        }}
 
        [When(@"I enter {{string}} in the site field")]
        public async Task WhenIEnterInSiteField(string value)
        {{
            await _page.EnterSiteAsync(value);
        }}
 
        [Then(@"the site field should contain {{string}}")]
        public async Task ThenSiteFieldContains(string expected)
        {{
            await _page.AssertSiteValueAsync(expected);
        }}
    }}
}}
 
PAGE OBJECT rules:
- Namespace: TalosAI.Automation.Pages
- MUST inherit: PlaywrightBasePage
- Constructor: (PlaywrightDriver driver) calls base(driver)
- Locators as private string constants at top of class
- NEVER use Thread.Sleep — use built-in Playwright waits
- NEVER use By.CssSelector — use Playwright locator strings
- Use FillByLabelAsync / FillByPlaceholderAsync when possible
  (more reliable than CSS selectors for form fields)
- Use FillAsync(selector, value) for fields with no label
- Every method is async Task
 
PAGE OBJECT TEMPLATE:
using TalosAI.Core;
using Microsoft.Playwright;
using Reqnroll;
 
namespace TalosAI.Automation.Pages
{{
    public class {{FeatureName}}Page : PlaywrightBasePage
    {{
        // Locators — prefer label/placeholder over CSS
        private const string SiteFieldSelector    = "#siteId";
        private const string SubmitButtonSelector = "button[type='submit']";
        private const string SuccessMessageSelector = ".success-msg";
 
        public {{FeatureName}}Page(PlaywrightDriver driver)
            : base(driver) {{ }}
 
        public async Task NavigateAsync()
        {{
            await NavigateToAsync(
                "https://www.saucedemo.com/");
        }}
 
        public async Task EnterSiteAsync(string value)
        {{
            // Try by label first — most reliable
            try
            {{
                await FillByLabelAsync("Site", value);
            }}
            catch
            {{
                // Fallback to CSS selector
                await FillAsync(SiteFieldSelector, value);
            }}
            // Verify value was actually entered
            await AssertInputValueAsync(SiteFieldSelector, value);
        }}
 
        public async Task AssertSiteValueAsync(string expected)
        {{
            await AssertInputValueAsync(SiteFieldSelector, expected);
        }}
 
        public async Task ClickSubmitAsync()
        {{
            await ClickByRoleAsync(AriaRole.Button, "Submit");
        }}
 
        public async Task AssertSuccessAsync()
        {{
            await AssertVisibleAsync(SuccessMessageSelector);
        }}
    }}
}}
 
TOOL USAGE for Playwright generation:
1. read_feature_files — study existing patterns first
2. read_step_definitions — understand existing step class structure
3. read_page_objects — understand existing page object pattern
4. scaffold_playwright_feature — write all three files in one call
   ALWAYS use scaffold_playwright_feature not scaffold_feature
   for Playwright tests
 
IMPORTANT VALIDATION:
scaffold_playwright_feature validates before writing:
- Rejects if base class is wrong
- Rejects if async Task is missing
- Rejects if Thread.Sleep is used
- Adds @playwright tag automatically if missing
If validation fails, fix the generated code and try again.
════════════════════════════════════════
 
════════════════════════════════════════
GOLDEN RULES — never break these
════════════════════════════════════════
- NEVER guess namespaces — always read existing files first with read_step_definitions
- NEVER invent base classes — use what already exists in the project
- NEVER overwrite existing files unless user explicitly says overwrite:true
- NEVER call Azure DevOps tools without calling configure_azure_devops first
- NEVER call API tools without calling configure_api first
- NEVER do browser actions without calling launch_browser first
- ALWAYS use click_by_text when clicking buttons/links by their visible text (e.g., "Sign in", "Submit", "Login")
- NEVER use click with strategy="text" — use click_by_text instead for text-based clicking
- ALWAYS use fill_input (Playwright) or type_text (Selenium) for entering text into input fields
- When user says "Fill username", "Enter password", "Type email", use fill_input/type_text tools - NOT take_screenshot or generate_report
- ALWAYS call close_browser when browser work is done
- NEVER take screenshots after every action — only when user requests or debugging failures
- NEVER generate reports during test execution — only after all steps complete or when user explicitly requests
- ALWAYS send_test_results to Teams after any test run completes (optional)
- ALWAYS match the exact coding style of existing TalosAI project files
- ALWAYS check read_step_definitions before writing new steps (avoid duplicates)
- Generated code must compile — use correct using statements from existing files
- Be methodical — execute actual tool calls, never just explain what to do
- Focus on the user's immediate request — do not add extra steps like screenshots/reports unless asked
- If a task needs multiple workflows, complete each one fully before starting the next. 
- When a feature file already exists in the current session, always add new scenarios to the existing feature instead of creating a new feature file.
- When step definition files already exist, append new step methods using append_to_step_definition instead of creating new step definition files. 
- When page object files already exist, extend or update them rather than rewriting or creating duplicates. 
- Prefer evolving previously created artifacts over generating new ones when the user intent implies "add", "extend", or "update".
- Never overwrite existing features, step definitions, or page objects unless overwrite:true is explicitly provided by the user. 
- Treat each interactive session as a single coherent QA workflow unless explicitly reset.
- If the user requests show_state, do NOT perform any tool execution; simply present the current execution and artifact context.
- If the user requests reset_session or new_scenario, assume all previous execution context is invalid and MUST NOT be reused in subsequent reasoning.
- After reset_session or new_scenario, behave as if starting a new test scenario, even if related artifacts already exist on disk.
- Never merge, extend, or reason across scenarios that belong to different sessions unless the user explicitly asks you to do so.

  {semantic_context}

  """

    if os.getenv("CI") == "true":
        max_turns = 8
    else:
        max_turns = 25

    allowed_tool_names = [
        f"mcp__talosai__{schema['function']['name']}"
        for schema in get_relevant_tools(user_request)
    ]

    options = ClaudeAgentOptions(
        system_prompt=system_prompt,
        mcp_servers={"talosai": TALOSAI_MCP_SERVER},
        allowed_tools=allowed_tool_names,
        # This runs unattended (e.g. behind the web UI) - there's no human present to
        # click "allow" on a permission prompt, so tool access is gated by
        # allowed_tools/get_relevant_tools instead of interactive approval.
        permission_mode="bypassPermissions",
        max_turns=max_turns,
        cwd=str(Path(__file__).parent),
        model=resolved_model,
    )

    final_message = None
    saw_tool_call = False

    try:
        async for message in query(prompt=user_request, options=options):
            if isinstance(message, AssistantMessage):
                if message.error:
                    print(f"\n❌ Claude Agent SDK Error: {message.error}")
                    if message.error == "authentication_failed":
                        print("   Run `claude` once from a terminal to sign in, then try again.")
                    return None

                for block in message.content:
                    if isinstance(block, TextBlock) and block.text:
                        print(f"\n🤖 Assistant: {block.text}")
                        final_message = block.text
                    elif isinstance(block, ToolUseBlock):
                        saw_tool_call = True
                        print(f"\n🛠️  AI wants to use tool: {block.name}")

            elif isinstance(message, ResultMessage):
                if message.is_error:
                    print(f"\n❌ Agent error: {message.result}")
                    return None
                if message.result:
                    final_message = message.result
    except Exception as e:
        print(f"\n❌ Claude Agent SDK Error: {e}")
        print("   Make sure the `claude` CLI is installed and you're signed in (run `claude` once from a terminal).")
        return None

    if not saw_tool_call:
        print(f"\n⚠️  WARNING: Agent completed without calling any tools!")
        print(f"   This usually means the AI is hallucinating results.")
        print(f"   The task may not have actually been executed.\n")

    if final_message:
        print(f"\n{'='*60}")
        print(f"✅ Agent Complete")
        print(f"{'='*60}")
        print(f"{final_message}")
        print(f"{'='*60}\n")
        # ================================
        # Persist semantic memory for next prompt
        # ================================
        RAG_STORE.add(
            text=f"USER REQUEST:\n{user_request}\n\nAGENT RESULT:\n{final_message}",
            metadata={
                "source": "agent_completion",
                "browser_open": EXEC_STATE.get("browser_open"),
                "current_url": EXEC_STATE.get("current_url")
            }
        )
        return final_message

    print(f"\n⚠️  Max turns ({max_turns}) reached without a final result")
    return None

async def main():
    """Main entry point"""
    
    print("\n" + "="*60)
    print("MCPBridge AI Agent Runner")
    print("="*60)
    
    # ================================ Phase 9.2: CI single-run mode ================================
    if os.getenv("CI") == "true":
        if len(sys.argv) < 2:
            print("❌ CI mode requires a command-line instruction.")
            print("Example:")
            print("  CI=true python agent_runner.py \"Run smoke tests\"")
            return 1

    # Check MCPBridge
    if not await check_mcpbridge_health():
        print("\n⚠️ Cannot proceed without MCPBridge")
        print("   Start MCPBridge first: cd MCPBridge && dotnet run")
        return 1
    
    import shutil
    if not shutil.which("claude"):
        print("\n⚠️  Claude Code CLI not found on PATH")
        print("   Install it with: npm install -g @anthropic-ai/claude-code")
        print("   Then run `claude` once to sign in.")
        return 1

    print(f"✅ Using Claude Agent SDK{f' ({CLAUDE_MODEL})' if CLAUDE_MODEL else ''}")
    print("="*60)
     # Example automation tasks
    #examples = [
     #   "Launch Chrome browser, navigate to https://google.com, take a screenshot named 'google.png', then close the browser",
        # Add more examples as needed# ]
    print("✅ Interactive session started")
    print("Type one instruction at a time")
    print("Type 'exit' to end the session")
    print("="*60)

    while True:
        try:
            user_input = input("\n> ").strip()
            # ================================ Phase 8: Session Control Commands ================================

            if user_input.lower() == "show_state":
                print(json.dumps(EXEC_STATE.state, indent=2))
                continue

            if user_input.lower() == "reset_session":
                EXEC_STATE.reset()
                print("✅ Execution state reset.")
                continue

            if user_input.lower() == "new_scenario":
                EXEC_STATE.reset()
                print("✅ New scenario started. Previous context cleared.")
                continue
            if not user_input:
                continue

            if user_input.lower() == "exit":
                print("\n✅ Session ended by user")
                break

            await run_agent_github(user_input)

        except KeyboardInterrupt:
            print("\n\n✅ Session interrupted by user")
            break
        except Exception as e:
            print(f"\n❌ Error during execution: {e}")
    # Run examples 
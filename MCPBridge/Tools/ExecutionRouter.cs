using McpBridge.Models;
using System.Reflection;
using System.Text.Json;

namespace McpBridge.Tools;

/// <summary>
/// ExecutionRouter sits between the MCP tool call and the actual handler.
/// Strategy:
///   1. Always attempt Playwright first (UI or API).
///   2. On failure, detect tool category (UI vs API) and route to correct fallback.
///      UI  → SeleniumToolHandler
///      API → RestSharpToolHandler
/// All tool names in this router mirror PlaywrightToolHandler exactly so
/// agent_runner.py does not need to know about fallbacks.
/// </summary>

public class ExecutionRouter
{
    private readonly PlaywrightToolHandler  _playwright;
    private readonly SeleniumToolHandler    _selenium;
    private readonly RestSharpToolHandler   _restSharp;

    // Tool names that belong to UI domain → Selenium fallback
    private static readonly HashSet<string> UiToolNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "launch_browser", "close_browser", "navigate_to", "navigate", "click_element",
        "fill_input", "select_option", "press_key", "hover_element",
        "take_screenshot", "wait_for_element", "scroll_to_element",
        "assert_text_visible", "assert_element_visible", "assert_element_hidden",
        "assert_page_title", "assert_url_contains", "assert_input_value",
        "assert_element_count", "click", "type_text", "find_element",
        // ── Extended UI Actions (Comprehensive Coverage) ──
        "double_click_element", "right_click_element", "check_element", "uncheck_element",
        "set_checked_state", "type_text", "get_element_text", "get_input_value",
        "get_element_attribute", "is_element_visible", "is_element_enabled",
        "is_element_checked", "select_option_by_label", "drag_to_element",
        "screenshot_element", "wait_for_element_hidden"
    };

    // Tool names that belong to API domain → RestSharp fallback
    private static readonly HashSet<string> ApiToolNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "api_request", "assert_response_status", "assert_response_body_contains",
        "assert_json_path", "assert_response_header", "api_get", "api_post", 
        "api_put", "api_delete", "configure_api"
    };

    public ExecutionRouter(
        PlaywrightToolHandler playwright,
        SeleniumToolHandler   selenium,
        RestSharpToolHandler  restSharp)
    {
        _playwright = playwright;
        _selenium   = selenium;
        _restSharp  = restSharp;
    }

    /// <summary>
    /// Execute a tool call by name with a JSON arguments string.
    /// Tries Playwright first; falls back based on tool category.
    /// Now includes self-healing selector analysis on failures.
    /// </summary>
    public async Task<ToolResponse> ExecuteAsync(
        string toolName,
        Dictionary<string, object> args)
    {
        // ── 1. Try Playwright first ─────────────────────────────
        try
        {
            var playwrightResult = await InvokePlaywrightAsync(toolName, args);
            return ToolResponse.Ok(new
            {
                engine = "playwright",
                result = playwrightResult
            });
        }
        catch (Exception playwrightEx)
        {
            var reason = playwrightEx.Message;
            Console.WriteLine($"[ExecutionRouter] Playwright failed for '{toolName}': {reason}");

            // ── Assertion tools verify a specific expected condition.
            // Self-healing (retrying with a different selector) or falling back to a
            // different engine would silently change what is being checked — a genuinely
            // failing assertion must never be reinterpreted into a reported pass. Report
            // the failure as-is; no healing, no engine fallback.
            if (toolName.StartsWith("assert_", StringComparison.OrdinalIgnoreCase))
            {
                return ToolResponse.Fail($"Assertion failed for '{toolName}': {reason}");
            }

            // ── Self-Healing: Analyze selector failures (action tools only) ──
            bool isSelectorFailure = reason.Contains("selector", StringComparison.OrdinalIgnoreCase) ||
                                      reason.Contains("timeout", StringComparison.OrdinalIgnoreCase) ||
                                      reason.Contains("not found", StringComparison.OrdinalIgnoreCase);
            
            if (isSelectorFailure && args.ContainsKey("selector"))
            {
                var originalSelector = args["selector"]?.ToString() ?? "";
                var fallbackSelectors = SelectorHealingService.GenerateFallbackSelectors(originalSelector);
                
                Console.WriteLine($"[SELF-HEAL] Generated {fallbackSelectors.Count - 1} fallback selectors for '{originalSelector}'");
                
                // Try fallback selectors with Playwright before switching engines
                for (int i = 1; i < fallbackSelectors.Count; i++)
                {
                    try
                    {
                        args["selector"] = fallbackSelectors[i];
                        var healedResult = await InvokePlaywrightAsync(toolName, args);
                        
                        SelectorHealingService.LogSelfHealingAttempt(
                            toolName, originalSelector, fallbackSelectors[i], success: true);
                        
                        return ToolResponse.Ok(new
                        {
                            engine = "playwright-self-healed",
                            result = healedResult,
                            healing_info = new
                            {
                                original_selector = originalSelector,
                                healed_selector = fallbackSelectors[i],
                                message = "Self-healing applied. Consider updating page object."
                            }
                        });
                    }
                    catch (Exception healEx)
                    {
                        SelectorHealingService.LogSelfHealingAttempt(
                            toolName, originalSelector, fallbackSelectors[i], success: false);
                        Console.WriteLine($"[SELF-HEAL] Fallback #{i} failed: {healEx.Message}");
                    }
                }
                
                // Restore original selector for engine fallback
                args["selector"] = originalSelector;
            }

            // ── 2. UI fallback → Selenium ────────────────────────
            if (UiToolNames.Contains(toolName))
            {
                try
                {
                    Console.WriteLine($"[ExecutionRouter] Attempting Selenium fallback for '{toolName}'");
                    var seleniumResult = InvokeSelenium(toolName, args);

                    return ToolResponse.Ok(new
                    {
                        engine = "selenium-fallback",
                        result = seleniumResult.Result,
                        warning = $"Playwright failed: {reason}. Used Selenium fallback."
                    });
                }
                catch (Exception seleniumEx)
                {
                    return ToolResponse.Fail(
                        $"Playwright and Selenium both failed for '{toolName}'.\n" +
                        $"Playwright: {playwrightEx.Message}\n" +
                        $"Selenium: {seleniumEx.Message}");
                }
            }

            // ── 3. API fallback → RestSharp ──────────────────────
            if (ApiToolNames.Contains(toolName))
            {
                try
                {
                    Console.WriteLine($"[ExecutionRouter] Attempting RestSharp fallback for '{toolName}'");
                    var restResult = InvokeRestSharp(toolName, args);

                    return ToolResponse.Ok(new
                    {
                        engine = "restsharp-fallback",
                        result = restResult.Result,
                        warning = $"Playwright failed: {reason}. Used RestSharp fallback."
                    });
                }
                catch (Exception restEx)
                {
                    return ToolResponse.Fail(
                        $"Playwright and RestSharp both failed for '{toolName}'.\n" +
                        $"Playwright: {playwrightEx.Message}\n" +
                        $"RestSharp: {restEx.Message}");
                }
            }

            // Not a UI or API tool, just return Playwright error
            return ToolResponse.Fail($"Tool '{toolName}' failed: {playwrightEx.Message}");
        }
    }
    // =========================================================
    // PRIVATE HELPERS
    // =========================================================

    /// <summary>
    /// Invoke a method on PlaywrightToolHandler using reflection.
    /// </summary>
    private async Task<string> InvokePlaywrightAsync(
        string toolName,
        Dictionary<string, object> args)
    {
        var method = _playwright.GetType()
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .FirstOrDefault(m => string.Equals(m.Name, toolName, StringComparison.OrdinalIgnoreCase))
            ?? throw new MissingMethodException(
                $"Playwright tool '{toolName}' not found.");

        var parameters = method.GetParameters();
        var invokeArgs = new object?[parameters.Length];

        for (int i = 0; i < parameters.Length; i++)
        {
            var param = parameters[i];
            if (args.TryGetValue(param.Name!, out var value))
            {
                invokeArgs[i] = ConvertParameter(value, param.ParameterType);
            }
            else if (param.HasDefaultValue)
            {
                invokeArgs[i] = param.DefaultValue;
            }
            else
            {
                throw new ArgumentException(
                    $"Required parameter '{param.Name}' missing for tool '{toolName}'.");
            }
        }

        var result = method.Invoke(_playwright, invokeArgs);

        return result switch
        {
            Task<string> t => await t,
            Task t => await t.ContinueWith(_ => "ok"),
            string s => s,
            _ => result?.ToString() ?? "ok"
        };
    }

    /// <summary>
    /// Invoke a method on SeleniumToolHandler using reflection.
    /// Maps Playwright tool names to Selenium method names.
    /// </summary>
    private ToolResponse InvokeSelenium(
        string toolName,
        Dictionary<string, object> args)
    {
        // Map Playwright tool names to Selenium method names
        var seleniumMethodName = MapToSeleniumMethod(toolName);
        
        // Transform parameters for Selenium compatibility
        var seleniumArgs = TransformArgsForSelenium(toolName, args);

        var method = _selenium.GetType()
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .FirstOrDefault(m =>
                string.Equals(m.Name, seleniumMethodName,
                    StringComparison.OrdinalIgnoreCase))
            ?? throw new MissingMethodException(
                $"Selenium method '{seleniumMethodName}' (mapped from '{toolName}') not found.");

        var result = method.Invoke(_selenium, new object[] { seleniumArgs });

        return result as ToolResponse
            ?? throw new InvalidOperationException(
                $"Selenium method '{seleniumMethodName}' did not return ToolResponse.");
    }
    
    /// <summary>
    /// Transform Playwright arguments to Selenium-compatible format.
    /// Applies to every UI tool generically — CSS selector syntax already
    /// supports "#id" and ".class", so no per-tool special-casing is needed
    /// beyond detecting XPath selectors.
    /// </summary>
    private Dictionary<string, object> TransformArgsForSelenium(
        string toolName,
        Dictionary<string, object> args)
    {
        var transformed = new Dictionary<string, object>(args);

        // Capture Playwright's "value" before the locator remap below overwrites it —
        // for tools like fill_input/select_option, "value" is the text/option to set,
        // not the locator.
        args.TryGetValue("value", out var originalValue);

        if (args.TryGetValue("selector", out var selectorObj) && selectorObj is not null)
        {
            var selector = selectorObj.ToString()!;
            transformed.Remove("selector");
            transformed["strategy"] = selector.StartsWith("/") || selector.StartsWith("(") ? "xpath" : "css";
            transformed["value"] = selector;
        }

        if ((toolName == "fill_input" || toolName == "type_text") && originalValue != null)
            transformed["text"] = originalValue;

        if (toolName == "select_option" && originalValue != null)
            transformed["select_value"] = originalValue;

        return transformed;
    }
    
    /// <summary>
    /// Map Playwright tool names to Selenium tool handler method names.
    /// Assertion tools ("assert_*") are intentionally absent — they bypass
    /// engine fallback entirely (see ExecuteAsync) and never reach this method.
    /// </summary>
    private string MapToSeleniumMethod(string playwrightToolName)
    {
        return playwrightToolName switch
        {
            "launch_browser" => "LaunchBrowser",
            "close_browser" => "CloseBrowser",
            "navigate_to" => "Navigate",
            "navigate" => "Navigate",
            "click_element" => "Click",
            "click" => "Click",
            "fill_input" => "TypeText",
            "type_text" => "TypeText",
            "select_option" => "SelectDropdown",
            "press_key" => "PressKey",
            "hover_element" => "HoverElement",
            "take_screenshot" => "TakeScreenshot",
            "wait_for_element" => "WaitForElement",
            "scroll_to_element" => "ScrollToElement",
            "find_element" => "FindElement",
            _ => playwrightToolName
        };
    }

    /// <summary>
    /// Invoke a method on RestSharpToolHandler using reflection.
    /// Maps Playwright tool names to RestSharp method names.
    /// </summary>
    private ToolResponse InvokeRestSharp(
        string toolName,
        Dictionary<string, object> args)
    {
        // Map Playwright tool names to RestSharp method names. api_request must
        // preserve the caller's original HTTP verb — forcing everything to POST
        // risks unintended writes for what was originally a GET/PUT/DELETE.
        var restSharpMethodName = toolName == "api_request" && args.TryGetValue("method", out var httpVerb)
            ? (httpVerb?.ToString()?.ToUpperInvariant() switch
              {
                  "GET" => "Get",
                  "PUT" => "Put",
                  "DELETE" => "Delete",
                  _ => "Post" // POST/PATCH — RestSharpToolHandler has no distinct PATCH method
              })
            : MapToRestSharpMethod(toolName);

        var method = _restSharp.GetType()
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .FirstOrDefault(candidate =>
                string.Equals(candidate.Name, restSharpMethodName,
                    StringComparison.OrdinalIgnoreCase))
            ?? throw new MissingMethodException(
                $"RestSharp method '{restSharpMethodName}' (mapped from '{toolName}') not found.");

        var result = method.Invoke(_restSharp, new object[] { args });

        return result as ToolResponse
            ?? throw new InvalidOperationException(
                $"RestSharp method '{restSharpMethodName}' did not return ToolResponse.");
    }
    
    /// <summary>
    /// Map Playwright tool names to RestSharp tool handler method names.
    /// Assertion tools ("assert_*") are intentionally absent — they bypass
    /// engine fallback entirely (see ExecuteAsync) and never reach this method.
    /// </summary>
    private string MapToRestSharpMethod(string playwrightToolName)
    {
        return playwrightToolName switch
        {
            "api_request" => "Post", // verb resolved separately in InvokeRestSharp
            _ => playwrightToolName
        };
    }

    /// <summary>
    /// Convert a parameter value to the target type.
    /// </summary>
    private static object? ConvertParameter(object value, Type targetType)
    {
        if (value == null)
            return null;

        if (value is JsonElement element)
        {
            if (targetType == typeof(string))
                return element.ValueKind == JsonValueKind.Null ? null : element.GetString();
            if (targetType == typeof(int))
                return element.GetInt32();
            if (targetType == typeof(bool))
                return element.GetBoolean();
            if (targetType == typeof(double))
                return element.GetDouble();

            return JsonSerializer.Deserialize(element.GetRawText(), targetType);
        }

        // Direct conversion
        if (targetType.IsAssignableFrom(value.GetType()))
            return value;

        // Try to convert using Convert class
        if (targetType == typeof(int) && value is long longValue)
            return (int)longValue;
        
        if (targetType == typeof(bool) && value is string boolStr)
            return bool.Parse(boolStr);

        try
        {
            return Convert.ChangeType(value, targetType);
        }
        catch
        {
            return JsonSerializer.Deserialize(
                JsonSerializer.Serialize(value),
                targetType);
        }
    }
}

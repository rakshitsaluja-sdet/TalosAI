using Microsoft.Playwright;
using System.ComponentModel;
using System.Text.Json;
using Microsoft.Playwright;

namespace McpBridge.Tools;

/// <summary>
/// Primary execution tool handler using Playwright.
/// Covers UI browser automation AND API testing.
/// Fallback is handled by ExecutionRouter — never call this directly from agent_runner.
/// </summary>

public class PlaywrightToolHandler : IDisposable
{
    private static PlaywrightToolHandler? _instance;
    private static readonly object _lock = new object();
    
    private IPlaywright? _playwright;
    private IBrowser?    _browser;
    private IPage?       _page;
    private IAPIRequestContext? _apiContext;

    // Singleton pattern to maintain browser state across tool calls
    public static PlaywrightToolHandler Instance
    {
        get
        {
            lock (_lock)
            {
                if (_instance == null)
                {
                    _instance = new PlaywrightToolHandler();
                }
                return _instance;
            }
        }
    }

    private PlaywrightToolHandler() { }

    // =========================================================
    // LIFECYCLE
    // =========================================================

    [Description("Launch a Chromium browser session. Call once per scenario.")]
    public async Task<string> launch_browser(
        [Description("Browser type: chromium | firefox | webkit")] string browserType = "chromium",
        [Description("Run headless (true/false)")] bool headless = false)
    {
        _playwright = await Playwright.CreateAsync();

        _browser = browserType.ToLower() switch
        {
            "firefox" => await _playwright.Firefox.LaunchAsync(new() { Headless = headless }),
            "webkit"  => await _playwright.Webkit.LaunchAsync(new()  { Headless = headless }),
            _         => await _playwright.Chromium.LaunchAsync(new() { Headless = headless })
        };

        _page = await _browser.NewPageAsync();
        return $"Browser launched: {browserType}, headless={headless}";
    }

    [Description("Close browser and release all Playwright resources.")]
    public async Task<string> close_browser()
    {
        if (_browser != null) await _browser.CloseAsync();
        _playwright?.Dispose();
        _browser    = null;
        _page       = null;
        _playwright = null;
        _instance = null; // Reset singleton
        return "Browser closed.";
    }

    // =========================================================
    // UI ACTIONS  (maps to SpecFlow When/Then steps)
    // =========================================================
    
    [Description("Navigate the current page to a URL. Alias for navigate_to.")]
    public async Task<string> navigate(
        [Description("Full URL to navigate to")] string url,
        [Description("Wait condition: load | domcontentloaded | networkidle")] string waitUntil = "load")
    {
        return await navigate_to(url, waitUntil);
    }

    [Description("Navigate the current page to a URL. Maps to 'When I navigate to <url>'.")]
    public async Task<string> navigate_to(
        [Description("Full URL to navigate to")] string url,
        [Description("Wait condition: load | domcontentloaded | networkidle")] string waitUntil = "load")
    {
        EnsurePage();
        var waitEnum = waitUntil switch
        {
            "domcontentloaded" => WaitUntilState.DOMContentLoaded,
            "networkidle"      => WaitUntilState.NetworkIdle,
            _                  => WaitUntilState.Load
        };
        await _page!.GotoAsync(url, new() { WaitUntil = waitEnum });
        return $"Navigated to {url}";
    }

    [Description("Click an element identified by a CSS selector or text. Maps to 'When I click <selector>'.")]
    public async Task<string> click_element(
        [Description("CSS selector, XPath, or visible text (prefix text= for text match)")] string selector,
        [Description("Timeout in milliseconds")] int timeoutMs = 30000)
    {
        EnsurePage();
        await _page!.ClickAsync(selector, new() { Timeout = timeoutMs });
        return $"Clicked: {selector}";
    }

    [Description("Fill a form input. Maps to 'When I fill in <selector> with <value>'.")]
    public async Task<string> fill_input(
        [Description("CSS selector targeting the input")] string selector,
        [Description("Value to type")] string value,
        [Description("Clear field before typing")] bool clearFirst = true)
    {
        EnsurePage();
        if (clearFirst) await _page!.FillAsync(selector, "");
        await _page!.FillAsync(selector, value);
        return $"Filled '{selector}' with '{value}'";
    }

    [Description("Select an option in a <select> element. Maps to 'When I select <value> from <selector>'.")]
    public async Task<string> select_option(
        [Description("CSS selector for the <select>")] string selector,
        [Description("Option value or label to select")] string value)
    {
        EnsurePage();
        await _page!.SelectOptionAsync(selector, value);
        return $"Selected '{value}' in '{selector}'";
    }

    [Description("Press a keyboard key. Maps to 'When I press <key>'.")]
    public async Task<string> press_key(
        [Description("Key name e.g. Enter, Tab, Escape, ArrowDown")] string key)
    {
        EnsurePage();
        await _page!.Keyboard.PressAsync(key);
        return $"Pressed key: {key}";
    }

    [Description("Hover over an element. Maps to 'When I hover over <selector>'.")]
    public async Task<string> hover_element(
        [Description("CSS selector")] string selector)
    {
        EnsurePage();
        await _page!.HoverAsync(selector);
        return $"Hovered over: {selector}";
    }

    [Description("Take a screenshot and save it. Maps to 'Then I take a screenshot'.")]
    public async Task<string> take_screenshot(
        [Description("Output file path including .png extension")] string outputPath = "screenshot.png",
        [Description("Capture full page")] bool fullPage = false)
    {
        EnsurePage();
        await _page!.ScreenshotAsync(new() { Path = outputPath, FullPage = fullPage });
        return $"Screenshot saved: {outputPath}";
    }

    [Description("Wait for an element to appear in the DOM. Maps to 'Then I wait for <selector>'.")]
    public async Task<string> wait_for_element(
        [Description("CSS selector")] string selector,
        [Description("State: visible | hidden | attached | detached")] string state = "visible",
        [Description("Timeout ms")] int timeoutMs = 30000)
    {
        EnsurePage();
        var waitState = state switch
        {
            "hidden"   => WaitForSelectorState.Hidden,
            "attached" => WaitForSelectorState.Attached,
            "detached" => WaitForSelectorState.Detached,
            _          => WaitForSelectorState.Visible
        };
        await _page!.WaitForSelectorAsync(selector, new() { State = waitState, Timeout = timeoutMs });
        return $"Element '{selector}' reached state '{state}'";
    }

    [Description("Scroll to an element. Maps to 'When I scroll to <selector>'.")]
    public async Task<string> scroll_to_element(
        [Description("CSS selector")] string selector)
    {
        EnsurePage();
        await _page!.Locator(selector).ScrollIntoViewIfNeededAsync();
        return $"Scrolled to: {selector}";
    }

    // =========================================================
    // EXTENDED UI ACTIONS (Comprehensive Coverage)
    // =========================================================

    [Description("Double-click an element. Maps to 'When I double-click <selector>'.")]
    public async Task<string> double_click_element(
        [Description("CSS selector")] string selector,
        [Description("Timeout in milliseconds")] int timeoutMs = 30000)
    {
        EnsurePage();
        await _page!.DblClickAsync(selector, new() { Timeout = timeoutMs });
        return $"Double-clicked: {selector}";
    }

    [Description("Right-click an element to show context menu. Maps to 'When I right-click <selector>'.")]
    public async Task<string> right_click_element(
        [Description("CSS selector")] string selector,
        [Description("Timeout in milliseconds")] int timeoutMs = 30000)
    {
        EnsurePage();
        await _page!.ClickAsync(selector, new() { Button = MouseButton.Right, Timeout = timeoutMs });
        return $"Right-clicked: {selector}";
    }

    [Description("Check a checkbox or radio button. Maps to 'When I check <selector>'.")]
    public async Task<string> check_element(
        [Description("CSS selector")] string selector)
    {
        EnsurePage();
        await _page!.CheckAsync(selector);
        return $"Checked: {selector}";
    }

    [Description("Uncheck a checkbox. Maps to 'When I uncheck <selector>'.")]
    public async Task<string> uncheck_element(
        [Description("CSS selector")] string selector)
    {
        EnsurePage();
        await _page!.UncheckAsync(selector);
        return $"Unchecked: {selector}";
    }

    [Description("Set checkbox state (checked=true/false). Maps to 'When I set <selector> to <checked>'.")]
    public async Task<string> set_checked_state(
        [Description("CSS selector")] string selector,
        [Description("Checked state: true or false")] bool isChecked)
    {
        EnsurePage();
        await _page!.SetCheckedAsync(selector, isChecked);
        return $"Set '{selector}' checked={isChecked}";
    }

    [Description("Type text with delay (for autocomplete/dynamic inputs). Maps to 'When I type <text> into <selector>'.")]
    public async Task<string> type_text(
        [Description("CSS selector")] string selector,
        [Description("Text to type")] string text,
        [Description("Delay between keystrokes in milliseconds")] int delayMs = 50)
    {
        EnsurePage();
        await _page!.Locator(selector).TypeAsync(text, new() { Delay = delayMs });
        return $"Typed '{text}' into '{selector}' with {delayMs}ms delay";
    }

    [Description("Get visible text content from element. Maps to 'Then I get text from <selector>'.")]
    public async Task<string> get_element_text(
        [Description("CSS selector")] string selector)
    {
        EnsurePage();
        var text = await _page!.Locator(selector).InnerTextAsync();
        return text;
    }

    [Description("Get input value from form field. Maps to 'Then I get value from <selector>'.")]
    public async Task<string> get_input_value(
        [Description("CSS selector")] string selector)
    {
        EnsurePage();
        var value = await _page!.Locator(selector).InputValueAsync();
        return value;
    }

    [Description("Get HTML attribute value. Maps to 'Then I get attribute <attr> from <selector>'.")]
    public async Task<string> get_element_attribute(
        [Description("CSS selector")] string selector,
        [Description("Attribute name e.g. href, src, data-testid")] string attributeName)
    {
        EnsurePage();
        var value = await _page!.Locator(selector).GetAttributeAsync(attributeName);
        return value ?? "";
    }

    [Description("Check if element is visible. Maps to 'Then <selector> is visible'.")]
    public async Task<string> is_element_visible(
        [Description("CSS selector")] string selector)
    {
        EnsurePage();
        var isVisible = await _page!.Locator(selector).IsVisibleAsync();
        return isVisible.ToString().ToLower();
    }

    [Description("Check if element is enabled. Maps to 'Then <selector> is enabled'.")]
    public async Task<string> is_element_enabled(
        [Description("CSS selector")] string selector)
    {
        EnsurePage();
        var isEnabled = await _page!.Locator(selector).IsEnabledAsync();
        return isEnabled.ToString().ToLower();
    }

    [Description("Check if checkbox is checked. Maps to 'Then <selector> is checked'.")]
    public async Task<string> is_element_checked(
        [Description("CSS selector")] string selector)
    {
        EnsurePage();
        var isChecked = await _page!.Locator(selector).IsCheckedAsync();
        return isChecked.ToString().ToLower();
    }

    [Description("Select dropdown option by label (visible text). Maps to 'When I select <label> from <selector>'.")]
    public async Task<string> select_option_by_label(
        [Description("CSS selector for dropdown")] string selector,
        [Description("Visible option text")] string label)
    {
        EnsurePage();
        await _page!.SelectOptionAsync(selector, new SelectOptionValue { Label = label });
        return $"Selected '{label}' from '{selector}'";
    }

    [Description("Drag element to target. Maps to 'When I drag <source> to <target>'.")]
    public async Task<string> drag_to_element(
        [Description("CSS selector for source element")] string sourceSelector,
        [Description("CSS selector for target element")] string targetSelector)
    {
        EnsurePage();
        var source = _page!.Locator(sourceSelector);
        var target = _page!.Locator(targetSelector);
        await source.DragToAsync(target);
        return $"Dragged '{sourceSelector}' to '{targetSelector}'";
    }

    [Description("Take screenshot of specific element. Maps to 'Then I screenshot <selector>'.")]
    public async Task<string> screenshot_element(
        [Description("CSS selector")] string selector,
        [Description("Output file path")] string outputPath = "element-screenshot.png")
    {
        EnsurePage();
        var bytes = await _page!.Locator(selector).ScreenshotAsync();
        await File.WriteAllBytesAsync(outputPath, bytes);
        return $"Element screenshot saved: {outputPath}";
    }

    [Description("Wait for element to be hidden. Maps to 'Then I wait for <selector> to disappear'.")]
    public async Task<string> wait_for_element_hidden(
        [Description("CSS selector")] string selector,
        [Description("Timeout ms")] int timeoutMs = 30000)
    {
        EnsurePage();
        await _page!.WaitForSelectorAsync(selector, new() { State = WaitForSelectorState.Hidden, Timeout = timeoutMs });
        return $"Element '{selector}' is now hidden";
    }

    // =========================================================
    // UI ASSERTIONS  (maps to SpecFlow Then steps)
    // =========================================================

    [Description("Assert page contains text. Maps to 'Then I should see <text>'.")]
    public async Task<string> assert_text_visible(
        [Description("Text to look for on the page")] string text,
        [Description("CSS selector to scope the search (optional)")] string? scope = null)
    {
        EnsurePage();
        var locator = scope != null
            ? _page!.Locator(scope).Filter(new() { HasText = text })
            : _page!.GetByText(text);

        await Assertions.Expect(locator.First).ToBeVisibleAsync();
        return $"PASS: text visible — '{text}'";
    }

    [Description("Assert element is visible. Maps to 'Then <selector> should be visible'.")]
    public async Task<string> assert_element_visible(
        [Description("CSS selector")] string selector)
    {
        EnsurePage();
        await Assertions.Expect(_page!.Locator(selector)).ToBeVisibleAsync();
        return $"PASS: element visible — '{selector}'";
    }

    [Description("Assert element is hidden. Maps to 'Then <selector> should not be visible'.")]
    public async Task<string> assert_element_hidden(
        [Description("CSS selector")] string selector)
    {
        EnsurePage();
        await Assertions.Expect(_page!.Locator(selector)).ToBeHiddenAsync();
        return $"PASS: element hidden — '{selector}'";
    }

    [Description("Assert page title matches. Maps to 'Then the page title should be <expected>'.")]
    public async Task<string> assert_page_title(
        [Description("Expected title text")] string expected)
    {
        EnsurePage();
        await Assertions.Expect(_page!).ToHaveTitleAsync(expected);
        return $"PASS: page title = '{expected}'";
    }

    [Description("Assert current URL matches. Maps to 'Then the URL should contain <partial>'.")]
    public async Task<string> assert_url_contains(
        [Description("Partial or full URL to match")] string partial)
    {
        EnsurePage();
        await Assertions.Expect(_page!).ToHaveURLAsync(new System.Text.RegularExpressions.Regex(partial));
        return $"PASS: URL contains '{partial}'";
    }

    [Description("Assert an input has a specific value. Maps to 'Then <selector> should have value <expected>'.")]
    public async Task<string> assert_input_value(
        [Description("CSS selector of the input")] string selector,
        [Description("Expected value")] string expected)
    {
        EnsurePage();
        await Assertions.Expect(_page!.Locator(selector)).ToHaveValueAsync(expected);
        return $"PASS: input '{selector}' = '{expected}'";
    }

    [Description("Assert element count. Maps to 'Then there should be <count> <selector>'.")]
    public async Task<string> assert_element_count(
        [Description("CSS selector")] string selector,
        [Description("Expected count")] int count)
    {
        EnsurePage();
        await Assertions.Expect(_page!.Locator(selector)).ToHaveCountAsync(count);
        return $"PASS: count of '{selector}' = {count}";
    }

    // =========================================================
    // API TESTING  (maps to SpecFlow Given/When/Then API steps)
    // =========================================================

    [Description("Send an HTTP request via Playwright API context. Maps to 'When I send a <method> request to <url>'.")]
    public async Task<string> api_request(
        [Description("HTTP method: GET | POST | PUT | PATCH | DELETE")] string method,
        [Description("Full URL")] string url,
        [Description("JSON request body (optional)")] string? body = null,
        [Description("JSON object of headers e.g. {\"Authorization\":\"Bearer token\"}")] string? headers = null,
        [Description("Base URL for the API context (optional)")] string? baseUrl = null)
    {
        _playwright ??= await Playwright.CreateAsync();

        var options = new APIRequestNewContextOptions();
        if (baseUrl != null) options.BaseURL = baseUrl;

        _apiContext ??= await _playwright.APIRequest.NewContextAsync(options);

        // Parse headers
        Dictionary<string, string>? parsedHeaders = null;
        if (headers != null)
            parsedHeaders = JsonSerializer.Deserialize<Dictionary<string, string>>(headers);

        IAPIResponse response;
        var reqOptions = new APIRequestContextOptions
        {
            Headers = parsedHeaders?.Select(kv => KeyValuePair.Create(kv.Key, kv.Value))
                          .ToDictionary(kv => kv.Key, kv => kv.Value)
        };

        if (body != null)
            reqOptions.DataObject = JsonSerializer.Deserialize<object>(body);

        response = method.ToUpper() switch
        {
            "POST"   => await _apiContext.PostAsync(url, reqOptions),
            "PUT"    => await _apiContext.PutAsync(url, reqOptions),
            "PATCH"  => await _apiContext.PatchAsync(url, reqOptions),
            "DELETE" => await _apiContext.DeleteAsync(url, reqOptions),
            _        => await _apiContext.GetAsync(url, reqOptions)
        };

        var responseBody = await response.TextAsync();

        // Store for subsequent assertion tools
        LastApiResponse = new ApiResponse
        {
            StatusCode = response.Status,
            Body       = responseBody,
            Headers    = response.Headers
        };

        return JsonSerializer.Serialize(new
        {
            status  = response.Status,
            ok      = response.Ok,
            body    = responseBody
        });
    }

    [Description("Assert HTTP response status code. Maps to 'Then the response status should be <code>'.")]
    public Task<string> assert_response_status(
        [Description("Expected HTTP status code e.g. 200, 201, 404")] int expectedStatus)
    {
        EnsureApiResponse();
        var actual = LastApiResponse!.StatusCode;
        if (actual != expectedStatus)
            throw new AssertionException($"Expected status {expectedStatus} but got {actual}");
        return Task.FromResult($"PASS: response status = {expectedStatus}");
    }

    [Description("Assert response body contains a string. Maps to 'Then the response body should contain <text>'.")]
    public Task<string> assert_response_body_contains(
        [Description("Text expected in response body")] string text)
    {
        EnsureApiResponse();
        if (!LastApiResponse!.Body.Contains(text, StringComparison.OrdinalIgnoreCase))
            throw new AssertionException($"Response body does not contain '{text}'. Body: {LastApiResponse.Body}");
        return Task.FromResult($"PASS: response body contains '{text}'");
    }

    [Description("Assert a JSON path value in the response. Maps to 'Then the response <jsonPath> should be <expected>'.")]
    public Task<string> assert_json_path(
        [Description("Dot-notation JSON path e.g. data.id or items[0].name")] string jsonPath,
        [Description("Expected value as string")] string expected)
    {
        EnsureApiResponse();
        var doc   = JsonDocument.Parse(LastApiResponse!.Body);
        var parts = jsonPath.Split('.');
        JsonElement current = doc.RootElement;

        foreach (var part in parts)
        {
            // Handle array indexing e.g. items[0]
            if (part.Contains('['))
            {
                var name = part[..part.IndexOf('[')];
                var idx  = int.Parse(part[(part.IndexOf('[') + 1)..part.IndexOf(']')]);
                current  = current.GetProperty(name)[idx];
            }
            else
            {
                current = current.GetProperty(part);
            }
        }

        var actual = current.ToString();
        if (actual != expected)
            throw new AssertionException($"JSON path '{jsonPath}': expected '{expected}' but got '{actual}'");

        return Task.FromResult($"PASS: {jsonPath} = '{expected}'");
    }

    [Description("Assert response header value. Maps to 'Then the response header <header> should be <value>'.")]
    public Task<string> assert_response_header(
        [Description("Header name")] string header,
        [Description("Expected value")] string expected)
    {
        EnsureApiResponse();
        if (!LastApiResponse!.Headers.TryGetValue(header.ToLower(), out var actual))
            throw new AssertionException($"Header '{header}' not present in response.");
        if (actual != expected)
            throw new AssertionException($"Header '{header}': expected '{expected}' but got '{actual}'");
        return Task.FromResult($"PASS: header '{header}' = '{expected}'");
    }

    // =========================================================
    // INTERNAL STATE
    // =========================================================

    internal ApiResponse? LastApiResponse { get; private set; }

    private void EnsurePage()
    {
        if (_page == null)
            throw new InvalidOperationException("Browser not launched. Call launch_browser first.");
    }

    private void EnsureApiResponse()
    {
        if (LastApiResponse == null)
            throw new InvalidOperationException("No API response stored. Call api_request first.");
    }

    public void Dispose()
    {
        _browser?.CloseAsync().GetAwaiter().GetResult();
        _apiContext?.DisposeAsync().GetAwaiter().GetResult();
        _playwright?.Dispose();
    }
}

public class ApiResponse
{
    public int StatusCode { get; set; }
    public string Body { get; set; } = "";
    public IReadOnlyDictionary<string, string> Headers { get; set; } = new Dictionary<string, string>();
}

public class AssertionException(string message) : Exception(message);

// McpBridge/Tools/SeleniumToolHandler.cs
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Edge;
using OpenQA.Selenium.Support.UI;
using McpBridge.Models;
using System.Text.Json;

namespace McpBridge.Tools;

public class SeleniumToolHandler : IDisposable
{
    private IWebDriver? _driver;
    private WebDriverWait? _wait;
    private readonly TimeSpan _timeout = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Safely quotes a string for interpolation into an XPath expression.
    /// A raw value containing a single quote would otherwise break out of the
    /// XPath string literal it's embedded in. Falls back to XPath's concat()
    /// trick when the value contains both quote characters.
    /// </summary>
    private static string XPathLiteral(string value)
    {
        if (!value.Contains('\''))
            return $"'{value}'";
        if (!value.Contains('"'))
            return $"\"{value}\"";

        var parts = value.Split('\'').Select(p => $"'{p}'");
        return "concat(" + string.Join(", \"'\", ", parts) + ")";
    }

    /// <summary>
    /// Waits (up to timeoutMs) for the document to report readyState "complete"
    /// instead of a fixed Thread.Sleep. Returns as soon as the page settles
    /// rather than always blocking for the full duration, and is a best-effort
    /// substitute for cases (post-scroll/hover/navigate) with no specific
    /// element condition to wait on.
    /// </summary>
    private void WaitForPageReady(int timeoutMs)
    {
        if (_driver == null) return;
        try
        {
            new WebDriverWait(_driver, TimeSpan.FromMilliseconds(timeoutMs))
                .Until(d => ((IJavaScriptExecutor)d).ExecuteScript("return document.readyState")?.ToString() == "complete");
        }
        catch (WebDriverTimeoutException)
        {
            // Best-effort — proceed even if the page never reports "complete".
        }
    }

    // ── Launch ────────────────────────────────────────────────────────
    public ToolResponse LaunchBrowser(Dictionary<string, object> args)
    {
        // Close existing browser session if one exists
        if (_driver != null)
        {
            try
            {
                _driver.Quit();
                _driver.Dispose();
            }
            catch
            {
                // Ignore errors during cleanup
            }
            finally
            {
                _driver = null;
                _wait = null;
            }
        }

        var browser = args.GetValueOrDefault("browser", "chrome")?.ToString()?.ToLower() ?? "chrome";
        
        // Safely parse headless parameter to handle JsonElement
        var headless = false;
        if (args.ContainsKey("headless"))
        {
            var headlessValue = args["headless"];
            if (headlessValue is JsonElement jsonElement)
            {
                headless = jsonElement.ValueKind == JsonValueKind.True;
            }
            else if (headlessValue is bool boolValue)
            {
                headless = boolValue;
            }
            else if (headlessValue is string strValue)
            {
                headless = bool.TryParse(strValue, out var result) && result;
            }
        }

        // Launch Edge browser
        if (browser.Contains("edge"))
        {
            var edgeOptions = new EdgeOptions();
            if (headless)
            {
                edgeOptions.AddArgument("--headless=new");
                edgeOptions.AddArgument("--no-sandbox");
                edgeOptions.AddArgument("--disable-dev-shm-usage");
                edgeOptions.AddArgument("--window-size=1920,1080");
            }
            edgeOptions.AddArgument("--disable-extensions");

            _driver = new EdgeDriver(edgeOptions);
            _driver.Manage().Window.Maximize();
            _wait = new WebDriverWait(_driver, _timeout);

            return ToolResponse.Ok(new
            {
                status = "ok",
                browser = "edge",
                sessionId = (_driver as EdgeDriver)?.SessionId?.ToString()
            });
        }
        // Default: Chrome
        else
        {
            var chromeOptions = new ChromeOptions();
            if (headless)
            {
                chromeOptions.AddArgument("--headless=new");
                chromeOptions.AddArgument("--no-sandbox");
                chromeOptions.AddArgument("--disable-dev-shm-usage");
                chromeOptions.AddArgument("--window-size=1920,1080");
            }
            chromeOptions.AddArgument("--disable-extensions");

            _driver = new ChromeDriver(chromeOptions);
            _driver.Manage().Window.Maximize();
            _wait = new WebDriverWait(_driver, _timeout);

            return ToolResponse.Ok(new
            {
                status = "ok",
                browser = "chrome",
                sessionId = (_driver as ChromeDriver)?.SessionId?.ToString()
            });
        }
    }

    // ── Navigate ──────────────────────────────────────────────────────
    public ToolResponse Navigate(Dictionary<string, object> args)
    {
        if (_driver == null)
        {
            return ToolResponse.Fail("Browser not launched. Call launch_browser first.");
        }

        try
        {
            var url = args["url"].ToString()!;
            _driver.Navigate().GoToUrl(url);
            
            return ToolResponse.Ok(new
            {
                status = "ok",
                url,
                title = _driver.Title,
                currentUrl = _driver.Url
            });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("invalid session"))
        {
            // Session was closed or invalid - reset driver
            _driver = null;
            return ToolResponse.Fail("Invalid session id. Browser session was closed. Please launch browser again.");
        }
        catch (WebDriverException ex)
        {
            return ToolResponse.Fail($"Navigation failed: {ex.Message}");
        }
    }

    // ── Find element ──────────────────────────────────────────────────
    public ToolResponse FindElement(Dictionary<string, object> args)
    {
        var el = GetElement(args);
        return ToolResponse.Ok(new
        {
            status = "ok",
            tag = el.TagName,
            text = el.Text,
            displayed = el.Displayed,
            enabled = el.Enabled
        });
    }

    // ── Click ─────────────────────────────────────────────────────────
    public ToolResponse Click(Dictionary<string, object> args)
    {
        var el = GetClickableElement(args);
        el.Click();
        return ToolResponse.Ok(new { status = "ok", action = "click" });
    }

    // ── Click By Text (ANY element type) ──────────────────────────────
    public ToolResponse ClickByText(Dictionary<string, object> args)
    {
        // ✅ FIX: Check if driver is initialized before proceeding
        if (_driver == null)
        {
            return ToolResponse.Fail("Selenium WebDriver is not initialized. Call launch_browser first or ensure Playwright is working correctly.");
        }

        var text = args["text"].ToString()!;
        var exactMatch = args.GetValueOrDefault("exact_match", false)?.ToString()?.ToLower() == "true";
        var timeout = args.ContainsKey("timeout") ? int.Parse(args["timeout"].ToString()!) : 30;

        // Create a custom wait with the specified timeout (not the default 15s)
        var customWait = new WebDriverWait(_driver, TimeSpan.FromSeconds(timeout));

        try
        {
            // Build comprehensive XPath strategies - try button first (most common)
            var xpaths = new List<string>();
            var quotedText = XPathLiteral(text);

            if (exactMatch)
            {
                xpaths.Add($"//button[normalize-space(.)={quotedText}]");
                xpaths.Add($"//a[normalize-space(.)={quotedText}]");
                xpaths.Add($"//*[normalize-space(.)={quotedText}]");
            }
            else
            {
                // Priority order: button, link, div/span (any element), then specific patterns
                xpaths.Add($"//button[contains(text(),{quotedText})]");
                xpaths.Add($"//button[contains(@class,'MuiButton') and contains(text(),{quotedText})]");
                xpaths.Add($"//a[contains(text(),{quotedText})]");
                xpaths.Add($"//*[contains(text(),{quotedText})]"); // Moved up - catches divs, spans, any element
                xpaths.Add($"//div[contains(text(),{quotedText}) and (@role='button' or contains(@class,'button'))]");
                xpaths.Add($"//span[contains(text(),{quotedText})]");
                xpaths.Add($"//*[@value={quotedText}]");
            }

            IWebElement? element = null;
            string? successfulXPath = null;
            string? lastError = null;

            // Try each XPath with proper ElementToBeClickable wait (like LoginSteps.cs)
            foreach (var xpath in xpaths)
            {
                try
                {
                    var by = By.XPath(xpath);
                    
                    // First try ElementToBeClickable (for buttons, links)
                    try
                    {
                        element = customWait.Until(SeleniumExtras.WaitHelpers.ExpectedConditions.ElementToBeClickable(by));
                    }
                    catch
                    {
                        // Fallback to ElementIsVisible for divs/spans (like LoginSteps.cs does)
                        element = customWait.Until(SeleniumExtras.WaitHelpers.ExpectedConditions.ElementIsVisible(by));
                    }
                    
                    if (element != null)
                    {
                        successfulXPath = xpath;
                        break;
                    }
                }
                catch (Exception ex)
                {
                    // Store last error for debugging
                    lastError = ex.Message;
                    // Try next XPath
                    continue;
                }
            }

            if (element == null)
            {
                return ToolResponse.Fail($"No clickable element found with text: '{text}'. Tried {xpaths.Count} XPath strategies. Last error: {lastError ?? "none"}. Page may still be loading or element doesn't exist.");
            }

            // Capture element info BEFORE clicking (to avoid stale element after navigation)
            var tagName = element.TagName;
            var elementText = element.Text;

            // Scroll element into view first
            ((IJavaScriptExecutor)_driver!).ExecuteScript("arguments[0].scrollIntoView({behavior: 'smooth', block: 'center'});", element);
            WaitForPageReady(500); // Brief pause after scroll

            // Try normal click first, fallback to JavaScript click (like LoginSteps.cs)
            try
            {
                element.Click();
            }
            catch
            {
                ((IJavaScriptExecutor)_driver!).ExecuteScript("arguments[0].click();", element);
            }

            return ToolResponse.Ok(new
            {
                status = "ok",
                action = "click_by_text",
                text,
                tagName,
                xpath = successfulXPath,
                elementText
            });
        }
        catch (Exception ex)
        {
            return ToolResponse.Fail($"Failed to click element with text '{text}': {ex.Message}");
        }
    }

    // ── Type text ─────────────────────────────────────────────────────
    public ToolResponse TypeText(Dictionary<string, object> args)
    {
        var el = GetClickableElement(args);
        var text = args["text"].ToString()!;
        el.Clear();
        el.SendKeys(text);
        return ToolResponse.Ok(new { status = "ok", action = "type", text });
    }

    // ── Get text ──────────────────────────────────────────────────────
    public ToolResponse GetText(Dictionary<string, object> args)
    {
        var el = GetElement(args);
        return ToolResponse.Ok(new { status = "ok", text = el.Text });
    }

    // ── Get attribute ─────────────────────────────────────────────────
    public ToolResponse GetAttribute(Dictionary<string, object> args)
    {
        var el = GetElement(args);
        var attr = args["attribute"].ToString()!;
        return ToolResponse.Ok(new
        {
            status = "ok",
            attribute = attr,
            value = el.GetAttribute(attr)
        });
    }

    // ── Assert visible ────────────────────────────────────────────────
    public ToolResponse AssertVisible(Dictionary<string, object> args)
    {
        var el = GetElement(args);
        if (!el.Displayed)
            return ToolResponse.Fail($"Element not visible: {args["value"]}");

        return ToolResponse.Ok(new
        {
            status = "passed",
            assertion = "element_visible",
            locator = args["value"]
        });
    }

    // ── Assert text contains ──────────────────────────────────────────
    public ToolResponse AssertTextContains(Dictionary<string, object> args)
    {
        var el = GetElement(args);
        var expected = args["expected_text"].ToString()!;
        var actual = el.Text;

        if (!actual.Contains(expected, StringComparison.OrdinalIgnoreCase))
            return ToolResponse.Fail(
                $"Expected '{expected}' in element text but got '{actual}'");

        return ToolResponse.Ok(new
        {
            status = "passed",
            assertion = "text_contains",
            expected,
            actual
        });
    }

    // ── Assert URL contains ───────────────────────────────────────────
    public ToolResponse AssertUrlContains(Dictionary<string, object> args)
    {
        if (_driver == null)
        {
            return ToolResponse.Fail("Selenium WebDriver is not initialized. Call launch_browser first.");
        }

        var expected = args["expected"].ToString()!;
        var actual = _driver.Url;

        if (!actual.Contains(expected))
            return ToolResponse.Fail($"URL '{actual}' does not contain '{expected}'");

        return ToolResponse.Ok(new
        {
            status = "passed",
            assertion = "url_contains",
            expected,
            actual
        });
    }

    // ── Take screenshot ───────────────────────────────────────────────
    public ToolResponse TakeScreenshot(Dictionary<string, object> args)
    {
        if (_driver == null)
        {
            return ToolResponse.Fail("Selenium WebDriver is not initialized. Call launch_browser first.");
        }

        var filename = args.GetValueOrDefault("filename", "screenshot.png")?.ToString()
                       ?? "screenshot.png";
        var screenshot = ((ITakesScreenshot)_driver).GetScreenshot();
        screenshot.SaveAsFile(filename);

        return ToolResponse.Ok(new
        {
            status = "ok",
            filename,
            fileSizeBytes = new FileInfo(filename).Length
        });
    }

    // ── Execute JavaScript ────────────────────────────────────────────
    public ToolResponse ExecuteScript(Dictionary<string, object> args)
    {
        if (_driver == null)
        {
            return ToolResponse.Fail("Selenium WebDriver is not initialized. Call launch_browser first.");
        }

        var script = args["script"].ToString()!;
        var result = ((IJavaScriptExecutor)_driver).ExecuteScript(script);
        return ToolResponse.Ok(new { status = "ok", result = result?.ToString() });
    }

    // ── Get page title / URL ──────────────────────────────────────────
    public ToolResponse GetPageInfo(Dictionary<string, object> args)
    {
        if (_driver == null)
        {
            return ToolResponse.Fail("Selenium WebDriver is not initialized. Call launch_browser first.");
        }

        return ToolResponse.Ok(new
        {
            status = "ok",
            title = _driver.Title,
            url = _driver.Url,
            pageSource = _driver.PageSource[..Math.Min(3000, _driver.PageSource.Length)]
        });
    }

    // ── Select dropdown ───────────────────────────────────────────────
    public ToolResponse SelectDropdown(Dictionary<string, object> args)
    {
        var el = GetElement(args);
        var selectEl = new OpenQA.Selenium.Support.UI.SelectElement(el);
        var value = args["select_value"].ToString()!;
        
        // Safely parse by_text parameter to handle JsonElement
        var byText = false;
        if (args.ContainsKey("by_text"))
        {
            var byTextValue = args["by_text"];
            if (byTextValue is JsonElement jsonElement)
            {
                byText = jsonElement.ValueKind == JsonValueKind.True;
            }
            else if (byTextValue is bool boolValue)
            {
                byText = boolValue;
            }
            else if (byTextValue is string strValue)
            {
                byText = bool.TryParse(strValue, out var result) && result;
            }
        }

        if (byText) selectEl.SelectByText(value);
        else selectEl.SelectByValue(value);

        return ToolResponse.Ok(new { status = "ok", selected = value });
    }

    // ── Wait for element ──────────────────────────────────────────────
    public ToolResponse WaitForElement(Dictionary<string, object> args)
    {
        if (_driver == null)
        {
            return ToolResponse.Fail("Selenium WebDriver is not initialized. Call launch_browser first.");
        }

        var by = BuildBy(args);
        var timeoutSeconds = args.ContainsKey("timeout") 
            ? int.Parse(args["timeout"].ToString()!) 
            : 30;

        var wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(timeoutSeconds));
        var element = wait.Until(d => d.FindElement(by));
        
        return ToolResponse.Ok(new
        {
            status = "ok",
            found = true,
            tag = element.TagName,
            displayed = element.Displayed
        });
    }

    // ── Wait for element to be visible ────────────────────────────────
    public ToolResponse WaitForVisible(Dictionary<string, object> args)
    {
        if (_driver == null)
        {
            return ToolResponse.Fail("Selenium WebDriver is not initialized. Call launch_browser first.");
        }

        var by = BuildBy(args);
        var timeoutSeconds = args.ContainsKey("timeout") 
            ? int.Parse(args["timeout"].ToString()!) 
            : 30;

        var wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(timeoutSeconds));
        var element = wait.Until(SeleniumExtras.WaitHelpers.ExpectedConditions.ElementIsVisible(by));
        
        return ToolResponse.Ok(new { status = "ok", visible = true });
    }

    // ── Wait for text to be present ───────────────────────────────────
    public ToolResponse WaitForText(Dictionary<string, object> args)
    {
        if (_driver == null)
        {
            return ToolResponse.Fail("Selenium WebDriver is not initialized. Call launch_browser first.");
        }

        var by = BuildBy(args);
        var text = args["text"].ToString()!;
        var timeoutSeconds = args.ContainsKey("timeout") 
            ? int.Parse(args["timeout"].ToString()!) 
            : 30;

        var wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(timeoutSeconds));
        var present = wait.Until(SeleniumExtras.WaitHelpers.ExpectedConditions
            .TextToBePresentInElementLocated(by, text));
        
        return ToolResponse.Ok(new { status = "ok", textPresent = present });
    }

    // ── Get all elements ──────────────────────────────────────────────
    public ToolResponse FindElements(Dictionary<string, object> args)
    {
        if (_driver == null)
        {
            return ToolResponse.Fail("Selenium WebDriver is not initialized. Call launch_browser first.");
        }

        var by = BuildBy(args);
        var elements = _driver.FindElements(by);
        
        var elementData = elements.Select(e => new
        {
            tag = e.TagName,
            text = e.Text,
            displayed = e.Displayed,
            enabled = e.Enabled,
            id = e.GetAttribute("id"),
            className = e.GetAttribute("class")
        }).ToList();
        
        return ToolResponse.Ok(new
        {
            status = "ok",
            count = elements.Count,
            elements = elementData
        });
    }

    // ── Get element count ─────────────────────────────────────────────
    public ToolResponse CountElements(Dictionary<string, object> args)
    {
        if (_driver == null)
        {
            return ToolResponse.Fail("Selenium WebDriver is not initialized. Call launch_browser first.");
        }

        var by = BuildBy(args);
        var count = _driver.FindElements(by).Count;
        
        return ToolResponse.Ok(new { status = "ok", count });
    }

    // ── Check element exists ──────────────────────────────────────────
    public ToolResponse ElementExists(Dictionary<string, object> args)
    {
        if (_driver == null)
        {
            return ToolResponse.Fail("Selenium WebDriver is not initialized. Call launch_browser first.");
        }

        var by = BuildBy(args);
        var exists = _driver.FindElements(by).Count > 0;
        
        return ToolResponse.Ok(new { status = "ok", exists });
    }

    // ── Scroll to element ─────────────────────────────────────────────
    public ToolResponse ScrollToElement(Dictionary<string, object> args)
    {
        var element = GetElement(args);
        ((IJavaScriptExecutor)_driver!).ExecuteScript(
            "arguments[0].scrollIntoView({behavior: 'smooth', block: 'center'});",
            element
        );
        WaitForPageReady(500);
        
        return ToolResponse.Ok(new { status = "ok", action = "scrolled" });
    }

    // ── Scroll page ───────────────────────────────────────────────────
    public ToolResponse ScrollPage(Dictionary<string, object> args)
    {
        if (_driver == null)
        {
            return ToolResponse.Fail("Selenium WebDriver is not initialized. Call launch_browser first.");
        }

        var direction = args.GetValueOrDefault("direction", "down")?.ToString() ?? "down";
        var pixels = args.ContainsKey("pixels") 
            ? int.Parse(args["pixels"].ToString()!) 
            : 300;

        var scrollAmount = direction.ToLower() == "up" ? -pixels : pixels;
        ((IJavaScriptExecutor)_driver).ExecuteScript($"window.scrollBy(0, {scrollAmount});");
        WaitForPageReady(300);
        
        return ToolResponse.Ok(new { status = "ok", direction, pixels });
    }

    // ── Hover over element ────────────────────────────────────────────
    public ToolResponse HoverElement(Dictionary<string, object> args)
    {
        var element = GetElement(args);
        var actions = new OpenQA.Selenium.Interactions.Actions(_driver!);
        actions.MoveToElement(element).Perform();
        WaitForPageReady(300);
        
        return ToolResponse.Ok(new { status = "ok", action = "hover" });
    }

    // ── Double click ──────────────────────────────────────────────────
    public ToolResponse DoubleClick(Dictionary<string, object> args)
    {
        var element = GetElement(args);
        var actions = new OpenQA.Selenium.Interactions.Actions(_driver!);
        actions.DoubleClick(element).Perform();
        
        return ToolResponse.Ok(new { status = "ok", action = "double_click" });
    }

    // ── Right click ───────────────────────────────────────────────────
    public ToolResponse RightClick(Dictionary<string, object> args)
    {
        var element = GetElement(args);
        var actions = new OpenQA.Selenium.Interactions.Actions(_driver!);
        actions.ContextClick(element).Perform();
        
        return ToolResponse.Ok(new { status = "ok", action = "right_click" });
    }

    // ── Clear and type ────────────────────────────────────────────────
    public ToolResponse ClearAndType(Dictionary<string, object> args)
    {
        var element = GetElement(args);
        var text = args["text"].ToString()!;
        
        element.Clear();
        element.SendKeys(Keys.Control + "a");
        element.SendKeys(Keys.Delete);
        element.SendKeys(text);
        
        return ToolResponse.Ok(new { status = "ok", action = "clear_and_type", text });
    }

    // ── Press key ─────────────────────────────────────────────────────
    public ToolResponse PressKey(Dictionary<string, object> args)
    {
        if (_driver == null)
        {
            return ToolResponse.Fail("Selenium WebDriver is not initialized. Call launch_browser first.");
        }

        var key = args["key"].ToString()!.ToLower();
        var element = args.ContainsKey("value") ? GetElement(args) : null;

        var keyToPress = key switch
        {
            "enter" => Keys.Enter,
            "tab" => Keys.Tab,
            "escape" => Keys.Escape,
            "space" => Keys.Space,
            "backspace" => Keys.Backspace,
            "delete" => Keys.Delete,
            "arrowup" => Keys.ArrowUp,
            "arrowdown" => Keys.ArrowDown,
            "arrowleft" => Keys.ArrowLeft,
            "arrowright" => Keys.ArrowRight,
            _ => key
        };

        if (element != null)
            element.SendKeys(keyToPress);
        else
            new OpenQA.Selenium.Interactions.Actions(_driver).SendKeys(keyToPress).Perform();
        
        return ToolResponse.Ok(new { status = "ok", key });
    }

    // ── Switch to frame ───────────────────────────────────────────────
    public ToolResponse SwitchToFrame(Dictionary<string, object> args)
    {
        if (_driver == null)
        {
            return ToolResponse.Fail("Selenium WebDriver is not initialized. Call launch_browser first.");
        }

        if (args.ContainsKey("index"))
        {
            var index = int.Parse(args["index"].ToString()!);
            _driver.SwitchTo().Frame(index);
        }
        else if (args.ContainsKey("name"))
        {
            var name = args["name"].ToString()!;
            _driver.SwitchTo().Frame(name);
        }
        else
        {
            var element = GetElement(args);
            _driver.SwitchTo().Frame(element);
        }
        
        return ToolResponse.Ok(new { status = "ok", action = "switched_to_frame" });
    }

    // ── Switch to default content ─────────────────────────────────────
    public ToolResponse SwitchToDefaultContent(Dictionary<string, object> args)
    {
        if (_driver == null)
        {
            return ToolResponse.Fail("Selenium WebDriver is not initialized. Call launch_browser first.");
        }

        _driver.SwitchTo().DefaultContent();
        return ToolResponse.Ok(new { status = "ok", action = "switched_to_default" });
    }

    // ── Switch to window ──────────────────────────────────────────────
    public ToolResponse SwitchToWindow(Dictionary<string, object> args)
    {
        if (_driver == null)
        {
            return ToolResponse.Fail("Selenium WebDriver is not initialized. Call launch_browser first.");
        }

        var windowHandle = args.ContainsKey("handle") 
            ? args["handle"].ToString()!
            : _driver.WindowHandles.Last();

        _driver.SwitchTo().Window(windowHandle);
        
        return ToolResponse.Ok(new
        {
            status = "ok",
            currentWindow = _driver.CurrentWindowHandle,
            title = _driver.Title
        });
    }

    // ── Get window handles ────────────────────────────────────────────
    public ToolResponse GetWindowHandles(Dictionary<string, object> args)
    {
        if (_driver == null)
        {
            return ToolResponse.Fail("Selenium WebDriver is not initialized. Call launch_browser first.");
        }

        var handles = _driver.WindowHandles;
        
        return ToolResponse.Ok(new
        {
            status = "ok",
            count = handles.Count,
            handles = handles.ToList(),
            current = _driver.CurrentWindowHandle
        });
    }

    // ── Close current window ──────────────────────────────────────────
    public ToolResponse CloseWindow(Dictionary<string, object> args)
    {
        if (_driver == null)
        {
            return ToolResponse.Fail("Selenium WebDriver is not initialized. Call launch_browser first.");
        }

        _driver.Close();
        
        if (_driver.WindowHandles.Count > 0)
            _driver.SwitchTo().Window(_driver.WindowHandles[0]);
        
        return ToolResponse.Ok(new { status = "ok", action = "window_closed" });
    }

    // ── Handle alert ──────────────────────────────────────────────────
    public ToolResponse HandleAlert(Dictionary<string, object> args)
    {
        if (_driver == null)
        {
            return ToolResponse.Fail("Selenium WebDriver is not initialized. Call launch_browser first.");
        }

        var action = args.GetValueOrDefault("action", "accept")?.ToString() ?? "accept";

        var wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(5));
        var alert = wait.Until(SeleniumExtras.WaitHelpers.ExpectedConditions.AlertIsPresent());
        
        var alertText = alert.Text;
        
        if (action.ToLower() == "accept")
            alert.Accept();
        else if (action.ToLower() == "dismiss")
            alert.Dismiss();
        else if (args.ContainsKey("text"))
        {
            alert.SendKeys(args["text"].ToString()!);
            alert.Accept();
        }
        
        return ToolResponse.Ok(new { status = "ok", action, alertText });
    }

    // ── Get cookies ───────────────────────────────────────────────────
    public ToolResponse GetCookies(Dictionary<string, object> args)
    {
        if (_driver == null)
        {
            return ToolResponse.Fail("Selenium WebDriver is not initialized. Call launch_browser first.");
        }

        var cookies = _driver.Manage().Cookies.AllCookies;
        var cookieData = cookies.Select(c => new
        {
            name = c.Name,
            value = c.Value,
            domain = c.Domain,
            path = c.Path,
            expiry = c.Expiry?.ToString()
        }).ToList();
        
        return ToolResponse.Ok(new { status = "ok", count = cookies.Count, cookies = cookieData });
    }

    // ── Add cookie ────────────────────────────────────────────────────
    public ToolResponse AddCookie(Dictionary<string, object> args)
    {
        if (_driver == null)
        {
            return ToolResponse.Fail("Selenium WebDriver is not initialized. Call launch_browser first.");
        }

        var name = args["name"].ToString()!;
        var value = args["value"].ToString()!;
        var domain = args.ContainsKey("domain") ? args["domain"].ToString() : null;

        var cookie = new Cookie(name, value, domain, "/", null);
        _driver.Manage().Cookies.AddCookie(cookie);
        
        return ToolResponse.Ok(new { status = "ok", action = "cookie_added", name });
    }

    // ── Delete cookie ─────────────────────────────────────────────────
    public ToolResponse DeleteCookie(Dictionary<string, object> args)
    {
        if (_driver == null)
        {
            return ToolResponse.Fail("Selenium WebDriver is not initialized. Call launch_browser first.");
        }

        var name = args["name"].ToString()!;
        _driver.Manage().Cookies.DeleteCookieNamed(name);
        
        return ToolResponse.Ok(new { status = "ok", action = "cookie_deleted", name });
    }

    // ── Get local storage ─────────────────────────────────────────────
    public ToolResponse GetLocalStorage(Dictionary<string, object> args)
    {
        if (_driver == null)
        {
            return ToolResponse.Fail("Selenium WebDriver is not initialized. Call launch_browser first.");
        }

        var key = args.ContainsKey("key") ? args["key"].ToString() : null;

        if (key != null)
        {
            // Pass the key as a script argument (arguments[0]) rather than interpolating
            // it into the script text — avoids JS-string injection via a quote/backslash.
            var value = ((IJavaScriptExecutor)_driver).ExecuteScript(
                "return localStorage.getItem(arguments[0]);", key);
            return ToolResponse.Ok(new { status = "ok", key, value = value?.ToString() });
        }
        else
        {
            var allKeys = ((IJavaScriptExecutor)_driver).ExecuteScript(
                "return Object.keys(localStorage);"
            );
            return ToolResponse.Ok(new { status = "ok", keys = allKeys });
        }
    }

    // ── Set local storage ─────────────────────────────────────────────
    public ToolResponse SetLocalStorage(Dictionary<string, object> args)
    {
        if (_driver == null)
        {
            return ToolResponse.Fail("Selenium WebDriver is not initialized. Call launch_browser first.");
        }

        var key = args["key"].ToString()!;
        var value = args["value"].ToString()!;

        ((IJavaScriptExecutor)_driver).ExecuteScript(
            "localStorage.setItem(arguments[0], arguments[1]);", key, value
        );

        return ToolResponse.Ok(new { status = "ok", action = "storage_set", key });
    }

    // ── Refresh page ──────────────────────────────────────────────────
    public ToolResponse RefreshPage(Dictionary<string, object> args)
    {
        if (_driver == null)
        {
            return ToolResponse.Fail("Selenium WebDriver is not initialized. Call launch_browser first.");
        }

        _driver.Navigate().Refresh();
        WaitForPageReady(1000);
        
        return ToolResponse.Ok(new { status = "ok", action = "page_refreshed" });
    }

    // ── Go back ───────────────────────────────────────────────────────
    public ToolResponse GoBack(Dictionary<string, object> args)
    {
        if (_driver == null)
        {
            return ToolResponse.Fail("Selenium WebDriver is not initialized. Call launch_browser first.");
        }

        _driver.Navigate().Back();
        WaitForPageReady(500);

        return ToolResponse.Ok(new { status = "ok", action = "navigated_back" });
    }

    // ── Go forward ────────────────────────────────────────────────────
    public ToolResponse GoForward(Dictionary<string, object> args)
    {
        if (_driver == null)
        {
            return ToolResponse.Fail("Selenium WebDriver is not initialized. Call launch_browser first.");
        }

        _driver.Navigate().Forward();
        WaitForPageReady(500);

        return ToolResponse.Ok(new { status = "ok", action = "navigated_forward" });
    }

    // ── Get current URL ───────────────────────────────────────────────
    public ToolResponse GetCurrentUrl(Dictionary<string, object> args)
    {
        if (_driver == null)
        {
            return ToolResponse.Fail("Selenium WebDriver is not initialized. Call launch_browser first.");
        }

        return ToolResponse.Ok(new
        {
            status = "ok",
            url = _driver.Url,
            title = _driver.Title
        });
    }

    // ── Take full page screenshot ─────────────────────────────────────
    public ToolResponse TakeFullPageScreenshot(Dictionary<string, object> args)
    {
        if (_driver == null)
        {
            return ToolResponse.Fail("Selenium WebDriver is not initialized. Call launch_browser first.");
        }

        var filename = args.GetValueOrDefault("filename", "full_screenshot.png")?.ToString()
                       ?? "full_screenshot.png";

        var totalHeight = Convert.ToInt64(((IJavaScriptExecutor)_driver).ExecuteScript("return document.body.scrollHeight"));
        var viewportHeight = Convert.ToInt64(((IJavaScriptExecutor)_driver).ExecuteScript("return window.innerHeight"));

        ((IJavaScriptExecutor)_driver).ExecuteScript("window.scrollTo(0, 0);");
        WaitForPageReady(500);
        
        var screenshot = ((ITakesScreenshot)_driver).GetScreenshot();
        screenshot.SaveAsFile(filename);
        
        return ToolResponse.Ok(new
        {
            status = "ok",
            filename,
            totalHeight,
            viewportHeight
        });
    }

    // ── Get element CSS property ──────────────────────────────────────
    public ToolResponse GetCssProperty(Dictionary<string, object> args)
    {
        var element = GetElement(args);
        var property = args["property"].ToString()!;
        var value = element.GetCssValue(property);
        
        return ToolResponse.Ok(new { status = "ok", property, value });
    }

    // ── Is element selected (checkbox/radio) ──────────────────────────
    public ToolResponse IsSelected(Dictionary<string, object> args)
    {
        var element = GetElement(args);
        var selected = element.Selected;
        
        return ToolResponse.Ok(new { status = "ok", selected });
    }

    // ── Upload file ───────────────────────────────────────────────────
    public ToolResponse UploadFile(Dictionary<string, object> args)
    {
        var element = GetElement(args);
        var filePath = args["file_path"].ToString()!;
        
        if (!File.Exists(filePath))
            return ToolResponse.Fail($"File not found: {filePath}");
        
        element.SendKeys(Path.GetFullPath(filePath));
        
        return ToolResponse.Ok(new { status = "ok", action = "file_uploaded", file = filePath });
    }

    // ── Hover over element ────────────────────────────────────────────
    public ToolResponse HoverOverElement(Dictionary<string, object> args)
    {
        var element = GetElement(args);
        var actions = new OpenQA.Selenium.Interactions.Actions(_driver!);
        actions.MoveToElement(element).Perform();
        WaitForPageReady(300);
        
        return ToolResponse.Ok(new { status = "ok", action = "hover" });
    }

    // ── Close browser ─────────────────────────────────────────────────
    public ToolResponse CloseBrowser(Dictionary<string, object> args)
    {
        _driver?.Quit();
        _driver = null;
        return ToolResponse.Ok(new { status = "ok", action = "browser_closed" });
    }

    // ── Internal helpers ──────────────────────────────────────────────
    private IWebElement GetElement(Dictionary<string, object> args)
    {
        // ✅ FIX: Check if wait is initialized
        if (_wait == null || _driver == null)
        {
            throw new InvalidOperationException("Selenium WebDriver is not initialized. Call launch_browser first.");
        }

        var by = BuildBy(args);
        return _wait.Until(d => d.FindElement(by));
    }

    private IWebElement GetClickableElement(Dictionary<string, object> args)
    {
        // ✅ FIX: Check if wait is initialized
        if (_wait == null || _driver == null)
        {
            throw new InvalidOperationException("Selenium WebDriver is not initialized. Call launch_browser first.");
        }

        var by = BuildBy(args);
        return _wait.Until(SeleniumExtras.WaitHelpers.ExpectedConditions
            .ElementToBeClickable(by));
    }

    private static By BuildBy(Dictionary<string, object> args)
    {
        var strategy = args.GetValueOrDefault("strategy", "css")?.ToString() ?? "css";
        var value = args["value"].ToString()!;

        return strategy switch
        {
            "css" => By.CssSelector(value),
            "xpath" => By.XPath(value),
            "id" => By.Id(value),
            "name" => By.Name(value),
            "text" => By.PartialLinkText(value),
            "tag" => By.TagName(value),
            _ => By.CssSelector(value)
        };
    }

    public void Dispose()
    {
        _driver?.Quit();
        _driver?.Dispose();
    }
}
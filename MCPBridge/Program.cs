// McpBridge/McpBridge.cs
using McpBridge.Models;
using McpBridge.Tools;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// Register Playwright as singleton instance (maintains browser state)
builder.Services.AddSingleton(sp => PlaywrightToolHandler.Instance);

// Register fallback engines
builder.Services.AddSingleton<SeleniumToolHandler>();
builder.Services.AddSingleton<RestSharpToolHandler>();

// Register ExecutionRouter for intelligent fallback
builder.Services.AddSingleton<ExecutionRouter>();

// Register other tool handlers
builder.Services.AddSingleton<SpecFlowToolHandler>();
builder.Services.AddSingleton<AzureDevOpsToolHandler>();
builder.Services.AddSingleton<PerformanceToolHandler>();
builder.Services.AddSingleton<TestDataToolHandler>();
builder.Services.AddSingleton<DatabaseToolHandler>();
builder.Services.AddSingleton<ImageToolHandler>();
builder.Services.AddSingleton<ReportingToolHandler>();
builder.Services.AddSingleton<SolutionReaderToolHandler>();
builder.Services.AddSingleton<SolutionWriterToolHandler>();

var app = builder.Build();

// Guards concurrent /execute calls: the tool handlers above are registered as
// singletons holding mutable state (a single shared WebDriver/Page/results list).
// Serializing execution prevents two concurrent tool calls from corrupting a
// shared browser session or results collection. Test-automation tool calls are
// typically driven by one agent/session at a time, so this is a pragmatic fix
// rather than full multi-tenant isolation.
var executionLock = new SemaphoreSlim(1, 1);

// ── Health check ─────────────────────────────────────────────────────
app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "McpBridge" }));

// ── Tool list (for MCP server discovery) ─────────────────────────────
app.MapGet("/tools", () => Results.Ok(new object[]
{
    // Playwright tools (Primary) - UI Automation
    new { name = "launch_browser",      category = "playwright", fallback = "selenium" },
    new { name = "close_browser",       category = "playwright", fallback = "selenium" },
    new { name = "navigate_to",         category = "playwright", fallback = "selenium" },
    new { name = "click_element",       category = "playwright", fallback = "selenium" },
    new { name = "fill_input",          category = "playwright", fallback = "selenium" },
    new { name = "select_option",       category = "playwright", fallback = "selenium" },
    new { name = "press_key",           category = "playwright", fallback = "selenium" },
    new { name = "hover_element",       category = "playwright", fallback = "selenium" },
    new { name = "take_screenshot",     category = "playwright", fallback = "selenium" },
    new { name = "wait_for_element",    category = "playwright", fallback = "selenium" },
    new { name = "scroll_to_element",   category = "playwright", fallback = "selenium" },
    new { name = "assert_text_visible", category = "playwright", fallback = "selenium" },
    new { name = "assert_element_visible", category = "playwright", fallback = "selenium" },
    new { name = "assert_element_hidden", category = "playwright", fallback = "selenium" },
    new { name = "assert_page_title",   category = "playwright", fallback = "selenium" },
    new { name = "assert_url_contains", category = "playwright", fallback = "selenium" },
    new { name = "assert_input_value",  category = "playwright", fallback = "selenium" },
    new { name = "assert_element_count", category = "playwright", fallback = "selenium" },
    
    // ── NEW: Extended UI Actions (Comprehensive Self-Healing Coverage) ──
    new { name = "double_click_element",     category = "playwright", fallback = "selenium" },
    new { name = "right_click_element",      category = "playwright", fallback = "selenium" },
    new { name = "check_element",            category = "playwright", fallback = "selenium" },
    new { name = "uncheck_element",          category = "playwright", fallback = "selenium" },
    new { name = "set_checked_state",        category = "playwright", fallback = "selenium" },
    new { name = "type_text",                category = "playwright", fallback = "selenium" },
    new { name = "get_element_text",         category = "playwright", fallback = "selenium" },
    new { name = "get_input_value",          category = "playwright", fallback = "selenium" },
    new { name = "get_element_attribute",    category = "playwright", fallback = "selenium" },
    new { name = "is_element_visible",       category = "playwright", fallback = "none" },
    new { name = "is_element_enabled",       category = "playwright", fallback = "none" },
    new { name = "is_element_checked",       category = "playwright", fallback = "none" },
    new { name = "select_option_by_label",   category = "playwright", fallback = "selenium" },
    new { name = "drag_to_element",          category = "playwright", fallback = "selenium" },
    new { name = "screenshot_element",       category = "playwright", fallback = "selenium" },
    new { name = "wait_for_element_hidden",  category = "playwright", fallback = "selenium" },
    
    // Playwright tools - API Testing
    new { name = "api_request",         category = "playwright", fallback = "restsharp" },
    new { name = "assert_response_status", category = "playwright", fallback = "restsharp" },
    new { name = "assert_response_body_contains", category = "playwright", fallback = "restsharp" },
    new { name = "assert_json_path",    category = "playwright", fallback = "restsharp" },
    new { name = "assert_response_header", category = "playwright", fallback = "restsharp" },
    
    // Selenium tools - Basic (Fallback)
    new { name = "navigate",            category = "selenium" },
    new { name = "find_element",        category = "selenium" },
    new { name = "click",               category = "selenium" },
    new { name = "click_by_text",       category = "selenium" },
    new { name = "type_text",           category = "selenium" },
    new { name = "get_text",            category = "selenium" },
    new { name = "get_attribute",       category = "selenium" },
    new { name = "assert_visible",      category = "selenium" },
    new { name = "assert_text_contains",category = "selenium" },
    new { name = "execute_script",      category = "selenium" },
    new { name = "get_page_info",       category = "selenium" },
    new { name = "select_dropdown",     category = "selenium" },
    new { name = "scroll_to_element",   category = "selenium" },
    new { name = "scroll_page",         category = "selenium" },
    new { name = "hover_element",       category = "selenium" },
    new { name = "double_click",        category = "selenium" },
    new { name = "right_click",         category = "selenium" },
    new { name = "clear_and_type",      category = "selenium" },
    new { name = "press_key",           category = "selenium" },
    new { name = "switch_to_frame",     category = "selenium" },
    new { name = "switch_to_default_content", category = "selenium" },
    new { name = "switch_to_window",    category = "selenium" },
    new { name = "get_window_handles",  category = "selenium" },
    new { name = "close_window",        category = "selenium" },
    new { name = "handle_alert",        category = "selenium" },
    new { name = "get_cookies",         category = "selenium" },
    new { name = "add_cookie",          category = "selenium" },
    new { name = "delete_cookie",       category = "selenium" },
    new { name = "get_local_storage",   category = "selenium" },
    new { name = "set_local_storage",   category = "selenium" },
    new { name = "refresh_page",        category = "selenium" },
    new { name = "go_back",             category = "selenium" },
    new { name = "go_forward",          category = "selenium" },
    new { name = "get_current_url",     category = "selenium" },
    new { name = "take_full_page_screenshot", category = "selenium" },
    new { name = "get_css_property",    category = "selenium" },
    new { name = "is_selected",         category = "selenium" },
    new { name = "upload_file",         category = "selenium" },
    
    // RestSharp tools
    new { name = "configure_api",       category = "api" },
    new { name = "api_get",             category = "api" },
    new { name = "api_post",            category = "api" },
    new { name = "api_put",             category = "api" },
    new { name = "api_delete",          category = "api" },
    new { name = "assert_status_code",  category = "api" },
    new { name = "assert_json_path",    category = "api" },
    
    // SpecFlow tools
    new { name = "run_feature",         category = "specflow" },
    new { name = "run_scenario",        category = "specflow" },
    new { name = "list_scenarios",      category = "specflow" },
    new { name = "parse_last_results",  category = "specflow" },
    
    // Azure DevOps tools
    new { name = "configure_azure_devops",   category = "azuredevops" },
    new { name = "get_user_story",           category = "azuredevops" },
    new { name = "get_active_user_stories",  category = "azuredevops" },
    new { name = "get_work_items_by_query",  category = "azuredevops" },
    new { name = "get_user_stories_by_iteration", category = "azuredevops" },
    new { name = "get_user_stories_by_tag",  category = "azuredevops" },
    new { name = "generate_test_scenarios",  category = "azuredevops" },
    
    // Performance tools (NBomber)
    new { name = "configure_performance_test", category = "performance" },
    new { name = "run_load_test",             category = "performance" },
    new { name = "run_stress_test",           category = "performance" },
    new { name = "run_spike_test",            category = "performance" },
    new { name = "get_performance_summary",    category = "performance" },
    new { name = "export_performance_report", category = "performance" },

    // Test Data tools (Bogus)
    new { name = "generate_person_data",      category = "testdata" },
    new { name = "generate_user_data",        category = "testdata" },
    new { name = "generate_product_data",     category = "testdata" },
    new { name = "generate_order_data",       category = "testdata" },
    new { name = "generate_financial_data",   category = "testdata" },
    new { name = "generate_custom_data",      category = "testdata" },
    new { name = "generate_batch_test_data",  category = "testdata" },
    
    // Database tools (SqlClient)
    new { name = "configure_database",        category = "database" },
    new { name = "execute_query",             category = "database" },
    new { name = "execute_non_query",         category = "database" },
    new { name = "execute_scalar",            category = "database" },
    new { name = "verify_data_exists",        category = "database" },
    new { name = "insert_test_data",          category = "database" },
    new { name = "delete_test_data",          category = "database" },
    new { name = "get_table_schema",          category = "database" },
    new { name = "execute_stored_procedure",  category = "database" },
    new { name = "backup_test_data",          category = "database" },
    
    // Image tools (Magick.NET)
    new { name = "compare_images",            category = "image" },
    new { name = "resize_image",              category = "image" },
    new { name = "crop_image",                category = "image" },
    new { name = "annotate_image",            category = "image" },
    new { name = "convert_image_format",      category = "image" },
    new { name = "get_image_properties",      category = "image" },
    new { name = "create_screenshot_comparison", category = "image" },
    new { name = "batch_compare_screenshots", category = "image" },
    
    // Reporting tools (Allure)
    new { name = "configure_reporting",       category = "reporting" },
    new { name = "start_test",                category = "reporting" },
    new { name = "log_test_step",             category = "reporting" },
    new { name = "attach_screenshot",         category = "reporting" },
    new { name = "end_test",                  category = "reporting" },
    new { name = "generate_report",           category = "reporting" },
    new { name = "get_test_statistics",       category = "reporting" },
    new { name = "export_to_html",            category = "reporting" },
    new { name = "clear_results",             category = "reporting" },
    new { name = "generate_allure_report",    category = "reporting" },
    new { name = "open_allure_report",        category = "reporting" },
    
    // Solution Reader/Writer tools
    new { name = "scan_solution",             category = "solution" },
    new { name = "read_feature_files",        category = "solution" },
    new { name = "read_step_definitions",     category = "solution" },
    new { name = "read_page_objects",         category = "solution" },
    new { name = "write_feature_file",        category = "solution" },
    new { name = "write_step_definition",     category = "solution" },
    new { name = "write_page_object",         category = "solution" },
}));

// ── Main dispatch endpoint ────────────────────────────────────────────
app.MapPost("/execute", async (HttpRequest httpRequest,
    ExecutionRouter router,
    SeleniumToolHandler selenium,
    RestSharpToolHandler api,
    SpecFlowToolHandler specflow,
    AzureDevOpsToolHandler azureDevOps,
    PerformanceToolHandler performance,
    TestDataToolHandler testData,
    DatabaseToolHandler database,
    ImageToolHandler image,
    SolutionReaderToolHandler reader,
    SolutionWriterToolHandler writer,
    ReportingToolHandler reporting) =>
{
    ToolRequest? request;
    try
    {
        request = await JsonSerializer.DeserializeAsync<ToolRequest>(
            httpRequest.Body,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(ToolResponse.Fail($"Invalid request JSON: {ex.Message}"));
    }

    if (request == null)
        return Results.BadRequest(ToolResponse.Fail("Empty request"));

    ToolResponse response;

    await executionLock.WaitAsync();
    try
    {
        response = request.ToolName switch
        {
            // Playwright tools - Routed through ExecutionRouter with fallback
            "launch_browser" or "close_browser" or "navigate_to" or "navigate" or "click_element" or
            "fill_input" or "select_option" or "press_key" or "hover_element" or "click" or
            "take_screenshot" or "wait_for_element" or "scroll_to_element" or "type_text" or
            "assert_text_visible" or "assert_element_visible" or "assert_element_hidden" or
            "assert_page_title" or "assert_url_contains" or "assert_input_value" or
            "assert_element_count" or "api_request" or "assert_response_status" or
            "assert_response_body_contains" or "assert_json_path" or "assert_response_header" or
            // Extended UI Actions — previously advertised in /tools but never dispatched
            "double_click_element" or "right_click_element" or "check_element" or "uncheck_element" or
            "set_checked_state" or "get_element_text" or "get_input_value" or "get_element_attribute" or
            "is_element_visible" or "is_element_enabled" or "is_element_checked" or
            "select_option_by_label" or "drag_to_element" or "screenshot_element" or "wait_for_element_hidden"
                => await router.ExecuteAsync(request.ToolName, request.Arguments),

            // Selenium - Basic (Direct, for legacy/specific use - bypass router)
            "find_element" => selenium.FindElement(request.Arguments),
            "click_by_text" => selenium.ClickByText(request.Arguments),
            "get_text" => selenium.GetText(request.Arguments),
            "get_attribute" => selenium.GetAttribute(request.Arguments),
            "assert_visible" => selenium.AssertVisible(request.Arguments),
            "assert_text_contains" => selenium.AssertTextContains(request.Arguments),
            "execute_script" => selenium.ExecuteScript(request.Arguments),
            "get_page_info" => selenium.GetPageInfo(request.Arguments),
            "select_dropdown" => selenium.SelectDropdown(request.Arguments),

            // Selenium - Enhanced
            "wait_for_visible" => selenium.WaitForVisible(request.Arguments),
            "wait_for_text" => selenium.WaitForText(request.Arguments),
            "find_elements" => selenium.FindElements(request.Arguments),
            "count_elements" => selenium.CountElements(request.Arguments),
            "element_exists" => selenium.ElementExists(request.Arguments),
            "scroll_page" => selenium.ScrollPage(request.Arguments),
            "double_click" => selenium.DoubleClick(request.Arguments),
            "right_click" => selenium.RightClick(request.Arguments),
            "clear_and_type" => selenium.ClearAndType(request.Arguments),
            "switch_to_frame" => selenium.SwitchToFrame(request.Arguments),
            "switch_to_default_content" => selenium.SwitchToDefaultContent(request.Arguments),
            "switch_to_window" => selenium.SwitchToWindow(request.Arguments),
            "get_window_handles" => selenium.GetWindowHandles(request.Arguments),
            "close_window" => selenium.CloseWindow(request.Arguments),
            "handle_alert" => selenium.HandleAlert(request.Arguments),
            "get_cookies" => selenium.GetCookies(request.Arguments),
            "add_cookie" => selenium.AddCookie(request.Arguments),
            "delete_cookie" => selenium.DeleteCookie(request.Arguments),
            "get_local_storage" => selenium.GetLocalStorage(request.Arguments),
            "set_local_storage" => selenium.SetLocalStorage(request.Arguments),
            "refresh_page" => selenium.RefreshPage(request.Arguments),
            "go_back" => selenium.GoBack(request.Arguments),
            "go_forward" => selenium.GoForward(request.Arguments),
            "get_current_url" => selenium.GetCurrentUrl(request.Arguments),
            "take_full_page_screenshot" => selenium.TakeFullPageScreenshot(request.Arguments),
            "get_css_property" => selenium.GetCssProperty(request.Arguments),
            "is_selected" => selenium.IsSelected(request.Arguments),
            "upload_file" => selenium.UploadFile(request.Arguments),

            // RestSharp / API
            "configure_api" => api.ConfigureApi(request.Arguments),
            "api_get" => api.Get(request.Arguments),
            "api_post" => api.Post(request.Arguments),
            "api_put" => api.Put(request.Arguments),
            "api_delete" => api.Delete(request.Arguments),
            "assert_status_code" => api.AssertStatusCode(request.Arguments),

            // SpecFlow / ReqNroll
            "run_feature" => specflow.RunFeature(request.Arguments),
            "run_scenario" => specflow.RunScenario(request.Arguments),
            "list_scenarios" => specflow.ListScenarios(request.Arguments),
            "parse_last_results" => specflow.ParseLastResults(request.Arguments),

            // Azure DevOps
            "configure_azure_devops" => azureDevOps.ConfigureAzureDevOps(request.Arguments),
            "get_user_story" => await azureDevOps.GetUserStoryAsync(request.Arguments),
            "get_active_user_stories" => await azureDevOps.GetActiveUserStoriesAsync(request.Arguments),
            "get_work_items_by_query" => await azureDevOps.GetWorkItemsByQueryAsync(request.Arguments),
            "get_user_stories_by_iteration" => await azureDevOps.GetUserStoriesByIterationAsync(request.Arguments),
            "get_user_stories_by_tag" => await azureDevOps.GetUserStoriesByTagAsync(request.Arguments),
            "generate_test_scenarios" => await azureDevOps.GenerateTestScenariosAsync(request.Arguments),

            // Performance (NBomber)
            "configure_performance_test" => performance.ConfigurePerformanceTest(request.Arguments),
            "run_load_test" => performance.RunLoadTest(request.Arguments),
            "run_stress_test" => performance.RunStressTest(request.Arguments),
            "run_spike_test"          => performance.RunSpikeTest(request.Arguments),
            "get_performance_summary"   => performance.GetPerformanceSummary(request.Arguments),
            "export_performance_report" => performance.ExportPerformanceReport(request.Arguments),

            // Test Data (Bogus)
            "generate_person_data" => testData.GeneratePersonData(request.Arguments),
            "generate_user_data" => testData.GenerateUserData(request.Arguments),
            "generate_product_data" => testData.GenerateProductData(request.Arguments),
            "generate_order_data" => testData.GenerateOrderData(request.Arguments),
            "generate_financial_data" => testData.GenerateFinancialData(request.Arguments),
            "generate_custom_data" => testData.GenerateCustomData(request.Arguments),
            "generate_batch_test_data" => testData.GenerateBatchTestData(request.Arguments),

            // Database (SqlClient)
            "configure_database" => database.ConfigureDatabase(request.Arguments),
            "execute_query" => database.ExecuteQuery(request.Arguments),
            "execute_non_query" => database.ExecuteNonQuery(request.Arguments),
            "execute_scalar" => database.ExecuteScalar(request.Arguments),
            "verify_data_exists" => database.VerifyDataExists(request.Arguments),
            "insert_test_data" => database.InsertTestData(request.Arguments),
            "delete_test_data" => database.DeleteTestData(request.Arguments),
            "get_table_schema" => database.GetTableSchema(request.Arguments),
            "execute_stored_procedure" => database.ExecuteStoredProcedure(request.Arguments),
            "backup_test_data" => database.BackupTestData(request.Arguments),

            // Image (Magick.NET)
            "compare_images" => image.CompareImages(request.Arguments),
            "resize_image" => image.ResizeImage(request.Arguments),
            "crop_image" => image.CropImage(request.Arguments),
            "annotate_image" => image.AnnotateImage(request.Arguments),
            "convert_image_format" => image.ConvertImageFormat(request.Arguments),
            "get_image_properties" => image.GetImageProperties(request.Arguments),
            "create_screenshot_comparison" => image.CreateScreenshotComparison(request.Arguments),
            "batch_compare_screenshots" => image.BatchCompareScreenshots(request.Arguments),

            // Reporting (Allure)
            "configure_reporting" => reporting.ConfigureReporting(request.Arguments),
            "start_test" => reporting.StartTest(request.Arguments),
            "log_test_step" => reporting.LogTestStep(request.Arguments),
            "attach_screenshot" => reporting.AttachScreenshot(request.Arguments),
            "end_test" => reporting.EndTest(request.Arguments),
            "generate_report" => reporting.GenerateReport(request.Arguments),
            "get_test_statistics" => reporting.GetTestStatistics(request.Arguments),
            "export_to_html" => reporting.ExportToHtml(request.Arguments),
            "clear_results" => reporting.ClearResults(request.Arguments),
            "generate_allure_report" => reporting.GenerateAllureReport(request.Arguments),
            "open_allure_report" => reporting.OpenAllureReport(request.Arguments),

            // Solution Reader
            "scan_solution" => reader.ScanSolution(request.Arguments),
            "read_feature_files" => reader.ReadFeatureFiles(request.Arguments),
            "read_step_definitions" => reader.ReadStepDefinitions(request.Arguments),
            "read_page_objects" => reader.ReadPageObjects(request.Arguments),
            "read_file" => reader.ReadFile(request.Arguments),
            "read_api_clients" => reader.ReadApiClients(request.Arguments),
            "read_config" => reader.ReadConfig(request.Arguments),
            "search_in_solution" => reader.SearchInSolution(request.Arguments),
            "get_project_structure" => reader.GetProjectStructure(request.Arguments),

            // Solution Writer
            "write_feature_file" => writer.WriteFeatureFile(request.Arguments),
            "write_step_definition" => writer.WriteStepDefinition(request.Arguments),
            "write_page_object" => writer.WritePageObject(request.Arguments),
            "write_class_file" => writer.WriteClassFile(request.Arguments),
            "append_to_step_def" => writer.AppendToStepDefinition(request.Arguments),
            "scaffold_feature" => writer.ScaffoldFeature(request.Arguments),
            // Playwright Solution Writer
            "write_playwright_feature" => writer.WritePlaywrightFeatureFile(request.Arguments),
            "write_playwright_steps"   => writer.WritePlaywrightStepDefinition(request.Arguments),
            "write_playwright_page_object" => writer.WritePlaywrightPageObject(request.Arguments),
            "scaffold_playwright_feature"   => writer.ScaffoldPlaywrightFeature(request.Arguments),

            _ => ToolResponse.Fail($"Unknown tool: {request.ToolName}")
        };
    }
    catch (Exception ex)
    {
        // Log full detail server-side only — stack traces/internal paths must
        // never be returned to the calling MCP client (information disclosure).
        app.Logger.LogError(ex, "Tool execution error for '{ToolName}'", request.ToolName);
        response = ToolResponse.Fail($"Tool execution error: {ex.Message}");
    }
    finally
    {
        executionLock.Release();
    }

    return response.Success
        ? Results.Ok(response)
        : Results.UnprocessableEntity(response);
});

app.Run("http://localhost:5555");


// MCPBridge/Tools/AzureDevOpsToolHandler.cs
using McpBridge.Models;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace McpBridge.Tools;

public class AzureDevOpsToolHandler
{
    private readonly HttpClient _httpClient;
    private string? _organization;
    private string? _project;
    private string? _pat;
    private string? _baseUrl;

    public AzureDevOpsToolHandler()
    {
        _httpClient = new HttpClient();
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
    }

    /// <summary>
    /// Escapes a value for safe interpolation into a WIQL string literal.
    /// WIQL uses the same '' escaping convention as SQL string literals — the
    /// Azure DevOps WIQL REST endpoint takes a raw query string with no
    /// parameterized-query support, so this is the only injection guard available.
    /// </summary>
    private static string EscapeWiqlLiteral(string value) => value.Replace("'", "''");

    public ToolResponse ConfigureAzureDevOps(Dictionary<string, object> args)
    {
        _organization = args["organization"].ToString()!;
        _project = args["project"].ToString()!;
        _pat = args.ContainsKey("pat") ? args["pat"].ToString() : Environment.GetEnvironmentVariable("AZURE_DEVOPS_PAT");

        if (string.IsNullOrEmpty(_pat))
            return ToolResponse.Fail("Azure DevOps PAT not provided. Set AZURE_DEVOPS_PAT environment variable or pass 'pat' parameter.");

        _baseUrl = $"https://dev.azure.com/{Uri.EscapeDataString(_organization)}/{Uri.EscapeDataString(_project)}/_apis";

        var authToken = Convert.ToBase64String(Encoding.ASCII.GetBytes($":{_pat}"));
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authToken);
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        return ToolResponse.Ok(new
        {
            status = "ok",
            organization = _organization,
            project = _project,
            baseUrl = _baseUrl
        });
    }

    public async Task<ToolResponse> GetWorkItemsByQueryAsync(Dictionary<string, object> args)
    {
        EnsureConfigured();

        var wiql = args.ContainsKey("wiql")
            ? args["wiql"].ToString()!
            : "SELECT [System.Id], [System.Title], [System.State], [System.Description] FROM WorkItems WHERE [System.WorkItemType] = 'User Story' AND [System.State] <> 'Closed' ORDER BY [System.ChangedDate] DESC";

        var queryUrl = $"{_baseUrl}/wit/wiql?api-version=7.0";

        var queryBody = new { query = wiql };
        var content = new StringContent(JsonSerializer.Serialize(queryBody), Encoding.UTF8, "application/json");

        try
        {
            var response = await _httpClient.PostAsync(queryUrl, content);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                return ToolResponse.Fail($"Failed to query work items: {response.StatusCode} - {responseBody}");

            var queryResult = JsonSerializer.Deserialize<JsonElement>(responseBody);
            var workItemRefs = queryResult.GetProperty("workItems");

            var workItemIds = new List<int>();
            foreach (var item in workItemRefs.EnumerateArray())
            {
                workItemIds.Add(item.GetProperty("id").GetInt32());
            }

            if (workItemIds.Count == 0)
                return ToolResponse.Ok(new { status = "ok", count = 0, workItems = new List<object>() });

            var ids = string.Join(",", workItemIds.Take(50));
            var detailsUrl = $"{_baseUrl}/wit/workitems?ids={ids}&api-version=7.0";

            var detailsResponse = await _httpClient.GetAsync(detailsUrl);
            var detailsBody = await detailsResponse.Content.ReadAsStringAsync();

            if (!detailsResponse.IsSuccessStatusCode)
                return ToolResponse.Fail($"Failed to get work item details: {detailsResponse.StatusCode}");

            var detailsResult = JsonSerializer.Deserialize<JsonElement>(detailsBody);
            var workItems = detailsResult.GetProperty("value");

            var items = new List<object>();
            foreach (var wi in workItems.EnumerateArray())
            {
                var fields = wi.GetProperty("fields");
                items.Add(new
                {
                    id = wi.GetProperty("id").GetInt32(),
                    title = fields.GetProperty("System.Title").GetString(),
                    state = fields.GetProperty("System.State").GetString(),
                    workItemType = fields.GetProperty("System.WorkItemType").GetString(),
                    assignedTo = fields.TryGetProperty("System.AssignedTo", out var assignee) 
                        ? (assignee.ValueKind == JsonValueKind.Object ? assignee.GetProperty("displayName").GetString() : null)
                        : null,
                    description = fields.TryGetProperty("System.Description", out var desc) 
                        ? StripHtml(desc.GetString()) 
                        : null,
                    acceptanceCriteria = fields.TryGetProperty("Microsoft.VSTS.Common.AcceptanceCriteria", out var ac) 
                        ? StripHtml(ac.GetString()) 
                        : null,
                    tags = fields.TryGetProperty("System.Tags", out var tags) 
                        ? tags.GetString() 
                        : null
                });
            }

            return ToolResponse.Ok(new { status = "ok", count = items.Count, workItems = items });
        }
        catch (Exception ex)
        {
            return ToolResponse.Fail($"Error querying Azure DevOps: {ex.Message}");
        }
    }

    public async Task<ToolResponse> GetUserStoryAsync(Dictionary<string, object> args)
    {
        EnsureConfigured();

        var workItemId = int.Parse(args["id"].ToString()!);
        var url = $"{_baseUrl}/wit/workitems/{workItemId}?api-version=7.0";

        try
        {
            var response = await _httpClient.GetAsync(url);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                return ToolResponse.Fail($"Failed to get work item: {response.StatusCode}");

            var result = JsonSerializer.Deserialize<JsonElement>(responseBody);
            var fields = result.GetProperty("fields");

            var userStory = new
            {
                id = result.GetProperty("id").GetInt32(),
                title = fields.GetProperty("System.Title").GetString(),
                state = fields.GetProperty("System.State").GetString(),
                workItemType = fields.GetProperty("System.WorkItemType").GetString(),
                description = fields.TryGetProperty("System.Description", out var desc) 
                    ? StripHtml(desc.GetString()) 
                    : null,
                acceptanceCriteria = fields.TryGetProperty("Microsoft.VSTS.Common.AcceptanceCriteria", out var ac) 
                    ? StripHtml(ac.GetString()) 
                    : null,
                assignedTo = fields.TryGetProperty("System.AssignedTo", out var assignee) 
                    ? (assignee.ValueKind == JsonValueKind.Object ? assignee.GetProperty("displayName").GetString() : null)
                    : null,
                createdDate = fields.GetProperty("System.CreatedDate").GetString(),
                changedDate = fields.GetProperty("System.ChangedDate").GetString(),
                tags = fields.TryGetProperty("System.Tags", out var tags) 
                    ? tags.GetString() 
                    : null,
                priority = fields.TryGetProperty("Microsoft.VSTS.Common.Priority", out var priority) 
                    ? priority.GetInt32() 
                    : 0
            };

            return ToolResponse.Ok(new { status = "ok", userStory });
        }
        catch (Exception ex)
        {
            return ToolResponse.Fail($"Error getting user story: {ex.Message}");
        }
    }

    public async Task<ToolResponse> GetUserStoriesByIterationAsync(Dictionary<string, object> args)
    {
        EnsureConfigured();

        var iterationPath = EscapeWiqlLiteral(args["iteration_path"].ToString()!);

        var wiql = $@"
            SELECT [System.Id], [System.Title], [System.State], [System.Description]
            FROM WorkItems
            WHERE [System.WorkItemType] = 'User Story'
            AND [System.IterationPath] = '{iterationPath}'
            ORDER BY [Microsoft.VSTS.Common.Priority] ASC, [System.ChangedDate] DESC";

        return await GetWorkItemsByQueryAsync(new Dictionary<string, object> { { "wiql", wiql } });
    }

    public async Task<ToolResponse> GetUserStoriesByTagAsync(Dictionary<string, object> args)
    {
        EnsureConfigured();

        var tag = EscapeWiqlLiteral(args["tag"].ToString()!);

        var wiql = $@"
            SELECT [System.Id], [System.Title], [System.State]
            FROM WorkItems
            WHERE [System.WorkItemType] = 'User Story'
            AND [System.Tags] CONTAINS '{tag}'
            ORDER BY [System.ChangedDate] DESC";

        return await GetWorkItemsByQueryAsync(new Dictionary<string, object> { { "wiql", wiql } });
    }

    public async Task<ToolResponse> GetActiveUserStoriesAsync(Dictionary<string, object> args)
    {
        EnsureConfigured();

        var wiql = @"
            SELECT [System.Id], [System.Title], [System.State], [System.Description]
            FROM WorkItems
            WHERE [System.WorkItemType] = 'User Story'
            AND [System.State] IN ('New', 'Active', 'Committed', 'In Progress')
            ORDER BY [Microsoft.VSTS.Common.Priority] ASC, [System.ChangedDate] DESC";

        return await GetWorkItemsByQueryAsync(new Dictionary<string, object> { { "wiql", wiql } });
    }

    public async Task<ToolResponse> GenerateTestScenariosAsync(Dictionary<string, object> args)
    {
        var userStoryId = int.Parse(args["id"].ToString()!);
        
        var userStoryResponse = await GetUserStoryAsync(new Dictionary<string, object> { { "id", userStoryId } });
        
        if (!userStoryResponse.Success)
            return userStoryResponse;

        var resultJson = JsonSerializer.Serialize(userStoryResponse.Result);
        var resultDict = JsonSerializer.Deserialize<Dictionary<string, object>>(resultJson)!;
        var userStoryJson = resultDict["userStory"].ToString()!;
        var userStory = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(userStoryJson)!;

        var title = userStory["title"].GetString();
        var description = userStory["description"].GetString();
        var acceptanceCriteria = userStory["acceptanceCriteria"].GetString();

        var sanitizedTitle = SanitizeFilename(title);
        var scenarios = new
        {
            userStoryId,
            userStoryTitle = title,
            description,
            acceptanceCriteria,
            suggestedFeatureFile = $"US{userStoryId}_{sanitizedTitle}.feature",
            gherkinTemplate = $@"Feature: {title}
  User Story: US-{userStoryId}
  As a user
  I want to {description}
  So that I can achieve business value

Background:
  Given the system is ready for testing

Scenario: {title} - Happy Path
  Given the preconditions are met
  When I perform the main action
  Then the expected result is achieved
  And the acceptance criteria are satisfied

Scenario: {title} - Negative Test
  Given invalid conditions exist
  When I attempt the action
  Then an appropriate error is shown

# Acceptance Criteria:
# {acceptanceCriteria}
",
            testCases = new List<object>
            {
                new { scenario = "Happy Path", priority = "High", status = "To Do" },
                new { scenario = "Negative Testing", priority = "Medium", status = "To Do" },
                new { scenario = "Boundary Conditions", priority = "Medium", status = "To Do" },
                new { scenario = "Error Handling", priority = "Low", status = "To Do" }
            },
            suggestedStepDefinitions = new List<string>
            {
                "[Given(@\"the system is ready for testing\")]",
                "[When(@\"I perform the main action\")]",
                "[Then(@\"the expected result is achieved\")]",
                "[Then(@\"the acceptance criteria are satisfied\")]"
            }
        };

        return ToolResponse.Ok(new { status = "ok", scenarios });
    }

    private void EnsureConfigured()
    {
        if (string.IsNullOrEmpty(_baseUrl))
            throw new InvalidOperationException("Azure DevOps not configured. Call configure_azure_devops first.");
    }

    private static string? StripHtml(string? html)
    {
        if (string.IsNullOrEmpty(html))
            return html;

        return System.Text.RegularExpressions.Regex.Replace(html, "<.*?>", string.Empty)
            .Replace("&nbsp;", " ")
            .Replace("&amp;", "&")
            .Replace("&lt;", "<")
            .Replace("&gt;", ">")
            .Replace("&quot;", "\"")
            .Trim();
    }

    private static string SanitizeFilename(string? filename)
    {
        if (string.IsNullOrEmpty(filename))
            return "UnknownFeature";

        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(filename
            .Where(c => !invalid.Contains(c))
            .ToArray())
            .Replace(" ", "_");
        
        return sanitized.Length > 50 ? sanitized.Substring(0, 50) : sanitized;
    }
}

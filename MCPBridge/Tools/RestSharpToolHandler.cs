// McpBridge/Tools/RestSharpToolHandler.cs
using RestSharp;
using McpBridge.Models;
using System.Text.Json;

namespace McpBridge.Tools;

public class RestSharpToolHandler
{
    private RestClient? _client;
    private string _baseUrl = string.Empty;

    // ── Configure base URL ────────────────────────────────────────────
    public ToolResponse ConfigureApi(Dictionary<string, object> args)
    {
        _baseUrl = args["base_url"].ToString()!;
        _client = new RestClient(_baseUrl);
        return ToolResponse.Ok(new { status = "ok", baseUrl = _baseUrl });
    }

    // ── GET request ───────────────────────────────────────────────────
    public ToolResponse Get(Dictionary<string, object> args)
    {
        if (_client == null)
            return ToolResponse.Fail("API not configured — call configure_api first.");

        var endpoint = args["endpoint"].ToString()!;
        var request = new RestRequest(endpoint, Method.Get);
        AddHeaders(request, args);

        var response = _client.Execute(request);
        return BuildResponse(response);
    }

    // ── POST request ──────────────────────────────────────────────────
    public ToolResponse Post(Dictionary<string, object> args)
    {
        if (_client == null)
            return ToolResponse.Fail("API not configured — call configure_api first.");

        var endpoint = args["endpoint"].ToString()!;
        var request = new RestRequest(endpoint, Method.Post);
        AddHeaders(request, args);

        if (args.TryGetValue("body", out var body))
            request.AddJsonBody(body);

        var response = _client.Execute(request);
        return BuildResponse(response);
    }

    // ── PUT request ───────────────────────────────────────────────────
    public ToolResponse Put(Dictionary<string, object> args)
    {
        if (_client == null)
            return ToolResponse.Fail("API not configured — call configure_api first.");

        var endpoint = args["endpoint"].ToString()!;
        var request = new RestRequest(endpoint, Method.Put);
        AddHeaders(request, args);

        if (args.TryGetValue("body", out var body))
            request.AddJsonBody(body);

        var response = _client.Execute(request);
        return BuildResponse(response);
    }

    // ── DELETE request ────────────────────────────────────────────────
    public ToolResponse Delete(Dictionary<string, object> args)
    {
        if (_client == null)
            return ToolResponse.Fail("API not configured — call configure_api first.");

        var endpoint = args["endpoint"].ToString()!;
        var request = new RestRequest(endpoint, Method.Delete);
        AddHeaders(request, args);

        var response = _client.Execute(request);
        return BuildResponse(response);
    }

    // ── Assert status code ────────────────────────────────────────────
    public ToolResponse AssertStatusCode(Dictionary<string, object> args)
    {
        // Used after a request — caller stores last response
        var expected = int.Parse(args["expected_status"].ToString()!);
        var actual = (int)(_lastResponse?.StatusCode ?? 0);

        if (actual != expected)
            return ToolResponse.Fail(
                $"Expected HTTP {expected} but got {actual}. Body: {_lastResponseBody}");

        return ToolResponse.Ok(new
        {
            status = "passed",
            assertion = "status_code",
            expected,
            actual
        });
    }

    // ── Assert JSON path ──────────────────────────────────────────────
    public ToolResponse AssertJsonPath(Dictionary<string, object> args)
    {
        var path = args["json_path"].ToString()!;
        var expected = args["expected_value"].ToString()!;

        if (_lastResponseBody == null)
            return ToolResponse.Fail("No previous response body to assert against");

        var doc = JsonDocument.Parse(_lastResponseBody);
        var pathParts = path.TrimStart('$', '.').Split('.');
        JsonElement current = doc.RootElement;

        foreach (var part in pathParts)
        {
            if (!current.TryGetProperty(part, out current))
                return ToolResponse.Fail($"JSON path '{path}' not found in response");
        }

        var actual = current.ToString();
        if (!actual.Contains(expected))
            return ToolResponse.Fail(
                $"JSON path '{path}': expected '{expected}', got '{actual}'");

        return ToolResponse.Ok(new
        {
            status = "passed",
            assertion = "json_path",
            path,
            expected,
            actual
        });
    }

    // ── Assert response body contains ─────────────────────────────────
    public ToolResponse AssertResponseBodyContains(Dictionary<string, object> args)
    {
        var expected = args["text"].ToString()!;

        if (_lastResponseBody == null)
            return ToolResponse.Fail("No previous response body to assert against");

        if (!_lastResponseBody.Contains(expected, StringComparison.OrdinalIgnoreCase))
            return ToolResponse.Fail(
                $"Response body does not contain '{expected}'. Body: {_lastResponseBody}");

        return ToolResponse.Ok(new
        {
            status = "passed",
            assertion = "response_body_contains",
            expected
        });
    }

    // ── Assert response header ────────────────────────────────────────
    public ToolResponse AssertResponseHeader(Dictionary<string, object> args)
    {
        var header = args["header"].ToString()!;
        var expected = args["expected"].ToString()!;

        if (_lastResponse?.Headers == null)
            return ToolResponse.Fail("No previous response headers to assert against");

        var actual = _lastResponse.Headers
            .FirstOrDefault(h => string.Equals(h.Name, header, StringComparison.OrdinalIgnoreCase))
            ?.Value?.ToString();

        if (actual == null)
            return ToolResponse.Fail($"Header '{header}' not present in response.");

        if (!string.Equals(actual, expected, StringComparison.Ordinal))
            return ToolResponse.Fail($"Header '{header}': expected '{expected}' but got '{actual}'");

        return ToolResponse.Ok(new
        {
            status = "passed",
            assertion = "response_header",
            header,
            expected,
            actual
        });
    }

    // ── Helpers ───────────────────────────────────────────────────────
    private RestResponse? _lastResponse;
    private string? _lastResponseBody;

    private ToolResponse BuildResponse(RestResponse response)
    {
        _lastResponse = response;
        _lastResponseBody = response.Content;

        object? parsedBody = null;
        if (!string.IsNullOrEmpty(response.Content))
        {
            try { parsedBody = JsonSerializer.Deserialize<object>(response.Content); }
            catch { parsedBody = response.Content; }
        }

        // Build headers dictionary safely, handling duplicates
        var headers = new Dictionary<string, object>();
        if (response.Headers != null)
        {
            foreach (var header in response.Headers)
            {
                if (header.Name == null) continue;
                
                // If header already exists, concatenate values (standard HTTP behavior)
                if (headers.ContainsKey(header.Name))
                {
                    var existingValue = headers[header.Name]?.ToString() ?? "";
                    var newValue = header.Value?.ToString() ?? "";
                    headers[header.Name] = $"{existingValue}, {newValue}";
                }
                else
                {
                    headers[header.Name] = header.Value?.ToString() ?? "";
                }
            }
        }

        return ToolResponse.Ok(new
        {
            statusCode = (int)response.StatusCode,
            isSuccessful = response.IsSuccessful,
            body = parsedBody,
            headers
        });
    }

    private static void AddHeaders(RestRequest request, Dictionary<string, object> args)
    {
        if (args.TryGetValue("headers", out var headers) &&
            headers is Dictionary<string, object> headerDict)
        {
            foreach (var (key, value) in headerDict)
                request.AddHeader(key, value.ToString()!);
        }
    }
}


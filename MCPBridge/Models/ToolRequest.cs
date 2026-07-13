// McpBridge/Models/ToolRequest.cs
namespace McpBridge.Models;

public class ToolRequest
{
    public string ToolName { get; set; } = string.Empty;
    public Dictionary<string, object> Arguments { get; set; } = new();
}

public class ToolResponse
{
    public bool Success { get; set; }
    public object? Result { get; set; }
    public string? Error { get; set; }

    public static ToolResponse Ok(object result) =>
        new() { Success = true, Result = result };

    public static ToolResponse Fail(string error) =>
        new() { Success = false, Error = error };
}
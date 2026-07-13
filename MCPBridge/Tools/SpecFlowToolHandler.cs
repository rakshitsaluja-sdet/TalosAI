// McpBridge/Tools/SpecFlowToolHandler.cs
using McpBridge.Models;
using System.Diagnostics;
using System.Text.Json;

namespace McpBridge.Tools;

public class SpecFlowToolHandler
{
    // ── Run a .feature file or tag filter ─────────────────────────────
    public ToolResponse RunFeature(Dictionary<string, object> args)
    {
        var featureFile = args.GetValueOrDefault("feature_file")?.ToString();
        var tags = args.GetValueOrDefault("tags")?.ToString();
        var projectPath = args.GetValueOrDefault("project_path",
            "../YourExistingProject")?.ToString()!;

        var arguments = new List<string> { "test", projectPath };

        if (!string.IsNullOrEmpty(featureFile))
        {
            arguments.Add("--filter");
            arguments.Add($"FullyQualifiedName~{featureFile}");
        }

        if (!string.IsNullOrEmpty(tags))
        {
            // SpecFlow tag filter syntax
            arguments.Add("--filter");
            arguments.Add($"Category={tags.TrimStart('@')}");
        }

        arguments.Add("--logger");
        arguments.Add("trx;LogFileName=TestResults.trx");
        arguments.Add("--no-build");

        var result = RunProcess("dotnet", arguments);

        return ToolResponse.Ok(new
        {
            status = result.ExitCode == 0 ? "passed" : "failed",
            exitCode = result.ExitCode,
            stdout = result.StdOut[..Math.Min(5000, result.StdOut.Length)],
            stderr = result.StdErr[..Math.Min(2000, result.StdErr.Length)],
            passed = result.ExitCode == 0
        });
    }

    // ── Run by scenario title ─────────────────────────────────────────
    public ToolResponse RunScenario(Dictionary<string, object> args)
    {
        var scenarioName = args["scenario_name"].ToString()!;
        var projectPath = args.GetValueOrDefault("project_path",
            "../YourExistingProject")?.ToString()!;

        var arguments = new List<string>
        {
            "test", projectPath,
            "--filter", $"Name~{scenarioName}",
            "--logger", "console;verbosity=normal"
        };

        var result = RunProcess("dotnet", arguments);

        return ToolResponse.Ok(new
        {
            status = result.ExitCode == 0 ? "passed" : "failed",
            scenarioName,
            exitCode = result.ExitCode,
            output = result.StdOut[..Math.Min(5000, result.StdOut.Length)]
        });
    }

    // ── List all scenarios ────────────────────────────────────────────
    public ToolResponse ListScenarios(Dictionary<string, object> args)
    {
        var projectPath = args.GetValueOrDefault("project_path",
            "../YourExistingProject")?.ToString()!;

        var arguments = new List<string> { "test", projectPath, "--list-tests" };
        var result = RunProcess("dotnet", arguments);

        var scenarios = result.StdOut
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Where(l => l.Trim().Length > 0 && !l.StartsWith("Test run"))
            .Select(l => l.Trim())
            .ToList();

        return ToolResponse.Ok(new
        {
            status = "ok",
            count = scenarios.Count,
            scenarios
        });
    }

    // ── Parse TRX results ─────────────────────────────────────────────
    public ToolResponse ParseLastResults(Dictionary<string, object> args)
    {
        var trxPath = args.GetValueOrDefault("trx_path",
            "TestResults/TestResults.trx")?.ToString()!;

        if (!File.Exists(trxPath))
            return ToolResponse.Fail($"TRX file not found: {trxPath}");

        var content = File.ReadAllText(trxPath);
        // Simple parse — extract summary counts
        var passed = CountOccurrences(content, "outcome=\"Passed\"");
        var failed = CountOccurrences(content, "outcome=\"Failed\"");
        var skipped = CountOccurrences(content, "outcome=\"NotExecuted\"");

        return ToolResponse.Ok(new
        {
            status = "ok",
            passed,
            failed,
            skipped,
            total = passed + failed + skipped
        });
    }

    // ── Helpers ───────────────────────────────────────────────────────
    private record ProcessResult(int ExitCode, string StdOut, string StdErr);

    /// <summary>
    /// Runs a process with each argument passed as a distinct array element
    /// (ProcessStartInfo.ArgumentList) rather than a single interpolated
    /// command-line string. This prevents an argument value containing a
    /// quote character from breaking out and injecting extra CLI flags.
    /// </summary>
    private static ProcessResult RunProcess(string command, IEnumerable<string> arguments)
    {
        var psi = new ProcessStartInfo
        {
            FileName = command,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var arg in arguments)
            psi.ArgumentList.Add(arg);

        using var process = Process.Start(psi)!;
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        return new ProcessResult(process.ExitCode, stdout, stderr);
    }

    private static int CountOccurrences(string text, string pattern)
    {
        int count = 0, index = 0;
        while ((index = text.IndexOf(pattern, index,
            StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            count++;
            index += pattern.Length;
        }
        return count;
    }
}

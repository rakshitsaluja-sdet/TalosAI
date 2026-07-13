// MCPBridge/Tools/PerformanceToolHandler.cs
using NBomber.CSharp;
using McpBridge.Models;

namespace McpBridge.Tools;

public class PerformanceToolHandler
{
    private string? _baseUrl;
    private Dictionary<string, string> _headers = new();
    private int _duration = 10;
    private int _virtualUsers = 10;
    // Default session name is timestamp-based so two sessions never collide
    // even if the user never supplies a session_name.
    private string _sessionName  = $"Session_{DateTime.Now:yyyyMMdd_HHmmss}";
    private string _reportFolder = "nbomber-reports";

    // ── Session result accumulator ────────────────────────────────────
    // Every test appends here so get_performance_summary can
    // return the full session history, not just the last run.
    private readonly List<object> _sessionResults = new();

    // ── Configure Performance Test ────────────────────────────────────
    public ToolResponse ConfigurePerformanceTest(Dictionary<string, object> args)
    {
        _baseUrl = args.GetValueOrDefault("base_url")?.ToString();
        
        if (args.ContainsKey("duration"))
            _duration = int.Parse(args["duration"].ToString()!);
            
        if (args.ContainsKey("virtual_users"))
            _virtualUsers = int.Parse(args["virtual_users"].ToString()!);

        if (args.ContainsKey("headers"))
        {
            var headers = args["headers"] as Dictionary<string, object>;
            if (headers != null)
            {
                _headers = headers.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value?.ToString() ?? ""
                );
            }
        }

        if (args.ContainsKey("session_name"))
            _sessionName = args["session_name"].ToString()!;

        if (args.ContainsKey("report_folder"))
            _reportFolder = args["report_folder"].ToString()!;

        return ToolResponse.Ok(new
        {
            status        = "ok",
            baseUrl       = _baseUrl,
            duration      = _duration,
            virtualUsers  = _virtualUsers,
            sessionName   = _sessionName,
            reportFolder  = _reportFolder
        });
    }

    // ── Run Load Test ─────────────────────────────────────────────────
    public ToolResponse RunLoadTest(Dictionary<string, object> args)
    {
        var endpoint = args["endpoint"].ToString()!;
        var method = args.GetValueOrDefault("method", "GET")?.ToString() ?? "GET";
        var scenarioName = args.GetValueOrDefault("scenario_name", "LoadTest")?.ToString() ?? "LoadTest";
        // Sanitize: NBomber uses scenarioName in report filenames — strip Windows-invalid chars
        scenarioName = System.Text.RegularExpressions.Regex.Replace(
            scenarioName, @"[<>:""/\\|?*\x00-\x1f\s]", "_");

        using var httpClient = new HttpClient();
        httpClient.BaseAddress = new Uri(_baseUrl!);
        
        foreach (var header in _headers)
        {
            httpClient.DefaultRequestHeaders.Add(header.Key, header.Value);
        }
        
        var scenario = Scenario.Create(scenarioName, async context =>
        {
            var response = method.ToUpper() switch
            {
                "POST" => await httpClient.PostAsync(endpoint, null),
                "PUT" => await httpClient.PutAsync(endpoint, null),
                "DELETE" => await httpClient.DeleteAsync(endpoint),
                _ => await httpClient.GetAsync(endpoint)
            };

            return response.IsSuccessStatusCode 
                ? Response.Ok() 
                : Response.Fail();
        })
        .WithWarmUpDuration(TimeSpan.FromSeconds(5))
        .WithLoadSimulations(
            Simulation.Inject(rate: _virtualUsers, 
                            interval: TimeSpan.FromSeconds(1),
                            during: TimeSpan.FromSeconds(_duration))
        );

        var stats = NBomberRunner
            .RegisterScenarios(scenario)
            .WithReportFolder(Path.Combine(_reportFolder, _sessionName))
            .WithReportFileName($"{scenarioName}_{DateTime.Now:yyyyMMdd_HHmmss}")
            .WithReportFormats(NBomber.Contracts.Stats.ReportFormat.Html,
                               NBomber.Contracts.Stats.ReportFormat.Csv)
            .Run();

        var sceneStats = stats.ScenarioStats[0];

        var result = new
        {
            testType = "load",
            scenario = scenarioName,
            endpoint,
            method,
            duration = _duration,
            totalRequests = sceneStats.Ok.Request.Count + sceneStats.Fail.Request.Count,
            successfulRequests = sceneStats.Ok.Request.Count,
            failedRequests = sceneStats.Fail.Request.Count,
            requestsPerSecond = sceneStats.Ok.Request.RPS,
            latency = new
            {
                min = sceneStats.Ok.Latency.MinMs,
                mean = sceneStats.Ok.Latency.MeanMs,
                max = sceneStats.Ok.Latency.MaxMs,
                p50 = sceneStats.Ok.Latency.Percent50,
                p75 = sceneStats.Ok.Latency.Percent75,
                p95 = sceneStats.Ok.Latency.Percent95,
                p99 = sceneStats.Ok.Latency.Percent99
            }
        };

        _sessionResults.Add(result);
        return ToolResponse.Ok(result);
    }

    // ── Run Stress Test ───────────────────────────────────────────────
    public ToolResponse RunStressTest(Dictionary<string, object> args)
    {
        var endpoint = args["endpoint"].ToString()!;
        var maxUsers = args.ContainsKey("max_users") 
            ? int.Parse(args["max_users"].ToString()!) 
            : 100;

        using var httpClient = new HttpClient();
        httpClient.BaseAddress = new Uri(_baseUrl!);
        
        var scenario = Scenario.Create("StressTest", async context =>
        {
            var response = await httpClient.GetAsync(endpoint);
            return response.IsSuccessStatusCode 
                ? Response.Ok() 
                : Response.Fail();
        })
        .WithLoadSimulations(
            Simulation.RampingInject(rate: maxUsers,
                                   interval: TimeSpan.FromSeconds(1),
                                   during: TimeSpan.FromSeconds(30))
        );

        var stats = NBomberRunner
            .RegisterScenarios(scenario)
            .WithReportFolder(Path.Combine(_reportFolder, _sessionName))
            .WithReportFileName($"StressTest_{endpoint.Replace("/", "_")}_{DateTime.Now:yyyyMMdd_HHmmss}")
            .WithReportFormats(NBomber.Contracts.Stats.ReportFormat.Html,
                               NBomber.Contracts.Stats.ReportFormat.Csv)
            .Run();

        var sceneStats = stats.ScenarioStats[0];

        var result = new
        {
            testType = "stress",
            endpoint,
            maxUsers,
            totalRequests = sceneStats.Ok.Request.Count + sceneStats.Fail.Request.Count,
            successfulRequests = sceneStats.Ok.Request.Count,
            failedRequests = sceneStats.Fail.Request.Count,
            maxRPS = sceneStats.Ok.Request.RPS,
            latencyP99 = sceneStats.Ok.Latency.Percent99
        };

        _sessionResults.Add(result);
        return ToolResponse.Ok(result);
    }

    // ── Run Spike Test ────────────────────────────────────────────────
    public ToolResponse RunSpikeTest(Dictionary<string, object> args)
    {
        var endpoint = args["endpoint"].ToString()!;
        var normalLoad = args.ContainsKey("normal_load") 
            ? int.Parse(args["normal_load"].ToString()!) 
            : 10;
        var spikeLoad = args.ContainsKey("spike_load") 
            ? int.Parse(args["spike_load"].ToString()!) 
            : 100;

        using var httpClient = new HttpClient();
        httpClient.BaseAddress = new Uri(_baseUrl!);
        
        var scenario = Scenario.Create("SpikeTest", async context =>
        {
            var response = await httpClient.GetAsync(endpoint);
            return response.IsSuccessStatusCode 
                ? Response.Ok() 
                : Response.Fail();
        })
        .WithLoadSimulations(
            Simulation.Inject(rate: normalLoad, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(10)),
            Simulation.Inject(rate: spikeLoad, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(10)),
            Simulation.Inject(rate: normalLoad, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(10))
        );

        var stats = NBomberRunner
            .RegisterScenarios(scenario)
            .WithReportFolder(Path.Combine(_reportFolder, _sessionName))
            .WithReportFileName($"SpikeTest_{endpoint.Replace("/", "_")}_{DateTime.Now:yyyyMMdd_HHmmss}")
            .WithReportFormats(NBomber.Contracts.Stats.ReportFormat.Html,
                               NBomber.Contracts.Stats.ReportFormat.Csv)
            .Run();

        var sceneStats = stats.ScenarioStats[0];

        var result = new
        {
            testType = "spike",
            endpoint,
            normalLoad,
            spikeLoad,
            totalRequests = sceneStats.Ok.Request.Count + sceneStats.Fail.Request.Count,
            successfulRequests = sceneStats.Ok.Request.Count,
            failedRequests = sceneStats.Fail.Request.Count,
            recoveredAfterSpike = sceneStats.Fail.Request.Count == 0
        };

        _sessionResults.Add(result);
        return ToolResponse.Ok(result);
    }

    // ── Get Performance Summary ───────────────────────────────────────
    // Returns all load/stress/spike results accumulated this session.
    // This is what the agent should call when the user asks for a
    // "performance report" — NOT generate_report (which is for Allure).
    public ToolResponse GetPerformanceSummary(Dictionary<string, object> args)
    {
        if (_sessionResults.Count == 0)
            return ToolResponse.Ok(new
            {
                status  = "no_results",
                message = "No performance tests have been run in this session yet.",
                tests   = Array.Empty<object>()
            });

        var totalRequests  = 0;
        var totalSucceeded = 0;
        var totalFailed    = 0;

        // Aggregate totals across all tests using reflection-safe JSON round-trip
        foreach (var r in _sessionResults)
        {
            var json  = System.Text.Json.JsonSerializer.Serialize(r);
            var doc   = System.Text.Json.JsonDocument.Parse(json).RootElement;

            if (doc.TryGetProperty("totalRequests",    out var tr)) totalRequests  += tr.GetInt32();
            if (doc.TryGetProperty("successfulRequests", out var sr)) totalSucceeded += sr.GetInt32();
            if (doc.TryGetProperty("failedRequests",   out var fr)) totalFailed    += fr.GetInt32();
        }

        return ToolResponse.Ok(new
        {
            status         = "ok",
            testsRun       = _sessionResults.Count,
            totalRequests,
            totalSucceeded,
            totalFailed,
            overallPassRate = totalRequests > 0
                ? Math.Round((double)totalSucceeded / totalRequests * 100, 2)
                : 0,
            tests = _sessionResults
        });
    }

    // ── Export Performance Report to HTML ─────────────────────────────
    // Generates a consolidated HTML report from all _sessionResults
    // (load + stress + spike) and writes it to the session folder.
    // NBomber per-test HTML files remain untouched alongside it.
    public ToolResponse ExportPerformanceReport(Dictionary<string, object> args)
    {
        if (_sessionResults.Count == 0)
            return ToolResponse.Fail(
                "No performance tests have been run in this session. " +
                "Run at least one load/stress/spike test first.");

        var sessionFolder = Path.Combine(_reportFolder, _sessionName);
        Directory.CreateDirectory(sessionFolder);

        // ── Aggregate totals ──────────────────────────────────────────
        var totalRequests  = 0;
        var totalSucceeded = 0;
        var totalFailed    = 0;
        var docs = new List<System.Text.Json.JsonElement>();

        foreach (var r in _sessionResults)
        {
            var json = System.Text.Json.JsonSerializer.Serialize(r);
            var doc  = System.Text.Json.JsonDocument.Parse(json).RootElement;
            docs.Add(doc);

            if (doc.TryGetProperty("totalRequests",      out var tr)) totalRequests  += tr.GetInt32();
            if (doc.TryGetProperty("successfulRequests", out var sr)) totalSucceeded += sr.GetInt32();
            if (doc.TryGetProperty("failedRequests",     out var fr)) totalFailed    += fr.GetInt32();
        }

        var passRate = totalRequests > 0
            ? Math.Round((double)totalSucceeded / totalRequests * 100, 2)
            : 0;

        // ── Build rows ────────────────────────────────────────────────
        var rows = new System.Text.StringBuilder();
        foreach (var doc in docs)
        {
            var testType  = doc.TryGetProperty("testType",  out var tt) ? tt.GetString() : "-";
            var endpoint  = doc.TryGetProperty("endpoint",  out var ep) ? ep.GetString() : "-";
            var total     = doc.TryGetProperty("totalRequests",      out var tq) ? tq.GetInt32().ToString() : "-";
            var passed    = doc.TryGetProperty("successfulRequests", out var sq) ? sq.GetInt32().ToString() : "-";
            var failed    = doc.TryGetProperty("failedRequests",     out var fq) ? fq.GetInt32().ToString() : "-";

            // RPS — field name differs per test type
            var rps = "-";
            if (doc.TryGetProperty("requestsPerSecond", out var rp)) rps = Math.Round(rp.GetDouble(), 2).ToString();
            else if (doc.TryGetProperty("maxRPS",       out var mr)) rps = Math.Round(mr.GetDouble(), 2).ToString();

            // Latency — only load test has full breakdown
            var p95 = "-";
            var p99 = "-";
            if (doc.TryGetProperty("latency", out var lat))
            {
                if (lat.TryGetProperty("p95", out var l95)) p95 = Math.Round(l95.GetDouble(), 2) + " ms";
                if (lat.TryGetProperty("p99", out var l99)) p99 = Math.Round(l99.GetDouble(), 2) + " ms";
            }
            else if (doc.TryGetProperty("latencyP99", out var lp99))
            {
                p99 = Math.Round(lp99.GetDouble(), 2) + " ms";
            }

            var failedInt = doc.TryGetProperty("failedRequests", out var fqInt) ? fqInt.GetInt32() : 0;
            var rowColor  = failedInt == 0 ? "#e8f5e9" : "#ffebee";

            rows.AppendLine($@"
            <tr style='background:{rowColor}'>
                <td>{System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(testType ?? "")}</td>
                <td><code>{endpoint}</code></td>
                <td>{total}</td>
                <td style='color:#2e7d32'>{passed}</td>
                <td style='color:{(failedInt > 0 ? "#c62828" : "#2e7d32")}'>{failed}</td>
                <td>{rps}</td>
                <td>{p95}</td>
                <td>{p99}</td>
            </tr>");
        }

        // ── Build HTML ────────────────────────────────────────────────
        var html = $@"<!DOCTYPE html>
<html lang='en'>
<head>
  <meta charset='UTF-8'>
  <title>Performance Report — {_sessionName}</title>
  <style>
    body {{ font-family: Segoe UI, Arial, sans-serif; margin: 32px; background: #fafafa; color: #212121; }}
    h1   {{ color: #1565c0; margin-bottom: 4px; }}
    .meta {{ color: #616161; font-size: 0.9em; margin-bottom: 24px; }}
    .summary {{ display: flex; gap: 24px; margin-bottom: 32px; }}
    .card {{ background: #fff; border-radius: 8px; padding: 16px 24px; box-shadow: 0 1px 4px rgba(0,0,0,.12); min-width: 140px; }}
    .card .val {{ font-size: 2em; font-weight: 700; color: #1565c0; }}
    .card .lbl {{ font-size: 0.8em; color: #757575; margin-top: 4px; }}
    table {{ width: 100%; border-collapse: collapse; background: #fff; border-radius: 8px; overflow: hidden; box-shadow: 0 1px 4px rgba(0,0,0,.12); }}
    th {{ background: #1565c0; color: #fff; padding: 12px 16px; text-align: left; font-size: 0.85em; }}
    td {{ padding: 11px 16px; border-bottom: 1px solid #e0e0e0; font-size: 0.9em; }}
    tr:last-child td {{ border-bottom: none; }}
    code {{ background: #f5f5f5; padding: 2px 6px; border-radius: 4px; font-size: 0.95em; }}
  </style>
</head>
<body>
  <h1>Performance Report</h1>
  <div class='meta'>Session: <strong>{_sessionName}</strong> &nbsp;|&nbsp; Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss} &nbsp;|&nbsp; Base URL: {_baseUrl}</div>

  <div class='summary'>
    <div class='card'><div class='val'>{_sessionResults.Count}</div><div class='lbl'>Tests Run</div></div>
    <div class='card'><div class='val'>{totalRequests}</div><div class='lbl'>Total Requests</div></div>
    <div class='card'><div class='val' style='color:#2e7d32'>{totalSucceeded}</div><div class='lbl'>Successful</div></div>
    <div class='card'><div class='val' style='color:{(totalFailed > 0 ? "#c62828" : "#2e7d32")}'>{totalFailed}</div><div class='lbl'>Failed</div></div>
    <div class='card'><div class='val'>{passRate}%</div><div class='lbl'>Pass Rate</div></div>
  </div>

  <table>
    <thead>
      <tr>
        <th>Test Type</th><th>Endpoint</th><th>Total Req</th>
        <th>Passed</th><th>Failed</th><th>RPS</th><th>P95 Latency</th><th>P99 Latency</th>
      </tr>
    </thead>
    <tbody>
      {rows}
    </tbody>
  </table>
</body>
</html>";

        // ── Write file ────────────────────────────────────────────────
        var reportFile = Path.Combine(sessionFolder, $"consolidated_report_{DateTime.Now:yyyyMMdd_HHmmss}.html");
        File.WriteAllText(reportFile, html);

        return ToolResponse.Ok(new
        {
            status        = "ok",
            sessionName   = _sessionName,
            testsIncluded = _sessionResults.Count,
            totalRequests,
            totalSucceeded,
            totalFailed,
            passRate,
            reportFile    = Path.GetFullPath(reportFile),
            message       = $"Consolidated report saved — {_sessionResults.Count} test(s), {passRate}% pass rate. Open the file above to view."
        });
    }
}

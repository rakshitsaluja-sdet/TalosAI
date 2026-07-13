// MCPBridge/Tools/ReportingToolHandler.cs
using McpBridge.Models;
using System.Text.Json;

namespace McpBridge.Tools;

public class ReportingToolHandler
{
    private string _reportDirectory = "allure-results";
    private string _htmlReportDirectory = "test-reports";
    private List<TestResult> _testResults = new();

    private class TestResult
    {
        public string TestName { get; set; } = "";
        public string Status { get; set; } = "";
        public long Duration { get; set; }
        public string? ErrorMessage { get; set; }
        public List<string> Steps { get; set; } = new();
        public Dictionary<string, string> Attachments { get; set; } = new();
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public int TestId { get; set; }
    }

    // ?? Configure Reporting ???????????????????????????????????????????
    public ToolResponse ConfigureReporting(Dictionary<string, object> args)
    {
        _reportDirectory = args.GetValueOrDefault("report_dir", "allure-results")?.ToString() ?? "allure-results";
        _htmlReportDirectory = args.GetValueOrDefault("html_report_dir", "test-reports")?.ToString() ?? "test-reports";

        Directory.CreateDirectory(_reportDirectory);
        Directory.CreateDirectory(_htmlReportDirectory);

        return ToolResponse.Ok(new
        {
            status = "ok",
            reportDirectory = _reportDirectory,
            htmlReportDirectory = _htmlReportDirectory
        });
    }

    // ?? Start Test ????????????????????????????????????????????????????
    public ToolResponse StartTest(Dictionary<string, object> args)
    {
        var testName = args["test_name"].ToString()!;
        var feature = args.GetValueOrDefault("feature")?.ToString();
        var scenario = args.GetValueOrDefault("scenario")?.ToString();

        var result = new TestResult
        {
            TestName = testName,
            StartTime = DateTime.UtcNow,
            Status = "running",
            TestId = _testResults.Count
        };

        _testResults.Add(result);

        return ToolResponse.Ok(new
        {
            status = "ok",
            testName,
            testId = result.TestId
        });
    }

    // ?? Log Test Step ?????????????????????????????????????????????????
    public ToolResponse LogTestStep(Dictionary<string, object> args)
    {
        var testId = args.ContainsKey("test_id") 
            ? int.Parse(args["test_id"].ToString()!) 
            : _testResults.Count - 1;
        var step = args["step"].ToString()!;

        if (testId >= 0 && testId < _testResults.Count)
        {
            _testResults[testId].Steps.Add($"{DateTime.UtcNow:HH:mm:ss} - {step}");
        }

        return ToolResponse.Ok(new
        {
            status = "ok",
            testId,
            step
        });
    }

    // ?? Attach Screenshot ?????????????????????????????????????????????
    public ToolResponse AttachScreenshot(Dictionary<string, object> args)
    {
        var testId = args.ContainsKey("test_id") 
            ? int.Parse(args["test_id"].ToString()!) 
            : _testResults.Count - 1;
        var screenshotPath = args["screenshot"].ToString()!;
        var title = args.GetValueOrDefault("title", "Screenshot")?.ToString() ?? "Screenshot";

        if (testId >= 0 && testId < _testResults.Count)
        {
            _testResults[testId].Attachments[title] = screenshotPath;
        }

        return ToolResponse.Ok(new
        {
            status = "ok",
            testId,
            attachment = screenshotPath
        });
    }

    // ?? End Test ??????????????????????????????????????????????????????
    public ToolResponse EndTest(Dictionary<string, object> args)
    {
        var testId = args.ContainsKey("test_id") 
            ? int.Parse(args["test_id"].ToString()!) 
            : _testResults.Count - 1;
        var status = args["status"].ToString()!.ToLower();
        var errorMessage = args.GetValueOrDefault("error")?.ToString();

        if (testId >= 0 && testId < _testResults.Count)
        {
            _testResults[testId].Status = status;
            _testResults[testId].EndTime = DateTime.UtcNow;
            _testResults[testId].Duration = (long)(_testResults[testId].EndTime - _testResults[testId].StartTime).TotalMilliseconds;
            _testResults[testId].ErrorMessage = errorMessage;

            // Write Allure result file
            WriteAllureResult(_testResults[testId]);
        }

        return ToolResponse.Ok(new
        {
            status = "ok",
            testId,
            testStatus = status,
            duration = testId >= 0 && testId < _testResults.Count 
                ? _testResults[testId].Duration 
                : 0
        });
    }

    // ?? Generate Report ???????????????????????????????????????????????
    public ToolResponse GenerateReport(Dictionary<string, object> args)
    {
        var reportType = args.GetValueOrDefault("type", "summary")?.ToString() ?? "summary";

        // Only count completed tests (not "running")
        var completedTests = _testResults.Where(t => t.Status != "running").ToList();
        var passed = completedTests.Count(t => t.Status == "passed");
        var failed = completedTests.Count(t => t.Status == "failed");
        var skipped = completedTests.Count(t => t.Status == "skipped");
        var total = completedTests.Count;

        var summary = new
        {
            status = "ok",
            total,
            passed,
            failed,
            skipped,
            passRate = total > 0 ? (double)passed / total * 100 : 0,
            totalDuration = completedTests.Sum(t => t.Duration),
            averageDuration = total > 0 ? completedTests.Average(t => t.Duration) : 0,
            tests = completedTests.Select(t => new
            {
                name = t.TestName,
                status = t.Status,
                duration = t.Duration,
                steps = t.Steps.Count,
                attachments = t.Attachments.Count,
                error = t.ErrorMessage
            }).ToList()
        };

        // Write summary file with timestamp
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var summaryPath = Path.Combine(_reportDirectory, $"summary_{timestamp}.json");
        File.WriteAllText(summaryPath, JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true }));

        return ToolResponse.Ok(summary);
    }

    // ?? Get Test Statistics ???????????????????????????????????????????
    public ToolResponse GetTestStatistics(Dictionary<string, object> args)
    {
        var groupBy = args.GetValueOrDefault("group_by", "status")?.ToString() ?? "status";
        var completedTests = _testResults.Where(t => t.Status != "running").ToList();

        object stats = groupBy.ToLower() switch
        {
            "status" => completedTests.GroupBy(t => t.Status).Select(g => new
            {
                group = g.Key,
                count = g.Count(),
                avgDuration = g.Average(t => t.Duration)
            }).ToList<object>(),
            "duration" => new[]
            {
                new { range = "< 1s", count = completedTests.Count(t => t.Duration < 1000) },
                new { range = "1-5s", count = completedTests.Count(t => t.Duration >= 1000 && t.Duration < 5000) },
                new { range = "5-10s", count = completedTests.Count(t => t.Duration >= 5000 && t.Duration < 10000) },
                new { range = "> 10s", count = completedTests.Count(t => t.Duration >= 10000) }
            }.ToList<object>(),
            _ => (object)new List<object>()
        };

        return ToolResponse.Ok(new
        {
            status = "ok",
            groupBy,
            statistics = stats
        });
    }

    // ?? Export Report to HTML ?????????????????????????????????????????
    public ToolResponse ExportToHtml(Dictionary<string, object> args)
    {
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        
        // Create organized folder structure: test-reports/html-reports/
        var htmlReportsDir = Path.Combine(_htmlReportDirectory, "html-reports");
        Directory.CreateDirectory(htmlReportsDir);
        
        // Always add timestamp to filename to prevent overwriting
        var outputFileName = args.ContainsKey("output") && !string.IsNullOrEmpty(args["output"]?.ToString())
            ? Path.GetFileNameWithoutExtension(args["output"].ToString()!) + $"_{timestamp}.html"
            : $"test-report_{timestamp}.html";
        
        var outputPath = Path.Combine(htmlReportsDir, outputFileName);

        // Only count completed tests
        var completedTests = _testResults.Where(t => t.Status != "running").ToList();
        var passed = completedTests.Count(t => t.Status == "passed");
        var failed = completedTests.Count(t => t.Status == "failed");
        var skipped = completedTests.Count(t => t.Status == "skipped");
        var total = completedTests.Count;
        var passRate = total > 0 ? (double)passed / total * 100 : 0;

        var html = GenerateBeautifulHtmlReport(completedTests, passed, failed, skipped, total, passRate, timestamp);

        try
        {
            File.WriteAllText(outputPath, html);

            // Also create an index.html that lists all reports
            CreateReportIndex(htmlReportsDir);

            return ToolResponse.Ok(new
            {
                status = "ok",
                reportPath = outputPath,
                fileName = Path.GetFileName(outputPath),
                indexPath = Path.Combine(htmlReportsDir, "index.html"),
                summary = new { total, passed, failed, skipped, passRate }
            });
        }
        catch (Exception ex)
        {
            return ToolResponse.Fail($"Failed to export HTML report: {ex.Message}");
        }
    }

    // ?? Generate Beautiful HTML Report ???????????????????????????????
    private string GenerateBeautifulHtmlReport(List<TestResult> completedTests, int passed, int failed, int skipped, int total, double passRate, string timestamp)
    {
        var testRows = string.Join("", completedTests.Select(t => $@"
            <tr>
                <td>{t.TestName}</td>
                <td><span class='badge badge-{t.Status.ToLower()}'>{t.Status.ToUpper()}</span></td>
                <td>{t.Duration} ms</td>
                <td>{t.Steps.Count}</td>
                <td>{t.Attachments.Count}</td>
                <td>{t.StartTime:HH:mm:ss}</td>
                <td>{(t.Duration / 1000.0):F2}s</td>
            </tr>"));

        return $@"
<!DOCTYPE html>
<html lang='en'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>Test Execution Report - {DateTime.Now:yyyy-MM-dd HH:mm:ss}</title>
    <style>
        * {{ margin: 0; padding: 0; box-sizing: border-box; }}
        body {{ font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); padding: 20px; }}
        .container {{ max-width: 1400px; margin: 0 auto; background: white; border-radius: 15px; box-shadow: 0 10px 40px rgba(0,0,0,0.3); overflow: hidden; }}
        .header {{ background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); color: white; padding: 30px; text-align: center; }}
        .header h1 {{ font-size: 2.5em; margin-bottom: 10px; }}
        .header .timestamp {{ opacity: 0.9; font-size: 1.1em; }}
        .summary {{ display: grid; grid-template-columns: repeat(auto-fit, minmax(200px, 1fr)); gap: 20px; padding: 30px; background: #f8f9fa; }}
        .stat-card {{ background: white; padding: 25px; border-radius: 10px; text-align: center; box-shadow: 0 4px 6px rgba(0,0,0,0.1); transition: transform 0.3s; }}
        .stat-card:hover {{ transform: translateY(-5px); box-shadow: 0 6px 12px rgba(0,0,0,0.15); }}
        .stat-value {{ font-size: 3em; font-weight: bold; margin-bottom: 10px; }}
        .stat-label {{ color: #666; font-size: 1.1em; text-transform: uppercase; letter-spacing: 1px; }}
        .stat-passed .stat-value {{ color: #28a745; }}
        .stat-failed .stat-value {{ color: #dc3545; }}
        .stat-total .stat-value {{ color: #007bff; }}
        .stat-rate .stat-value {{ color: #17a2b8; }}
        .content {{ padding: 30px; }}
        .section-title {{ font-size: 1.8em; margin-bottom: 20px; color: #333; border-bottom: 3px solid #667eea; padding-bottom: 10px; }}
        table {{ width: 100%; border-collapse: collapse; margin-top: 20px; background: white; box-shadow: 0 2px 4px rgba(0,0,0,0.1); }}
        th {{ background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); color: white; padding: 15px; text-align: left; font-weight: 600; }}
        td {{ padding: 15px; border-bottom: 1px solid #e9ecef; }}
        tr:hover {{ background: #f8f9fa; }}
        .badge {{ display: inline-block; padding: 6px 12px; border-radius: 20px; font-size: 0.85em; font-weight: 600; }}
        .badge-passed {{ background: #28a745; color: white; }}
        .badge-failed {{ background: #dc3545; color: white; }}
        .badge-skipped {{ background: #ffc107; color: #333; }}
        .footer {{ background: #f8f9fa; padding: 20px; text-align: center; color: #666; border-top: 2px solid #dee2e6; }}
        .no-tests {{ text-align: center; padding: 50px; color: #999; font-size: 1.2em; }}
        @media print {{ body {{ background: white; }} .container {{ box-shadow: none; }} }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>?? Test Execution Report</h1>
            <div class='timestamp'>Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}</div>
        </div>
        
        <div class='summary'>
            <div class='stat-card stat-total'>
                <div class='stat-value'>{total}</div>
                <div class='stat-label'>Total Tests</div>
            </div>
            <div class='stat-card stat-passed'>
                <div class='stat-value'>{passed}</div>
                <div class='stat-label'>Passed</div>
            </div>
            <div class='stat-card stat-failed'>
                <div class='stat-value'>{failed}</div>
                <div class='stat-label'>Failed</div>
            </div>
            <div class='stat-card stat-rate'>
                <div class='stat-value'>{passRate:F1}%</div>
                <div class='stat-label'>Pass Rate</div>
            </div>
        </div>
        
        <div class='content'>
            <h2 class='section-title'>Test Results</h2>
            {(total > 0 ? $@"
            <table>
                <thead>
                    <tr>
                        <th>Test Name</th>
                        <th>Status</th>
                        <th>Duration</th>
                        <th>Steps</th>
                        <th>Attachments</th>
                        <th>Start Time</th>
                        <th>Elapsed</th>
                    </tr>
                </thead>
                <tbody>
                    {testRows}
                </tbody>
            </table>" : "<div class='no-tests'>No test results available</div>")}
        </div>
        
        <div class='footer'>
            <p>Report generated by MCPBridge Test Automation Framework</p>
            <p>Timestamp: {timestamp} | Total Duration: {completedTests.Sum(t => t.Duration) / 1000.0:F2}s</p>
        </div>
    </div>
</body>
</html>";
    }

    // ?? Create Report Index ???????????????????????????????????????????????
    private void CreateReportIndex(string htmlReportsDir)
    {
        var reports = Directory.GetFiles(htmlReportsDir, "test-report_*.html")
            .Concat(Directory.GetFiles(htmlReportsDir, "extent-report_*.html"))
            .Select(f => new FileInfo(f))
            .OrderByDescending(f => f.LastWriteTime)
            .ToList();

        var reportRows = string.Join("", reports.Select((r, index) => $@"
            <tr>
                <td>{index + 1}</td>
                <td><a href='{r.Name}' target='_blank'>{r.Name}</a></td>
                <td>{r.LastWriteTime:yyyy-MM-dd HH:mm:ss}</td>
                <td>{(r.Length / 1024.0):F2} KB</td>
                <td><a href='{r.Name}' class='btn-view'>View Report</a></td>
            </tr>"));

        var latestTime = reports.Any() ? reports.First().LastWriteTime.ToString("HH:mm:ss") : "N/A";
        var noReportsMessage = reports.Count == 0 ? "<p style='text-align:center; color:#999; padding:50px;'>No reports generated yet</p>" : "";

        var indexHtml = $@"
<!DOCTYPE html>
<html>
<head>
    <title>Test Reports Index</title>
    <style>
        body {{ font-family: Arial, sans-serif; margin: 0; padding: 20px; background: #f5f5f5; }}
        .container {{ max-width: 1200px; margin: 0 auto; background: white; padding: 30px; border-radius: 10px; box-shadow: 0 2px 10px rgba(0,0,0,0.1); }}
        h1 {{ color: #333; border-bottom: 3px solid #667eea; padding-bottom: 10px; }}
        table {{ width: 100%; border-collapse: collapse; margin-top: 20px; }}
        th {{ background: #667eea; color: white; padding: 12px; text-align: left; }}
        td {{ padding: 12px; border-bottom: 1px solid #ddd; }}
        tr:hover {{ background: #f8f9fa; }}
        a {{ color: #667eea; text-decoration: none; font-weight: 600; }}
        a:hover {{ text-decoration: underline; }}
        .btn-view {{ background: #28a745; color: white; padding: 8px 16px; border-radius: 5px; display: inline-block; }}
        .btn-view:hover {{ background: #218838; text-decoration: none; }}
        .stats {{ display: flex; gap: 20px; margin-bottom: 20px; }}
        .stat {{ background: #f8f9fa; padding: 15px; border-radius: 5px; flex: 1; text-align: center; }}
        .stat-value {{ font-size: 2em; font-weight: bold; color: #667eea; }}
    </style>
</head>
<body>
    <div class='container'>
        <h1>?? Test Reports Archive</h1>
        <div class='stats'>
            <div class='stat'>
                <div class='stat-value'>{reports.Count}</div>
                <div>Total Reports</div>
            </div>
            <div class='stat'>
                <div class='stat-value'>{latestTime}</div>
                <div>Latest Report</div>
            </div>
        </div>
        <table>
            <tr>
                <th>#</th>
                <th>Report Name</th>
                <th>Generated</th>
                <th>Size</th>
                <th>Action</th>
            </tr>
            {reportRows}
        </table>
        {noReportsMessage}
    </div>
</body>
</html>";

        File.WriteAllText(Path.Combine(htmlReportsDir, "index.html"), indexHtml);
    }

    // ?? Generate Allure HTML Report (with fallback) ??????????????????
    public ToolResponse GenerateAllureReport(Dictionary<string, object> args)
    {
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var allureOutputDir = args.GetValueOrDefault("output_dir", $"allure-report-{timestamp}")?.ToString() 
            ?? $"allure-report-{timestamp}";
        
        try
        {
            // Find allure executable
            string allureCommand = FindAllureCommand();
            
            if (string.IsNullOrEmpty(allureCommand))
            {
                // Fallback: Generate beautiful extent report instead
                return GenerateFallbackReport(timestamp);
            }

            // Check Allure version
            var allureCheck = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = allureCommand,
                Arguments = "--version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });

            if (allureCheck == null)
            {
                return GenerateFallbackReport(timestamp);
            }

            allureCheck.WaitForExit();
            var allureVersion = allureCheck.StandardOutput.ReadToEnd();

            // Generate Allure report with serve option for proper viewing
            var generateProcess = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = allureCommand,
                Arguments = $"generate \"{_reportDirectory}\" -o \"{allureOutputDir}\" --clean",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Directory.GetCurrentDirectory()
            });

            if (generateProcess == null)
            {
                return GenerateFallbackReport(timestamp);
            }

            generateProcess.WaitForExit();
            var output = generateProcess.StandardOutput.ReadToEnd();
            var error = generateProcess.StandardError.ReadToEnd();

            if (generateProcess.ExitCode == 0)
            {
                var indexPath = Path.Combine(allureOutputDir, "index.html");
                var fullPath = Path.GetFullPath(indexPath);

                // Create a simple HTTP server launcher script for Allure
                CreateAllureServerLauncher(allureOutputDir);

                return ToolResponse.Ok(new
                {
                    status = "ok",
                    allureVersion = allureVersion.Trim(),
                    reportDirectory = allureOutputDir,
                    indexPath = fullPath,
                    message = "Allure HTML report generated successfully. Use 'allure serve' or open via HTTP server for full functionality.",
                    note = "To view properly, run: allure open " + allureOutputDir,
                    output = output.Trim()
                });
            }
            else
            {
                // Fallback on error
                return GenerateFallbackReport(timestamp);
            }
        }
        catch (Exception)
        {
            // Fallback on exception
            return GenerateFallbackReport(timestamp);
        }
    }

    // ?? Generate Fallback Beautiful Report ????????????????????????????
    private ToolResponse GenerateFallbackReport(string timestamp)
    {
        var completedTests = _testResults.Where(t => t.Status != "running").ToList();
        var passed = completedTests.Count(t => t.Status == "passed");
        var failed = completedTests.Count(t => t.Status == "failed");
        var skipped = completedTests.Count(t => t.Status == "skipped");
        var total = completedTests.Count;
        var passRate = total > 0 ? (double)passed / total * 100 : 0;

        var htmlReportsDir = Path.Combine(_htmlReportDirectory, "html-reports");
        Directory.CreateDirectory(htmlReportsDir);
        
        var reportPath = Path.Combine(htmlReportsDir, $"extent-report_{timestamp}.html");
        var html = GenerateBeautifulHtmlReport(completedTests, passed, failed, skipped, total, passRate, timestamp);
        
        File.WriteAllText(reportPath, html);
        CreateReportIndex(htmlReportsDir);

        return ToolResponse.Ok(new
        {
            status = "ok",
            message = "Allure CLI not available. Generated beautiful extent report as fallback.",
            reportPath = reportPath,
            summary = new { total, passed, failed, skipped, passRate },
            note = "Install Allure CLI for advanced reporting: npm install -g allure-commandline"
        });
    }

    // ?? Create Allure Server Launcher ?????????????????????????????????
    private void CreateAllureServerLauncher(string allureDir)
    {
        var launcherPath = Path.Combine(allureDir, "VIEW_REPORT.bat");
        var launcherContent = $@"@echo off
echo Starting Allure Report Server...
echo.
echo The report will open in your default browser.
echo Press Ctrl+C to stop the server.
echo.
allure open {allureDir}
pause
";
        File.WriteAllText(launcherPath, launcherContent);
    }

    // ?? Helper: Find Allure Command ???????????????????????????????????
    private string FindAllureCommand()
    {
        var possibleLocations = new List<string>
        {
            "allure",
            "allure.cmd",
            "allure.bat",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "npm", "allure.cmd"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "npm", "allure.bat"),
            @"C:\Users\" + Environment.UserName + @"\AppData\Roaming\npm\allure.cmd",
            @"C:\Program Files\nodejs\allure.cmd",
            @"C:\ProgramData\chocolatey\bin\allure.exe",
        };

        foreach (var location in possibleLocations)
        {
            try
            {
                var testProcess = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = location,
                    Arguments = "--version",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                });

                if (testProcess != null)
                {
                    testProcess.WaitForExit(3000);
                    if (testProcess.ExitCode == 0)
                    {
                        return location;
                    }
                }
            }
            catch
            {
                continue;
            }
        }

        var pathDirs = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User)?.Split(';') ?? Array.Empty<string>();
        foreach (var dir in pathDirs)
        {
            var allurePath = Path.Combine(dir, "allure.cmd");
            if (File.Exists(allurePath))
            {
                return allurePath;
            }
            
            allurePath = Path.Combine(dir, "allure.bat");
            if (File.Exists(allurePath))
            {
                return allurePath;
            }
        }

        return string.Empty;
    }

    // ?? Helper: Write Allure Result ???????????????????????????????????
    private void WriteAllureResult(TestResult result)
    {
        var allureResult = new
        {
            uuid = Guid.NewGuid().ToString(),
            name = result.TestName,
            status = result.Status,
            start = ((DateTimeOffset)result.StartTime).ToUnixTimeMilliseconds(),
            stop = ((DateTimeOffset)result.EndTime).ToUnixTimeMilliseconds(),
            steps = result.Steps.Select((step, index) => new
            {
                name = step,
                status = "passed",
                stage = "finished",
                start = ((DateTimeOffset)result.StartTime).ToUnixTimeMilliseconds() + (index * 100),
                stop = ((DateTimeOffset)result.StartTime).ToUnixTimeMilliseconds() + ((index + 1) * 100)
            }).ToList(),
            attachments = result.Attachments.Select(a => new
            {
                name = a.Key,
                source = a.Value,
                type = "image/png"
            }).ToList()
        };

        var fileName = $"{result.TestId:D4}-{Guid.NewGuid()}-result.json";
        var filePath = Path.Combine(_reportDirectory, fileName);
        File.WriteAllText(filePath, JsonSerializer.Serialize(allureResult, new JsonSerializerOptions { WriteIndented = true }));
    }

    // ?? Clear Test Results ????????????????????????????????????????????
    public ToolResponse ClearResults(Dictionary<string, object> args)
    {
        var previousCount = _testResults.Count;
        _testResults.Clear();

        return ToolResponse.Ok(new
        {
            status = "ok",
            clearedCount = previousCount
        });
    }

    // ?? Open Allure Report in Browser ?????????????????????????????????
    public ToolResponse OpenAllureReport(Dictionary<string, object> args)
    {
        var reportDir = args.GetValueOrDefault("report_dir", "")?.ToString();
        
        if (string.IsNullOrEmpty(reportDir))
        {
            var allureReports = Directory.GetDirectories(".", "allure-report-*")
                .OrderByDescending(d => Directory.GetCreationTime(d))
                .ToList();
            
            if (allureReports.Any())
            {
                reportDir = allureReports.First();
            }
            else
            {
                return ToolResponse.Fail("No Allure report directory found. Generate report first.");
            }
        }
        
        var indexPath = Path.Combine(reportDir, "index.html");

        if (!File.Exists(indexPath))
        {
            return ToolResponse.Fail($"Allure report not found at: {indexPath}. Generate report first.");
        }

        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = Path.GetFullPath(indexPath),
                UseShellExecute = true
            });

            return ToolResponse.Ok(new
            {
                status = "ok",
                message = "Allure report opened in browser",
                path = Path.GetFullPath(indexPath)
            });
        }
        catch (Exception ex)
        {
            return ToolResponse.Fail($"Failed to open report: {ex.Message}");
        }
    }
}

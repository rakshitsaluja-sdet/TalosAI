using McpBridge.Models;
using System.Text;
using System.Text.Json;

namespace McpBridge.Tools;

public class SolutionReaderToolHandler
{
    // Filenames that must never be read back to an LLM caller, even from inside
    // the allowed project root — these commonly hold real secrets/connection strings.
    private static readonly string[] SecretFilePatterns =
    {
        "appsettings", ".secrets", "config.properties", "azure.credentials", ".env"
    };

    private static bool IsSecretFile(string filePath)
    {
        var name = Path.GetFileName(filePath);
        return SecretFilePatterns.Any(p => name.Contains(p, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Resolves a caller-supplied path and verifies it falls inside the detected
    /// solution root. Blocks reads of arbitrary files elsewhere on disk (path
    /// traversal via "..", absolute paths outside the repo, etc.).
    /// </summary>
    private static string EnsureWithinSolutionRoot(string path)
    {
        var root = Path.GetFullPath(GetDefaultSolutionPath());
        var resolved = Path.GetFullPath(path);

        if (!resolved.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException(
                $"Path '{path}' resolves outside the allowed solution root ('{root}').");

        return resolved;
    }

    // ── 1. Scan entire solution structure ─────────────────────────────
    // Agent calls this first to understand what exists
    public ToolResponse ScanSolution(Dictionary<string, object> args)
    {
        var solutionPath = args.GetValueOrDefault("solution_path",
            GetDefaultSolutionPath())?.ToString()!;

        try
        {
            solutionPath = EnsureWithinSolutionRoot(solutionPath);
        }
        catch (UnauthorizedAccessException ex)
        {
            return ToolResponse.Fail(ex.Message);
        }

        if (!Directory.Exists(solutionPath))
            return ToolResponse.Fail($"Solution path not found: {solutionPath}");

        var structure = new Dictionary<string, object>();

        // Find all projects (.csproj)
        var projects = Directory.GetFiles(
            solutionPath, "*.csproj", SearchOption.AllDirectories);

        var projectList = new List<object>();
        foreach (var proj in projects)
        {
            var projDir = Path.GetDirectoryName(proj)!;
            var projName = Path.GetFileNameWithoutExtension(proj);

            projectList.Add(new
            {
                name = projName,
                path = proj,
                folder = projDir,
                folders = GetSubFolders(projDir),
                fileCount = Directory.GetFiles(
                    projDir, "*.*", SearchOption.AllDirectories).Length
            });
        }

        structure["projects"] = projectList;
        structure["solutionPath"] = solutionPath;
        structure["totalProjects"] = projectList.Count;

        return ToolResponse.Ok(structure);
    }

    // ── 2. Read all feature files in a project ────────────────────────
    public ToolResponse ReadFeatureFiles(Dictionary<string, object> args)
    {
        string projectPath;
        try
        {
            projectPath = EnsureWithinSolutionRoot(args["project_path"].ToString()!);
        }
        catch (UnauthorizedAccessException ex)
        {
            return ToolResponse.Fail(ex.Message);
        }

        var includeContent = args.GetValueOrDefault("include_content", false)?.ToString()?.ToLower() == "true";

        var features = Directory.GetFiles(
            projectPath, "*.feature", SearchOption.AllDirectories);

        var result = new List<object>();
        foreach (var f in features)
        {
            var content = File.ReadAllText(f);
            var lines = content.Split('\n');
            var scenarioCount = lines.Count(l => l.Trim().StartsWith("Scenario:", StringComparison.OrdinalIgnoreCase));

            var featureInfo = new Dictionary<string, object>
            {
                ["fileName"] = Path.GetFileName(f),
                ["filePath"] = f,
                ["folder"] = Path.GetDirectoryName(f)!,
                ["lineCount"] = lines.Length,
                ["scenarioCount"] = scenarioCount,
                ["sizeKB"] = new FileInfo(f).Length / 1024.0
            };

            // Only include full content if explicitly requested
            if (includeContent)
            {
                featureInfo["content"] = content;
            }

            result.Add(featureInfo);
        }

        return ToolResponse.Ok(new
        {
            count = result.Count,
            totalScenarios = result.Sum(r => (int)((Dictionary<string, object>)r)["scenarioCount"]),
            features = result
        });
    }

    // ── 3. Read all step definition files ────────────────────────────
    public ToolResponse ReadStepDefinitions(Dictionary<string, object> args)
    {
        string projectPath;
        try
        {
            projectPath = EnsureWithinSolutionRoot(args["project_path"].ToString()!);
        }
        catch (UnauthorizedAccessException ex)
        {
            return ToolResponse.Fail(ex.Message);
        }

        // Find StepDefinitions folder or any file with [Binding]
        var csFiles = Directory.GetFiles(
            projectPath, "*.cs", SearchOption.AllDirectories)
            .Where(f => File.ReadAllText(f).Contains("[Binding]") ||
                        f.Contains("Step") ||
                        f.Contains("step"))
            .ToList();

        var result = new List<object>();
        foreach (var f in csFiles)
        {
            var content = File.ReadAllText(f);
            result.Add(new
            {
                fileName = Path.GetFileName(f),
                filePath = f,
                content = content,
                stepCount = CountOccurrences(content, "[Given") +
                             CountOccurrences(content, "[When") +
                             CountOccurrences(content, "[Then")
            });
        }

        return ToolResponse.Ok(new
        {
            count = result.Count,
            stepDefinitions = result
        });
    }

    // ── 4. Read all page object files ─────────────────────────────────
    public ToolResponse ReadPageObjects(Dictionary<string, object> args)
    {
        string projectPath;
        try
        {
            projectPath = EnsureWithinSolutionRoot(args["project_path"].ToString()!);
        }
        catch (UnauthorizedAccessException ex)
        {
            return ToolResponse.Fail(ex.Message);
        }

        var csFiles = Directory.GetFiles(
            projectPath, "*.cs", SearchOption.AllDirectories)
            .Where(f => f.Contains("Page") ||
                        f.Contains("page") ||
                        File.ReadAllText(f).Contains("IWebDriver") ||
                        File.ReadAllText(f).Contains("FindElement"))
            .ToList();

        var result = new List<object>();
        foreach (var f in csFiles)
        {
            result.Add(new
            {
                fileName = Path.GetFileName(f),
                filePath = f,
                content = File.ReadAllText(f),
                folder = Path.GetDirectoryName(f)
            });
        }

        return ToolResponse.Ok(new { count = result.Count, pageObjects = result });
    }

    // ── 5. Read a single specific file ────────────────────────────────
    public ToolResponse ReadFile(Dictionary<string, object> args)
    {
        var filePath = args["file_path"].ToString()!;

        try
        {
            filePath = EnsureWithinSolutionRoot(filePath);
            if (IsSecretFile(filePath))
                return ToolResponse.Fail($"Reading '{Path.GetFileName(filePath)}' is blocked — it matches a secret-file pattern.");
        }
        catch (UnauthorizedAccessException ex)
        {
            return ToolResponse.Fail(ex.Message);
        }

        if (!File.Exists(filePath))
            return ToolResponse.Fail($"File not found: {filePath}");

        return ToolResponse.Ok(new
        {
            fileName = Path.GetFileName(filePath),
            filePath,
            content = File.ReadAllText(filePath),
            extension = Path.GetExtension(filePath),
            sizeBytes = new FileInfo(filePath).Length
        });
    }

    // ── 6. Read API client files ──────────────────────────────────────
    public ToolResponse ReadApiClients(Dictionary<string, object> args)
    {
        string projectPath;
        try
        {
            projectPath = EnsureWithinSolutionRoot(args["project_path"].ToString()!);
        }
        catch (UnauthorizedAccessException ex)
        {
            return ToolResponse.Fail(ex.Message);
        }

        var csFiles = Directory.GetFiles(
            projectPath, "*.cs", SearchOption.AllDirectories)
            .Where(f => f.Contains("Client") ||
                        f.Contains("Api") ||
                        f.Contains("Service") ||
                        File.ReadAllText(f).Contains("RestClient") ||
                        File.ReadAllText(f).Contains("HttpClient"))
            .ToList();

        var result = new List<object>();
        foreach (var f in csFiles)
        {
            result.Add(new
            {
                fileName = Path.GetFileName(f),
                filePath = f,
                content = File.ReadAllText(f)
            });
        }

        return ToolResponse.Ok(new { count = result.Count, apiClients = result });
    }

    // ── 7. Read app settings / config ─────────────────────────────────
    public ToolResponse ReadConfig(Dictionary<string, object> args)
    {
        string projectPath;
        try
        {
            projectPath = EnsureWithinSolutionRoot(args["project_path"].ToString()!);
        }
        catch (UnauthorizedAccessException ex)
        {
            return ToolResponse.Fail(ex.Message);
        }

        var configFiles = Directory.GetFiles(
            projectPath, "*.json", SearchOption.AllDirectories)
            .Concat(Directory.GetFiles(
                projectPath, "*.xml", SearchOption.AllDirectories))
            .Concat(Directory.GetFiles(
                projectPath, "*.yaml", SearchOption.AllDirectories))
            .Concat(Directory.GetFiles(
                projectPath, "*.yml", SearchOption.AllDirectories))
            .Where(f => !f.Contains("node_modules") &&
                        !f.Contains("bin") &&
                        !f.Contains("obj") &&
                        !IsSecretFile(f))
            .ToList();

        var result = new List<object>();
        foreach (var f in configFiles)
        {
            result.Add(new
            {
                fileName = Path.GetFileName(f),
                filePath = f,
                content = File.ReadAllText(f)
            });
        }

        return ToolResponse.Ok(new { count = result.Count, configFiles = result });
    }

    // ── 8. Search for pattern across solution ─────────────────────────
    // e.g. agent searches for "how are waits handled"
    public ToolResponse SearchInSolution(Dictionary<string, object> args)
    {
        string solutionPath;
        try
        {
            solutionPath = EnsureWithinSolutionRoot(args["solution_path"].ToString()!);
        }
        catch (UnauthorizedAccessException ex)
        {
            return ToolResponse.Fail(ex.Message);
        }

        var searchTerm = args["search_term"].ToString()!;
        var extension = args.GetValueOrDefault(
            "file_extension", "*.cs")?.ToString() ?? "*.cs";

        var files = Directory.GetFiles(
            solutionPath, extension, SearchOption.AllDirectories)
            .Where(f => !f.Contains("\\bin\\") &&
                        !f.Contains("\\obj\\") &&
                        !IsSecretFile(f));

        var matches = new List<object>();
        foreach (var f in files)
        {
            var content = File.ReadAllText(f);
            if (!content.Contains(searchTerm,
                StringComparison.OrdinalIgnoreCase)) continue;

            // Find matching lines with context
            var lines = content.Split('\n');
            var matchingLines = lines
                .Select((line, idx) => new { line, idx })
                .Where(x => x.line.Contains(searchTerm,
                    StringComparison.OrdinalIgnoreCase))
                .Select(x => new
                {
                    lineNumber = x.idx + 1,
                    content = x.line.Trim()
                })
                .ToList();

            matches.Add(new
            {
                fileName = Path.GetFileName(f),
                filePath = f,
                matchCount = matchingLines.Count,
                matchingLines
            });
        }

        return ToolResponse.Ok(new
        {
            searchTerm,
            totalFiles = matches.Count,
            matches
        });
    }

    // ── 9. Get folder structure of a project ──────────────────────────
    public ToolResponse GetProjectStructure(Dictionary<string, object> args)
    {
        string projectPath;
        try
        {
            projectPath = EnsureWithinSolutionRoot(args["project_path"].ToString()!);
        }
        catch (UnauthorizedAccessException ex)
        {
            return ToolResponse.Fail(ex.Message);
        }

        var allFiles = Directory.GetFiles(
            projectPath, "*.*", SearchOption.AllDirectories)
            .Where(f => !f.Contains("\\bin\\") &&
                        !f.Contains("\\obj\\") &&
                        !f.Contains("\\.git\\"))
            .Select(f => new
            {
                relativePath = Path.GetRelativePath(projectPath, f),
                fileName = Path.GetFileName(f),
                extension = Path.GetExtension(f),
                folder = Path.GetRelativePath(
                    projectPath,
                    Path.GetDirectoryName(f)!)
            })
            .GroupBy(f => f.folder)
            .Select(g => new
            {
                folder = g.Key,
                files = g.Select(f => f.fileName).ToList()
            })
            .ToList();

        return ToolResponse.Ok(new
        {
            projectPath,
            totalFiles = allFiles.Sum(f => f.files.Count),
            structure = allFiles
        });
    }

    // ── Helpers ───────────────────────────────────────────────────────
    private static string GetDefaultSolutionPath()
    {
        // Start from MCPBridge bin folder and walk up to repository root
        var dir = AppDomain.CurrentDomain.BaseDirectory;

        // Walk up until we find a directory containing .csproj files (solution level)
        while (dir != null)
        {
            // Check if this directory contains multiple .csproj files (solution level)
            var csprojFiles = Directory.GetFiles(dir, "*.csproj", SearchOption.TopDirectoryOnly);
            var subdirProjects = Directory.GetDirectories(dir)
                .SelectMany(d => Directory.GetFiles(d, "*.csproj", SearchOption.TopDirectoryOnly))
                .ToList();

            // If we find a .sln or .slnx file, that's the solution root
            if (Directory.GetFiles(dir, "*.sln").Any() || Directory.GetFiles(dir, "*.slnx").Any())
                return dir;

            // If we find multiple project directories with .csproj, this is solution level
            if (subdirProjects.Count >= 2)
                return dir;

            dir = Directory.GetParent(dir)?.FullName;
        }

        // Fallback: Try to find repository root by looking for .git directory
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var gitDir = new DirectoryInfo(baseDir);
        while (gitDir != null)
        {
            if (Directory.Exists(Path.Combine(gitDir.FullName, ".git")))
                return gitDir.FullName;
            gitDir = gitDir.Parent;
        }

        return AppDomain.CurrentDomain.BaseDirectory;
    }

    private static List<string> GetSubFolders(string path)
    {
        return Directory.GetDirectories(path)
            .Select(d => Path.GetFileName(d)!)
            .Where(d => d != "bin" && d != "obj" && d != ".git")
            .ToList();
    }

    private static int CountOccurrences(string text, string pattern)
    {
        int count = 0, index = 0;
        while ((index = text.IndexOf(pattern, index,
            StringComparison.OrdinalIgnoreCase)) >= 0)
        { count++; index += pattern.Length; }
        return count;
    }
}

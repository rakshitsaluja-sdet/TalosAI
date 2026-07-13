using System.Text.Json;
using McpBridge.Models;

namespace McpBridge.Tools;

public class SolutionWriterToolHandler
{
    // Helper method to resolve repository root
    private string GetRepositoryRoot()
    {
        var currentDir = Directory.GetCurrentDirectory();

        // Search upward for .git directory
        var dir = new DirectoryInfo(currentDir);
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, ".git")))
                return dir.FullName;
            dir = dir.Parent;
        }

        // Fallback to current directory
        return currentDir;
    }

    // Helper method to normalize project path. Always resolves relative to the
    // repository root and verifies the result stays inside it — a caller cannot
    // escape the repo via an absolute path or "..' segments.
    private string NormalizeProjectPath(string projectPath)
    {
        var repoRoot = Path.GetFullPath(GetRepositoryRoot());

        // Handle "TalosAIProject"/"TalosAI" shorthand
        if (projectPath.Equals("TalosAIProject", StringComparison.OrdinalIgnoreCase) ||
            projectPath.Equals("TalosAI", StringComparison.OrdinalIgnoreCase))
        {
            return Path.Combine(repoRoot, "TalosAI", "automation");
        }

        var combined = Path.IsPathRooted(projectPath)
            ? projectPath
            : Path.Combine(repoRoot, projectPath);

        var resolved = Path.GetFullPath(combined);

        if (!resolved.StartsWith(repoRoot, StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException(
                $"project_path '{projectPath}' resolves outside the repository root.");

        return resolved;
    }

    /// <summary>
    /// Resolves project_path/sub_folder/file_name into a safe absolute path
    /// guaranteed to stay inside the repository root. Used by every write
    /// method so LLM-supplied path fragments (including "..") can never
    /// escape the repo, regardless of whether the caller used an absolute or
    /// relative project_path.
    /// </summary>
    private string ResolveWriteTarget(string projectPath, string subFolder, string fileName)
    {
        var repoRoot = Path.GetFullPath(GetRepositoryRoot());
        var normalizedProject = NormalizeProjectPath(projectPath);

        if (fileName.IndexOfAny(new[] { '/', '\\' }) >= 0 || fileName.Contains(".."))
            throw new UnauthorizedAccessException(
                $"file_name '{fileName}' must not contain path separators or '..'.");

        if (!string.IsNullOrEmpty(subFolder) &&
            subFolder.Split('/', '\\').Any(seg => seg == ".."))
            throw new UnauthorizedAccessException(
                $"sub_folder '{subFolder}' must not contain '..' segments.");

        var combined = string.IsNullOrEmpty(subFolder)
            ? Path.Combine(normalizedProject, fileName)
            : Path.Combine(normalizedProject, subFolder, fileName);

        var resolved = Path.GetFullPath(combined);

        if (!resolved.StartsWith(repoRoot, StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException(
                $"Resolved write path '{resolved}' is outside the repository root.");

        return resolved;
    }

    /// <summary>
    /// Resolves a caller-supplied existing-file path for append operations,
    /// enforcing repo containment and a .cs extension.
    /// </summary>
    private string ResolveExistingCsFilePath(string filePath)
    {
        var repoRoot = Path.GetFullPath(GetRepositoryRoot());
        var combined = Path.IsPathRooted(filePath) ? filePath : Path.Combine(repoRoot, filePath);
        var resolved = Path.GetFullPath(combined);

        if (!resolved.StartsWith(repoRoot, StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException(
                $"file_path '{filePath}' resolves outside the repository root.");

        if (!string.Equals(Path.GetExtension(resolved), ".cs", StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("append_to_step_def only allows editing .cs files.");

        return resolved;
    }

    private static bool GetOverwriteFlag(Dictionary<string, object> args) =>
        args.TryGetValue("overwrite", out var v) && bool.TryParse(v?.ToString(), out var b) && b;

    // ── 1. Write a feature file into the TalosAI project ───────────────
    public ToolResponse WriteFeatureFile(Dictionary<string, object> args)
    {
        var projectPath = args["project_path"].ToString()!;
        var fileName = args["file_name"].ToString()!;
        var content = args["content"].ToString()!;
        var subFolder = args.GetValueOrDefault(
            "sub_folder", "Features")?.ToString() ?? "Features";

        if (!fileName.EndsWith(".feature"))
            fileName += ".feature";

        string fullPath;
        try
        {
            fullPath = ResolveWriteTarget(projectPath, subFolder, fileName);
        }
        catch (UnauthorizedAccessException ex)
        {
            return ToolResponse.Fail(ex.Message);
        }

        var overwrite = GetOverwriteFlag(args);
        if (File.Exists(fullPath) && !overwrite)
            return ToolResponse.Fail(
                $"File already exists: {fullPath}. " +
                $"Pass overwrite:true to replace it.");

        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, content);

        return ToolResponse.Ok(new
        {
            status = "written",
            filePath = fullPath,
            fileName,
            lines = content.Split('\n').Length
        });
    }

    // ── 2. Write a step definition file ──────────────────────────────
    public ToolResponse WriteStepDefinition(Dictionary<string, object> args)
    {
        var projectPath = args["project_path"].ToString()!;
        var fileName = args["file_name"].ToString()!;
        var content = args["content"].ToString()!;
        var subFolder = args.GetValueOrDefault(
            "sub_folder", "Steps")?.ToString() ?? "Steps";

        if (!fileName.EndsWith(".cs"))
            fileName += ".cs";

        string fullPath;
        try
        {
            fullPath = ResolveWriteTarget(projectPath, subFolder, fileName);
        }
        catch (UnauthorizedAccessException ex)
        {
            return ToolResponse.Fail(ex.Message);
        }

        var overwrite = GetOverwriteFlag(args);
        if (File.Exists(fullPath) && !overwrite)
            return ToolResponse.Fail(
                $"File already exists: {fullPath}. " +
                $"Pass overwrite:true to replace it.");

        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, content);

        return ToolResponse.Ok(new
        {
            status = "written",
            filePath = fullPath,
            fileName
        });
    }

    // ── 3. Write a page object file ───────────────────────────────────
    public ToolResponse WritePageObject(Dictionary<string, object> args)
    {
        var projectPath = args["project_path"].ToString()!;
        var fileName = args["file_name"].ToString()!;
        var content = args["content"].ToString()!;
        var subFolder = args.GetValueOrDefault(
            "sub_folder", "Pages")?.ToString() ?? "Pages";

        if (!fileName.EndsWith(".cs"))
            fileName += ".cs";

        string fullPath;
        try
        {
            fullPath = ResolveWriteTarget(projectPath, subFolder, fileName);
        }
        catch (UnauthorizedAccessException ex)
        {
            return ToolResponse.Fail(ex.Message);
        }

        var overwrite = GetOverwriteFlag(args);
        if (File.Exists(fullPath) && !overwrite)
            return ToolResponse.Fail(
                $"File already exists: {fullPath}. " +
                $"Pass overwrite:true to replace it.");

        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, content);

        return ToolResponse.Ok(new
        {
            status = "written",
            filePath = fullPath,
            fileName
        });
    }

    // ════════════════════════════════════════════════════════════════
    // PLAYWRIGHT-SPECIFIC WRITE METHODS
    // Added to existing SolutionWriterToolHandler
    // ════════════════════════════════════════════════════════════════

    // ── Write Playwright feature file ────────────────────────────────────
    // Same as write_feature_file but enforces @playwright tag
    public ToolResponse WritePlaywrightFeatureFile(
        Dictionary<string, object> args)
    {
        var projectPath = args["project_path"].ToString()!;
        var fileName = args["file_name"].ToString()!;
        var content = args["content"].ToString()!;
        var subFolder = args.GetValueOrDefault(
            "sub_folder", "Features")?.ToString() ?? "Features";

        if (!fileName.EndsWith(".feature"))
            fileName += ".feature";

        string fullPath;
        try
        {
            fullPath = ResolveWriteTarget(projectPath, subFolder, fileName);
        }
        catch (UnauthorizedAccessException ex)
        {
            return ToolResponse.Fail(ex.Message);
        }

        var overwrite = GetOverwriteFlag(args);
        if (File.Exists(fullPath) && !overwrite)
            return ToolResponse.Fail(
                $"File already exists: {fullPath}. " +
                "Pass overwrite:true to replace it.");

        // Enforce @playwright tag on every scenario
        var lines = content.Split('\n').ToList();
        var updatedLines = new List<string>();

        foreach (var line in lines)
        {
            var trimmed = line.Trim();

            // Add @playwright tag before Scenario if not present
            if ((trimmed.StartsWith("Scenario:") ||
                 trimmed.StartsWith("Scenario Outline:")) &&
                (updatedLines.Count == 0 ||
                 !updatedLines[^1].Trim().Contains("@playwright")))
            {
                // Find indentation
                var indent = line.Length - line.TrimStart().Length;
                var spaces = new string(' ', indent);
                updatedLines.Add($"{spaces}@playwright @regression");
            }
            updatedLines.Add(line);
        }

        content = string.Join('\n', updatedLines);

        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, content);

        return ToolResponse.Ok(new
        {
            status = "written",
            filePath = fullPath,
            fileName,
            hasPlaywrightTag = content.Contains("@playwright"),
            lines = content.Split('\n').Length
        });
    }

    // ── Write Playwright step definition ─────────────────────────────────
    public ToolResponse WritePlaywrightStepDefinition(
        Dictionary<string, object> args)
    {
        var projectPath = args["project_path"].ToString()!;
        var fileName = args["file_name"].ToString()!;
        var content = args["content"].ToString()!;
        var subFolder = args.GetValueOrDefault(
            "sub_folder", "Steps")?.ToString() ?? "Steps";

        if (!fileName.EndsWith(".cs"))
            fileName += ".cs";

        // Validate it inherits from PlaywrightBaseSteps
        if (!content.Contains("PlaywrightBaseSteps"))
            return ToolResponse.Fail(
                "Playwright step definition must inherit from " +
                "PlaywrightBaseSteps. Regenerate with correct base class.");

        // Validate it uses async Task not void
        if (content.Contains("public void ") &&
            !content.Contains("public async Task"))
            return ToolResponse.Fail(
                "Playwright step definitions must use " +
                "'public async Task' not 'public void'. " +
                "Playwright API is async. Regenerate.");

        string fullPath;
        try
        {
            fullPath = ResolveWriteTarget(projectPath, subFolder, fileName);
        }
        catch (UnauthorizedAccessException ex)
        {
            return ToolResponse.Fail(ex.Message);
        }

        var overwrite = GetOverwriteFlag(args);
        if (File.Exists(fullPath) && !overwrite)
            return ToolResponse.Fail(
                $"File already exists: {fullPath}. " +
                "Pass overwrite:true to replace it.");

        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, content);

        return ToolResponse.Ok(new
        {
            status = "written",
            filePath = fullPath,
            fileName
        });
    }

    // ── Write Playwright page object ──────────────────────────────────────
    public ToolResponse WritePlaywrightPageObject(
        Dictionary<string, object> args)
    {
        var projectPath = args["project_path"].ToString()!;
        var fileName = args["file_name"].ToString()!;
        var content = args["content"].ToString()!;
        var subFolder = args.GetValueOrDefault(
            "sub_folder", "Pages")?.ToString() ?? "Pages";

        if (!fileName.EndsWith(".cs"))
            fileName += ".cs";

        // Validate it inherits from PlaywrightBasePage
        if (!content.Contains("PlaywrightBasePage"))
            return ToolResponse.Fail(
                "Playwright page object must inherit from " +
                "PlaywrightBasePage. Regenerate with correct base class.");

        // Validate no Thread.Sleep
        if (content.Contains("Thread.Sleep"))
            return ToolResponse.Fail(
                "Playwright page objects must NOT use Thread.Sleep. " +
                "Use await Page.WaitForSelectorAsync() or " +
                "built-in Playwright auto-waiting. Regenerate.");

        string fullPath;
        try
        {
            fullPath = ResolveWriteTarget(projectPath, subFolder, fileName);
        }
        catch (UnauthorizedAccessException ex)
        {
            return ToolResponse.Fail(ex.Message);
        }

        var overwrite = GetOverwriteFlag(args);
        if (File.Exists(fullPath) && !overwrite)
            return ToolResponse.Fail(
                $"File already exists: {fullPath}. " +
                "Pass overwrite:true to replace it.");

        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, content);

        return ToolResponse.Ok(new
        {
            status = "written",
            filePath = fullPath,
            fileName
        });
    }

    // ── Scaffold complete Playwright feature ────────────────────────────── One call to write all three files with validation
    public ToolResponse ScaffoldPlaywrightFeature(
        Dictionary<string, object> args)
    {
        var projectPath = args["project_path"].ToString()!;
        var featureName = args["feature_name"].ToString()!;
        var featureContent = args["feature_content"].ToString()!;
        var stepDefContent = args["step_def_content"].ToString()!;
        var pageObjContent = args.GetValueOrDefault(
            "page_object_content", "")?.ToString() ?? "";

        var results = new List<object>();

        // Write feature file
        var featureResult = WritePlaywrightFeatureFile(
            new Dictionary<string, object>
            {
                ["project_path"] = projectPath,
                ["file_name"] = $"{featureName}.feature",
                ["content"] = featureContent,
                ["sub_folder"] = "Features"
            });
        results.Add(new { file = "feature", result = featureResult });

        if (!featureResult.Success)
            return ToolResponse.Fail(
                $"Feature file failed: {featureResult.Error}");

        // Write step definition
        var stepResult = WritePlaywrightStepDefinition(
            new Dictionary<string, object>
            {
                ["project_path"] = projectPath,
                ["file_name"] = $"{featureName}Steps.cs",
                ["content"] = stepDefContent,
                ["sub_folder"] = "Steps"
            });
        results.Add(new { file = "steps", result = stepResult });

        if (!stepResult.Success)
            return ToolResponse.Fail(
                $"Step definition failed: {stepResult.Error}");

        // Write page object if provided
        if (!string.IsNullOrEmpty(pageObjContent))
        {
            var pageResult = WritePlaywrightPageObject(
                new Dictionary<string, object>
                {
                    ["project_path"] = projectPath,
                    ["file_name"] = $"{featureName}Page.cs",
                    ["content"] = pageObjContent,
                    ["sub_folder"] = "Pages"
                });
            results.Add(new { file = "pageObject", result = pageResult });

            if (!pageResult.Success)
                return ToolResponse.Fail(
                    $"Page object failed: {pageResult.Error}");
        }

        return ToolResponse.Ok(new
        {
            status = "scaffold complete",
            framework = "Playwright + SpecFlow",
            featureName,
            files = results
        });
    }


    // ── 4. Write any generic C# class file ───────────────────────────
    public ToolResponse WriteClassFile(Dictionary<string, object> args)
    {
        var projectPath = args["project_path"].ToString()!;
        var fileName = args["file_name"].ToString()!;
        var content = args["content"].ToString()!;
        var subFolder = args.GetValueOrDefault(
            "sub_folder", "")?.ToString() ?? "";

        if (!fileName.EndsWith(".cs"))
            fileName += ".cs";

        string fullPath;
        try
        {
            fullPath = ResolveWriteTarget(projectPath, subFolder, fileName);
        }
        catch (UnauthorizedAccessException ex)
        {
            return ToolResponse.Fail(ex.Message);
        }

        var overwrite = GetOverwriteFlag(args);
        if (File.Exists(fullPath) && !overwrite)
            return ToolResponse.Fail(
                $"File already exists: {fullPath}. " +
                $"Pass overwrite:true to replace it.");

        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, content);

        return ToolResponse.Ok(new
        {
            status = "written",
            filePath = fullPath,
            fileName
        });
    }

    // ── 5. Append steps to an EXISTING step definition ───────────────
    // Instead of creating new file, adds methods to existing one
    public ToolResponse AppendToStepDefinition(
        Dictionary<string, object> args)
    {
        var newStepsCode = args["new_steps_code"].ToString()!;

        string filePath;
        try
        {
            filePath = ResolveExistingCsFilePath(args["file_path"].ToString()!);
        }
        catch (UnauthorizedAccessException ex)
        {
            return ToolResponse.Fail(ex.Message);
        }

        if (!File.Exists(filePath))
            return ToolResponse.Fail($"File not found: {filePath}");

        var existing = File.ReadAllText(filePath);

        // Insert before last closing brace of the class
        var lastBrace = existing.LastIndexOf('}');
        if (lastBrace < 0)
            return ToolResponse.Fail(
                "Could not find closing brace in file.");

        var lastClassBrace = existing.LastIndexOf('}', lastBrace - 1);
        if (lastClassBrace < 0)
            return ToolResponse.Fail(
                "Could not find class closing brace.");

        var updated = existing[..lastClassBrace]
                    + "\n\n"
                    + newStepsCode
                    + "\n"
                    + existing[lastClassBrace..];

        File.WriteAllText(filePath, updated);

        return ToolResponse.Ok(new
        {
            status = "appended",
            filePath,
            addedLines = newStepsCode.Split('\n').Length
        });
    }

    // ── 6. Create entire test structure for a feature in one shot ─────
    // Agent calls this to scaffold everything at once
    public ToolResponse ScaffoldFeature(Dictionary<string, object> args)
    {
        var projectPath = args["project_path"].ToString()!;
        var featureName = args["feature_name"].ToString()!;
        var featureContent = args["feature_content"].ToString()!;
        var stepDefContent = args["step_def_content"].ToString()!;
        var pageObjContent = args.GetValueOrDefault(
            "page_object_content", "")?.ToString() ?? "";

        var results = new List<(string File, ToolResponse Result)>();

        // Write feature file
        var featureResult = WriteFeatureFile(new Dictionary<string, object>
        {
            ["project_path"] = projectPath,
            ["file_name"] = $"{featureName}.feature",
            ["content"] = featureContent,
            ["sub_folder"] = "Features"
        });
        results.Add(("feature", featureResult));

        // Write step definition
        var stepResult = WriteStepDefinition(new Dictionary<string, object>
        {
            ["project_path"] = projectPath,
            ["file_name"] = $"{featureName}Steps.cs",
            ["content"] = stepDefContent,
            ["sub_folder"] = "Steps"
        });
        results.Add(("steps", stepResult));

        // Write page object if provided
        if (!string.IsNullOrEmpty(pageObjContent))
        {
            var pageResult = WritePageObject(
                new Dictionary<string, object>
                {
                    ["project_path"] = projectPath,
                    ["file_name"] = $"{featureName}Page.cs",
                    ["content"] = pageObjContent,
                    ["sub_folder"] = "Pages"
                });
            results.Add(("pageObject", pageResult));
        }

        var allOk = results.All(r => r.Result.Success);
        var resultsForResponse = results.Select(r => new { file = r.File, result = r.Result }).ToList();

        return allOk
            ? ToolResponse.Ok(new
            {
                status = "scaffold complete",
                feature = featureName,
                files = resultsForResponse
            })
            : ToolResponse.Fail(
                $"Some files failed: " +
                $"{JsonSerializer.Serialize(resultsForResponse)}");
    }
}

using ArchitectCentral.AgentStudio.Services;
using Microsoft.Extensions.AI;
using System.ComponentModel;

namespace ArchitectCentral.AgentStudio.Workflows;

/// <summary>
/// Reusable AI function tools for agent generation workflows
/// Uses dependency injection instead of service locator pattern
/// </summary>
public class AgentGenerationTools
{
    private readonly EnhancedRAGQueryService _enhancedRagService;

    public AgentGenerationTools(EnhancedRAGQueryService enhancedRagService)
    {
        _enhancedRagService = enhancedRagService;
    }

    /// <summary>
    /// Get all AI functions for workflow agents
    /// </summary>
    public AIFunction[] GetAllTools()
    {
        return new[]
    {
        QueryArchitectureDocs,
        QueryAcceptedPatterns,
        QueryCodeExamples,
        QueryAntiPatterns,
        QueryRelatedTechnologies,
        ReadExistingArtifact,
        ListRepositoryFiles,
        AnalyzeCodebase
    };
    }

    /// <summary>
    /// Query the architecture documentation RAG system (general purpose)
    /// </summary>
    public AIFunction QueryArchitectureDocs =>
        AIFunctionFactory.Create(QueryArchitectureDocsImpl, name: "query_architecture_docs");

    /// <summary>
    /// Query for ACCEPTED architectural patterns and decisions (authoritative sources only)
    /// </summary>
    public AIFunction QueryAcceptedPatterns =>
        AIFunctionFactory.Create(QueryAcceptedPatternsImpl, name: "query_accepted_patterns");

    /// <summary>
    /// Query for code examples and implementation samples (high quality only)
    /// </summary>
    public AIFunction QueryCodeExamples =>
        AIFunctionFactory.Create(QueryCodeExamplesImpl, name: "query_code_examples");

    /// <summary>
    /// Query for anti-patterns, pitfalls, and things to avoid (negative sentiment)
    /// </summary>
    public AIFunction QueryAntiPatterns =>
        AIFunctionFactory.Create(QueryAntiPatternsImpl, name: "query_anti_patterns");

    /// <summary>
    /// Query for related technologies and dependencies (framework/library guidance)
    /// </summary>
    public AIFunction QueryRelatedTechnologies =>
        AIFunctionFactory.Create(QueryRelatedTechnologiesImpl, name: "query_related_technologies");

    /// <summary>
    /// Read an existing artifact from the repository
    /// </summary>
    public AIFunction ReadExistingArtifact =>
        AIFunctionFactory.Create(ReadExistingArtifactImpl, name: "read_existing_artifact");

    /// <summary>
    /// List files in the repository
    /// </summary>
    public AIFunction ListRepositoryFiles =>
        AIFunctionFactory.Create(ListRepositoryFilesImpl, name: "list_repository_files");

    /// <summary>
    /// Analyze codebase structure and dependencies
    /// </summary>
    public AIFunction AnalyzeCodebase =>
        AIFunctionFactory.Create(AnalyzeCodebaseImpl, name: "analyze_codebase");

    // Tool implementations

    private async Task<string> QueryArchitectureDocsImpl(
        [Description("The search query to find relevant architecture documentation")] string query)
    {
        // Use simple overload that returns List<RAGResult> directly
        var results = await _enhancedRagService.QueryAsync(query);

        if (results.Count == 0)
            return "No relevant documentation found for this query.";

        var output = $"Found {results.Count} relevant documents:\n\n";
        foreach (var result in results.Take(5))
        {
            output += $"[DOC] {result.DocumentType}";
            if (!string.IsNullOrEmpty(result.Section))
                output += $" - {result.Section}";
            output += $" (Similarity: {result.Similarity:P0})\n";
            output += $"{result.Content.Substring(0, Math.Min(300, result.Content.Length))}...\n\n";
        }
        return output;
    }

    private async Task<string> QueryAcceptedPatternsImpl(
        [Description("Search for accepted architectural patterns, decisions, or standards")] string query)
    {
        // Query for ACCEPTED patterns - these are authoritative sources
        var options = new RAGQueryOptions
        {
            EnableQueryOptimization = true,
            EnableHybridSearch = true,
            ApplySuggestedFilters = true,
            RerankByQuality = true
        };

        var ragResponse = await _enhancedRagService.QueryAsync(query, null, options);

        if (ragResponse.Results.Count == 0)
            return $"No accepted patterns found for query: {query}\n\nTry broadening your search terms.";

        var response = $"Found {ragResponse.Results.Count} ACCEPTED architectural patterns:\n\n";
        foreach (var result in ragResponse.Results)
        {
            response += $"✓ [{result.DocumentType}] {result.Section ?? "Main"}\n";
            response += $"  Relevance: {result.Similarity:P0}\n";
            response += $"  Content: {result.Content.Substring(0, Math.Min(400, result.Content.Length))}...\n\n";
        }

        response += "ℹ️  Note: These are ACCEPTED standards - follow them for consistency.\n";
        return response;
    }

    private async Task<string> QueryCodeExamplesImpl(
        [Description("Search for code examples, implementation samples, or usage patterns")] string query)
    {
        // Focus on practical implementation guidance
        var enhancedQuery = $"{query} examples implementation code";

        var options = new RAGQueryOptions
        {
            EnableQueryOptimization = true,
            EnableHybridSearch = true
        };

        var ragResponse = await _enhancedRagService.QueryAsync(enhancedQuery, null, options);

        if (ragResponse.Results.Count == 0)
            return $"No code examples found for: {query}\n\nTry searching for the pattern name directly.";

        var response = $"Found {ragResponse.Results.Count} code examples:\n\n";
        foreach (var result in ragResponse.Results.Take(3))  // Limit to 3 for readability
        {
            response += $"📝 [{result.DocumentType}] {result.Section ?? "Example"}\n";
            response += $"   Relevance: {result.Similarity:P0}\n";

            // Extract code blocks if present
            var content = result.Content;
            if (content.Contains("```"))
            {
                var codeStart = content.IndexOf("```");
                var codeEnd = content.IndexOf("```", codeStart + 3);
                if (codeEnd > codeStart)
                {
                    var codeBlock = content.Substring(codeStart, codeEnd - codeStart + 3);
                    response += $"{codeBlock}\n\n";
                }
            }
            else
            {
                // No code block, show first 300 chars
                response += $"   {content.Substring(0, Math.Min(300, content.Length))}...\n\n";
            }
        }

        response += "ℹ️  Use these examples as templates, but adapt to your specific needs.\n";
        return response;
    }

    private async Task<string> QueryAntiPatternsImpl(
        [Description("Search for anti-patterns, common mistakes, pitfalls, or things to avoid")] string query)
    {
        // Focus on what NOT to do
        var enhancedQuery = $"{query} anti-pattern pitfall avoid mistake deprecated";

        var options = new RAGQueryOptions
        {
            EnableQueryOptimization = true,
            EnableHybridSearch = true
        };

        var ragResponse = await _enhancedRagService.QueryAsync(enhancedQuery, null, options);

        if (ragResponse.Results.Count == 0)
            return $"No anti-patterns found for: {query}\n\nThis might mean there are no known issues with this approach.";

        var response = $"Found {ragResponse.Results.Count} anti-patterns or warnings:\n\n";
        foreach (var result in ragResponse.Results)
        {
            response += $"⚠️  [{result.DocumentType}] {result.Section ?? "Warning"}\n";
            response += $"    Relevance: {result.Similarity:P0}\n";
            response += $"    {result.Content.Substring(0, Math.Min(350, result.Content.Length))}...\n\n";
        }

        response += "ℹ️  AVOID these patterns - they represent known issues or deprecated approaches.\n";
        return response;
    }

    private async Task<string> QueryRelatedTechnologiesImpl(
        [Description("Search for guidance on related technologies, frameworks, or libraries")] string query)
    {
        // Discover dependencies and related technologies
        var enhancedQuery = $"{query} framework library dependency integration";

        var options = new RAGQueryOptions
        {
            EnableQueryOptimization = true,
            EnableHybridSearch = true
        };

        var ragResponse = await _enhancedRagService.QueryAsync(enhancedQuery, null, options);

        if (ragResponse.Results.Count == 0)
            return $"No related technology guidance found for: {query}";

        var response = $"Found {ragResponse.Results.Count} related technology references:\n\n";
        foreach (var result in ragResponse.Results)
        {
            response += $"🔗 [{result.DocumentType}] {result.Section ?? "Technology"}\n";
            response += $"   Relevance: {result.Similarity:P0}\n";
            response += $"   {result.Content.Substring(0, Math.Min(300, result.Content.Length))}...\n\n";
        }

        response += "ℹ️  Consider these related technologies when designing your solution.\n";
        return response;
    }

    private static string ReadExistingArtifactImpl(
        [Description("Type of artifact: 'agent', 'skill', or 'instruction'")] string artifactType,
        [Description("Name/filename of the artifact (e.g., 'create-feature.skill.md')")] string name,
        [Description("Repository path")] string repositoryPath)
    {
        var copilotDir = Path.Combine(repositoryPath, ".copilot");
        if (!Directory.Exists(copilotDir))
            return $"No .copilot directory found in repository at {repositoryPath}";

        string filePath = artifactType.ToLowerInvariant() switch
        {
            "agent" => Path.Combine(copilotDir, name.EndsWith(".agent") ? name : $"{name}.agent"),
            "skill" => Path.Combine(copilotDir, "skills", name.EndsWith(".skill.md") ? name : $"{name}.skill.md"),
            "instruction" => Path.Combine(copilotDir, "instructions", name.EndsWith(".instructions.md") ? name : $"{name}.instructions.md"),
            _ => throw new ArgumentException($"Unknown artifact type: {artifactType}")
        };

        if (File.Exists(filePath))
        {
            var content = File.ReadAllText(filePath);
            return $"Found existing {artifactType} '{name}':\n\n{content}";
        }

        return $"No existing {artifactType} named '{name}' found in repository.";
    }

    private static string ListRepositoryFilesImpl(
        [Description("Repository path to scan")] string repositoryPath,
        [Description("Optional: directory to scan within repo (e.g., 'src', '.copilot')")] string? subdirectory = null,
        [Description("Optional: file pattern (e.g., '*.cs', '*.md')")] string? pattern = null)
    {
        var searchPath = string.IsNullOrEmpty(subdirectory)
            ? repositoryPath
            : Path.Combine(repositoryPath, subdirectory);

        if (!Directory.Exists(searchPath))
            return $"Directory not found: {searchPath}";

        var searchPattern = string.IsNullOrEmpty(pattern) ? "*" : pattern;
        var files = Directory.GetFiles(searchPath, searchPattern, SearchOption.AllDirectories);

        if (files.Length == 0)
            return $"No files found matching pattern '{searchPattern}' in {searchPath}";

        var response = $"Found {files.Length} files:\n\n";
        foreach (var file in files.Take(50))
        {
            var relativePath = Path.GetRelativePath(repositoryPath, file);
            var size = new FileInfo(file).Length;
            response += $"  {relativePath} ({FormatBytes(size)})\n";
        }

        if (files.Length > 50)
            response += $"\n... and {files.Length - 50} more files";

        return response;
    }

    private static string AnalyzeCodebaseImpl(
        [Description("Repository path to analyze")] string repositoryPath,
        [Description("Optional: specific files to analyze (comma-separated)")] string? specificFiles = null)
    {
        var analysis = new System.Text.StringBuilder();
        analysis.AppendLine("# Codebase Analysis\n");

        var projectFiles = Directory.GetFiles(repositoryPath, "*.csproj", SearchOption.AllDirectories)
            .Concat(Directory.GetFiles(repositoryPath, "package.json", SearchOption.AllDirectories))
            .Concat(Directory.GetFiles(repositoryPath, "pom.xml", SearchOption.AllDirectories))
            .Concat(Directory.GetFiles(repositoryPath, "build.gradle", SearchOption.AllDirectories))
            .ToList();

        analysis.AppendLine($"## Project Files ({projectFiles.Count})");
        foreach (var proj in projectFiles.Take(10))
        {
            var relativePath = Path.GetRelativePath(repositoryPath, proj);
            analysis.AppendLine($"  - {relativePath}");
        }
        analysis.AppendLine();

        var copilotDir = Path.Combine(repositoryPath, ".copilot");
        if (Directory.Exists(copilotDir))
        {
            analysis.AppendLine("## Existing Copilot Artifacts");

            var agentFiles = Directory.GetFiles(copilotDir, "*.agent", SearchOption.TopDirectoryOnly);
            analysis.AppendLine($"  Agent files: {agentFiles.Length}");

            var skillsDir = Path.Combine(copilotDir, "skills");
            if (Directory.Exists(skillsDir))
            {
                var skills = Directory.GetFiles(skillsDir, "*.skill.md");
                analysis.AppendLine($"  Skills: {skills.Length}");
            }

            var instructionsDir = Path.Combine(copilotDir, "instructions");
            if (Directory.Exists(instructionsDir))
            {
                var instructions = Directory.GetFiles(instructionsDir, "*.instructions.md");
                analysis.AppendLine($"  Instructions: {instructions.Length}");
            }
            analysis.AppendLine();
        }
        else
        {
            analysis.AppendLine("## Existing Copilot Artifacts");
            analysis.AppendLine("  No .copilot directory found - this will be a new generation.\n");
        }

        analysis.AppendLine("## Detected Technologies");
        if (Directory.GetFiles(repositoryPath, "*.csproj", SearchOption.AllDirectories).Length != 0)
            analysis.AppendLine("  - .NET / C#");
        if (Directory.GetFiles(repositoryPath, "package.json", SearchOption.AllDirectories).Length != 0)
            analysis.AppendLine("  - Node.js / JavaScript / TypeScript");
        if (Directory.GetFiles(repositoryPath, "requirements.txt", SearchOption.AllDirectories).Length != 0)
            analysis.AppendLine("  - Python");
        if (Directory.GetFiles(repositoryPath, "pom.xml", SearchOption.AllDirectories).Length != 0 ||
            Directory.GetFiles(repositoryPath, "build.gradle", SearchOption.AllDirectories).Length != 0)
            analysis.AppendLine("  - Java");

        return analysis.ToString();
    }

    private static string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }
}

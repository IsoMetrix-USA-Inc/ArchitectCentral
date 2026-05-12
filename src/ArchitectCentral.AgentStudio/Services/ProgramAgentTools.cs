using System.ComponentModel;
using ArchitectCentral.AgentStudio.Models.Codebase;
using Microsoft.Extensions.AI;

namespace ArchitectCentral.AgentStudio.Services;

/// <summary>
/// Provides AI function tools for Program.cs agents
/// Replaces the Func closures pattern with proper DI
/// </summary>
public class ProgramAgentTools
{
    private readonly RAGQueryService _ragService;
    private readonly AgentGeneratorService _agentService;

    public ProgramAgentTools(RAGQueryService ragService, AgentGeneratorService agentService)
    {
        _ragService = ragService;
        _agentService = agentService;
    }

    /// <summary>
    /// Query architecture documentation using RAG
    /// </summary>
    public async Task<string> QueryArchitectureDocs(
        [Description("The search query to find relevant architecture documentation")] string query)
    {
        var results = await _ragService.QueryAsync(query);

        if (results.Count == 0)
            return "No relevant documentation found.";

        var response = $"Found {results.Count} relevant documents:\n\n";
        foreach (var result in results.Take(3))
        {
            response += $"[DOC] {result.DocumentType}";
            if (!string.IsNullOrEmpty(result.Section))
                response += $" - {result.Section}";
            response += $" (Similarity: {result.Similarity:P0})\n";
            response += $"{result.Content.Substring(0, Math.Min(200, result.Content.Length))}...\n\n";
        }
        return response;
    }

    /// <summary>
    /// Generate agent artifacts for a codebase
    /// </summary>
    public async Task<string> GenerateAgentArtifacts(
        [Description("Name of the codebase/project")] string projectName,
        [Description("Description of what the codebase does")] string description,
        [Description("Runtime environment (e.g., .NET 8, Node.js)")] string runtime,
        [Description("Framework used (e.g., ASP.NET Core, Express)")] string framework)
    {
        var request = new CodebaseRequest
        {
            Name = projectName,
            Description = description,
            Runtime = runtime,
            Framework = framework,
            Dependencies = new()
        };

        var artifacts = await _agentService.GenerateArtifactsAsync(request);

        return $"[SUCCESS] Generated artifacts for {projectName}:\n\n" +
               $"[AGENT] Agent File:\n{artifacts.AgentFile.Substring(0, Math.Min(300, artifacts.AgentFile.Length))}...\n\n" +
               $"[SKILLS] Skills: {artifacts.Skills.Count} generated\n" +
               $"[INSTRUCTIONS] Instructions: {artifacts.Instructions.Count} generated";
    }

    /// <summary>
    /// Get current time as string
    /// </summary>
    public string GetCurrentTime()
    {
        return DateTime.Now.ToString("HH:mm:ss");
    }

    /// <summary>
    /// Get AI functions for agent registration
    /// </summary>
    public AIFunction[] GetAgentTools() => new[]
    {
        AIFunctionFactory.Create(QueryArchitectureDocs, name: "query_architecture_docs"),
        AIFunctionFactory.Create(GenerateAgentArtifacts, name: "generate_agent_artifacts"),
        AIFunctionFactory.Create(GetCurrentTime, name: "get_current_time")
    };
}

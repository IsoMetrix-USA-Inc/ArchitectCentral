using ArchitectCentral.AgentStudio.Configuration;
using ArchitectCentral.AgentStudio.Models.Codebase;
using ArchitectCentral.AgentStudio.Models.Workflow;
using ArchitectCentral.AgentStudio.Services;
using ArchitectCentral.AgentStudio.Workflows;
using Microsoft.Extensions.AI;
using System.ComponentModel;

namespace ArchitectCentral.AgentStudio.Agents;

public class WorkflowAgentWrapper
{
    private readonly IChatClient _chatClient;
    private readonly Func<RAGQueryService> _getRagService;
    private readonly AppSettings _settings;

    public WorkflowAgentWrapper(IChatClient chatClient, Func<RAGQueryService> getRagService, AppSettings settings)
    {
        _chatClient = chatClient;
        _getRagService = getRagService;
        _settings = settings;
    }

    /// <summary>
    /// Get the AIFunction tool for triggering the workflow
    /// </summary>
    public AIFunction GetWorkflowTool()
    {
        return AIFunctionFactory.Create(TriggerWorkflowImpl, name: "trigger_workflow");
    }

    private async Task<string> TriggerWorkflowImpl(
        [Description("Name of the project")] string projectName,
        [Description("Description")] string description,
        [Description("Runtime (Node.js, .NET, etc)")] string runtime,
        [Description("Framework (Express, ASP.NET Core, etc)")] string framework,
        [Description("Optional dependencies")] string? dependencies = null)
    {
        try
        {
            var depList = string.IsNullOrWhiteSpace(dependencies) ? new List<string>() : dependencies.Split(',').Select(s => s.Trim()).ToList();

            var codebaseMetadata = new CodebaseRequest
            {
                Name = projectName,
                Description = description,
                Runtime = runtime,
                Framework = framework,
                Dependencies = depList.Select(d => new Dependency { Name = d }).ToList(),
                RepositoryPath = ""
            };

            var ragService = _getRagService();
            var architectureQuery = $"{runtime} {framework} best practices";
            var ragResults = await ragService.QueryAsync(architectureQuery);

            var discovery = new DiscoveryState
            {
                Codebase = codebaseMetadata,
                RelevantPatterns = ragResults,
                Existing = null,
                Changes = null
            };

            var context = new GenerationContext
            {
                CodebaseInfo = new CodebaseRequest
                {
                    Name = projectName,
                    Runtime = runtime,
                    Framework = framework,
                    Description = description
                },
                ArchitecturePatterns = ragResults,
                Existing = null,
                IterationCounts = new Dictionary<string, int>(),
                QualityMetrics = new Dictionary<string, double>()
            };

            var result = await AgentFileWorkflow.ExecuteAsync(discovery, context, _chatClient, _settings);

            return $@"[SUCCESS] Workflow Complete!

Quality Score: {result.QualityScore:P0}
Iterations: {result.Iteration}
Status: {(result.Approved ? "Approved" : "Needs Review")}

Generated Content:
{result.Content}";
        }
        catch (Exception ex)
        {
            return $"[ERROR] Workflow failed: {ex.Message}";
        }
    }
}

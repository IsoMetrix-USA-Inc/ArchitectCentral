using ArchitectCentral.AgentStudio.Models.Showcase;
using Microsoft.Agents.AI.Workflows;
using System.Text.Json;

namespace ArchitectCentral.AgentStudio.Workflows.Executors;

/// <summary>
/// Entry point executor that converts string input to ShowcaseWorkflowRequest
/// Uses the correct Executor<TInput, TOutput> pattern from Agent Framework
/// </summary>
public sealed class ShowcaseInputExecutor() : Executor<string, ShowcaseWorkflowRequest>("ShowcaseInput")
{
    public override ValueTask<ShowcaseWorkflowRequest> HandleAsync(
        string message,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        Console.WriteLine("\n📥 Showcase Input Executor: Received String");
        Console.WriteLine($"   Text: {message?.Substring(0, Math.Min(100, message?.Length ?? 0))}...");

        // Try to parse as JSON first
        try
        {
            var request = JsonSerializer.Deserialize<ShowcaseWorkflowRequest>(message ?? "{}");
            if (request != null && !string.IsNullOrEmpty(request.CodebaseName))
            {
                Console.WriteLine($"   ✅ Parsed as JSON: {request.CodebaseName}");
                return ValueTask.FromResult(request);
            }
        }
        catch
        {
            // Not JSON, treat as natural language description
        }

        // Fallback: create request from text
        Console.WriteLine("   📝 Using text as description");
        return ValueTask.FromResult(new ShowcaseWorkflowRequest
        {
            CodebaseName = "Project",
            Description = message ?? "No description provided",
            ArtifactType = "guide"
        });
    }
}

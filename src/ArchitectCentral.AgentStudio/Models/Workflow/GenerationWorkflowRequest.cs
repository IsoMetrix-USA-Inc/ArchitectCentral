using ArchitectCentral.AgentStudio.Models.Codebase;

namespace ArchitectCentral.AgentStudio.Models.Workflow;

/// <summary>
/// Request to start the agent generation workflow
/// </summary>
public class GenerationWorkflowRequest
{
    public CodebaseRequest Codebase { get; set; } = new();
    public string? RepositoryPath { get; set; }
    public bool ForceRegenerate { get; set; } // If true, ignore existing artifacts
    public bool RequireHumanApproval { get; set; } = true;
}

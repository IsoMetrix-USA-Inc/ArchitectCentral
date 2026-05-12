namespace ArchitectCentral.AgentStudio.Models.Requests;

/// <summary>
/// Request model for workflow-based agent generation
/// </summary>
public class WorkflowGenerationRequest
{
    public string? ProjectName { get; set; }
    public string? Description { get; set; }
    public string? Runtime { get; set; }
    public string? Framework { get; set; }
    public List<string>? KeyDependencies { get; set; }
    public string? RepositoryPath { get; set; }
}

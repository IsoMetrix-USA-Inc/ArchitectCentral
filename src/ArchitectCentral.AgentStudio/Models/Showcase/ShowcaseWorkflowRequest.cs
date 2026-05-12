namespace ArchitectCentral.AgentStudio.Models.Showcase;

/// <summary>
/// Request model for showcase workflow generation
/// </summary>
public class ShowcaseWorkflowRequest
{
    public string CodebaseName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ArtifactType { get; set; } = "Agent";
}

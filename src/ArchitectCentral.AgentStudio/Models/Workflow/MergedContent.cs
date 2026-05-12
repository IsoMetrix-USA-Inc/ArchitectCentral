namespace ArchitectCentral.AgentStudio.Models.Workflow;

/// <summary>
/// Result of merging operation
/// </summary>
public class MergedContent
{
    public string Content { get; set; } = string.Empty;
    public List<string> ChangesSummary { get; set; } = new();
    public bool RequiresUserReview { get; set; }
}

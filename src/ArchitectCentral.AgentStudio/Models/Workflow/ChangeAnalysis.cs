namespace ArchitectCentral.AgentStudio.Models.Workflow;

/// <summary>
/// Analysis of what changed between existing and new context
/// </summary>
public class ChangeAnalysis
{
    public List<string> NewDependencies { get; set; } = new();
    public List<string> RemovedDependencies { get; set; } = new();
    public List<string> NewArchitecturePatterns { get; set; } = new();
    public string Summary { get; set; } = string.Empty;
}

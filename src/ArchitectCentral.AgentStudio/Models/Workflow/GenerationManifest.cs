using ArchitectCentral.AgentStudio.Models.Codebase;

namespace ArchitectCentral.AgentStudio.Models.Workflow;

/// <summary>
/// Metadata about a generation run
/// </summary>
public class GenerationManifest
{
    public DateTime GeneratedAt { get; set; }
    public string Mode { get; set; } = "create"; // "create" or "update"
    public CodebaseRequest Codebase { get; set; } = new();
    public List<string> ArchitecturePatternsUsed { get; set; } = new();
    public Dictionary<string, double> QualityScores { get; set; } = new();
    public string GeneratedBy { get; set; } = "AgentStudio-Workflow";
    public string Version { get; set; } = "1.0.0";
}

namespace ArchitectCentral.AgentStudio.Models.Workflow;

/// <summary>
/// Information about existing artifacts found in the repository
/// </summary>
public class ExistingArtifacts
{
    public string? AgentFile { get; set; }
    public Dictionary<string, string> Skills { get; set; } = new(); // filename -> content
    public Dictionary<string, string> Instructions { get; set; } = new(); // filename -> content
    public GenerationManifest? Manifest { get; set; }
    public DateTime? LastGeneratedAt { get; set; }
}

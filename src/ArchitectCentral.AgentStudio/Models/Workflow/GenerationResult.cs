using ArchitectCentral.AgentStudio.Models.Artifacts;

namespace ArchitectCentral.AgentStudio.Models.Workflow;

/// <summary>
/// Final result of the generation workflow
/// </summary>
public class GenerationResult
{
    public bool Success { get; set; }
    public GeneratedArtifacts Artifacts { get; set; } = new();
    public GenerationManifest Manifest { get; set; } = new();
    public List<string> FilePaths { get; set; } = new();
    public TimeSpan Duration { get; set; }
    public List<string> Warnings { get; set; } = new();
    public string? ErrorMessage { get; set; }
}

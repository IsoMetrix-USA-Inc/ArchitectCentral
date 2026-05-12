namespace ArchitectCentral.AgentStudio.Models.Workflow;

/// <summary>
/// Draft artifact during generation process
/// </summary>
public class ArtifactDraft
{
    public string Type { get; set; } = string.Empty; // "AgentFile", "Skill", "Instruction"
    public string Name { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public int Iteration { get; set; } = 1;
    public bool Approved { get; set; }
    public List<string> Feedback { get; set; } = new();
    public double QualityScore { get; set; }
}

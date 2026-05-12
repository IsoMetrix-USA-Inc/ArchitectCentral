namespace ArchitectCentral.AgentStudio.Models.Showcase;

/// <summary>
/// Final result of the showcase workflow execution
/// </summary>
public class ShowcaseResult
{
    public string WorkflowId { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public int Iterations { get; set; }
    public double FinalScore { get; set; }
    public int KnowledgeChunksUsed { get; set; }
    public TimeSpan Duration { get; set; }
}

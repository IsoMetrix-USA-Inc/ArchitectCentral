namespace ArchitectCentral.AgentStudio.Models.Synthesis;

/// <summary>
/// Result of knowledge synthesis operation
/// </summary>
public class SynthesisResult
{
    public KnowledgeGraph KnowledgeGraph { get; set; } = new();
    public int InputChunkCount { get; set; }
    public int OutputItemCount { get; set; }
    public int DuplicatesMerged { get; set; }
    public double SynthesisTimeMs { get; set; }
    public string Summary { get; set; } = string.Empty;
}

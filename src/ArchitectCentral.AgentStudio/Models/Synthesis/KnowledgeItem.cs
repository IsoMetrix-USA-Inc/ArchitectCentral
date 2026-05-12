namespace ArchitectCentral.AgentStudio.Models.Synthesis;

/// <summary>
/// Individual knowledge item extracted from documentation chunks
/// </summary>
public class KnowledgeItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string DocumentType { get; set; } = string.Empty;
    public string? Section { get; set; }
    public double QualityScore { get; set; }
    public double RelevanceScore { get; set; }
    public List<string> SourceChunkIds { get; set; } = new();
    public List<string> Tags { get; set; } = new();
    public List<string> Frameworks { get; set; } = new();
    public bool HasCodeExamples { get; set; }
    public string? Sentiment { get; set; }
    public string? MetadataJson { get; set; }
}

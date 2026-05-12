using ArchitectCentral.AgentStudio.Models.RAG;

namespace ArchitectCentral.AgentStudio.Models.Showcase;

/// <summary>
/// Internal context state for showcase workflow execution
/// </summary>
public class ShowcaseContext
{
    public string WorkflowId { get; set; } = string.Empty;
    public ShowcaseWorkflowRequest Request { get; set; } = new();
    public DateTimeOffset StartTime { get; set; }
    public DiscoveryResult? Discovery { get; set; }
    public List<RAGResult> RawKnowledge { get; set; } = new();
    public List<RAGResult> SynthesizedKnowledge { get; set; } = new();
    public string? FinalContent { get; set; }
    public CritiqueResult? FinalCritique { get; set; }
    public int TotalIterations { get; set; }
}

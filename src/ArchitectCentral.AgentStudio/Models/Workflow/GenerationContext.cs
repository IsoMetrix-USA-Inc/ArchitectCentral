using ArchitectCentral.AgentStudio.Models.Codebase;
using ArchitectCentral.AgentStudio.Models.RAG;
using ArchitectCentral.AgentStudio.Models.Artifacts;

namespace ArchitectCentral.AgentStudio.Models.Workflow;

/// <summary>
/// Shared context across the entire agent generation workflow
/// </summary>
public class GenerationContext
{
    public const string Scope = "GenerationContextScope";
    public const string Key = "singleton";

    // Workflow identification
    public string WorkflowId { get; set; } = string.Empty;
    public DateTimeOffset StartTime { get; set; }

    // Input
    public GenerationWorkflowRequest Request { get; set; } = new();

    // Stage outputs
    public DiscoveryState? Discovery { get; set; }
    public List<RAGResult> ArchitectureKnowledge { get; set; } = new();
    public List<RAGResult> SynthesizedKnowledge { get; set; } = new();
    public ArtifactDraft? FinalArtifact { get; set; }
    public EnhancedCriticDecision? FinalCritique { get; set; }

    // Legacy properties (for backward compatibility)
    public CodebaseRequest CodebaseInfo { get; set; } = new();
    public List<RAGResult> ArchitecturePatterns { get; set; } = new();
    public ExistingArtifacts? Existing { get; set; }
    public Dictionary<string, int> IterationCounts { get; set; } = new();
    public Dictionary<string, double> QualityMetrics { get; set; } = new();
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
}

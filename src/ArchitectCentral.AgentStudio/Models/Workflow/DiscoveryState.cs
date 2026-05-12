using ArchitectCentral.AgentStudio.Models.Codebase;
using ArchitectCentral.AgentStudio.Models.RAG;

namespace ArchitectCentral.AgentStudio.Models.Workflow;

/// <summary>
/// Output from the Discovery phase
/// </summary>
public class DiscoveryState
{
    public CodebaseRequest Codebase { get; set; } = new();
    public string UserRequirements { get; set; } = string.Empty;
    public string ArtifactType { get; set; } = "Agent";
    public ExistingArtifacts? Existing { get; set; }
    public List<RAGResult> RelevantPatterns { get; set; } = new();
    public ChangeAnalysis? Changes { get; set; }
    public bool IsUpdate => Existing != null;
}

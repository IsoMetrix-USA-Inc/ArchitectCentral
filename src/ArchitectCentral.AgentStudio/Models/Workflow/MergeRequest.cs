using ArchitectCentral.AgentStudio.Models.RAG;

namespace ArchitectCentral.AgentStudio.Models.Workflow;

/// <summary>
/// Request for merging existing and new content
/// </summary>
public class MergeRequest
{
    public string ArtifactType { get; set; } = string.Empty; // "AgentFile", "Skill", "Instruction"
    public string Name { get; set; } = string.Empty;
    public string ExistingContent { get; set; } = string.Empty;
    public string GeneratedContent { get; set; } = string.Empty;
    public List<RAGResult> ArchitectureContext { get; set; } = new();
}

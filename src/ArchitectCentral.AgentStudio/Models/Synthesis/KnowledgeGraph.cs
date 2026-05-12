namespace ArchitectCentral.AgentStudio.Models.Synthesis;

/// <summary>
/// Structured knowledge graph organized by type for agent generation
/// </summary>
public class KnowledgeGraph
{
    public List<KnowledgeItem> Principles { get; set; } = new();
    public List<KnowledgeItem> Patterns { get; set; } = new();
    public List<KnowledgeItem> Practices { get; set; } = new();
    public List<KnowledgeItem> Examples { get; set; } = new();
    public List<KnowledgeItem> AntiPatterns { get; set; } = new();
    public List<KnowledgeItem> RelatedTechnologies { get; set; } = new();

    public int TotalItems => 
        Principles.Count + Patterns.Count + Practices.Count + 
        Examples.Count + AntiPatterns.Count + RelatedTechnologies.Count;
}

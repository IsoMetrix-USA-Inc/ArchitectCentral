namespace ArchitectCentral.AgentStudio.Models.Showcase;

/// <summary>
/// Result of the discovery phase
/// </summary>
public class DiscoveryResult
{
    public string CodebaseName { get; set; } = string.Empty;
    public List<string> Technologies { get; set; } = new();
    public List<string> ArchitecturePatterns { get; set; } = new();
    public string RequirementsSummary { get; set; } = string.Empty;
    public string Complexity { get; set; } = string.Empty;
}

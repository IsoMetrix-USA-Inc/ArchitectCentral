namespace ArchitectCentral.AgentStudio.Models.Agent;

/// <summary>
/// Represents an agent definition loaded from markdown file
/// </summary>
public class AgentDefinition
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Instructions { get; set; } = string.Empty;
    public List<string> ToolNames { get; set; } = new();
    public List<string> ConversationStarters { get; set; } = new();
    public Dictionary<string, string> Metadata { get; set; } = new();

    /// <summary>
    /// File path where this definition was loaded from
    /// </summary>
    public string SourceFile { get; set; } = string.Empty;
}

using System.Text.Json.Serialization;

namespace ArchitectCentral.AgentStudio.Models.Workflow;

/// <summary>
/// Critic agent's decision about content quality
/// </summary>
public class CriticDecision
{
    [JsonPropertyName("approved")]
    public bool Approved { get; set; }

    [JsonPropertyName("feedback")]
    public string Feedback { get; set; } = string.Empty;

    [JsonPropertyName("quality_score")]
    public double QualityScore { get; set; }

    [JsonPropertyName("specific_issues")]
    public List<string> SpecificIssues { get; set; } = new();

    // Non-JSON properties for workflow routing
    [JsonIgnore]
    public string Content { get; set; } = string.Empty;

    [JsonIgnore]
    public int Iteration { get; set; }

    [JsonIgnore]
    public string ArtifactType { get; set; } = string.Empty;
}

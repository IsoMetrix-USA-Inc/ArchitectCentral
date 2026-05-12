using System.Text.Json.Serialization;

namespace ArchitectCentral.AgentStudio.Models.Workflow;

/// <summary>
/// Comprehensive quality rubric for evaluating generated artifacts.
/// Based on 6-dimension scoring system for artifact quality assessment.
/// </summary>
public class QualityRubric
{
    /// <summary>
    /// Completeness: Does the artifact cover all necessary aspects?
    /// 0.0-1.0: 0=missing critical elements, 0.5=partial, 1.0=comprehensive
    /// </summary>
    [JsonPropertyName("completeness")]
    public double Completeness { get; set; }

    /// <summary>
    /// Accuracy: Is the information technically correct and aligned with standards?
    /// 0.0-1.0: 0=major errors, 0.5=minor issues, 1.0=fully accurate
    /// </summary>
    [JsonPropertyName("accuracy")]
    public double Accuracy { get; set; }

    /// <summary>
    /// Clarity: Is the content clear, well-organized, and easy to understand?
    /// 0.0-1.0: 0=confusing, 0.5=adequate, 1.0=crystal clear
    /// </summary>
    [JsonPropertyName("clarity")]
    public double Clarity { get; set; }

    /// <summary>
    /// Consistency: Does it align with architecture patterns and coding standards?
    /// 0.0-1.0: 0=conflicts with standards, 0.5=mostly aligned, 1.0=perfect fit
    /// </summary>
    [JsonPropertyName("consistency")]
    public double Consistency { get; set; }

    /// <summary>
    /// Actionability: Can developers immediately use this guidance?
    /// 0.0-1.0: 0=too vague, 0.5=needs some clarification, 1.0=ready to use
    /// </summary>
    [JsonPropertyName("actionability")]
    public double Actionability { get; set; }

    /// <summary>
    /// Tailoring: Is it specific to the codebase, not generic?
    /// 0.0-1.0: 0=generic template, 0.5=partially customized, 1.0=highly specific
    /// </summary>
    [JsonPropertyName("tailoring")]
    public double Tailoring { get; set; }

    /// <summary>
    /// Overall score (weighted average of all dimensions)
    /// </summary>
    [JsonIgnore]
    public double OverallScore => CalculateOverallScore();

    /// <summary>
    /// Calculate weighted overall score
    /// Completeness and Accuracy are weighted higher (20% each)
    /// Other dimensions are 15% each
    /// </summary>
    private double CalculateOverallScore()
    {
        return (Completeness * 0.20) +
               (Accuracy * 0.20) +
               (Clarity * 0.15) +
               (Consistency * 0.15) +
               (Actionability * 0.15) +
               (Tailoring * 0.15);
    }

    /// <summary>
    /// Get textual assessment of overall quality
    /// </summary>
    [JsonIgnore]
    public string QualityLevel => OverallScore switch
    {
        >= 0.9 => "Excellent",
        >= 0.8 => "Good",
        >= 0.7 => "Acceptable",
        >= 0.6 => "Needs Improvement",
        _ => "Poor"
    };

    /// <summary>
    /// Check if quality meets the minimum threshold for approval
    /// </summary>
    public bool MeetsThreshold(double threshold = 0.8) => OverallScore >= threshold;

    public override string ToString()
    {
        return $"""
            Quality Rubric:
            - Completeness:   {Completeness:P0} (covers all aspects)
            - Accuracy:       {Accuracy:P0} (technically correct)
            - Clarity:        {Clarity:P0} (easy to understand)
            - Consistency:    {Consistency:P0} (aligns with standards)
            - Actionability:  {Actionability:P0} (ready to use)
            - Tailoring:      {Tailoring:P0} (codebase-specific)
            
            Overall: {OverallScore:P0} ({QualityLevel})
            """;
    }
}

/// <summary>
/// Enhanced critic decision with detailed rubric scoring
/// </summary>
public class EnhancedCriticDecision
{
    [JsonPropertyName("approved")]
    public bool Approved { get; set; }

    [JsonPropertyName("rubric")]
    public QualityRubric Rubric { get; set; } = new();

    [JsonPropertyName("feedback")]
    public string Feedback { get; set; } = string.Empty;

    [JsonPropertyName("specific_issues")]
    public List<string> SpecificIssues { get; set; } = new();

    [JsonPropertyName("strengths")]
    public List<string> Strengths { get; set; } = new();

    [JsonPropertyName("improvement_suggestions")]
    public List<string> ImprovementSuggestions { get; set; } = new();

    // Non-JSON properties for workflow routing
    [JsonIgnore]
    public string Content { get; set; } = string.Empty;

    [JsonIgnore]
    public int Iteration { get; set; }

    [JsonIgnore]
    public string ArtifactType { get; set; } = string.Empty;

    [JsonIgnore]
    public double QualityScore => Rubric.OverallScore;

    public override string ToString()
    {
        return $"""
            === CRITIQUE DECISION ===
            Status: {(Approved ? "✅ APPROVED" : "❌ REJECTED")}
            Iteration: {Iteration}
            
            {Rubric}
            
            Feedback: {Feedback}
            
            {(SpecificIssues.Count > 0 ? $"Issues Found ({SpecificIssues.Count}):\n" + string.Join("\n", SpecificIssues.Select((x, i) => $"  {i + 1}. {x}")) : "No issues found.")}
            
            {(Strengths.Count > 0 ? $"\nStrengths ({Strengths.Count}):\n" + string.Join("\n", Strengths.Select((x, i) => $"  {i + 1}. {x}")) : "")}
            
            {(ImprovementSuggestions.Count > 0 ? $"\nSuggestions ({ImprovementSuggestions.Count}):\n" + string.Join("\n", ImprovementSuggestions.Select((x, i) => $"  {i + 1}. {x}")) : "")}
            """;
    }
}

namespace ArchitectCentral.AgentStudio.Models.Showcase;

/// <summary>
/// Quality critique result with detailed scoring
/// </summary>
public class CritiqueResult
{
    public double Completeness { get; set; }
    public double Accuracy { get; set; }
    public double Clarity { get; set; }
    public double Consistency { get; set; }
    public double Actionability { get; set; }
    public double Tailoring { get; set; }
    public double OverallScore { get; set; }
    public bool Approved { get; set; }
    public string Feedback { get; set; } = string.Empty;
    public List<string> Issues { get; set; } = new();
}

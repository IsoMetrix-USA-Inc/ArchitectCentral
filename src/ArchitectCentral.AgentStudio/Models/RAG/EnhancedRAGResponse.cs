using ArchitectCentral.AgentStudio.Services.Query;

namespace ArchitectCentral.AgentStudio.Models.RAG;

/// <summary>
/// Enhanced RAG response with optimization details
/// </summary>
public class EnhancedRAGResponse
{
    public string OriginalQuery { get; set; } = string.Empty;
    public string ProcessedQuery { get; set; } = string.Empty;
    public List<RAGResult> Results { get; set; } = new();
    public OptimizedQuery? QueryOptimization { get; set; }
    public string SearchMethod { get; set; } = string.Empty;
    public double ElapsedMilliseconds { get; set; }
    public int ResultCount { get; set; }
}

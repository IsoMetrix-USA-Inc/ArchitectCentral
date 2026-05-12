namespace ArchitectCentral.AgentStudio.Models.RAG;

/// <summary>
/// Configuration options for Enhanced RAG queries
/// </summary>
public class RAGQueryOptions
{
    /// <summary>
    /// Enable query optimization (expansion, rewriting, classification)
    /// </summary>
    public bool EnableQueryOptimization { get; set; } = true;

    /// <summary>
    /// Enable hybrid search (vector + keyword + metadata)
    /// </summary>
    public bool EnableHybridSearch { get; set; } = true;

    /// <summary>
    /// Apply filters suggested by query optimizer
    /// </summary>
    public bool ApplySuggestedFilters { get; set; } = true;

    /// <summary>
    /// Re-rank results by quality score
    /// </summary>
    public bool RerankByQuality { get; set; } = true;

    /// <summary>
    /// Override default TopK
    /// </summary>
    public int? TopK { get; set; }

    /// <summary>
    /// Override default similarity threshold
    /// </summary>
    public double? SimilarityThreshold { get; set; }

    /// <summary>
    /// Additional runtime filters
    /// </summary>
    public Dictionary<string, object>? AdditionalFilters { get; set; }
}

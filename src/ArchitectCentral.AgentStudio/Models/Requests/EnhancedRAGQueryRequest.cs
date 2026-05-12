using System.ComponentModel.DataAnnotations;

namespace ArchitectCentral.AgentStudio.Models.Requests;

/// <summary>
/// Request model for Enhanced RAG query
/// </summary>
public class EnhancedRAGQueryRequest
{
    [Required(ErrorMessage = "Query is required")]
    [StringLength(500, MinimumLength = 3, ErrorMessage = "Query must be between 3 and 500 characters")]
    public string Query { get; set; } = string.Empty;
    
    public string? ConversationContext { get; set; }
    public bool? EnableQueryOptimization { get; set; }
    public bool? EnableHybridSearch { get; set; }
    public bool? ApplySuggestedFilters { get; set; }
    public bool? RerankByQuality { get; set; }
    public int? TopK { get; set; }
    public double? SimilarityThreshold { get; set; }
}

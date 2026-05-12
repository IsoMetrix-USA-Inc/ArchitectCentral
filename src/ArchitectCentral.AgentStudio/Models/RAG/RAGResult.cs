using System.ComponentModel.DataAnnotations;

namespace ArchitectCentral.AgentStudio.Models.RAG;

/// <summary>
/// Result from RAG query
/// </summary>
public class RAGResult
{
    [Required(ErrorMessage = "ChunkId is required")]
    public string ChunkId { get; set; } = string.Empty;
    
    [Required(ErrorMessage = "DocumentType is required")]
    [StringLength(100, ErrorMessage = "DocumentType must not exceed 100 characters")]
    public string DocumentType { get; set; } = string.Empty;
    
    [Required(ErrorMessage = "Content is required")]
    public string Content { get; set; } = string.Empty;
    
    [StringLength(200, ErrorMessage = "Section must not exceed 200 characters")]
    public string? Section { get; set; }
    
    [Range(0.0, 1.0, ErrorMessage = "Similarity must be between 0.0 and 1.0")]
    public double Similarity { get; set; }
    
    public string? MetadataJson { get; set; }
}

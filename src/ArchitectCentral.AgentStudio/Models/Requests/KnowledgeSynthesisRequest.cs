using System.ComponentModel.DataAnnotations;

namespace ArchitectCentral.AgentStudio.Models.Requests;

/// <summary>
/// Request model for Knowledge Synthesis
/// </summary>
public class KnowledgeSynthesisRequest
{
    [Required(ErrorMessage = "Base query is required")]
    [StringLength(500, MinimumLength = 3, ErrorMessage = "Query must be between 3 and 500 characters")]
    public string BaseQuery { get; set; } = string.Empty;
}

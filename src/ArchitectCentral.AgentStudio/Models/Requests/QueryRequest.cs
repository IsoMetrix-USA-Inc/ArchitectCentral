using System.ComponentModel.DataAnnotations;

namespace ArchitectCentral.AgentStudio.Models.Requests;

/// <summary>
/// Request model for querying architecture documentation
/// </summary>
public class QueryRequest
{
    [Required(ErrorMessage = "Query is required")]
    [StringLength(500, MinimumLength = 3, ErrorMessage = "Query must be between 3 and 500 characters")]
    public string Query { get; set; } = string.Empty;
}

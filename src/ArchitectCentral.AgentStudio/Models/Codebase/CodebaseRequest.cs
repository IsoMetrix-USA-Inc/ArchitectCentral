using System.ComponentModel.DataAnnotations;

namespace ArchitectCentral.AgentStudio.Models.Codebase;

/// <summary>
/// Codebase metadata and analysis request.
/// Unified model that handles both input requests and workflow metadata.
/// </summary>
public class CodebaseRequest
{
    [Required(ErrorMessage = "Name is required")]
    [StringLength(200, MinimumLength = 1, ErrorMessage = "Name must be between 1 and 200 characters")]
    public string Name { get; set; } = string.Empty;
    
    [Required(ErrorMessage = "Description is required")]
    [StringLength(1000, MinimumLength = 1, ErrorMessage = "Description must be between 1 and 1000 characters")]
    public string Description { get; set; } = string.Empty;
    
    [Required(ErrorMessage = "Runtime is required")]
    [StringLength(100, MinimumLength = 1, ErrorMessage = "Runtime must be between 1 and 100 characters")]
    public string Runtime { get; set; } = string.Empty;
    
    [Required(ErrorMessage = "Framework is required")]
    [StringLength(100, MinimumLength = 1, ErrorMessage = "Framework must be between 1 and 100 characters")]
    public string Framework { get; set; } = string.Empty;
    
    public List<Dependency> Dependencies { get; set; } = new();
    
    public string RepositoryPath { get; set; } = string.Empty;

    /// <summary>
    /// Gets simplified list of key dependency names for quick access
    /// </summary>
    public List<string> KeyDependencies => Dependencies.Select(d => d.Name).ToList();
}

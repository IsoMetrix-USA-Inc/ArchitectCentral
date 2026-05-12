using System.ComponentModel.DataAnnotations;

namespace ArchitectCentral.AgentStudio.Models.Codebase;

public class Dependency
{
    [Required(ErrorMessage = "Dependency name is required")]
    [StringLength(100, MinimumLength = 1, ErrorMessage = "Dependency name must be between 1 and 100 characters")]
    public string Name { get; set; } = string.Empty;
    
    [Required(ErrorMessage = "Dependency version is required")]
    [StringLength(50, MinimumLength = 1, ErrorMessage = "Version must be between 1 and 50 characters")]
    public string Version { get; set; } = string.Empty;
    
    [StringLength(200, ErrorMessage = "Role must not exceed 200 characters")]
    public string Role { get; set; } = string.Empty;
}

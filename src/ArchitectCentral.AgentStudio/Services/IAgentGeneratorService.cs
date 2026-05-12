using ArchitectCentral.AgentStudio.Models.Artifacts;
using ArchitectCentral.AgentStudio.Models.Codebase;

namespace ArchitectCentral.AgentStudio.Services;

/// <summary>
/// Interface for agent generator service - enables testing and mocking
/// </summary>
public interface IAgentGeneratorService
{
    Task<GeneratedArtifacts> GenerateArtifactsAsync(CodebaseRequest request);
}

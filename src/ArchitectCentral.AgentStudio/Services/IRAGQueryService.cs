using ArchitectCentral.AgentStudio.Models.RAG;

namespace ArchitectCentral.AgentStudio.Services;

/// <summary>
/// Interface for RAG query service - enables testing and mocking
/// </summary>
public interface IRAGQueryService
{
    Task<List<RAGResult>> QueryAsync(string query, int maxResults = 5);
}

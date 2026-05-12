using ArchitectCentral.AgentStudio.Configuration;
using ArchitectCentral.AgentStudio.Models.RAG;
using Azure;
using Azure.AI.OpenAI;
using Npgsql;
using OpenAI.Embeddings;
using Pgvector;

namespace ArchitectCentral.AgentStudio.Services;

/// <summary>
/// Service for querying the RAG vector database
/// </summary>
public class RAGQueryService : IRAGQueryService
{
    private readonly ILogger<RAGQueryService> _logger;
    private readonly NpgsqlDataSource _dataSource;
    private readonly AzureOpenAIClient _openAIClient;
    private readonly EmbeddingClient _embeddingClient;
    private readonly RAGConfig _config;

    public RAGQueryService(
        ILogger<RAGQueryService> logger,
        AppSettings settings,
        NpgsqlDataSource dataSource)
    {
        _logger = logger;
        _config = settings.AgentStudio.RAG;

        // Use the injected data source that already has pgvector support configured
        _dataSource = dataSource;

        _openAIClient = new AzureOpenAIClient(
            new Uri(settings.AzureOpenAI.Endpoint),
            new AzureKeyCredential(settings.AzureOpenAI.ApiKey));

        _embeddingClient = _openAIClient.GetEmbeddingClient(settings.AzureOpenAI.EmbeddingDeploymentName);
    }

    public async Task<List<RAGResult>> QueryAsync(string query, int maxResults = 5)
    {
        _logger.LogInformation("RAG Query: '{Query}' (top {TopK}, threshold {Threshold})",
            query, maxResults, _config.SimilarityThreshold);

        // Generate embedding for query
        var queryEmbedding = await GenerateEmbeddingAsync(query);

        // Search vector database
        var results = await SearchDatabaseAsync(queryEmbedding, maxResults, _config.SimilarityThreshold);

        _logger.LogInformation("Found {Count} results above threshold", results.Count);
        return results;
    }

    private async Task<float[]> GenerateEmbeddingAsync(string text)
    {
        var response = await _embeddingClient.GenerateEmbeddingAsync(text);
        return response.Value.ToFloats().ToArray();
    }

    private async Task<List<RAGResult>> SearchDatabaseAsync(float[] queryEmbedding, int topK, double threshold)
    {
        var results = new List<RAGResult>();

        await using var conn = await _dataSource.OpenConnectionAsync();
        
        // Reload types to ensure pgvector types are available
        // This is necessary after opening a connection from a data source with UseVector()
        conn.ReloadTypes();

        var sql = @"
            SELECT 
                chunk_id,
                doc_type,
                content,
                section,
                metadata,
                1 - (embedding <=> @embedding) AS similarity
            FROM document_chunks
            WHERE (1 - (embedding <=> @embedding)) >= @threshold
            ORDER BY embedding <=> @embedding
            LIMIT @topK";

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("embedding", new Vector(queryEmbedding));
        cmd.Parameters.AddWithValue("threshold", threshold);
        cmd.Parameters.AddWithValue("topK", topK);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            results.Add(new RAGResult
            {
                ChunkId = reader.GetString(0),
                DocumentType = reader.GetString(1),
                Content = reader.GetString(2),
                Section = reader.IsDBNull(3) ? null : reader.GetString(3),
                Similarity = reader.GetDouble(5),
                MetadataJson = reader.IsDBNull(4) ? null : reader.GetString(4)
            });
        }

        return results;
    }
}

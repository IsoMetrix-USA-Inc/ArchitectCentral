using KnowledgebaseVectoriser.Models;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace KnowledgebaseVectoriser.Services
{
    /// <summary>
    /// Service for interacting with the PostgreSQL vector database
    /// </summary>
    public class VectorDatabaseService : IDisposable
    {
        private readonly ILogger<VectorDatabaseService> _logger;
        private readonly NpgsqlDataSource _dataSource;
        private readonly EmbeddingService _embeddingService;

        public VectorDatabaseService(
            ILogger<VectorDatabaseService> logger,
            string connectionString,
            EmbeddingService embeddingService)
        {
            _logger = logger;
            _embeddingService = embeddingService;

            // Build data source with vector support
            var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
            dataSourceBuilder.UseVector();
            _dataSource = dataSourceBuilder.Build();
        }

        /// <summary>
        /// Test database connection
        /// </summary>
        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                await using var connection = await _dataSource.OpenConnectionAsync();
                
                // Reload types to ensure pgvector types are available
                connection.ReloadTypes();
                
                await using var cmd = new NpgsqlCommand("SELECT 1", connection);
                await cmd.ExecuteScalarAsync();
                
                _logger.LogInformation("Database connection successful");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Database connection failed");
                return false;
            }
        }

        /// <summary>
        /// Verify pgvector extension is installed
        /// </summary>
        public async Task<bool> VerifyPgVectorAsync()
        {
            try
            {
                await using var connection = await _dataSource.OpenConnectionAsync();
                
                // Reload types to ensure pgvector types are available
                connection.ReloadTypes();
                
                await using var cmd = new NpgsqlCommand(
                    "SELECT COUNT(*) FROM pg_extension WHERE extname = 'vector'", 
                    connection);
                
                var count = (long)(await cmd.ExecuteScalarAsync() ?? 0L);
                
                if (count > 0)
                {
                    _logger.LogInformation("pgvector extension is installed");
                    return true;
                }
                else
                {
                    _logger.LogError("pgvector extension is not installed");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to verify pgvector extension");
                return false;
            }
        }

        /// <summary>
        /// Upsert document chunks with embeddings
        /// </summary>
        public async Task<int> UpsertChunksAsync(List<DocumentChunk> chunks)
        {
            if (chunks == null || chunks.Count == 0)
            {
                return 0;
            }

            _logger.LogInformation("Upserting {Count} chunks to database", chunks.Count);

            // Generate embeddings for all chunks
            var texts = chunks.Select(c => c.Content).ToList();
            var embeddings = await _embeddingService.GenerateEmbeddingsBatchAsync(texts);

            // Assign embeddings to chunks
            for (int i = 0; i < chunks.Count; i++)
            {
                chunks[i].Embedding = embeddings[i];
            }

            // Upsert to database
            int insertedCount = 0;
            await using var connection = await _dataSource.OpenConnectionAsync();
            
            // Reload types to ensure pgvector types are available
            connection.ReloadTypes();

            foreach (var chunk in chunks)
            {
                try
                {
                    // V2 schema requires source_file_path and uses doc_type_v2 enum
                    await using var cmd = new NpgsqlCommand(@"
                        INSERT INTO document_chunks 
                            (chunk_id, source_doc_id, source_file_path, doc_type, section, content, 
                             chunk_index, metadata, created_at, embedding)
                        VALUES 
                            (@chunk_id, @source_doc_id, @source_file_path, @doc_type::doc_type_v2, 
                             @section, @content, @chunk_index, @metadata::jsonb, @created_at, @embedding)
                        ON CONFLICT (chunk_id) 
                        DO UPDATE SET
                            content = EXCLUDED.content,
                            metadata = EXCLUDED.metadata,
                            embedding = EXCLUDED.embedding,
                            created_at = EXCLUDED.created_at
                    ", connection);

                    cmd.Parameters.AddWithValue("chunk_id", chunk.ChunkId);
                    cmd.Parameters.AddWithValue("source_doc_id", chunk.SourceDocumentId);
                    cmd.Parameters.AddWithValue("source_file_path", chunk.SourceDocumentId); // Use doc ID as placeholder
                    cmd.Parameters.AddWithValue("doc_type", chunk.DocumentType);
                    cmd.Parameters.AddWithValue("section", chunk.Section ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("content", chunk.Content);
                    cmd.Parameters.AddWithValue("chunk_index", chunk.ChunkIndex);
                    cmd.Parameters.AddWithValue("metadata", chunk.MetadataJson ?? "{}");
                    cmd.Parameters.AddWithValue("created_at", chunk.CreatedAt);
                    cmd.Parameters.AddWithValue("embedding", new Pgvector.Vector(chunk.Embedding!.Value.ToArray()));

                    await cmd.ExecuteNonQueryAsync();
                    insertedCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to upsert chunk: {ChunkId}", chunk.ChunkId);
                }
            }

            _logger.LogInformation("Successfully upserted {Count}/{Total} chunks", insertedCount, chunks.Count);
            return insertedCount;
        }

        /// <summary>
        /// Search for similar chunks using vector similarity
        /// </summary>
        public async Task<List<SearchResult>> SearchAsync(string query, int topK = 5, string? documentType = null)
        {
            _logger.LogInformation("Searching for: '{Query}' (top {TopK})", query, topK);

            // Generate embedding for query
            var queryEmbedding = await _embeddingService.GenerateEmbeddingAsync(query);

            await using var connection = await _dataSource.OpenConnectionAsync();
            
            // Reload types to ensure pgvector types are available
            connection.ReloadTypes();

            var sql = @"
                SELECT 
                    chunk_id,
                    source_doc_id,
                    doc_type::text,
                    section,
                    content,
                    metadata,
                    1 - (embedding <=> @embedding) as similarity
                FROM document_chunks
                WHERE (@doc_type::text IS NULL OR doc_type::text = @doc_type::text)
                ORDER BY embedding <=> @embedding
                LIMIT @limit::integer
            ";

            await using var cmd = new NpgsqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("embedding", new Pgvector.Vector(queryEmbedding.ToArray()));
            cmd.Parameters.AddWithValue("limit", topK);
            cmd.Parameters.AddWithValue("doc_type", (object?)documentType ?? DBNull.Value);

            var results = new List<SearchResult>();

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add(new SearchResult
                {
                    ChunkId = reader.GetString(0),
                    SourceDocumentId = reader.GetString(1),
                    DocumentType = reader.GetString(2),
                    Section = reader.IsDBNull(3) ? null : reader.GetString(3),
                    Content = reader.GetString(4),
                    Metadata = reader.IsDBNull(5) ? null : reader.GetString(5),
                    Similarity = reader.GetFloat(6)
                });
            }

            _logger.LogInformation("Found {Count} results", results.Count);
            return results;
        }

        /// <summary>
        /// Get database statistics
        /// </summary>
        public async Task<DatabaseStats> GetStatsAsync()
        {
            await using var connection = await _dataSource.OpenConnectionAsync();
            
            // Reload types to ensure pgvector types are available
            connection.ReloadTypes();

            var stats = new DatabaseStats();

            // Get chunk counts by type
            await using var cmd = new NpgsqlCommand(@"
                SELECT 
                    doc_type::text,
                    COUNT(*) as count
                FROM document_chunks
                GROUP BY doc_type::text
            ", connection);

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var type = reader.GetString(0);
                var count = reader.GetInt64(1);
                stats.ChunkCounts[type] = count;
            }

            return stats;
        }

        public void Dispose()
        {
            _dataSource?.Dispose();
        }
    }

    /// <summary>
    /// Search result from vector similarity search
    /// </summary>
    public class SearchResult
    {
        public string ChunkId { get; set; } = string.Empty;
        public string SourceDocumentId { get; set; } = string.Empty;
        public string DocumentType { get; set; } = string.Empty;
        public string? Section { get; set; }
        public string Content { get; set; } = string.Empty;
        public string? Metadata { get; set; }
        public float Similarity { get; set; }
    }

    /// <summary>
    /// Database statistics
    /// </summary>
    public class DatabaseStats
    {
        public Dictionary<string, long> ChunkCounts { get; set; } = new();
        public long TotalChunks => ChunkCounts.Values.Sum();
    }
}

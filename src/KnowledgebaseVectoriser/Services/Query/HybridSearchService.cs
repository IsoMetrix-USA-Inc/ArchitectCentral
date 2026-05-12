using Microsoft.Extensions.Logging;
using Npgsql;
using Pgvector;

namespace KnowledgebaseVectoriser.Services.Query
{
    /// <summary>
    /// Configuration for hybrid search
    /// </summary>
    public class HybridSearchConfig
    {
        /// <summary>
        /// Enable vector similarity search
        /// </summary>
        public bool EnableVectorSearch { get; set; } = true;

        /// <summary>
        /// Enable keyword/full-text search
        /// </summary>
        public bool EnableKeywordSearch { get; set; } = true;

        /// <summary>
        /// Enable metadata filtering
        /// </summary>
        public bool EnableMetadataFiltering { get; set; } = true;

        /// <summary>
        /// Result fusion strategy
        /// </summary>
        public FusionStrategy FusionStrategy { get; set; } = FusionStrategy.ReciprocalRankFusion;

        /// <summary>
        /// RRF constant (k), typically 60
        /// </summary>
        public int RrfConstant { get; set; } = 60;

        /// <summary>
        /// Weights for weighted linear combination (if used)
        /// </summary>
        public SearchWeights Weights { get; set; } = new();

        /// <summary>
        /// Maximum results to retrieve per search method
        /// </summary>
        public int MaxResultsPerMethod { get; set; } = 20;

        /// <summary>
        /// Final result limit after fusion
        /// </summary>
        public int FinalResultLimit { get; set; } = 10;

        /// <summary>
        /// Minimum similarity threshold (0.0-1.0)
        /// </summary>
        public double MinimumSimilarity { get; set; } = 0.65;

        /// <summary>
        /// Enable pre-filtering before vector search
        /// </summary>
        public bool EnablePreFiltering { get; set; } = true;
    }

    /// <summary>
    /// Search weights for weighted linear combination
    /// </summary>
    public class SearchWeights
    {
        public double VectorSimilarity { get; set; } = 0.60;
        public double KeywordMatch { get; set; } = 0.25;
        public double MetadataRelevance { get; set; } = 0.15;
    }

    /// <summary>
    /// Fusion strategy for combining results
    /// </summary>
    public enum FusionStrategy
    {
        ReciprocalRankFusion,      // RRF - standard, no normalization needed
        WeightedLinearCombination, // Weighted sum of normalized scores
        MaxScore                   // Take maximum score across methods
    }

    /// <summary>
    /// Hybrid search service combining vector, keyword, and metadata search
    /// </summary>
    public class HybridSearchService
    {
        private readonly ILogger<HybridSearchService> _logger;
        private readonly HybridSearchConfig _config;
        private readonly string _connectionString;

        public HybridSearchService(
            ILogger<HybridSearchService> logger,
            string connectionString,
            HybridSearchConfig? config = null)
        {
            _logger = logger;
            _connectionString = connectionString;
            _config = config ?? new HybridSearchConfig();
        }

        /// <summary>
        /// Execute hybrid search combining vector, keyword, and metadata
        /// </summary>
        public async Task<List<HybridSearchResult>> SearchAsync(
            Vector queryEmbedding,
            string queryText,
            SearchFilters? filters = null,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation(
                "Executing hybrid search: Vector={Vector}, Keyword={Keyword}, Filters={HasFilters}",
                _config.EnableVectorSearch,
                _config.EnableKeywordSearch,
                filters != null
            );

            var searchTasks = new List<Task<List<SearchResultWithRank>>>();

            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            // Stage 1: Vector Similarity Search
            if (_config.EnableVectorSearch)
            {
                searchTasks.Add(VectorSearchAsync(
                    connection,
                    queryEmbedding,
                    filters,
                    cancellationToken
                ));
            }

            // Stage 2: Keyword/Full-Text Search
            if (_config.EnableKeywordSearch)
            {
                searchTasks.Add(KeywordSearchAsync(
                    connection,
                    queryText,
                    filters,
                    cancellationToken
                ));
            }

            // Stage 3: Metadata-Only Search (if filters provided but other methods disabled)
            if (!_config.EnableVectorSearch && 
                !_config.EnableKeywordSearch && 
                _config.EnableMetadataFiltering &&
                filters != null)
            {
                searchTasks.Add(MetadataSearchAsync(
                    connection,
                    filters,
                    cancellationToken
                ));
            }

            // Execute all searches in parallel
            var allResults = await Task.WhenAll(searchTasks);

            // Stage 4: Fuse results
            var fusedResults = FuseResults(allResults, _config.FusionStrategy);

            _logger.LogInformation(
                "Hybrid search complete: {SearchCount} methods, {TotalResults} results before fusion, {FinalResults} after fusion",
                searchTasks.Count,
                allResults.Sum(r => r.Count),
                fusedResults.Count
            );

            return fusedResults;
        }

        /// <summary>
        /// Vector similarity search using pgvector
        /// </summary>
        private async Task<List<SearchResultWithRank>> VectorSearchAsync(
            NpgsqlConnection connection,
            Vector queryEmbedding,
            SearchFilters? filters,
            CancellationToken cancellationToken)
        {
            var startTime = DateTime.UtcNow;
            var results = new List<SearchResultWithRank>();

            var sql = BuildVectorSearchQuery(filters);

            await using var cmd = new NpgsqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("query_embedding", queryEmbedding);
            cmd.Parameters.AddWithValue("limit", _config.MaxResultsPerMethod);
            cmd.Parameters.AddWithValue("min_similarity", 1.0 - _config.MinimumSimilarity);

            // Add filter parameters
            AddFilterParameters(cmd, filters);

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

            int rank = 0;
            while (await reader.ReadAsync(cancellationToken))
            {
                results.Add(new SearchResultWithRank
                {
                    ChunkId = reader.GetString(0),
                    Content = reader.GetString(1),
                    SourceDocId = reader.GetString(2),
                    DocType = reader.GetString(3),
                    Section = reader.IsDBNull(4) ? null : reader.GetString(4),
                    Metadata = reader.IsDBNull(5) ? null : reader.GetString(5),
                    Similarity = 1.0 - reader.GetDouble(6), // Convert distance to similarity
                    Rank = rank++,
                    SearchMethod = "vector"
                });
            }

            var elapsed = DateTime.UtcNow - startTime;
            _logger.LogDebug(
                "Vector search: {Count} results in {Elapsed}ms",
                results.Count,
                elapsed.TotalMilliseconds
            );

            return results;
        }

        /// <summary>
        /// Keyword/full-text search using PostgreSQL FTS
        /// </summary>
        private async Task<List<SearchResultWithRank>> KeywordSearchAsync(
            NpgsqlConnection connection,
            string queryText,
            SearchFilters? filters,
            CancellationToken cancellationToken)
        {
            var startTime = DateTime.UtcNow;
            var results = new List<SearchResultWithRank>();

            var sql = BuildKeywordSearchQuery(filters);

            await using var cmd = new NpgsqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("query_text", queryText);
            cmd.Parameters.AddWithValue("limit", _config.MaxResultsPerMethod);

            // Add filter parameters
            AddFilterParameters(cmd, filters);

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

            int rank = 0;
            while (await reader.ReadAsync(cancellationToken))
            {
                var tsRank = reader.GetDouble(6);
                
                results.Add(new SearchResultWithRank
                {
                    ChunkId = reader.GetString(0),
                    Content = reader.GetString(1),
                    SourceDocId = reader.GetString(2),
                    DocType = reader.GetString(3),
                    Section = reader.IsDBNull(4) ? null : reader.GetString(4),
                    Metadata = reader.IsDBNull(5) ? null : reader.GetString(5),
                    Similarity = tsRank, // Use ts_rank as similarity
                    Rank = rank++,
                    SearchMethod = "keyword"
                });
            }

            var elapsed = DateTime.UtcNow - startTime;
            _logger.LogDebug(
                "Keyword search: {Count} results in {Elapsed}ms",
                results.Count,
                elapsed.TotalMilliseconds
            );

            return results;
        }

        /// <summary>
        /// Metadata-only search
        /// </summary>
        private async Task<List<SearchResultWithRank>> MetadataSearchAsync(
            NpgsqlConnection connection,
            SearchFilters filters,
            CancellationToken cancellationToken)
        {
            var startTime = DateTime.UtcNow;
            var results = new List<SearchResultWithRank>();

            var sql = BuildMetadataSearchQuery(filters);

            await using var cmd = new NpgsqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("limit", _config.MaxResultsPerMethod);
            AddFilterParameters(cmd, filters);

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

            int rank = 0;
            while (await reader.ReadAsync(cancellationToken))
            {
                results.Add(new SearchResultWithRank
                {
                    ChunkId = reader.GetString(0),
                    Content = reader.GetString(1),
                    SourceDocId = reader.GetString(2),
                    DocType = reader.GetString(3),
                    Section = reader.IsDBNull(4) ? null : reader.GetString(4),
                    Metadata = reader.IsDBNull(5) ? null : reader.GetString(5),
                    Similarity = 1.0, // Default similarity for metadata matches
                    Rank = rank++,
                    SearchMethod = "metadata"
                });
            }

            var elapsed = DateTime.UtcNow - startTime;
            _logger.LogDebug(
                "Metadata search: {Count} results in {Elapsed}ms",
                results.Count,
                elapsed.TotalMilliseconds
            );

            return results;
        }

        /// <summary>
        /// Build vector search SQL query
        /// </summary>
        private string BuildVectorSearchQuery(SearchFilters? filters)
        {
            var whereConditions = new List<string>
            {
                "embedding <=> @query_embedding <= @min_similarity"
            };

            AddFilterConditions(whereConditions, filters);

            var whereClause = whereConditions.Any()
                ? "WHERE " + string.Join(" AND ", whereConditions)
                : "";

            // Use V2 schema if available, otherwise V1
            var tableName = "document_chunks"; // Will be "document_chunks_v2" when migrated

            return $@"
                SELECT 
                    chunk_id,
                    content,
                    source_document_id,
                    document_type,
                    section,
                    metadata_json,
                    embedding <=> @query_embedding as distance
                FROM {tableName}
                {whereClause}
                ORDER BY embedding <=> @query_embedding
                LIMIT @limit";
        }

        /// <summary>
        /// Build keyword search SQL query
        /// </summary>
        private string BuildKeywordSearchQuery(SearchFilters? filters)
        {
            var whereConditions = new List<string>
            {
                "to_tsvector('english', content) @@ plainto_tsquery('english', @query_text)"
            };

            AddFilterConditions(whereConditions, filters);

            var whereClause = whereConditions.Any()
                ? "WHERE " + string.Join(" AND ", whereConditions)
                : "";

            var tableName = "document_chunks";

            return $@"
                SELECT 
                    chunk_id,
                    content,
                    source_document_id,
                    document_type,
                    section,
                    metadata_json,
                    ts_rank(to_tsvector('english', content), plainto_tsquery('english', @query_text)) as rank
                FROM {tableName}
                {whereClause}
                ORDER BY rank DESC
                LIMIT @limit";
        }

        /// <summary>
        /// Build metadata search SQL query
        /// </summary>
        private string BuildMetadataSearchQuery(SearchFilters filters)
        {
            var whereConditions = new List<string>();
            AddFilterConditions(whereConditions, filters);

            var whereClause = whereConditions.Any()
                ? "WHERE " + string.Join(" AND ", whereConditions)
                : "";

            var tableName = "document_chunks";

            return $@"
                SELECT 
                    chunk_id,
                    content,
                    source_document_id,
                    document_type,
                    section,
                    metadata_json
                FROM {tableName}
                {whereClause}
                ORDER BY created_at DESC
                LIMIT @limit";
        }

        /// <summary>
        /// Add filter conditions to WHERE clause
        /// </summary>
        private void AddFilterConditions(List<string> conditions, SearchFilters? filters)
        {
            if (filters == null || !_config.EnableMetadataFiltering)
                return;

            if (!string.IsNullOrWhiteSpace(filters.DocType))
                conditions.Add("document_type = @doc_type");

            if (!string.IsNullOrWhiteSpace(filters.Status))
                conditions.Add("status = @status");

            if (!string.IsNullOrWhiteSpace(filters.Category))
                conditions.Add("category = @category");

            if (!string.IsNullOrWhiteSpace(filters.Sentiment))
                conditions.Add("sentiment = @sentiment");

            if (filters.MinQualityScore.HasValue)
                conditions.Add("quality_score >= @min_quality");

            if (filters.TechnicalTags?.Any() == true)
                conditions.Add("technical_tags && @technical_tags");

            if (filters.FrameworkRefs?.Any() == true)
                conditions.Add("framework_refs && @framework_refs");

            if (filters.HasCodeExamples.HasValue)
                conditions.Add("has_code_examples = @has_code_examples");
        }

        /// <summary>
        /// Add filter parameters to command
        /// </summary>
        private void AddFilterParameters(NpgsqlCommand cmd, SearchFilters? filters)
        {
            if (filters == null)
                return;

            if (!string.IsNullOrWhiteSpace(filters.DocType))
                cmd.Parameters.AddWithValue("doc_type", filters.DocType);

            if (!string.IsNullOrWhiteSpace(filters.Status))
                cmd.Parameters.AddWithValue("status", filters.Status);

            if (!string.IsNullOrWhiteSpace(filters.Category))
                cmd.Parameters.AddWithValue("category", filters.Category);

            if (!string.IsNullOrWhiteSpace(filters.Sentiment))
                cmd.Parameters.AddWithValue("sentiment", filters.Sentiment);

            if (filters.MinQualityScore.HasValue)
                cmd.Parameters.AddWithValue("min_quality", filters.MinQualityScore.Value);

            if (filters.TechnicalTags?.Any() == true)
                cmd.Parameters.AddWithValue("technical_tags", filters.TechnicalTags.ToArray());

            if (filters.FrameworkRefs?.Any() == true)
                cmd.Parameters.AddWithValue("framework_refs", filters.FrameworkRefs.ToArray());

            if (filters.HasCodeExamples.HasValue)
                cmd.Parameters.AddWithValue("has_code_examples", filters.HasCodeExamples.Value);
        }

        /// <summary>
        /// Fuse results from multiple search methods
        /// </summary>
        private List<HybridSearchResult> FuseResults(
            List<SearchResultWithRank>[] allResults,
            FusionStrategy strategy)
        {
            var startTime = DateTime.UtcNow;

            // Flatten all results
            var flatResults = allResults.SelectMany(r => r).ToList();

            // Group by chunk_id
            var groupedResults = flatResults
                .GroupBy(r => r.ChunkId)
                .Select(g => new
                {
                    ChunkId = g.Key,
                    Results = g.ToList()
                })
                .ToList();

            // Calculate fused scores
            var fusedResults = groupedResults
                .Select(g => CalculateFusedScore(g.Results, strategy))
                .OrderByDescending(r => r.FusedScore)
                .Take(_config.FinalResultLimit)
                .ToList();

            var elapsed = DateTime.UtcNow - startTime;
            _logger.LogDebug(
                "Result fusion ({Strategy}): {InputCount} results → {OutputCount} results in {Elapsed}ms",
                strategy,
                flatResults.Count,
                fusedResults.Count,
                elapsed.TotalMilliseconds
            );

            return fusedResults;
        }

        /// <summary>
        /// Calculate fused score for a chunk appearing in multiple result sets
        /// </summary>
        private HybridSearchResult CalculateFusedScore(
            List<SearchResultWithRank> results,
            FusionStrategy strategy)
        {
            // Take the first result for base data
            var baseResult = results[0];

            double fusedScore = strategy switch
            {
                FusionStrategy.ReciprocalRankFusion => CalculateRrfScore(results),
                FusionStrategy.WeightedLinearCombination => CalculateWeightedScore(results),
                FusionStrategy.MaxScore => CalculateMaxScore(results),
                _ => CalculateRrfScore(results)
            };

            return new HybridSearchResult
            {
                ChunkId = baseResult.ChunkId,
                Content = baseResult.Content,
                SourceDocId = baseResult.SourceDocId,
                DocType = baseResult.DocType,
                Section = baseResult.Section,
                Metadata = baseResult.Metadata,
                FusedScore = fusedScore,
                SearchMethods = results.Select(r => r.SearchMethod).Distinct().ToList(),
                ComponentScores = results.ToDictionary(
                    r => r.SearchMethod,
                    r => r.Similarity
                )
            };
        }

        /// <summary>
        /// Reciprocal Rank Fusion (RRF)
        /// </summary>
        private double CalculateRrfScore(List<SearchResultWithRank> results)
        {
            // RRF formula: score = Σ(1 / (k + rank_i))
            // where k is typically 60, rank_i is the position in result set i
            var k = _config.RrfConstant;
            return results.Sum(r => 1.0 / (k + r.Rank));
        }

        /// <summary>
        /// Weighted linear combination of normalized scores
        /// </summary>
        private double CalculateWeightedScore(List<SearchResultWithRank> results)
        {
            double score = 0.0;

            foreach (var result in results)
            {
                var weight = result.SearchMethod switch
                {
                    "vector" => _config.Weights.VectorSimilarity,
                    "keyword" => _config.Weights.KeywordMatch,
                    "metadata" => _config.Weights.MetadataRelevance,
                    _ => 1.0
                };

                // Normalize similarity to [0, 1] and apply weight
                var normalizedScore = Math.Max(0, Math.Min(1, result.Similarity));
                score += weight * normalizedScore;
            }

            return score;
        }

        /// <summary>
        /// Maximum score across all methods
        /// </summary>
        private double CalculateMaxScore(List<SearchResultWithRank> results)
        {
            return results.Max(r => r.Similarity);
        }
    }

    /// <summary>
    /// Search filters for hybrid search
    /// </summary>
    public class SearchFilters
    {
        public string? DocType { get; set; }
        public string? Status { get; set; }
        public string? Category { get; set; }
        public string? Sentiment { get; set; }
        public decimal? MinQualityScore { get; set; }
        public List<string>? TechnicalTags { get; set; }
        public List<string>? FrameworkRefs { get; set; }
        public bool? HasCodeExamples { get; set; }
    }

    /// <summary>
    /// Search result with rank information
    /// </summary>
    internal class SearchResultWithRank
    {
        public string ChunkId { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string SourceDocId { get; set; } = string.Empty;
        public string DocType { get; set; } = string.Empty;
        public string? Section { get; set; }
        public string? Metadata { get; set; }
        public double Similarity { get; set; }
        public int Rank { get; set; }
        public string SearchMethod { get; set; } = string.Empty;
    }

    /// <summary>
    /// Hybrid search result with fused score
    /// </summary>
    public class HybridSearchResult
    {
        public string ChunkId { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string SourceDocId { get; set; } = string.Empty;
        public string DocType { get; set; } = string.Empty;
        public string? Section { get; set; }
        public string? Metadata { get; set; }
        public double FusedScore { get; set; }
        public List<string> SearchMethods { get; set; } = new();
        public Dictionary<string, double> ComponentScores { get; set; } = new();
    }
}

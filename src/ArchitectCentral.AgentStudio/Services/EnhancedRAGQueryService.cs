using ArchitectCentral.AgentStudio.Configuration;
using ArchitectCentral.AgentStudio.Models.RAG;
using ArchitectCentral.AgentStudio.Services.Query;
using Azure;
using Azure.AI.OpenAI;
using Npgsql;
using OpenAI.Embeddings;
using Pgvector;

namespace ArchitectCentral.AgentStudio.Services;

/// <summary>
/// Enhanced RAG Query Service with hybrid search, query optimization, and metadata filtering
/// </summary>
public class EnhancedRAGQueryService
{
    private readonly ILogger<EnhancedRAGQueryService> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly NpgsqlDataSource _dataSource;
    private readonly AzureOpenAIClient _openAIClient;
    private readonly EmbeddingClient _embeddingClient;
    private readonly RAGConfig _config;

    // Optional: Query optimizer and hybrid search
    private readonly QueryOptimizerService _queryOptimizer;
    private readonly HybridSearchService _hybridSearch;

    public EnhancedRAGQueryService(
        ILogger<EnhancedRAGQueryService> logger,
        ILoggerFactory loggerFactory,
        AppSettings settings,
        NpgsqlDataSource dataSource)
    {
        _logger = logger;
        _loggerFactory = loggerFactory;
        _config = settings.AgentStudio.RAG;

        // Use the injected data source that already has pgvector support configured
        _dataSource = dataSource;

        _openAIClient = new AzureOpenAIClient(
            new Uri(settings.AzureOpenAI.Endpoint),
            new AzureKeyCredential(settings.AzureOpenAI.ApiKey));

        _embeddingClient = _openAIClient.GetEmbeddingClient(settings.AzureOpenAI.EmbeddingDeploymentName);

        // Initialize query optimizer
        var optimizerConfig = new QueryOptimizationConfig
        {
            EnableQueryExpansion = true,
            EnableQueryRewriting = true,
            EnableQueryClassification = true,
            EnableFilterSuggestion = true,
            MaxExpandedTerms = 5,
            UseConversationContext = false
        };
        _queryOptimizer = new QueryOptimizerService(
            _loggerFactory.CreateLogger<QueryOptimizerService>(),
            optimizerConfig);

        // Initialize hybrid search with the shared DataSource
        var hybridConfig = new HybridSearchConfig
        {
            EnableVectorSearch = true,
            EnableKeywordSearch = true,
            EnableMetadataFiltering = true,
            FusionStrategy = FusionStrategy.ReciprocalRankFusion,
            MaxResultsPerMethod = 20,
            FinalResultLimit = _config.TopK,
            MinimumSimilarity = _config.SimilarityThreshold
        };
        _hybridSearch = new HybridSearchService(
            _loggerFactory.CreateLogger<HybridSearchService>(),
            _dataSource,  // Pass the DataSource instead of connection string
            hybridConfig);
    }

    /// <summary>
    /// Query with automatic optimization and hybrid search
    /// </summary>
    public async Task<EnhancedRAGResponse> QueryAsync(
        string query,
        string? conversationContext = null,
        RAGQueryOptions? options = null)
    {
        var queryOptions = options ?? new RAGQueryOptions();
        var startTime = DateTime.UtcNow;

        _logger.LogInformation(
            "Enhanced RAG Query: '{Query}' (Optimizer=True, Hybrid={Hybrid})",
            query,
            queryOptions.EnableHybridSearch
        );

        OptimizedQuery? optimizedQuery = null;
        string processedQuery = query;
        SearchFilters? filters = null;

        // Step 1: Query Optimization (if available)
        if (_queryOptimizer != null && queryOptions.EnableQueryOptimization)
        {
            optimizedQuery = _queryOptimizer.OptimizeQuery(query, conversationContext);
            processedQuery = optimizedQuery.ProcessedQuery;

            // Build filters from suggestions
            if (queryOptions.ApplySuggestedFilters)
            {
                filters = BuildFiltersFromSuggestions(optimizedQuery.SuggestedFilters);
            }

            _logger.LogInformation(
                "Query optimized: Type={Type}, Strategy={Strategy}, Expansions={ExpCount}, Filters={FilterCount}",
                optimizedQuery.QueryType,
                optimizedQuery.SearchStrategy,
                optimizedQuery.ExpandedTerms.Count,
                optimizedQuery.SuggestedFilters.Count
            );
        }

        // Step 2: Generate embedding
        var queryEmbedding = await GenerateEmbeddingAsync(processedQuery);

        List<RAGResult> results;

        // Step 3: Execute search (hybrid or standard)
        if (queryOptions.EnableHybridSearch)
        {
            // Use hybrid search
            var hybridResults = await _hybridSearch.SearchAsync(
                new Vector(queryEmbedding),
                processedQuery,
                filters
            );

            results = ConvertHybridResults(hybridResults);

            _logger.LogInformation(
                "Hybrid search: {Count} results (avg fused score: {AvgScore:F3})",
                results.Count,
                hybridResults.Average(r => r.FusedScore)
            );
        }
        else
        {
            // Use standard vector search
            results = await StandardSearchAsync(
                queryEmbedding,
                queryOptions.TopK ?? _config.TopK,
                queryOptions.SimilarityThreshold ?? _config.SimilarityThreshold,
                filters
            );

            _logger.LogInformation(
                "Vector search: {Count} results (avg similarity: {AvgSim:F3})",
                results.Count,
                results.Count > 0 ? results.Average(r => r.Similarity) : 0
            );
        }

        // Step 4: Apply additional filters if specified
        if (queryOptions.AdditionalFilters != null)
        {
            results = ApplyAdditionalFilters(results, queryOptions.AdditionalFilters);
        }

        // Step 5: Re-rank by quality if enabled
        if (queryOptions.RerankByQuality)
        {
            results = RerankByQuality(results);
        }

        var elapsed = DateTime.UtcNow - startTime;

        return new EnhancedRAGResponse
        {
            OriginalQuery = query,
            ProcessedQuery = processedQuery,
            Results = results,
            QueryOptimization = optimizedQuery,
            SearchMethod = _hybridSearch != null && queryOptions.EnableHybridSearch
                ? "hybrid"
                : "vector",
            ElapsedMilliseconds = elapsed.TotalMilliseconds,
            ResultCount = results.Count
        };
    }

    /// <summary>
    /// Backward-compatible query method
    /// </summary>
    public async Task<List<RAGResult>> QueryAsync(string query)
    {
        var response = await QueryAsync(query, null, new RAGQueryOptions
        {
            EnableQueryOptimization = false,
            EnableHybridSearch = false
        });

        return response.Results;
    }

    /// <summary>
    /// Generate embedding for text
    /// </summary>
    private async Task<float[]> GenerateEmbeddingAsync(string text)
    {
        var response = await _embeddingClient.GenerateEmbeddingAsync(text);
        return response.Value.ToFloats().ToArray();
    }

    /// <summary>
    /// Standard vector search (backward compatible)
    /// </summary>
    private async Task<List<RAGResult>> StandardSearchAsync(
        float[] queryEmbedding,
        int topK,
        double threshold,
        SearchFilters? filters = null)
    {
        var results = new List<RAGResult>();

        await using var conn = await _dataSource.OpenConnectionAsync();

        // Reload types to ensure pgvector types are available
        // This is necessary after opening a connection from a data source with UseVector()
        conn.ReloadTypes();

        var whereConditions = new List<string>
        {
            "(1 - (embedding <=> @embedding)) >= @threshold"
        };

        // Add filter conditions
        AddFilterConditions(whereConditions, filters);

        var whereClause = "WHERE " + string.Join(" AND ", whereConditions);

        var sql = $@"
            SELECT 
                chunk_id,
                doc_type,
                content,
                section,
                metadata,
                1 - (embedding <=> @embedding) AS similarity
            FROM document_chunks
            {whereClause}
            ORDER BY embedding <=> @embedding
            LIMIT @topK";

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("embedding", new Vector(queryEmbedding));
        cmd.Parameters.AddWithValue("threshold", threshold);
        cmd.Parameters.AddWithValue("topK", topK);

        // Add filter parameters
        AddFilterParameters(cmd, filters);

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

    /// <summary>
    /// Convert hybrid search results to RAG results
    /// </summary>
    private static List<RAGResult> ConvertHybridResults(List<HybridSearchResult> hybridResults)
    {
        return hybridResults.Select(h => new RAGResult
        {
            ChunkId = h.ChunkId,
            DocumentType = h.DocType,
            Content = h.Content,
            Section = h.Section,
            Similarity = h.FusedScore,
            MetadataJson = h.Metadata
        }).ToList();
    }

    /// <summary>
    /// Build filters from query optimizer suggestions
    /// </summary>
    private static SearchFilters BuildFiltersFromSuggestions(List<FilterSuggestion> suggestions)
    {
        var filters = new SearchFilters();

        foreach (var suggestion in suggestions)
        {
            switch (suggestion.FilterType)
            {
                case "status":
                    filters.Status = suggestion.FilterValue;
                    break;

                case "sentiment":
                    filters.Sentiment = suggestion.FilterValue;
                    break;

                case "quality_score":
                    var scoreStr = suggestion.FilterValue.Replace(">=", "").Trim();
                    if (decimal.TryParse(scoreStr, out var score))
                    {
                        filters.MinQualityScore = score;
                    }
                    break;

                case "has_code_examples":
                    if (bool.TryParse(suggestion.FilterValue, out var hasCode))
                    {
                        filters.HasCodeExamples = hasCode;
                    }
                    break;

                case "doc_type":
                    filters.DocType = suggestion.FilterValue;
                    break;

                case "category":
                    filters.Category = suggestion.FilterValue;
                    break;
            }
        }

        return filters;
    }

    /// <summary>
    /// Add filter conditions to WHERE clause
    /// </summary>
    private static void AddFilterConditions(List<string> conditions, SearchFilters? filters)
    {
        if (filters == null)
            return;

        if (!string.IsNullOrWhiteSpace(filters.DocType))
            conditions.Add("doc_type = @doc_type");

        if (!string.IsNullOrWhiteSpace(filters.Status))
            conditions.Add("status = @status");

        if (!string.IsNullOrWhiteSpace(filters.Category))
            conditions.Add("category = @category");

        if (!string.IsNullOrWhiteSpace(filters.Sentiment))
            conditions.Add("sentiment = @sentiment");

        if (filters.MinQualityScore.HasValue)
            conditions.Add("quality_score >= @min_quality");

        if (filters.TechnicalTags?.Count != 0)
            conditions.Add("technical_tags && @technical_tags");

        if (filters.FrameworkRefs?.Count != 0)
            conditions.Add("framework_refs && @framework_refs");

        if (filters.HasCodeExamples.HasValue)
            conditions.Add("has_code_examples = @has_code_examples");
    }

    /// <summary>
    /// Add filter parameters to command
    /// </summary>
    private static void AddFilterParameters(NpgsqlCommand cmd, SearchFilters? filters)
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

        if (filters.TechnicalTags?.Count != 0)
            cmd.Parameters.AddWithValue("technical_tags", filters.TechnicalTags.ToArray() ?? []);

        if (filters.FrameworkRefs?.Count != 0)
            cmd.Parameters.AddWithValue("framework_refs", filters.FrameworkRefs.ToArray());

        if (filters.HasCodeExamples.HasValue)
            cmd.Parameters.AddWithValue("has_code_examples", filters.HasCodeExamples.Value);
    }

    /// <summary>
    /// Apply additional runtime filters to results
    /// </summary>
    private static List<RAGResult> ApplyAdditionalFilters(
        List<RAGResult> results,
        Dictionary<string, object> filters)
    {
        // Example: Filter by metadata properties
        // This is a simplified implementation
        return results;
    }

    /// <summary>
    /// Re-rank results by quality score
    /// </summary>
    private static List<RAGResult> RerankByQuality(List<RAGResult> results)
    {
        return results
            .OrderByDescending(r =>
            {
                var qualityMultiplier = GetQualityMultiplier(r);
                return r.Similarity * qualityMultiplier;
            })
            .ToList();
    }

    /// <summary>
    /// Get quality multiplier for re-ranking
    /// </summary>
    private static double GetQualityMultiplier(RAGResult result)
    {
        if (string.IsNullOrWhiteSpace(result.MetadataJson))
            return 1.0;

        try
        {
            var metadata = System.Text.Json.JsonSerializer.Deserialize<
                Dictionary<string, System.Text.Json.JsonElement>>(result.MetadataJson);

            if (metadata != null && metadata.TryGetValue("quality_score", out var qualityElem))
            {
                var qualityScore = qualityElem.GetDouble();

                // Boost high-quality results
                if (qualityScore >= 0.8) return 1.2;
                if (qualityScore >= 0.7) return 1.1;
                if (qualityScore < 0.5) return 0.9;
            }
        }
        catch
        {
            // Ignore parse errors
        }

        return 1.0;
    }
}

/// <summary>
/// Options for RAG query
/// </summary>
public class RAGQueryOptions
{
    /// <summary>
    /// Enable query optimization (expansion, rewriting, classification)
    /// </summary>
    public bool EnableQueryOptimization { get; set; } = true;

    /// <summary>
    /// Enable hybrid search (vector + keyword + metadata)
    /// </summary>
    public bool EnableHybridSearch { get; set; } = true;

    /// <summary>
    /// Apply filters suggested by query optimizer
    /// </summary>
    public bool ApplySuggestedFilters { get; set; } = true;

    /// <summary>
    /// Re-rank results by quality score
    /// </summary>
    public bool RerankByQuality { get; set; } = true;

    /// <summary>
    /// Override default TopK
    /// </summary>
    public int? TopK { get; set; }

    /// <summary>
    /// Override default similarity threshold
    /// </summary>
    public double? SimilarityThreshold { get; set; }

    /// <summary>
    /// Additional runtime filters
    /// </summary>
    public Dictionary<string, object>? AdditionalFilters { get; set; }
}

/// <summary>
/// Enhanced RAG response with optimization details
/// </summary>
public class EnhancedRAGResponse
{
    public string OriginalQuery { get; set; } = string.Empty;
    public string ProcessedQuery { get; set; } = string.Empty;
    public List<RAGResult> Results { get; set; } = new();
    public OptimizedQuery? QueryOptimization { get; set; }
    public string SearchMethod { get; set; } = string.Empty;
    public double ElapsedMilliseconds { get; set; }
    public int ResultCount { get; set; }
}

using KnowledgebaseVectoriser.Models;
using Microsoft.Extensions.Logging;

namespace KnowledgebaseVectoriser.Services
{
    /// <summary>
    /// Service for querying the vector database using RAG (Retrieval Augmented Generation)
    /// </summary>
    public class RAGQueryService
    {
        private readonly ILogger<RAGQueryService> _logger;
        private readonly VectorDatabaseService _vectorDb;
        private readonly EmbeddingService _embeddingService;

        public RAGQueryService(
            ILogger<RAGQueryService> logger,
            VectorDatabaseService vectorDb,
            EmbeddingService embeddingService)
        {
            _logger = logger;
            _vectorDb = vectorDb;
            _embeddingService = embeddingService;
        }

        /// <summary>
        /// Search for relevant documentation chunks using semantic similarity
        /// </summary>
        public async Task<List<SearchResult>> SearchAsync(
            string query,
            int topK = 5,
            double similarityThreshold = 0.7,
            string? documentType = null)
        {
            _logger.LogInformation("RAG Query: '{Query}' (top {TopK}, threshold {Threshold})", 
                query, topK, similarityThreshold);

            // Perform vector similarity search
            var results = await _vectorDb.SearchAsync(query, topK, documentType);

            // Filter by similarity threshold
            var filtered = results.Where(r => r.Similarity >= similarityThreshold).ToList();

            _logger.LogInformation("Found {Total} results, {Filtered} above threshold {Threshold}",
                results.Count, filtered.Count, similarityThreshold);

            return filtered;
        }

        /// <summary>
        /// Search with metadata filters for more precise retrieval
        /// </summary>
        public async Task<List<SearchResult>> SearchWithFiltersAsync(
            string query,
            SearchFilters filters)
        {
            _logger.LogInformation("RAG Query with filters: '{Query}'", query);

            // Perform base search
            var results = await _vectorDb.SearchAsync(
                query, 
                filters.TopK, 
                filters.DocumentType);

            // Apply additional metadata filters
            var filtered = results.AsEnumerable();

            if (filters.SimilarityThreshold.HasValue)
            {
                filtered = filtered.Where(r => r.Similarity >= filters.SimilarityThreshold.Value);
            }

            if (filters.RequiredTags != null && filters.RequiredTags.Count > 0)
            {
                filtered = filtered.Where(r => 
                    filters.RequiredTags.All(tag => 
                        r.Metadata?.Contains(tag, StringComparison.OrdinalIgnoreCase) ?? false));
            }

            if (filters.ExcludeSections != null && filters.ExcludeSections.Count > 0)
            {
                filtered = filtered.Where(r => 
                    !filters.ExcludeSections.Any(section => 
                        r.Section?.Contains(section, StringComparison.OrdinalIgnoreCase) ?? false));
            }

            var finalResults = filtered.ToList();

            _logger.LogInformation("Filtered to {Count} results", finalResults.Count);

            return finalResults;
        }

        /// <summary>
        /// Build a coherent context from search results for LLM consumption
        /// </summary>
        public string BuildContext(List<SearchResult> results, int maxTokens = 4000)
        {
            _logger.LogInformation("Building context from {Count} results (max {MaxTokens} tokens)", 
                results.Count, maxTokens);

            var contextParts = new List<string>();
            int currentTokens = 0;

            foreach (var result in results.OrderByDescending(r => r.Similarity))
            {
                // Estimate tokens (rough: 1 token ≈ 4 characters)
                var tokens = result.Content.Length / 4;

                if (currentTokens + tokens > maxTokens)
                {
                    _logger.LogDebug("Reached token limit, stopping at {Count} chunks", contextParts.Count);
                    break;
                }

                // Build formatted chunk with metadata
                var chunkContext = $"""
                    ## Source: {result.SourceDocumentId} ({result.DocumentType})
                    **Section**: {result.Section ?? "General"}
                    **Relevance**: {result.Similarity:P1}
                    
                    {result.Content}
                    
                    ---
                    """;

                contextParts.Add(chunkContext);
                currentTokens += tokens;
            }

            var finalContext = string.Join("\n\n", contextParts);

            _logger.LogInformation("Built context: {Chunks} chunks, ~{Tokens} tokens", 
                contextParts.Count, currentTokens);

            return finalContext;
        }

        /// <summary>
        /// Perform multi-query retrieval for complex questions
        /// Generates multiple search queries to capture different aspects
        /// </summary>
        public async Task<List<SearchResult>> MultiQuerySearchAsync(
            string originalQuery,
            List<string> additionalQueries,
            int topKPerQuery = 3,
            double similarityThreshold = 0.7)
        {
            _logger.LogInformation("Multi-query search: {TotalQueries} queries", 
                additionalQueries.Count + 1);

            var allResults = new Dictionary<string, SearchResult>();

            // Search with original query
            var originalResults = await SearchAsync(originalQuery, topKPerQuery, similarityThreshold);
            foreach (var result in originalResults)
            {
                allResults[result.ChunkId] = result;
            }

            // Search with additional queries
            foreach (var additionalQuery in additionalQueries)
            {
                var results = await SearchAsync(additionalQuery, topKPerQuery, similarityThreshold);
                foreach (var result in results)
                {
                    // Keep the result with highest similarity if duplicate
                    if (!allResults.ContainsKey(result.ChunkId) || 
                        allResults[result.ChunkId].Similarity < result.Similarity)
                    {
                        allResults[result.ChunkId] = result;
                    }
                }
            }

            var deduplicated = allResults.Values
                .OrderByDescending(r => r.Similarity)
                .ToList();

            _logger.LogInformation("Multi-query returned {Count} unique results", deduplicated.Count);

            return deduplicated;
        }

        /// <summary>
        /// Search specifically for ADRs (Architecture Decision Records)
        /// </summary>
        public async Task<List<SearchResult>> SearchADRsAsync(
            string query,
            int topK = 5,
            double similarityThreshold = 0.7)
        {
            return await SearchAsync(query, topK, similarityThreshold, "ADR");
        }

        /// <summary>
        /// Search specifically for Guidelines
        /// </summary>
        public async Task<List<SearchResult>> SearchGuidelinesAsync(
            string query,
            int topK = 5,
            double similarityThreshold = 0.7)
        {
            return await SearchAsync(query, topK, similarityThreshold, "Guideline");
        }

        /// <summary>
        /// Hybrid search: Combine semantic search with keyword matching
        /// </summary>
        public async Task<List<SearchResult>> HybridSearchAsync(
            string query,
            List<string> keywords,
            int topK = 10,
            double semanticWeight = 0.7,
            double keywordWeight = 0.3)
        {
            _logger.LogInformation("Hybrid search: '{Query}' with {KeywordCount} keywords", 
                query, keywords.Count);

            // Perform semantic search
            var semanticResults = await _vectorDb.SearchAsync(query, topK * 2);

            // Score results by keyword presence
            var scoredResults = semanticResults.Select(r =>
            {
                var keywordScore = 0.0;
                var content = r.Content.ToLowerInvariant();

                foreach (var keyword in keywords)
                {
                    if (content.Contains(keyword.ToLowerInvariant()))
                    {
                        keywordScore += 1.0;
                    }
                }

                // Normalize keyword score (0-1)
                keywordScore = keywords.Count > 0 ? keywordScore / keywords.Count : 0;

                // Combine scores
                var hybridScore = (r.Similarity * semanticWeight) + (keywordScore * keywordWeight);

                return new
                {
                    Result = r,
                    HybridScore = (float)hybridScore,
                    SemanticScore = r.Similarity,
                    KeywordScore = (float)keywordScore
                };
            })
            .OrderByDescending(x => x.HybridScore)
            .Take(topK)
            .ToList();

            // Update similarity scores with hybrid scores
            var results = scoredResults.Select(x =>
            {
                x.Result.Similarity = x.HybridScore;
                return x.Result;
            }).ToList();

            _logger.LogInformation("Hybrid search returned {Count} results", results.Count);

            return results;
        }
    }

    /// <summary>
    /// Search filter options
    /// </summary>
    public class SearchFilters
    {
        public int TopK { get; set; } = 5;
        public double? SimilarityThreshold { get; set; } = 0.7;
        public string? DocumentType { get; set; }
        public List<string>? RequiredTags { get; set; }
        public List<string>? ExcludeSections { get; set; }
    }
}

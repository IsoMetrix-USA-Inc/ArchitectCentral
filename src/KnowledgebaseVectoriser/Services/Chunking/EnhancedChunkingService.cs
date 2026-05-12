using KnowledgebaseVectoriser.Models;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;

namespace KnowledgebaseVectoriser.Services.Chunking
{
    /// <summary>
    /// Enhanced chunking configuration with V2 schema support
    /// </summary>
    public class EnhancedChunkingConfig : ChunkingConfig
    {
        /// <summary>
        /// Overlap percentage (0.0 to 1.0). Default is 0.15 (15% overlap)
        /// </summary>
        public double OverlapPercentage { get; set; } = 0.15;

        /// <summary>
        /// Calculate overlap size based on max chunk size
        /// </summary>
        public int CalculateOverlapSize()
        {
            return (int)(MaxChunkSize * OverlapPercentage);
        }

        /// <summary>
        /// Enable tracking of overlap positions
        /// </summary>
        public bool TrackOverlapPositions { get; set; } = true;

        /// <summary>
        /// Strategy for handling boundaries (Token, Sentence, Paragraph, Semantic)
        /// </summary>
        public BoundaryStrategy BoundaryStrategy { get; set; } = BoundaryStrategy.Sentence;

        /// <summary>
        /// Minimum overlap size in tokens (safety check)
        /// </summary>
        public int MinOverlapSize { get; set; } = 20;

        /// <summary>
        /// Maximum overlap size in tokens (prevent excessive overlap)
        /// </summary>
        public int MaxOverlapSize { get; set; } = 150;
    }

    /// <summary>
    /// Boundary detection strategy for chunking
    /// </summary>
    public enum BoundaryStrategy
    {
        /// <summary>
        /// Split at token boundaries (fastest, least accurate)
        /// </summary>
        Token,

        /// <summary>
        /// Split at sentence boundaries (balanced)
        /// </summary>
        Sentence,

        /// <summary>
        /// Split at paragraph boundaries (preserves structure)
        /// </summary>
        Paragraph,

        /// <summary>
        /// Split at semantic boundaries (e.g., heading changes, section breaks)
        /// </summary>
        Semantic
    }

    /// <summary>
    /// Enhanced chunk with V2 schema metadata
    /// </summary>
    public class EnhancedDocumentChunk : DocumentChunk
    {
        /// <summary>
        /// Source file path for the document
        /// </summary>
        public string SourceFilePath { get; set; } = string.Empty;

        /// <summary>
        /// Total number of chunks in the source document
        /// </summary>
        public int ChunkTotal { get; set; } = 1;

        /// <summary>
        /// Start position of overlap region in tokens
        /// </summary>
        public int? OverlapStart { get; set; }

        /// <summary>
        /// End position of overlap region in tokens
        /// </summary>
        public int? OverlapEnd { get; set; }

        /// <summary>
        /// Whether this chunk has overlap with adjacent chunks
        /// </summary>
        public bool HasOverlap { get; set; }

        /// <summary>
        /// Heading hierarchy for this chunk (e.g., ["Architecture", "API Design", "Versioning"])
        /// </summary>
        public List<string> HeadingHierarchy { get; set; } = new();

        /// <summary>
        /// Estimated token count for the content
        /// </summary>
        public int ContentTokens { get; set; }

        /// <summary>
        /// SHA256 hash of the content for change detection
        /// </summary>
        public string ContentHash { get; set; } = string.Empty;

        /// <summary>
        /// ADR status (for ADR documents)
        /// </summary>
        public string? Status { get; set; }

        /// <summary>
        /// Category/topic of the content
        /// </summary>
        public string? Category { get; set; }

        /// <summary>
        /// Sentiment type (positive, negative, neutral, warning)
        /// </summary>
        public string? Sentiment { get; set; }

        /// <summary>
        /// Quality score (0.0 to 1.0)
        /// </summary>
        public decimal QualityScore { get; set; } = 0.5m;

        /// <summary>
        /// Whether the chunk contains code examples
        /// </summary>
        public bool HasCodeExamples { get; set; }

        /// <summary>
        /// Whether the chunk contains diagrams
        /// </summary>
        public bool HasDiagrams { get; set; }

        /// <summary>
        /// Technical tags extracted from content
        /// </summary>
        public List<string> TechnicalTags { get; set; } = new();

        /// <summary>
        /// Framework references found in content
        /// </summary>
        public List<string> FrameworkRefs { get; set; } = new();
    }

    /// <summary>
    /// Enhanced chunking service with V2 schema support and advanced features
    /// </summary>
    public class EnhancedChunkingService
    {
        private readonly EnhancedChunkingConfig _config;
        private readonly ILogger<EnhancedChunkingService> _logger;

        public EnhancedChunkingService(
            EnhancedChunkingConfig config,
            ILogger<EnhancedChunkingService> logger)
        {
            _config = config;
            _logger = logger;

            // Override base overlap size with calculated percentage
            if (_config.OverlapPercentage > 0)
            {
                _config.OverlapSize = Math.Clamp(
                    _config.CalculateOverlapSize(),
                    _config.MinOverlapSize,
                    _config.MaxOverlapSize
                );
            }

            _logger.LogInformation(
                "Enhanced chunking service initialized: MaxChunkSize={MaxSize}, OverlapPercentage={Overlap}%, OverlapSize={OverlapSize} tokens, BoundaryStrategy={Strategy}",
                _config.MaxChunkSize,
                _config.OverlapPercentage * 100,
                _config.OverlapSize,
                _config.BoundaryStrategy
            );
        }

        /// <summary>
        /// Chunk document with enhanced overlap tracking
        /// </summary>
        public async Task<List<EnhancedDocumentChunk>> ChunkWithOverlapAsync(
            DocumentInfo document,
            string content,
            ParsedMarkdown parsedMarkdown,
            IChunkingStrategy baseStrategy)
        {
            _logger.LogDebug("Enhanced chunking for {Path}", document.RelativePath);

            // Use base strategy to get initial chunks
            var baseChunks = await baseStrategy.ChunkAsync(document, content, parsedMarkdown);

            // Enhance chunks with V2 schema metadata
            var enhancedChunks = new List<EnhancedDocumentChunk>();
            var chunkTotal = baseChunks.Count;

            for (int i = 0; i < baseChunks.Count; i++)
            {
                var baseChunk = baseChunks[i];
                var enhancedChunk = await EnhanceChunkAsync(baseChunk, i, chunkTotal, content, parsedMarkdown);
                
                // Calculate overlap metadata if applicable
                if (_config.TrackOverlapPositions && i < baseChunks.Count - 1)
                {
                    CalculateOverlapPositions(enhancedChunk, baseChunks[i + 1], baseStrategy);
                }

                enhancedChunks.Add(enhancedChunk);
            }

            _logger.LogInformation(
                "Enhanced {Count} chunks for {Path}. Avg tokens: {AvgTokens}, With overlap: {WithOverlap}",
                enhancedChunks.Count,
                document.RelativePath,
                enhancedChunks.Average(c => c.ContentTokens),
                enhancedChunks.Count(c => c.HasOverlap)
            );

            return enhancedChunks;
        }

        /// <summary>
        /// Enhance a basic chunk with V2 schema metadata
        /// </summary>
        private async Task<EnhancedDocumentChunk> EnhanceChunkAsync(
            DocumentChunk baseChunk,
            int chunkIndex,
            int chunkTotal,
            string fullContent,
            ParsedMarkdown parsedMarkdown)
        {
            var enhanced = new EnhancedDocumentChunk
            {
                // Copy base properties
                ChunkId = baseChunk.ChunkId,
                SourceDocumentId = baseChunk.SourceDocumentId,
                DocumentType = baseChunk.DocumentType,
                Section = baseChunk.Section,
                Content = baseChunk.Content,
                ChunkIndex = chunkIndex,
                MetadataJson = baseChunk.MetadataJson,
                CreatedAt = baseChunk.CreatedAt,
                Embedding = baseChunk.Embedding,

                // Add V2 schema properties
                ChunkTotal = chunkTotal,
                ContentHash = CalculateContentHash(baseChunk.Content),
                ContentTokens = EstimateTokenCount(baseChunk.Content),
                
                // Initialize with defaults
                HasOverlap = false,
                QualityScore = 0.5m,
                Sentiment = "neutral"
            };

            // Extract heading hierarchy from section
            if (!string.IsNullOrEmpty(baseChunk.Section))
            {
                enhanced.HeadingHierarchy = baseChunk.Section
                    .Split(new[] { " > ", " / ", " - " }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(h => h.Trim())
                    .ToList();
            }

            // Detect code examples
            enhanced.HasCodeExamples = baseChunk.Content.Contains("```") ||
                                       baseChunk.Content.Contains("<code>") ||
                                       baseChunk.Content.Contains("<pre>");

            // Detect diagrams
            enhanced.HasDiagrams = baseChunk.Content.Contains("```mermaid") ||
                                   baseChunk.Content.Contains("flowchart") ||
                                   baseChunk.Content.Contains("diagram");

            // Extract technical tags (basic heuristics)
            enhanced.TechnicalTags = ExtractTechnicalTags(baseChunk.Content);

            // Extract framework references
            enhanced.FrameworkRefs = ExtractFrameworkReferences(baseChunk.Content);

            // Calculate basic quality score
            enhanced.QualityScore = CalculateBasicQualityScore(enhanced);

            return await Task.FromResult(enhanced);
        }

        /// <summary>
        /// Calculate overlap positions between adjacent chunks
        /// </summary>
        private void CalculateOverlapPositions(
            EnhancedDocumentChunk currentChunk,
            DocumentChunk nextChunk,
            IChunkingStrategy strategy)
        {
            // Try to find overlapping content
            var currentWords = currentChunk.Content.Split(new[] { ' ', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            var nextWords = nextChunk.Content.Split(new[] { ' ', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

            // Find the overlap by checking the end of current against start of next
            int overlapLength = 0;
            int maxCheck = Math.Min(currentWords.Length, nextWords.Length);

            for (int i = 1; i <= maxCheck; i++)
            {
                var currentEnd = string.Join(" ", currentWords.TakeLast(i));
                var nextStart = string.Join(" ", nextWords.Take(i));

                if (currentEnd.Equals(nextStart, StringComparison.OrdinalIgnoreCase))
                {
                    overlapLength = i;
                }
            }

            if (overlapLength > 0)
            {
                currentChunk.HasOverlap = true;
                currentChunk.OverlapStart = currentWords.Length - overlapLength;
                currentChunk.OverlapEnd = currentWords.Length;

                _logger.LogDebug(
                    "Chunk {ChunkId} has {OverlapWords} words of overlap with next chunk",
                    currentChunk.ChunkId,
                    overlapLength
                );
            }
        }

        /// <summary>
        /// Calculate SHA256 hash of content
        /// </summary>
        private string CalculateContentHash(string content)
        {
            using var sha256 = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(content);
            var hash = sha256.ComputeHash(bytes);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        /// <summary>
        /// Estimate token count (simplified)
        /// </summary>
        private int EstimateTokenCount(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return 0;

            // Rough estimate: 1 token ≈ 4 characters
            // More accurate counting should use tiktoken
            return Math.Max(1, text.Length / 4);
        }

        /// <summary>
        /// Extract technical tags from content using heuristics
        /// </summary>
        private List<string> ExtractTechnicalTags(string content)
        {
            var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var contentLower = content.ToLowerInvariant();

            // Architecture patterns
            if (contentLower.Contains("microservice")) tags.Add("microservices");
            if (contentLower.Contains("monolith")) tags.Add("monolith");
            if (contentLower.Contains("cqrs")) tags.Add("cqrs");
            if (contentLower.Contains("event sourcing")) tags.Add("event-sourcing");
            if (contentLower.Contains("saga")) tags.Add("saga");

            // Security
            if (contentLower.Contains("authentication")) tags.Add("authentication");
            if (contentLower.Contains("authorization")) tags.Add("authorization");
            if (contentLower.Contains("security") || contentLower.Contains("secure")) tags.Add("security");

            // Data
            if (contentLower.Contains("database") || contentLower.Contains("data store")) tags.Add("database");
            if (contentLower.Contains("caching") || contentLower.Contains("cache")) tags.Add("caching");
            if (contentLower.Contains("multi-tenant")) tags.Add("multi-tenant");

            // API
            if (contentLower.Contains("rest") || contentLower.Contains("restful")) tags.Add("rest-api");
            if (contentLower.Contains("graphql")) tags.Add("graphql");
            if (contentLower.Contains("grpc")) tags.Add("grpc");

            return tags.ToList();
        }

        /// <summary>
        /// Extract framework references from content
        /// </summary>
        private List<string> ExtractFrameworkReferences(string content)
        {
            var frameworks = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var contentLower = content.ToLowerInvariant();

            // .NET
            if (contentLower.Contains("asp.net core") || contentLower.Contains("aspnet")) frameworks.Add("aspnet-core");
            if (contentLower.Contains("entity framework") || contentLower.Contains("ef core")) frameworks.Add("entity-framework");
            if (contentLower.Contains(".net 8") || contentLower.Contains("dotnet 8")) frameworks.Add("dotnet-8");
            if (contentLower.Contains("mediatr")) frameworks.Add("mediatr");

            // JavaScript/TypeScript
            if (contentLower.Contains("vue.js") || contentLower.Contains("vue 3")) frameworks.Add("vuejs");
            if (contentLower.Contains("react")) frameworks.Add("react");
            if (contentLower.Contains("angular")) frameworks.Add("angular");
            if (contentLower.Contains("typescript")) frameworks.Add("typescript");

            // Azure
            if (contentLower.Contains("azure service bus")) frameworks.Add("azure-service-bus");
            if (contentLower.Contains("azure openai")) frameworks.Add("azure-openai");
            if (contentLower.Contains("azure ad") || contentLower.Contains("azure b2c")) frameworks.Add("azure-ad");

            // Databases
            if (contentLower.Contains("postgresql") || contentLower.Contains("pgvector")) frameworks.Add("postgresql");
            if (contentLower.Contains("redis")) frameworks.Add("redis");
            if (contentLower.Contains("snowflake")) frameworks.Add("snowflake");

            // Containers
            if (contentLower.Contains("docker")) frameworks.Add("docker");
            if (contentLower.Contains("kubernetes") || contentLower.Contains("k8s")) frameworks.Add("kubernetes");

            return frameworks.ToList();
        }

        /// <summary>
        /// Calculate basic quality score (0.0 to 1.0)
        /// </summary>
        private decimal CalculateBasicQualityScore(EnhancedDocumentChunk chunk)
        {
            decimal score = 0m;

            // Content length adequacy (0.3 weight)
            if (chunk.ContentTokens >= 100 && chunk.ContentTokens <= 1000)
                score += 0.3m;
            else if (chunk.ContentTokens >= 50 && chunk.ContentTokens < 100)
                score += 0.2m;
            else if (chunk.ContentTokens > 1000 && chunk.ContentTokens <= 2000)
                score += 0.2m;
            else
                score += 0.1m;

            // Has section (0.2 weight)
            if (!string.IsNullOrEmpty(chunk.Section))
                score += 0.2m;

            // Has code examples (0.2 weight)
            if (chunk.HasCodeExamples)
                score += 0.2m;

            // Has technical tags (0.15 weight)
            if (chunk.TechnicalTags.Count >= 3)
                score += 0.15m;
            else if (chunk.TechnicalTags.Count >= 1)
                score += 0.10m;

            // Has framework references (0.15 weight)
            if (chunk.FrameworkRefs.Count >= 2)
                score += 0.15m;
            else if (chunk.FrameworkRefs.Count >= 1)
                score += 0.10m;

            return Math.Min(1.0m, Math.Max(0.0m, score));
        }

        /// <summary>
        /// Create chunk relationships for overlap tracking
        /// </summary>
        public List<ChunkRelationship> CreateChunkRelationships(List<EnhancedDocumentChunk> chunks)
        {
            var relationships = new List<ChunkRelationship>();

            for (int i = 0; i < chunks.Count - 1; i++)
            {
                var currentChunk = chunks[i];
                var nextChunk = chunks[i + 1];

                if (currentChunk.HasOverlap)
                {
                    relationships.Add(new ChunkRelationship
                    {
                        ChunkId = currentChunk.ChunkId,
                        RelatedChunkId = nextChunk.ChunkId,
                        RelationshipType = "overlaps_with",
                        OverlapTokens = currentChunk.OverlapEnd - currentChunk.OverlapStart
                    });

                    relationships.Add(new ChunkRelationship
                    {
                        ChunkId = nextChunk.ChunkId,
                        RelatedChunkId = currentChunk.ChunkId,
                        RelationshipType = "continues_from",
                        OverlapTokens = currentChunk.OverlapEnd - currentChunk.OverlapStart
                    });
                }
            }

            return relationships;
        }
    }

    /// <summary>
    /// Represents a relationship between chunks
    /// </summary>
    public class ChunkRelationship
    {
        public string ChunkId { get; set; } = string.Empty;
        public string RelatedChunkId { get; set; } = string.Empty;
        public string RelationshipType { get; set; } = string.Empty;
        public int? OverlapTokens { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}

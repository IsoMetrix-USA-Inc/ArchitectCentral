using KnowledgebaseVectoriser.Models;
using KnowledgebaseVectoriser.Services.Chunking;
using KnowledgebaseVectoriser.Services.Metadata;
using Microsoft.Extensions.Logging;

namespace KnowledgebaseVectoriser.Services
{
    /// <summary>
    /// Enhanced document processing pipeline with chunking and metadata enrichment
    /// </summary>
    public class EnhancedDocumentProcessor
    {
        private readonly ILogger<EnhancedDocumentProcessor> _logger;
        private readonly DocumentScanner _scanner;
        private readonly MarkdownParser _markdownParser;
        private readonly ADRParser _adrParser;
        private readonly ChunkingConfig _chunkingConfig;
        private readonly EnhancedChunkingConfig? _enhancedConfig;
        private readonly EnhancedChunkingService? _enhancedChunkingService;
        private readonly MetadataEnricherService? _metadataEnricher;

        public EnhancedDocumentProcessor(
            ILogger<EnhancedDocumentProcessor> logger,
            DocumentScanner scanner,
            MarkdownParser markdownParser,
            ADRParser adrParser,
            ChunkingConfig chunkingConfig,
            EnhancedChunkingConfig? enhancedConfig = null,
            EnhancedChunkingService? enhancedChunkingService = null,
            MetadataEnricherService? metadataEnricher = null)
        {
            _logger = logger;
            _scanner = scanner;
            _markdownParser = markdownParser;
            _adrParser = adrParser;
            _chunkingConfig = chunkingConfig;
            _enhancedConfig = enhancedConfig;
            _enhancedChunkingService = enhancedChunkingService;
            _metadataEnricher = metadataEnricher;
        }

        /// <summary>
        /// Process all documents with enhanced chunking and metadata enrichment
        /// </summary>
        public async Task<EnhancedDocumentProcessingResult> ProcessAllDocumentsAsync()
        {
            _logger.LogInformation("Starting enhanced document processing pipeline...");
            
            var result = new EnhancedDocumentProcessingResult();
            var startTime = DateTime.UtcNow;

            // Step 1: Scan documents
            _logger.LogInformation("Step 1: Scanning documents...");
            var documents = await _scanner.ScanDocumentsAsync();
            result.TotalDocuments = documents.Count;

            // Step 2: Process each document type
            _logger.LogInformation("Step 2: Processing and chunking documents...");

            // Determine which chunking strategies to use
            bool useEnhanced = _enhancedConfig != null && _enhancedChunkingService != null;
            bool useMetadataEnricher = _metadataEnricher != null;

            if (useEnhanced)
            {
                _logger.LogInformation(
                    "Using Enhanced Chunking V2 with {Overlap}% overlap", 
                    _enhancedConfig!.OverlapPercentage * 100
                );
            }

            if (useMetadataEnricher)
            {
                _logger.LogInformation("Metadata enrichment enabled");
            }

            // Create chunking strategies
            var adrChunker = CreateADRChunker(useEnhanced);
            var guidelineChunker = new GuidelineChunkingStrategy(
                LoggerFactory.Create(b => b.AddConsole()).CreateLogger<GuidelineChunkingStrategy>(),
                _chunkingConfig);
            var diagramChunker = new DiagramChunkingStrategy(
                LoggerFactory.Create(b => b.AddConsole()).CreateLogger<DiagramChunkingStrategy>(),
                _chunkingConfig);

            // Track parsed markdown for metadata enrichment
            var parsedMarkdownMap = new Dictionary<string, ParsedMarkdown>();

            foreach (var document in documents)
            {
                try
                {
                    var content = await _scanner.ReadDocumentContentAsync(document);
                    var parsed = _markdownParser.Parse(content);
                    parsedMarkdownMap[document.FilePath] = parsed;

                    List<DocumentChunk> chunks = document.Type switch
                    {
                        DocumentType.ADR => await adrChunker.ChunkAsync(document, content, parsed),
                        DocumentType.Guideline => await guidelineChunker.ChunkAsync(document, content, parsed),
                        DocumentType.Diagram => await diagramChunker.ChunkAsync(document, content, parsed),
                        _ => new List<DocumentChunk>()
                    };

                    result.AllChunks.AddRange(chunks);
                    result.ProcessedDocuments++;

                    _logger.LogDebug(
                        "Processed {Type} document: {Path} -> {ChunkCount} chunks",
                        document.Type, 
                        document.RelativePath, 
                        chunks.Count
                    );
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing document: {Path}", document.RelativePath);
                    result.FailedDocuments++;
                }
            }

            // Step 3: Enrich metadata if service is available
            if (useMetadataEnricher && result.AllChunks.Count > 0)
            {
                _logger.LogInformation("Step 3: Enriching {Count} chunks with metadata...", result.AllChunks.Count);
                
                var enrichmentStartTime = DateTime.UtcNow;
                
                // Build document map for parsed markdown lookup
                var docMap = new Dictionary<string, ParsedMarkdown>();
                foreach (var chunk in result.AllChunks)
                {
                    var docPath = documents.FirstOrDefault(d => 
                        d.FilePath.Contains(chunk.SourceDocumentId))?.FilePath;
                    
                    if (docPath != null && parsedMarkdownMap.TryGetValue(docPath, out var parsed))
                    {
                        docMap[chunk.SourceDocumentId] = parsed;
                    }
                }

                // Enrich in batches
                var enrichedMetadata = _metadataEnricher.EnrichChunksBatch(result.AllChunks, docMap);

                // Apply enriched metadata to chunks
                for (int i = 0; i < result.AllChunks.Count; i++)
                {
                    var chunk = result.AllChunks[i];
                    var metadata = enrichedMetadata[i];

                    // If chunk is EnhancedDocumentChunk, populate V2 fields
                    if (chunk is EnhancedDocumentChunk enhancedChunk)
                    {
                        enhancedChunk.Status = metadata.Status;
                        enhancedChunk.Category = metadata.Category;
                        enhancedChunk.Sentiment = metadata.Sentiment;
                        enhancedChunk.QualityScore = metadata.QualityScore;
                        enhancedChunk.TechnicalTags = metadata.TechnicalTags;
                        enhancedChunk.FrameworkRefs = metadata.FrameworkRefs;
                    }
                }

                var enrichmentElapsed = DateTime.UtcNow - enrichmentStartTime;
                result.MetadataEnrichmentTimeMs = enrichmentElapsed.TotalMilliseconds;

                _logger.LogInformation(
                    "Metadata enrichment complete: {ElapsedMs}ms, Avg quality: {AvgQuality:F2}",
                    enrichmentElapsed.TotalMilliseconds,
                    enrichedMetadata.Average(e => (double)e.QualityScore)
                );

                // Store enrichment statistics
                result.EnrichmentStats = new EnrichmentStatistics
                {
                    TotalChunks = enrichedMetadata.Count,
                    AverageQualityScore = enrichedMetadata.Average(e => (double)e.QualityScore),
                    HighQualityCount = enrichedMetadata.Count(e => e.QualityScore >= 0.75m),
                    ChunksWithTags = enrichedMetadata.Count(e => e.TechnicalTags.Any()),
                    ChunksWithFrameworks = enrichedMetadata.Count(e => e.FrameworkRefs.Any()),
                    StatusDistribution = enrichedMetadata
                        .GroupBy(e => e.Status)
                        .ToDictionary(g => g.Key, g => g.Count()),
                    SentimentDistribution = enrichedMetadata
                        .GroupBy(e => e.Sentiment)
                        .ToDictionary(g => g.Key, g => g.Count()),
                    CategoryDistribution = enrichedMetadata
                        .GroupBy(e => e.Category)
                        .ToDictionary(g => g.Key, g => g.Count())
                };
            }

            var totalElapsed = DateTime.UtcNow - startTime;
            result.TotalProcessingTimeMs = totalElapsed.TotalMilliseconds;

            _logger.LogInformation("Enhanced document processing complete:");
            _logger.LogInformation("  Total documents: {Total}", result.TotalDocuments);
            _logger.LogInformation("  Processed: {Processed}", result.ProcessedDocuments);
            _logger.LogInformation("  Failed: {Failed}", result.FailedDocuments);
            _logger.LogInformation("  Total chunks: {Chunks}", result.AllChunks.Count);
            _logger.LogInformation("  ADR chunks: {ADR}", result.AllChunks.Count(c => c.DocumentType == "ADR"));
            _logger.LogInformation("  Guideline chunks: {Guideline}", result.AllChunks.Count(c => c.DocumentType == "Guideline"));
            _logger.LogInformation("  Diagram chunks: {Diagram}", result.AllChunks.Count(c => c.DocumentType == "Diagram"));
            _logger.LogInformation("  Processing time: {TimeMs}ms", totalElapsed.TotalMilliseconds);

            if (result.EnrichmentStats != null)
            {
                _logger.LogInformation("  Metadata enrichment:");
                _logger.LogInformation("    Average quality: {Quality:F2}", result.EnrichmentStats.AverageQualityScore);
                _logger.LogInformation("    High quality (>=0.75): {Count} ({Percent:F1}%)", 
                    result.EnrichmentStats.HighQualityCount,
                    result.EnrichmentStats.HighQualityCount * 100.0 / result.EnrichmentStats.TotalChunks);
                _logger.LogInformation("    With technical tags: {Count} ({Percent:F1}%)", 
                    result.EnrichmentStats.ChunksWithTags,
                    result.EnrichmentStats.ChunksWithTags * 100.0 / result.EnrichmentStats.TotalChunks);
            }

            return result;
        }

        /// <summary>
        /// Create appropriate ADR chunking strategy
        /// </summary>
        private IChunkingStrategy CreateADRChunker(bool useEnhanced)
        {
            if (useEnhanced)
            {
                return new EnhancedADRChunkingStrategy(
                    LoggerFactory.Create(b => b.AddConsole()).CreateLogger<EnhancedADRChunkingStrategy>(),
                    _enhancedConfig!,
                    _enhancedChunkingService!
                );
            }
            else
            {
                return new ADRChunkingStrategy(
                    LoggerFactory.Create(b => b.AddConsole()).CreateLogger<ADRChunkingStrategy>(),
                    _chunkingConfig
                );
            }
        }

        /// <summary>
        /// Process a single document
        /// </summary>
        public async Task<List<DocumentChunk>> ProcessDocumentAsync(DocumentInfo document)
        {
            _logger.LogInformation("Processing document: {Path}", document.RelativePath);

            var content = await _scanner.ReadDocumentContentAsync(document);
            var parsed = _markdownParser.Parse(content);

            bool useEnhanced = _enhancedConfig != null && _enhancedChunkingService != null;
            
            var chunker = document.Type switch
            {
                DocumentType.ADR => CreateADRChunker(useEnhanced),
                DocumentType.Guideline => new GuidelineChunkingStrategy(
                    LoggerFactory.Create(b => b.AddConsole()).CreateLogger<GuidelineChunkingStrategy>(),
                    _chunkingConfig),
                DocumentType.Diagram => new DiagramChunkingStrategy(
                    LoggerFactory.Create(b => b.AddConsole()).CreateLogger<DiagramChunkingStrategy>(),
                    _chunkingConfig),
                _ => throw new NotSupportedException($"Document type {document.Type} not supported")
            };

            var chunks = await chunker.ChunkAsync(document, content, parsed);

            // Enrich metadata if service available
            if (_metadataEnricher != null && chunks.Count > 0)
            {
                var docMap = new Dictionary<string, ParsedMarkdown>
                {
                    [document.FilePath] = parsed
                };

                var enrichedMetadata = _metadataEnricher.EnrichChunksBatch(chunks, docMap);

                for (int i = 0; i < chunks.Count; i++)
                {
                    if (chunks[i] is EnhancedDocumentChunk enhancedChunk)
                    {
                        var metadata = enrichedMetadata[i];
                        enhancedChunk.Status = metadata.Status;
                        enhancedChunk.Category = metadata.Category;
                        enhancedChunk.Sentiment = metadata.Sentiment;
                        enhancedChunk.QualityScore = metadata.QualityScore;
                        enhancedChunk.TechnicalTags = metadata.TechnicalTags;
                        enhancedChunk.FrameworkRefs = metadata.FrameworkRefs;
                    }
                }
            }

            return chunks;
        }

        /// <summary>
        /// Validate chunk quality
        /// </summary>
        public void ValidateChunkQuality(List<DocumentChunk> chunks)
        {
            _logger.LogInformation("\n=== Chunk Quality Validation ===");

            int tooSmall = 0;
            int tooLarge = 0;
            int optimal = 0;
            int withOverlap = 0;
            int totalOverlapTokens = 0;

            foreach (var chunk in chunks)
            {
                var tokenCount = chunk.Content.Length / 4; // Rough estimate

                if (tokenCount < 100)
                    tooSmall++;
                else if (tokenCount > _chunkingConfig.MaxChunkSize * 1.2)
                    tooLarge++;
                else
                    optimal++;

                // Track enhanced V2 metadata if present
                if (chunk is EnhancedDocumentChunk enhancedChunk)
                {
                    if (enhancedChunk.HasOverlap)
                    {
                        withOverlap++;
                        if (enhancedChunk.OverlapStart.HasValue && enhancedChunk.OverlapEnd.HasValue)
                        {
                            totalOverlapTokens += enhancedChunk.OverlapEnd.Value - enhancedChunk.OverlapStart.Value;
                        }
                    }
                }
            }

            _logger.LogInformation("Chunk size distribution:");
            _logger.LogInformation("  Too small (<100 tokens): {Count} ({Percent}%)", 
                tooSmall, Math.Round(tooSmall * 100.0 / chunks.Count, 1));
            _logger.LogInformation("  Optimal (100-{Max} tokens): {Count} ({Percent}%)", 
                _chunkingConfig.MaxChunkSize, optimal, Math.Round(optimal * 100.0 / chunks.Count, 1));
            _logger.LogInformation("  Too large (>{Max} tokens): {Count} ({Percent}%)", 
                _chunkingConfig.MaxChunkSize, tooLarge, Math.Round(tooLarge * 100.0 / chunks.Count, 1));

            // Show enhanced V2 statistics if available
            if (withOverlap > 0)
            {
                _logger.LogInformation("\n=== Enhanced Chunking V2 Statistics ===");
                _logger.LogInformation("  Chunks with overlap: {Count} ({Percent}%)", 
                    withOverlap, Math.Round(withOverlap * 100.0 / chunks.Count, 1));
                _logger.LogInformation("  Average overlap: {AvgOverlap} tokens", 
                    totalOverlapTokens / Math.Max(withOverlap, 1));

                var enhancedChunks = chunks.OfType<EnhancedDocumentChunk>().ToList();
                if (enhancedChunks.Any())
                {
                    var avgQuality = enhancedChunks.Average(c => (double)c.QualityScore);
                    var chunksWithTags = enhancedChunks.Count(c => c.TechnicalTags.Any());
                    var chunksWithFrameworks = enhancedChunks.Count(c => c.FrameworkRefs.Any());

                    _logger.LogInformation("  Average quality score: {AvgQuality:F2}", avgQuality);
                    _logger.LogInformation("  Chunks with technical tags: {Count} ({Percent}%)", 
                        chunksWithTags, Math.Round(chunksWithTags * 100.0 / enhancedChunks.Count, 1));
                    _logger.LogInformation("  Chunks with framework refs: {Count} ({Percent}%)", 
                        chunksWithFrameworks, Math.Round(chunksWithFrameworks * 100.0 / enhancedChunks.Count, 1));
                }
            }
        }
    }

    /// <summary>
    /// Enhanced result of document processing with enrichment statistics
    /// </summary>
    public class EnhancedDocumentProcessingResult
    {
        public int TotalDocuments { get; set; }
        public int ProcessedDocuments { get; set; }
        public int FailedDocuments { get; set; }
        public List<DocumentChunk> AllChunks { get; set; } = new();
        public double TotalProcessingTimeMs { get; set; }
        public double MetadataEnrichmentTimeMs { get; set; }
        public EnrichmentStatistics? EnrichmentStats { get; set; }
    }

    /// <summary>
    /// Statistics about metadata enrichment
    /// </summary>
    public class EnrichmentStatistics
    {
        public int TotalChunks { get; set; }
        public double AverageQualityScore { get; set; }
        public int HighQualityCount { get; set; }
        public int ChunksWithTags { get; set; }
        public int ChunksWithFrameworks { get; set; }
        public Dictionary<string, int> StatusDistribution { get; set; } = new();
        public Dictionary<string, int> SentimentDistribution { get; set; } = new();
        public Dictionary<string, int> CategoryDistribution { get; set; } = new();
    }
}

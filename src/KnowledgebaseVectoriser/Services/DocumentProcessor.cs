using KnowledgebaseVectoriser.Models;
using KnowledgebaseVectoriser.Services.Chunking;
using Microsoft.Extensions.Logging;

namespace KnowledgebaseVectoriser.Services
{
    /// <summary>
    /// Orchestrates the document processing pipeline
    /// </summary>
    public class DocumentProcessor
    {
        private readonly ILogger<DocumentProcessor> _logger;
        private readonly DocumentScanner _scanner;
        private readonly MarkdownParser _markdownParser;
        private readonly ADRParser _adrParser;
        private readonly ChunkingConfig _chunkingConfig;
        private readonly EnhancedChunkingConfig? _enhancedConfig;
        private readonly EnhancedChunkingService? _enhancedChunkingService;

        public DocumentProcessor(
            ILogger<DocumentProcessor> logger,
            DocumentScanner scanner,
            MarkdownParser markdownParser,
            ADRParser adrParser,
            ChunkingConfig chunkingConfig,
            EnhancedChunkingConfig? enhancedConfig = null,
            EnhancedChunkingService? enhancedChunkingService = null)
        {
            _logger = logger;
            _scanner = scanner;
            _markdownParser = markdownParser;
            _adrParser = adrParser;
            _chunkingConfig = chunkingConfig;
            _enhancedConfig = enhancedConfig;
            _enhancedChunkingService = enhancedChunkingService;
        }

        /// <summary>
        /// Process all documents and return chunks
        /// </summary>
        public async Task<DocumentProcessingResult> ProcessAllDocumentsAsync()
        {
            _logger.LogInformation("Starting document processing pipeline...");

            var result = new DocumentProcessingResult();

            // Step 1: Scan documents
            _logger.LogInformation("Step 1: Scanning documents...");
            var documents = await _scanner.ScanDocumentsAsync();
            result.TotalDocuments = documents.Count;

            // Step 2: Process each document type
            _logger.LogInformation("Step 2: Processing and chunking documents...");

            // Determine which chunking strategies to use
            bool useEnhanced = _enhancedConfig != null && _enhancedChunkingService != null;

            if (useEnhanced)
            {
                _logger.LogInformation("Using Enhanced Chunking V2 with {Overlap}% overlap",
                    _enhancedConfig!.OverlapPercentage * 100);
            }

            var adrChunker = useEnhanced
                ? new EnhancedADRChunkingStrategy(
                    LoggerFactory.Create(b => b.AddConsole()).CreateLogger<EnhancedADRChunkingStrategy>(),
                    _enhancedConfig!,
                    _enhancedChunkingService!)
                : (IChunkingStrategy)new ADRChunkingStrategy(
                    LoggerFactory.Create(b => b.AddConsole()).CreateLogger<ADRChunkingStrategy>(),
                    _chunkingConfig);

            var guidelineChunker = new GuidelineChunkingStrategy(
                LoggerFactory.Create(b => b.AddConsole()).CreateLogger<GuidelineChunkingStrategy>(),
                _chunkingConfig);

            var diagramChunker = new DiagramChunkingStrategy(
                LoggerFactory.Create(b => b.AddConsole()).CreateLogger<DiagramChunkingStrategy>(),
                _chunkingConfig);

            foreach (var document in documents)
            {
                try
                {
                    var content = await _scanner.ReadDocumentContentAsync(document);
                    var parsed = _markdownParser.Parse(content);

                    List<DocumentChunk> chunks = document.Type switch
                    {
                        DocumentType.ADR => await adrChunker.ChunkAsync(document, content, parsed),
                        DocumentType.Guideline => await guidelineChunker.ChunkAsync(document, content, parsed),
                        DocumentType.Diagram => await diagramChunker.ChunkAsync(document, content, parsed),
                        _ => new List<DocumentChunk>()
                    };

                    result.AllChunks.AddRange(chunks);
                    result.ProcessedDocuments++;

                    _logger.LogDebug("Processed {Type} document: {Path} -> {ChunkCount} chunks",
                        document.Type, document.RelativePath, chunks.Count);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing document: {Path}", document.RelativePath);
                    result.FailedDocuments++;
                }
            }

            _logger.LogInformation("Document processing complete:");
            _logger.LogInformation("  Total documents: {Total}", result.TotalDocuments);
            _logger.LogInformation("  Processed: {Processed}", result.ProcessedDocuments);
            _logger.LogInformation("  Failed: {Failed}", result.FailedDocuments);
            _logger.LogInformation("  Total chunks: {Chunks}", result.AllChunks.Count);
            _logger.LogInformation("  ADR chunks: {ADR}", result.AllChunks.Count(c => c.DocumentType == "ADR"));
            _logger.LogInformation("  Guideline chunks: {Guideline}", result.AllChunks.Count(c => c.DocumentType == "Guideline"));
            _logger.LogInformation("  Diagram chunks: {Diagram}", result.AllChunks.Count(c => c.DocumentType == "Diagram"));

            return result;
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

            // Show sample chunks
            _logger.LogInformation("\n=== Sample Chunks ===");
            var sampleChunks = chunks.Take(3).ToList();

            foreach (var chunk in sampleChunks)
            {
                var preview = chunk.Content.Length > 200
                    ? chunk.Content.Substring(0, 200) + "..."
                    : chunk.Content;

                _logger.LogInformation("\nChunk: {Id}", chunk.ChunkId);
                _logger.LogInformation("  Type: {Type}", chunk.DocumentType);
                _logger.LogInformation("  Section: {Section}", chunk.Section);
                _logger.LogInformation("  Tokens: ~{Tokens}", chunk.Content.Length / 4);

                // Show enhanced metadata if available
                if (chunk is EnhancedDocumentChunk enhancedChunk)
                {
                    if (enhancedChunk.HasOverlap)
                    {
                        var overlapTokens = enhancedChunk.OverlapEnd - enhancedChunk.OverlapStart;
                        _logger.LogInformation("  Overlap: {Tokens} tokens ({Percent}%)",
                            overlapTokens,
                            Math.Round((double)overlapTokens.GetValueOrDefault() / enhancedChunk.ContentTokens * 100, 1));
                    }
                    _logger.LogInformation("  Quality Score: {Score:F2}", enhancedChunk.QualityScore);
                    if (enhancedChunk.TechnicalTags.Any())
                    {
                        _logger.LogInformation("  Tags: {Tags}", string.Join(", ", enhancedChunk.TechnicalTags.Take(5)));
                    }
                }

                _logger.LogInformation("  Preview: {Preview}", preview.Replace("\n", " "));
            }
        }
    }

    /// <summary>
    /// Result of document processing
    /// </summary>
    public class DocumentProcessingResult
    {
        public int TotalDocuments { get; set; }
        public int ProcessedDocuments { get; set; }
        public int FailedDocuments { get; set; }
        public List<DocumentChunk> AllChunks { get; set; } = new();
    }
}

using KnowledgebaseVectoriser.Models;
using Microsoft.Extensions.Logging;

namespace KnowledgebaseVectoriser.Services.Chunking
{
    /// <summary>
    /// Chunking strategy for Architectural Guidelines
    /// Guidelines often contain code examples that should stay with their explanations
    /// </summary>
    public class GuidelineChunkingStrategy : BaseChunkingStrategy
    {
        private readonly ILogger<GuidelineChunkingStrategy> _logger;

        public GuidelineChunkingStrategy(ILogger<GuidelineChunkingStrategy> logger, ChunkingConfig config)
            : base(config)
        {
            _logger = logger;
        }

        public override async Task<List<DocumentChunk>> ChunkAsync(DocumentInfo document, string content, ParsedMarkdown parsedMarkdown)
        {
            _logger.LogDebug("Chunking Guideline: {Path}", document.RelativePath);

            var chunks = new List<DocumentChunk>();
            var chunkIndex = 0;

            var guidelineId = $"guideline_{Path.GetFileNameWithoutExtension(document.FilePath)}";

            foreach (var section in parsedMarkdown.Sections)
            {
                // Build section content with code blocks
                var sectionContent = section.Content;
                
                if (section.CodeBlocks.Count > 0)
                {
                    sectionContent += "\n\n" + string.Join("\n\n", section.CodeBlocks.Select((code, idx) => 
                        $"```\n{code}\n```"));
                }

                var tokenCount = EstimateTokenCount(sectionContent);

                if (tokenCount <= _config.MaxChunkSize)
                {
                    // Section fits in one chunk
                    var chunk = CreateChunk(document, guidelineId, chunkIndex++, sectionContent, section);
                    chunks.Add(chunk);
                }
                else
                {
                    // Section is too large - need to split carefully
                    _logger.LogDebug("Section '{Section}' is too large ({Tokens} tokens), attempting smart split", 
                        section.Title, tokenCount);

                    // Try to keep code blocks with preceding context
                    if (section.CodeBlocks.Count > 0)
                    {
                        var smartChunks = SplitSectionWithCodeBlocks(section, guidelineId, ref chunkIndex, document);
                        chunks.AddRange(smartChunks);
                    }
                    else
                    {
                        // No code blocks, just split with overlap
                        var splitChunks = SplitWithOverlap(sectionContent, _config.MaxChunkSize, _config.OverlapSize);
                        
                        foreach (var splitContent in splitChunks)
                        {
                            var chunk = CreateChunk(document, guidelineId, chunkIndex++, splitContent, section);
                            chunks.Add(chunk);
                        }
                    }
                }
            }

            _logger.LogInformation("Chunked Guideline {Id} into {Count} chunks", guidelineId, chunks.Count);
            
            // Apply minimum chunk size enforcement at strategy level
            if (_config.MergeShortChunks && chunks.Count > 1)
            {
                var originalCount = chunks.Count;
                chunks = MergeShortDocumentChunks(chunks, _config.MinChunkSize);
                
                if (chunks.Count < originalCount)
                {
                    _logger.LogDebug("Merged short chunks: {Original} -> {Final}", originalCount, chunks.Count);
                    
                    // Renumber chunk IDs after merging
                    for (int i = 0; i < chunks.Count; i++)
                    {
                        chunks[i].ChunkId = CreateChunkId(guidelineId, i);
                    }
                }
            }
            
            return await Task.FromResult(chunks);
        }

        /// <summary>
        /// Smart splitting that keeps code blocks with their explanatory context
        /// </summary>
        private List<DocumentChunk> SplitSectionWithCodeBlocks(
            MarkdownSection section, 
            string documentId, 
            ref int chunkIndex, 
            DocumentInfo document)
        {
            var chunks = new List<DocumentChunk>();
            
            // Split content by paragraphs to find natural boundaries
            var paragraphs = section.Content.Split(new[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries);
            
            var currentChunkContent = new List<string>();
            var currentTokenCount = 0;

            foreach (var paragraph in paragraphs)
            {
                var paragraphTokens = EstimateTokenCount(paragraph);
                
                if (currentTokenCount + paragraphTokens > _config.MaxChunkSize && currentChunkContent.Count > 0)
                {
                    // Save current chunk
                    var chunkContent = string.Join("\n\n", currentChunkContent);
                    chunks.Add(CreateChunk(document, documentId, chunkIndex++, chunkContent, section));
                    
                    // Start new chunk with overlap
                    if (currentChunkContent.Count > 0)
                    {
                        currentChunkContent = new List<string> { currentChunkContent.Last() };
                        currentTokenCount = EstimateTokenCount(currentChunkContent.Last());
                    }
                    else
                    {
                        currentChunkContent = new List<string>();
                        currentTokenCount = 0;
                    }
                }

                currentChunkContent.Add(paragraph);
                currentTokenCount += paragraphTokens;
            }

            // Add final chunk
            if (currentChunkContent.Count > 0)
            {
                var chunkContent = string.Join("\n\n", currentChunkContent);
                
                // Add code blocks to the last chunk if they exist
                if (section.CodeBlocks.Count > 0)
                {
                    chunkContent += "\n\n" + string.Join("\n\n", section.CodeBlocks.Select(code => 
                        $"```\n{code}\n```"));
                }
                
                chunks.Add(CreateChunk(document, documentId, chunkIndex++, chunkContent, section));
            }

            return chunks;
        }

        private DocumentChunk CreateChunk(
            DocumentInfo document,
            string documentId,
            int chunkIndex,
            string content,
            MarkdownSection section)
        {
            var metadata = new Dictionary<string, string>
            {
                ["section_title"] = section.Title,
                ["has_code_examples"] = section.CodeBlocks.Count > 0 ? "true" : "false",
                ["code_example_count"] = section.CodeBlocks.Count.ToString()
            };

            return new DocumentChunk
            {
                ChunkId = CreateChunkId(documentId, chunkIndex),
                SourceDocumentId = documentId,
                DocumentType = "Guideline",
                Section = section.Title,
                Content = content,
                ChunkIndex = chunkIndex,
                MetadataJson = BuildMetadataJson(document, section, metadata),
                CreatedAt = DateTime.UtcNow
            };
        }
    }
}

using KnowledgebaseVectoriser.Models;
using Microsoft.Extensions.Logging;

namespace KnowledgebaseVectoriser.Services.Chunking
{
    /// <summary>
    /// Chunking strategy for ADR documents that preserves section structure
    /// ADRs have well-defined sections (Context, Decision, Consequences) that should be kept together
    /// </summary>
    public class ADRChunkingStrategy : BaseChunkingStrategy
    {
        private readonly ILogger<ADRChunkingStrategy> _logger;

        // ADR sections that should be kept as separate chunks when possible
        private readonly HashSet<string> _adrSections = new(StringComparer.OrdinalIgnoreCase)
        {
            "status", "context", "decision", "consequences",
            "alternatives", "implications", "related decisions"
        };

        public ADRChunkingStrategy(ILogger<ADRChunkingStrategy> logger, ChunkingConfig config)
            : base(config)
        {
            _logger = logger;
        }

        public override async Task<List<DocumentChunk>> ChunkAsync(DocumentInfo document, string content, ParsedMarkdown parsedMarkdown)
        {
            _logger.LogDebug("Chunking ADR: {Path}", document.RelativePath);

            var chunks = new List<DocumentChunk>();
            var chunkIndex = 0;

            // Extract ADR ID from filename
            var fileName = Path.GetFileNameWithoutExtension(document.FilePath);
            var adrId = fileName.Length >= 4 && int.TryParse(fileName.Substring(0, 4), out int adrNumber)
                ? $"adr_{adrNumber:D4}"
                : $"adr_{fileName}";

            // Group sections by their importance and size
            var sectionGroups = GroupADRSections(parsedMarkdown.Sections);

            foreach (var group in sectionGroups)
            {
                var groupContent = string.Join("\n\n", group.Sections.Select(s => 
                    $"## {s.Title}\n\n{s.Content}"));

                var tokenCount = EstimateTokenCount(groupContent);

                if (tokenCount <= _config.MaxChunkSize)
                {
                    // Group fits in one chunk
                    var chunk = CreateChunk(document, adrId, chunkIndex++, groupContent, group.Sections.First(), group.AdditionalMetadata);
                    chunks.Add(chunk);
                }
                else
                {
                    // Need to split the group
                    _logger.LogDebug("Section group '{GroupName}' is too large ({Tokens} tokens), splitting with overlap", 
                        group.GroupName, tokenCount);

                    var splitChunks = SplitWithOverlap(groupContent, _config.MaxChunkSize, _config.OverlapSize);
                    
                    foreach (var splitContent in splitChunks)
                    {
                        var chunk = CreateChunk(document, adrId, chunkIndex++, splitContent, group.Sections.First(), group.AdditionalMetadata);
                        chunks.Add(chunk);
                    }
                }
            }

            _logger.LogInformation("Chunked ADR {Id} into {Count} chunks", adrId, chunks.Count);
            
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
                        chunks[i].ChunkId = CreateChunkId(adrId, i);
                    }
                }
            }
            
            return await Task.FromResult(chunks);
        }

        /// <summary>
        /// Group ADR sections logically
        /// </summary>
        private List<SectionGroup> GroupADRSections(List<MarkdownSection> sections)
        {
            var groups = new List<SectionGroup>();

            foreach (var section in sections)
            {
                var sectionTitle = section.Title.ToLowerInvariant().Trim();
                
                // Check if this is a standard ADR section
                bool isStandardSection = _adrSections.Any(adrSec => sectionTitle.Contains(adrSec));

                if (isStandardSection)
                {
                    // Standard ADR sections get their own group
                    groups.Add(new SectionGroup
                    {
                        GroupName = section.Title,
                        Sections = new List<MarkdownSection> { section },
                        AdditionalMetadata = new Dictionary<string, string>
                        {
                            ["adr_section"] = section.Title,
                            ["is_standard_section"] = "true"
                        }
                    });
                }
                else
                {
                    // Other sections can be grouped together
                    if (groups.Count > 0 && groups.Last().GroupName == "Other Sections")
                    {
                        groups.Last().Sections.Add(section);
                    }
                    else
                    {
                        groups.Add(new SectionGroup
                        {
                            GroupName = "Other Sections",
                            Sections = new List<MarkdownSection> { section },
                            AdditionalMetadata = new Dictionary<string, string>
                            {
                                ["is_standard_section"] = "false"
                            }
                        });
                    }
                }
            }

            return groups;
        }

        private DocumentChunk CreateChunk(
            DocumentInfo document, 
            string documentId, 
            int chunkIndex, 
            string content,
            MarkdownSection primarySection,
            Dictionary<string, string> additionalMetadata)
        {
            return new DocumentChunk
            {
                ChunkId = CreateChunkId(documentId, chunkIndex),
                SourceDocumentId = documentId,
                DocumentType = "ADR",
                Section = primarySection.Title,
                Content = content,
                ChunkIndex = chunkIndex,
                MetadataJson = BuildMetadataJson(document, primarySection, additionalMetadata),
                CreatedAt = DateTime.UtcNow
            };
        }

        private class SectionGroup
        {
            public string GroupName { get; set; } = string.Empty;
            public List<MarkdownSection> Sections { get; set; } = new();
            public Dictionary<string, string> AdditionalMetadata { get; set; } = new();
        }
    }
}
